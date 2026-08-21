// ROL: kaskadın penceresinden çıkan kar bloklarını saklar ve geri dönüldüğünde
// yazar (§10, Faz 10). Ayak izleri ve patikalar uzaklaşıp geri gelince yerinde duruyor.
// Çağıran: SnowFarCascade (kayma anında).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SnowPersistence : MonoBehaviour
{
    /// Blok kenarı, kaskad tekseli.
    ///
    // ASSUMPTION: §10 "4 m x 4 m blok" diyor ama kaskad tekseli 18.75 cm ve 4 m tam
    // sayı teksel etmiyor (21.33). Blok 32 teksele = 6 m'ye yuvarlandı; kesirli blok
    // her yazmada yarım teksel kaydırır ve saklanan iz zamanla bulanıklaşırdı.
    const int BlockTexels = 32;

    /// Bellek sınırı (§10). 512 blok x 32 x 32 x RGHalf = 2 MB.
    const int MaxBlocks = 512;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowFarCascade cascade;

    readonly Dictionary<Vector2Int, half2[]> blocks = new Dictionary<Vector2Int, half2[]>();

    /// LRU sırası. En eskisi başta.
    readonly LinkedList<Vector2Int> order = new LinkedList<Vector2Int>();
    readonly Dictionary<Vector2Int, LinkedListNode<Vector2Int>> nodes =
        new Dictionary<Vector2Int, LinkedListNode<Vector2Int>>();

    Texture2D staging;
    bool requestPending;

    /// Gezinen yakalama imleci. Her karede bir blok saklanıyor; bütün kaskadı
    /// taramak 1024 kare (~17 s) sürüyor.
    ///
    /// Neden gezinme: bir blok pencereden ÇIKARKEN saklanamaz — geri okuma asenkron
    /// ve blok o ana kadar gitmiş olur. Sürekli tarama, blok çıkarken deponun zaten
    /// sıcak olmasını sağlıyor. Bedeli: saklanan veri en fazla 17 saniye eski.
    int captureCursor;

    public int BlockCount => blocks.Count;

    /// Kaç blok bellek sınırına takılıp atıldı. Sıfırdan büyükse dünya sınırdan büyük.
    public int EvictedBlocks { get; private set; }

    /// half2: swe ve rhoN. Unity'nin `half` tipi Mathematics paketinde olmadığı için
    /// depolama ushort ikilisi; dönüşüm Mathf.FloatToHalf ile.
    struct half2
    {
        public ushort x;
        public ushort y;
    }

    void OnEnable()
    {
        if (cascade == null)
            throw new System.InvalidOperationException("SnowPersistence: SnowFarCascade atanmadı.");

        staging = new Texture2D(BlockTexels, BlockTexels, TextureFormat.RGHalf, false, true)
        {
            name = "Snow Persistence Staging",
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    void OnDisable()
    {
        if (staging != null) DestroyImmediate(staging);
        staging = null;

        blocks.Clear();
        order.Clear();
        nodes.Clear();
    }

    /// Bir bloğu GPU'dan alıp saklar. BLOKLAMAYAN okuma.
    public void StoreBlock(Vector2Int blockCoord, int texelX, int texelY)
    {
        if (requestPending || cascade.CascadeTexture == null) return;

        requestPending = true;

        AsyncGPUReadback.Request(cascade.CascadeTexture, 0, texelX, BlockTexels, texelY, BlockTexels,
                                 0, 1, TextureFormat.RGHalf, request =>
        {
            requestPending = false;
            if (request.hasError || !isActiveAndEnabled) return;

            var data = request.GetData<half2>();
            if (data.Length < BlockTexels * BlockTexels) return;

            var copy = new half2[BlockTexels * BlockTexels];
            data.CopyTo(copy);

            Insert(blockCoord, copy);
        });
    }

    /// Saklanan bloğu kaskada geri yazar. Yoksa false.
    public bool RestoreBlock(Vector2Int blockCoord, int texelX, int texelY)
    {
        if (!blocks.TryGetValue(blockCoord, out half2[] stored)) return false;
        if (cascade.CascadeTexture == null) return false;

        Touch(blockCoord);

        staging.SetPixelData(stored, 0);
        staging.Apply(false, false);

        Graphics.CopyTexture(staging, 0, 0, 0, 0, BlockTexels, BlockTexels,
                             cascade.CascadeTexture, 0, 0, texelX, texelY);

        return true;
    }

    void Insert(Vector2Int coord, half2[] data)
    {
        if (blocks.ContainsKey(coord))
        {
            blocks[coord] = data;
            Touch(coord);
            return;
        }

        // SINIRA GELİNCE EN ESKİSİ ATILIYOR. Sınırsız sözlük dağ ölçeğinde
        // gigabaytlara çıkardı; oyuncu geri döndüğünde en son gezdiği yerler duruyor.
        if (blocks.Count >= MaxBlocks)
        {
            LinkedListNode<Vector2Int> oldest = order.First;
            if (oldest != null)
            {
                blocks.Remove(oldest.Value);
                nodes.Remove(oldest.Value);
                order.RemoveFirst();

                EvictedBlocks++;
            }
        }

        blocks[coord] = data;
        nodes[coord] = order.AddLast(coord);
    }

    void Touch(Vector2Int coord)
    {
        if (!nodes.TryGetValue(coord, out LinkedListNode<Vector2Int> node)) return;

        order.Remove(node);
        nodes[coord] = order.AddLast(coord);
    }

    /// Her karede bir blok saklar.
    public void TickCapture(SnowFarCascade source)
    {
        if (requestPending) return;

        source.GetCaptureBlock(captureCursor, out Vector2Int coord, out int x, out int y);

        int total = source.BlocksPerSide * source.BlocksPerSide;
        captureCursor = total > 0 ? (captureCursor + 1) % total : 0;

        StoreBlock(coord, x, y);
    }

    /// Blok koordinatı ↔ kaskad tekseli. Blok ızgarası DÜNYAYA çapalı, kaskada değil.
    public static Vector2Int WorldToBlock(Vector2 worldXZ, float texelSize)
    {
        float blockSize = BlockTexels * texelSize;

        return new Vector2Int(Mathf.FloorToInt(worldXZ.x / blockSize),
                              Mathf.FloorToInt(worldXZ.y / blockSize));
    }

    public static int BlockSizeTexels => BlockTexels;
}
