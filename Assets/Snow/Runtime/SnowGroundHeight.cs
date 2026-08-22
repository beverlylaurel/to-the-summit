// ROL: zemin yüksekliğini bir dokuya pişirir ve global olarak yayınlar (spec §7).
// İki kaynak: Unity Terrain heightmap'i veya mesh tabanlı arazinin bake'i.
// Çağıran: SnowManager.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowGroundHeight : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    [Tooltip("groundSource = UnityTerrain iken kullanılacak arazi.")]
    [SerializeField] Terrain terrain;

    [Tooltip("groundSource = MeshBake iken bake kamerasının merkezi.")]
    [SerializeField] Transform bakeCenter;

    Texture2D terrainHeights;
    RenderTexture bakedHeights;

    /// Shader'a bağlanan doku. İki yolda da aynı örnekleme kullanılıyor.
    public Texture HeightTexture => settings != null && settings.GroundSource == SnowGroundSource.MeshBake
        ? (Texture)bakedHeights
        : terrainHeights;

    public Vector2 OriginXZ { get; private set; }
    public Vector2 SizeXZ { get; private set; } = Vector2.one;
    public float BaseY { get; private set; }
    public float HeightRange { get; private set; } = 1f;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowGroundHeight)}: {nameof(settings)} atanmadı.");

        RefreshGroundHeight();
    }

    void OnDisable()
    {
        if (terrainHeights != null) { DestroyImmediate(terrainHeights); terrainHeights = null; }

        if (bakedHeights != null)
        {
            bakedHeights.Release();
            DestroyImmediate(bakedHeights);
            bakedHeights = null;
        }
    }

    /// Arazi çalışma zamanında değişirse dışarıdan çağrılır. Otomatik algılama yok
    /// (spec §7.1).
    public void RefreshGroundHeight()
    {
        if (settings.GroundSource == SnowGroundSource.MeshBake) BakeFromMesh();
        else BakeFromTerrain();

        WriteGlobals();
    }

    // ------------------------------------------------------------------ terrain

    /// TERRAIN HEIGHTMAP'İ DOĞRUDAN ÖRNEKLENMİYOR. `terrainData.heightmapTexture`
    /// Unity sürümleri arasında farklı ölçekleme sabitleri taşıyor ve sessiz hata
    /// kaynağı (spec §7.1). Bir kez kendi dokumuza pişiriyoruz.
    void BakeFromTerrain()
    {
        var found = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        if (found.Length > 1)
            throw new System.InvalidOperationException(
                $"{nameof(SnowGroundHeight)}: sahnede {found.Length} Terrain var. " +
                "Çoklu terrain DESTEKLENMİYOR (spec §7.1).");

        if (terrain == null) terrain = found.Length == 1 ? found[0] : null;

        if (terrain == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowGroundHeight)}: Terrain yok. `groundSource = MeshBake` seçin.");

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;

        // h[y, x] — indeks sırasına dikkat, spec §7.1 özellikle uyarıyor.
        float[,] h = td.GetHeights(0, 0, res, res);

        if (terrainHeights != null && terrainHeights.width != res)
        {
            DestroyImmediate(terrainHeights);
            terrainHeights = null;
        }

        if (terrainHeights == null)
        {
            terrainHeights = new Texture2D(res, res, TextureFormat.RHalf, false, true)
            {
                name = "Tex_Ground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        var pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
        {
            int row = y * res;
            for (int x = 0; x < res; x++) pixels[row + x] = new Color(h[y, x], 0f, 0f, 0f);
        }

        terrainHeights.SetPixels(pixels);
        terrainHeights.Apply(false, true);

        Vector3 pos = terrain.transform.position;
        Vector3 size = td.size;

        OriginXZ = new Vector2(pos.x, pos.z);
        SizeXZ = new Vector2(size.x, size.z);
        BaseY = pos.y;
        HeightRange = size.y;
    }

    // --------------------------------------------------------------- mesh bake

    /// MESH TABANLI ARAZİ. Ortografik bir kamera tepeden bir kez bakar ve dünya
    /// Y'sini yazar; doku doğrudan metre tutuyor, o yüzden taban 0 ve aralık 1.
    void BakeFromMesh()
    {
        float area = Mathf.Max(1f, settings.GroundBakeArea);
        Vector3 center = bakeCenter != null ? bakeCenter.position : Vector3.zero;

        if (bakedHeights == null)
        {
            bakedHeights = new RenderTexture(1024, 1024, 24, RenderTextureFormat.RHalf)
            {
                name = "Tex_Ground",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            bakedHeights.Create();
        }

        OriginXZ = new Vector2(center.x - area * 0.5f, center.z - area * 0.5f);
        SizeXZ = new Vector2(area, area);
        BaseY = 0f;
        HeightRange = 1f;

        // ASSUMPTION: bake çizimi Faz 1'de yazılmıyor — spec §7.2 aynı replacement
        // shader'ı (`Hidden/Snow/SkyDepth`) kullanmasını istiyor ve o shader Faz 5'te
        // doğuyor. `groundSource` varsayılanı `UnityTerrain` ve bu projede tek bir
        // Terrain var, yani Faz 1–4 bu yoldan geçmiyor. Faz 5'te doldurulacak.
    }

    // ----------------------------------------------------------------- global

    void WriteGlobals()
    {
        Texture tex = HeightTexture;
        if (tex != null) Shader.SetGlobalTexture(SnowShaderIDs.GroundHeightTex, tex);

        Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ, new Vector4(OriginXZ.x, OriginXZ.y, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ, new Vector4(SizeXZ.x, SizeXZ.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, BaseY);
        Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, HeightRange);
    }
}
