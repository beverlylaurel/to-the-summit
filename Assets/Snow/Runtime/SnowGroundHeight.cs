// ROLE: bakes the ground height into a texture and publishes it globally (spec §7).
// Two sources: Unity Terrain's heightmap or a bake of the mesh-based terrain.
// CALLED BY: SnowManager.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowGroundHeight : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] SnowSettings settings;

    [Tooltip("The terrain to use when groundSource = UnityTerrain.")]
    [SerializeField] Terrain terrain;

    [Tooltip("The centre of the bake camera when groundSource = MeshBake.")]
    [SerializeField] Transform bakeCenter;

    Texture2D terrainHeights;
    RenderTexture bakedHeights;

    /// The texture bound to the shader. The same sampling is used on both paths.
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
                $"{nameof(SnowGroundHeight)}: {nameof(settings)} is not assigned.");

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

    /// Called from outside if the terrain changes at runtime. There is no automatic detection
    /// (spec §7.1).
    public void RefreshGroundHeight()
    {
        if (settings.GroundSource == SnowGroundSource.MeshBake) BakeFromMesh();
        else BakeFromTerrain();

        WriteGlobals();
    }

    // ------------------------------------------------------------------ terrain

    /// THE TERRAIN HEIGHTMAP IS NOT SAMPLED DIRECTLY. `terrainData.heightmapTexture` carries
    /// different scaling constants between Unity versions and is a source of silent errors
    /// (spec §7.1). We bake it into our own texture once.
    void BakeFromTerrain()
    {
        var found = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);

        if (found.Length > 1)
            throw new System.InvalidOperationException(
                $"{nameof(SnowGroundHeight)}: sahnede {found.Length} Terrain var. " +
                "Multiple terrains are NOT SUPPORTED (spec §7.1).");

        if (terrain == null) terrain = found.Length == 1 ? found[0] : null;

        if (terrain == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowGroundHeight)}: there is no Terrain. Select `groundSource = MeshBake`.");

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;

        // h[y, x] — mind the index order, spec §7.1 warns about it specifically.
        float[,] h = td.GetHeights(0, 0, res, res);

        if (terrainHeights != null && terrainHeights.width != res)
        {
            DestroyImmediate(terrainHeights);
            terrainHeights = null;
        }

        if (terrainHeights == null)
        {
            // FULL PRECISION IS MANDATORY, A DELIBERATE DEVIATION FROM THE SPEC.
            //
            // Spec §7.1 says `RHalf` and assumes a small terrain. This mountain is
            // 8000 m; because half's relative step is 2^-11 its value in metres grows
            // with the elevation (measured: 195 cm at 2000 m, 781 cm at 8000 m).
            // The snow thickness is 26–45 cm — with the ground sitting on two-metre
            // steps the snow surface was drawn in blocks.
            //
            // The cost: 4097² × 4 B = 67 MB. Static, baked once.
            terrainHeights = new Texture2D(res, res, TextureFormat.RFloat, false, true)
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

    /// MESH-BASED TERRAIN. An orthographic camera looks down once and writes the world
    /// Y; the texture holds metres directly, so the base is 0 and the range 1.
    void BakeFromMesh()
    {
        float area = Mathf.Max(1f, settings.GroundBakeArea);
        Vector3 center = bakeCenter != null ? bakeCenter.position : Vector3.zero;

        if (bakedHeights == null)
        {
            // On the mesh bake path the texture holds METRES (base 0, range 1); half would
            // give steps of metres around 6000 m there too.
            bakedHeights = new RenderTexture(1024, 1024, 24, RenderTextureFormat.RFloat)
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

        // ASSUMPTION: the bake draw is not written in Phase 1 — spec §7.2 wants it to use the
        // same replacement shader (`Hidden/Snow/SkyDepth`) and that shader is born in Phase 5.
        // The `groundSource` default is `UnityTerrain` and this project has a single Terrain,
        // so Phases 1–4 do not go through this path. It will be filled in at Phase 5.
    }

    // ----------------------------------------------------------------- global

    void WriteGlobals()
    {
        Texture tex = HeightTexture;
        if (tex != null) Shader.SetGlobalTexture(SnowShaderIDs.GroundHeightTex, tex);

        Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ, new Vector4(OriginXZ.x, OriginXZ.y, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ, new Vector4(SizeXZ.x, SizeXZ.y, 0f, 0f));

        // The ground normal is derived with this step (SnowCommon → SampleGroundNormal).
        // Sampled at the snow texel it falls on the same ground texel and the normal comes
        // out dead straight up everywhere.
        int width = tex != null ? Mathf.Max(1, tex.width) : 1;
        int height = tex != null ? Mathf.Max(1, tex.height) : 1;

        Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ,
            new Vector4(SizeXZ.x / width, SizeXZ.y / height, 0f, 0f));

        Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, BaseY);
        Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, HeightRange);
    }
}
