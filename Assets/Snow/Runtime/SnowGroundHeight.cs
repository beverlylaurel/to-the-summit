// ROL: Unity Terrain'in yükseklik haritasını bir dokuya pişirir. Kar mesh'i ve
// birikme simülasyonu zemin kotunu buradan okur.
// Çağıran: SnowManager (başlangıçta bir kez, sonra elle RefreshGroundHeight ile).

using UnityEngine;

[DisallowMultipleComponent]
public class SnowGroundHeight : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    [Tooltip("Yükseklik kaynağı. groundSource = UnityTerrain iken zorunlu.")]
    [SerializeField] Terrain terrain;

    Texture2D heightTexture;

    public Texture2D HeightTexture => heightTexture;
    public Vector2 OriginXZ { get; private set; }
    public Vector2 SizeXZ { get; private set; }
    public float BaseY { get; private set; }
    public float HeightRange { get; private set; }

    /// (res-1)/res ve 0.5/res — yarım teksel düzeltmesi.
    public Vector2 HeightUV { get; private set; }

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException("SnowGroundHeight: SnowSettings atanmadı.");

        if (settings.GroundSource != SnowGroundSource.UnityTerrain)
            throw new System.NotImplementedException(
                "SnowGroundHeight: yalnız UnityTerrain kaynağı yazıldı. " +
                "OrthographicCapture mesh tabanlı dünyalar için, bu projede gerekmiyor.");

        RefreshGroundHeight();
    }

    void OnDisable()
    {
        if (heightTexture == null) return;

        DestroyImmediate(heightTexture);
        heightTexture = null;
    }

    /// Terrain çalışma zamanında değişirse elle çağrılır. Otomatik algılama yok (§3).
    public void RefreshGroundHeight()
    {
        if (terrain == null)
            throw new System.InvalidOperationException("SnowGroundHeight: Terrain atanmadı.");

        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;

        // R16, RHalf DEĞİL.
        //
        // Spec §2.2 RHalf diyor ama sonucu kâğıtta hesaplayınca tutmuyor: half'in
        // 0.5–1.0 aralığındaki adımı 2^-11, dağın 6189 m'lik yükseklik menzilinde
        // 3.0 METRE eder. Kar derinliği santimetre mertebesinde, zemin 3 m oynayamaz.
        //
        // R16 tam olarak Unity Terrain'in kendi hassasiyeti: 65536 adım / 6189 m =
        // 9.4 cm. Yani kaynağın üstüne HİÇ hata eklemiyor. Half eklerdi.
        if (heightTexture == null || heightTexture.width != resolution)
        {
            if (heightTexture != null) DestroyImmediate(heightTexture);

            heightTexture = new Texture2D(resolution, resolution, TextureFormat.R16, false, true)
            {
                name = "Tex_GroundHeight",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        float[,] heights = data.GetHeights(0, 0, resolution, resolution);

        var pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            int row = y * resolution;
            for (int x = 0; x < resolution; x++)
                pixels[row + x] = new Color(heights[y, x], 0f, 0f, 0f);
        }

        heightTexture.SetPixels(pixels);
        heightTexture.Apply(false, false);

        Vector3 position = terrain.transform.position;
        Vector3 size = data.size;

        OriginXZ = new Vector2(position.x, position.z);
        SizeXZ = new Vector2(size.x, size.z);
        BaseY = position.y;
        HeightRange = size.y;
        HeightUV = new Vector2((resolution - 1f) / resolution, 0.5f / resolution);
    }
}
