// ROLE: stores the snow blocks leaving the region and writes them back on return
// (spec §21 Phase 10) [SOURCE: Rockstar patent US11534688B2].
// CALLED BY: SnowManager (from inside Dispatch).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// SO TRACKS DO NOT VANISH WHEN THEY LEAVE THE REGION.
///
/// The region is 16 m. Walking twenty metres and coming back, the path the player had
/// opened was gone — in a game where the same line is used again and again on a climb
/// that is the most noticeable gap.
///
/// ASSUMPTION: the block is stored at REDUCED resolution. The spec says a 4×4 m block
/// but does not do the arithmetic: at full resolution a block is 256×256 texels, i.e.
/// 256 KB at half precision, and with an LRU of 512 blocks that is 128 MB. Reduced by
/// four (6.25 cm/texel) a block is 32 KB and the total 16 MB.
[DisallowMultipleComponent]
public class SnowPersistence : MonoBehaviour
{
    /// Spec §21 Faz 10.
    const float BlockMeters = 4f;
    const int MaxBlocks = 512;

    /// The edge of a stored block, in texels.
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
            throw new System.InvalidOperationException($"{nameof(SnowPersistence)}: {nameof(manager)} is not assigned.");
        if (simCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowPersistence)}: the compute is not assigned.");

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

    /// SnowManager calls it inside a single CommandBuffer (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (blockBuffer == null || !manager.IsReady) return;

        SnowQualityData q = manager.Settings.QualityData;

        int blockTexels = Mathf.RoundToInt(BlockMeters / manager.TexelSize);
        if (blockTexels < StoredSide) return;

        RefreshCoverage(q);

        // THE INCOMING ONES FIRST. Writing and reading in the same frame would mean reading
        // back the block that was just written.
        for (int i = 0; i < entered.Count; i++)
            Unpack(cmd, entered[i], q, blockTexels);

        if (!packPending) RequestPack(cmd, q, blockTexels);
    }

    /// Updates the blocks the region covers; collects the ones newly entering.
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

            // IS IT FULLY INSIDE. Packing a partly-inside block would mean storing the half
            // that stays outside with the edge value.
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

    /// The block's bottom-left corner in the region texture, in texels.
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

        // THE OLDEST IS DROPPED. Growing without bound, the memory would keep rising through
        // a long climb and the symptom would be a crash arriving hours later.
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
