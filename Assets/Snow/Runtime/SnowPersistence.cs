// ROL: bölgeden çıkan kar bloklarını saklar, geri dönülünce yazar
// (spec §21 Faz 10) [KAYNAK: Rockstar patenti US11534688B2].
// Çağıran: SnowManager (Dispatch içinden).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// İZLER BÖLGEDEN ÇIKINCA KAYBOLMASIN.
///
/// Bölge 16 m. Oyuncu yirmi metre yürüyüp geri dönünce açtığı patika yok
/// olmuştu — tırmanışta aynı hattı defalarca kullanan bir oyunda bu en çok
/// göze batan eksik.
///
/// ASSUMPTION: blok İNDİRGENMİŞ çözünürlükte saklanıyor. Spec 4×4 m blok
/// diyor ama aritmetiğini yapmıyor: tam çözünürlükte bir blok 256×256 teksel
/// yani yarım hassasiyette 256 KB, LRU 512 blokla 128 MB eder. Dörde
/// indirgenince (6.25 cm/teksel) blok 32 KB ve toplam 16 MB.
[DisallowMultipleComponent]
public class SnowPersistence : MonoBehaviour
{
    /// Spec §21 Faz 10.
    const float BlockMeters = 4f;
    const int MaxBlocks = 512;

    /// Saklanan bloğun kenarı, teksel.
    const int StoredSide = 64;
    const int StoredValues = StoredSide * StoredSide * 4;

    [SerializeField] SnowManager manager;
    [SerializeField] ComputeShader simCompute;

    readonly Dictionary<Vector2Int, ushort[]> blocks = new(MaxBlocks);
    readonly LinkedList<Vector2Int> recency = new();
    readonly Dictionary<Vector2Int, LinkedListNode<Vector2Int>> nodes = new(MaxBlocks);

    readonly HashSet<Vector2Int> covered = new();
    readonly HashSet<Vector2Int> previous = new();
    readonly List<Vector2Int> insideList = new(32);
    readonly List<Vector2Int> entered = new(8);

    GraphicsBuffer blockBuffer;
    float[] staging;

    int packKernel = -1;
    int unpackKernel = -1;

    int packCursor;
    bool packPending;
    Vector2Int packTarget;

    public int StoredBlocks => blocks.Count;

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException($"{nameof(SnowPersistence)}: {nameof(manager)} atanmadı.");
        if (simCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowPersistence)}: compute atanmadı.");

        blockBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                                         StoredSide * StoredSide, 4 * sizeof(float));

        staging = new float[StoredValues];

        packKernel = simCompute.FindKernel("KBlockPack");
        unpackKernel = simCompute.FindKernel("KBlockUnpack");

        blocks.Clear();
        recency.Clear();
        nodes.Clear();
        covered.Clear();
        previous.Clear();

        packPending = false;
        packCursor = 0;
    }

    void OnDisable()
    {
        blockBuffer?.Dispose();
        blockBuffer = null;
    }

    /// SnowManager tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (blockBuffer == null || !manager.IsReady) return;

        SnowQualityData q = manager.Settings.QualityData;

        int blockTexels = Mathf.RoundToInt(BlockMeters / manager.TexelSize);
        if (blockTexels < StoredSide) return;

        RefreshCoverage(q);

        // GİRENLER ÖNCE. Aynı karede hem yazıp hem okumak, yazılan bloğu
        // geri okumak olurdu.
        for (int i = 0; i < entered.Count; i++)
            Unpack(cmd, entered[i], q, blockTexels);

        if (!packPending) RequestPack(cmd, q, blockTexels);
    }

    /// Bölgenin kapsadığı blokları günceller; yeni girenleri toplar.
    void RefreshCoverage(SnowQualityData q)
    {
        previous.Clear();
        foreach (Vector2Int b in covered) previous.Add(b);

        covered.Clear();
        insideList.Clear();
        entered.Clear();

        Vector2 center = manager.AreaCenter;
        float half = q.AreaSize * 0.5f;

        int minX = Mathf.FloorToInt((center.x - half) / BlockMeters);
        int maxX = Mathf.FloorToInt((center.x + half) / BlockMeters);
        int minY = Mathf.FloorToInt((center.y - half) / BlockMeters);
        int maxY = Mathf.FloorToInt((center.y + half) / BlockMeters);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var key = new Vector2Int(x, y);
            covered.Add(key);

            // TAMAMEN İÇERDE Mİ. Kısmen içerdeki bloğu paketlemek, dışarıda
            // kalan yarısını kenar değeriyle saklamak olurdu.
            float bx = x * BlockMeters;
            float by = y * BlockMeters;

            bool fullyInside = bx >= center.x - half && bx + BlockMeters <= center.x + half &&
                               by >= center.y - half && by + BlockMeters <= center.y + half;

            if (fullyInside) insideList.Add(key);

            if (!previous.Contains(key) && blocks.ContainsKey(key)) entered.Add(key);
        }
    }

    void RequestPack(CommandBuffer cmd, SnowQualityData q, int blockTexels)
    {
        if (insideList.Count == 0) return;

        packCursor = (packCursor + 1) % insideList.Count;
        packTarget = insideList[packCursor];

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.BlockTexels, blockTexels);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.BlockStored, StoredSide);
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.BlockOrigin,
                                  (Vector2)BlockOrigin(packTarget, q));

        cmd.SetComputeTextureParam(simCompute, packKernel, SnowShaderIDs.Snow, manager.SnowTexture);
        cmd.SetComputeBufferParam(simCompute, packKernel, SnowShaderIDs.BlockBuffer, blockBuffer);

        int groups = Mathf.CeilToInt(StoredSide / (float)SnowConstants.GroupSize);
        cmd.DispatchCompute(simCompute, packKernel, groups, groups, 1);

        packPending = true;
        cmd.RequestAsyncReadback(blockBuffer, OnPacked);
    }

    void OnPacked(AsyncGPUReadbackRequest request)
    {
        packPending = false;

        if (request.hasError) return;

        Unity.Collections.NativeArray<float> data = request.GetData<float>();

        ushort[] stored = Rent(packTarget);

        int count = Mathf.Min(StoredValues, data.Length);
        for (int i = 0; i < count; i++) stored[i] = Mathf.FloatToHalf(data[i]);

        Touch(packTarget);
    }

    void Unpack(CommandBuffer cmd, Vector2Int key, SnowQualityData q, int blockTexels)
    {
        if (!blocks.TryGetValue(key, out ushort[] stored)) return;

        for (int i = 0; i < StoredValues; i++) staging[i] = Mathf.HalfToFloat(stored[i]);

        blockBuffer.SetData(staging);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.BlockTexels, blockTexels);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.BlockStored, StoredSide);
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.BlockOrigin,
                                  (Vector2)BlockOrigin(key, q));

        cmd.SetComputeTextureParam(simCompute, unpackKernel, SnowShaderIDs.Snow, manager.SnowTexture);
        cmd.SetComputeTextureParam(simCompute, unpackKernel, SnowShaderIDs.SnowOut, manager.SnowTexture);
        cmd.SetComputeBufferParam(simCompute, unpackKernel, SnowShaderIDs.BlockBuffer, blockBuffer);

        int groups = Mathf.CeilToInt(blockTexels / (float)SnowConstants.GroupSize);
        cmd.DispatchCompute(simCompute, unpackKernel, groups, groups, 1);

        Touch(key);
    }

    /// Bloğun bölge dokusundaki sol alt köşesi, teksel.
    Vector2Int BlockOrigin(Vector2Int key, SnowQualityData q)
    {
        Vector2 center = manager.AreaCenter;
        float half = q.AreaSize * 0.5f;

        float localX = key.x * BlockMeters - (center.x - half);
        float localY = key.y * BlockMeters - (center.y - half);

        return new Vector2Int(Mathf.RoundToInt(localX / manager.TexelSize),
                              Mathf.RoundToInt(localY / manager.TexelSize));
    }

    // ------------------------------------------------------------------- LRU

    ushort[] Rent(Vector2Int key)
    {
        if (blocks.TryGetValue(key, out ushort[] existing)) return existing;

        // EN ESKİ ATILIYOR. Sınırsız büyüseydi uzun bir tırmanışta bellek
        // sürekli artardı ve belirtisi saatler sonra gelen bir çökme olurdu.
        if (blocks.Count >= MaxBlocks)
        {
            LinkedListNode<Vector2Int> oldest = recency.Last;

            if (oldest != null)
            {
                blocks.Remove(oldest.Value);
                nodes.Remove(oldest.Value);
                recency.RemoveLast();
            }
        }

        var array = new ushort[StoredValues];
        blocks[key] = array;

        return array;
    }

    void Touch(Vector2Int key)
    {
        if (nodes.TryGetValue(key, out LinkedListNode<Vector2Int> node))
        {
            recency.Remove(node);
            recency.AddFirst(node);
            return;
        }

        nodes[key] = recency.AddFirst(key);
    }
}
