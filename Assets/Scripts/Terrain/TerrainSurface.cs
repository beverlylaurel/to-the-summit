using System;
using UnityEngine;

/// Drives the mountain surface material. It makes no look decisions — those live in the
/// settings and in the shader. The only job here is passing the shared atmosphere state to the material.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainSurface : MonoBehaviour
{
    [SerializeField] TerrainMaterialSettings settings;
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;

    /// THE ALPENGLOW HAS TO SEE THE WEATHER. Read for the coverage only; the terrain
    /// writes nothing back. The reasoning is next to `ApplyAlpenglow`.
    [SerializeField] AtmosphereController atmosphere;

    [SerializeField] Texture2D surfaceMaps;
    [Tooltip("How much the terrain speeds the wind up or slows it down. It is baked against "
             + "the prevailing wind direction; the wind shelter reads it.")]
    [SerializeField] Texture2D windWeight;
    [SerializeField] Texture2D groundNormals;
    [SerializeField] Texture2DArray horizon;
    [Tooltip("Terrain height texture. The fog layers read the height above ground from it.")]
    [SerializeField] Texture2D terrainHeights;
    [SerializeField] Shader surfaceShader;

    static readonly int SurfaceMapsId = Shader.PropertyToID("_SurfaceMaps");
    static readonly int SurfaceMapsSizeId = Shader.PropertyToID("_SurfaceMapsSize");
    static readonly int GroundNormalsId = Shader.PropertyToID("_GroundNormals");
    static readonly int HorizonId = Shader.PropertyToID("_HorizonMap");
    static readonly int TerrainOriginId = Shader.PropertyToID("_TerrainOrigin");
    static readonly int TerrainSizeId = Shader.PropertyToID("_TerrainSize");

    static readonly int RockPrimaryId = Shader.PropertyToID("_RockPrimary");
    static readonly int RockSecondaryId = Shader.PropertyToID("_RockSecondary");
    static readonly int LowlandTintId = Shader.PropertyToID("_LowlandTint");
    static readonly int AlpineTintId = Shader.PropertyToID("_AlpineTint");
    static readonly int LichenColorId = Shader.PropertyToID("_LichenColor");
    static readonly int OxideColorId = Shader.PropertyToID("_OxideColor");
    static readonly int ScreeColorId = Shader.PropertyToID("_ScreeColor");

    static readonly int GrainScaleId = Shader.PropertyToID("_GrainScale");
    static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
    static readonly int RockSmoothnessId = Shader.PropertyToID("_RockSmoothness");
    static readonly int BandThicknessId = Shader.PropertyToID("_BandThickness");
    static readonly int BandWarpId = Shader.PropertyToID("_BandWarp");
    static readonly int BandWarpScaleId = Shader.PropertyToID("_BandWarpScale");
    static readonly int BandContrastId = Shader.PropertyToID("_BandContrast");
    static readonly int LowlandCeilingId = Shader.PropertyToID("_LowlandCeiling");
    static readonly int AlpineFloorId = Shader.PropertyToID("_AlpineFloor");
    static readonly int AltitudeTintStrengthId = Shader.PropertyToID("_AltitudeTintStrength");
    static readonly int LichenAmountId = Shader.PropertyToID("_LichenAmount");
    static readonly int LichenCeilingId = Shader.PropertyToID("_LichenCeiling");
    static readonly int LichenMoistureBiasId = Shader.PropertyToID("_LichenMoistureBias");
    static readonly int LichenSunSensitivityId = Shader.PropertyToID("_LichenSunSensitivity");
    static readonly int OxideAmountId = Shader.PropertyToID("_OxideAmount");
    static readonly int OxideScaleId = Shader.PropertyToID("_OxideScale");
    static readonly int ScreeAmountId = Shader.PropertyToID("_ScreeAmount");
    static readonly int ScreeRangeId = Shader.PropertyToID("_ScreeRange");
    static readonly int ScreeSlopeLimitId = Shader.PropertyToID("_ScreeSlopeLimit");
    static readonly int PatternSeedId = Shader.PropertyToID("_PatternSeed");
    /// Six textures per surface. The suffixes match exactly what the shader's
    /// DECLARE_SURFACE_DETAIL macro produces; written out separately in two places, one map
    /// would silently stay empty when another was added.
    static readonly string[] SurfaceMapSuffixes =
        { "Normal", "NormalLut", "Rough", "RoughLut", "Height", "HeightLut" };
    static readonly int SandAlbedoId = Shader.PropertyToID("_SandAlbedo");
    static readonly int SandNormalId = Shader.PropertyToID("_SandNormal");
    static readonly int SandRoughId = Shader.PropertyToID("_SandRough");
    static readonly int SandAOId = Shader.PropertyToID("_SandAO");
    static readonly int SandTintId = Shader.PropertyToID("_SandTint");
    static readonly int SandAmountId = Shader.PropertyToID("_SandAmount");
    static readonly int SandTexScaleId = Shader.PropertyToID("_SandTexScale");
    static readonly int SandNormalStrengthId = Shader.PropertyToID("_SandNormalStrength");
    static readonly int SandBandAboveId = Shader.PropertyToID("_SandBandAbove");
    static readonly int SandBandBelowId = Shader.PropertyToID("_SandBandBelow");
    static readonly int SandFadeId = Shader.PropertyToID("_SandFade");
    static readonly int SandSlopeCosId = Shader.PropertyToID("_SandSlopeCos");
    static readonly int SandPatchScaleId = Shader.PropertyToID("_SandPatchScale");
    static readonly int SandPatchThresholdId = Shader.PropertyToID("_SandPatchThreshold");

    static readonly int WetDarkeningId = Shader.PropertyToID("_WetDarkening");
    static readonly int WetSmoothnessId = Shader.PropertyToID("_WetSmoothness");
    static readonly int BumpStrengthId = Shader.PropertyToID("_BumpStrength");
    static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    static readonly int CavityStrengthId = Shader.PropertyToID("_CavityStrength");

    static readonly int DawnColorId = Shader.PropertyToID("_SurfaceDawnColor");
    static readonly int DawnDirId = Shader.PropertyToID("_SurfaceDawnDir");
    static readonly int DawnStrengthId = Shader.PropertyToID("_SurfaceDawnStrength");
    static readonly int AlpenglowFacingId = Shader.PropertyToID("_AlpenglowFacing");

    static readonly int TerrainHeightMapId = Shader.PropertyToID("_TerrainHeightMap");
    static readonly int TerrainHeightAreaId = Shader.PropertyToID("_TerrainHeightArea");
    /// The atmosphere writes it, here it is only READ: whether the wind threshold was crossed
    /// and by how much. The threshold rule lives in the atmosphere's settings; building it a
    /// second time here would split the two systems. `PrecipitationRenderer` reads the global
    /// for the same reason.
    static readonly int WetnessId = Shader.PropertyToID("_SurfaceWetness");
    static readonly int WindDirId = Shader.PropertyToID("_SurfaceWindDir");
    static readonly int SunDirId = Shader.PropertyToID("_SurfaceSunDir");

    Material material;
    int appliedRevision = -1;
    float wetness;

    /// The terrain's horizontal span (metres). The surface map's UV derives from it; kept here
    /// so the component is not looked up on every query.
    float terrainSpan;

    /// The span is written during material setup, but its readers can run earlier (the wind
    /// shelter asks on the first frame). At zero it is taken from the terrain's own data.
    float TerrainSpan
    {
        get
        {
            if (terrainSpan <= 0f) terrainSpan = GetComponent<Terrain>().terrainData.size.x;
            return terrainSpan;
        }
    }

    public TerrainMaterialSettings Settings => settings;

    /// The terrain's wind weight at the given world position, 0.67-2.0. 1 is neutral.
    /// On windward and convex surfaces the wind speeds up, on leeward and concave ones it slows
    /// down (Liston & Sturm). The map is stored halved so it fits in a byte.
    public float WindWeightAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return windWeight.GetPixelBilinear((worldPos.x - origin.x) / span,
                                           (worldPos.z - origin.z) / span).r * 2f;
    }

    /// The surface map's SLOPE channel (1 flat, 0 steep) at the given world position.
    /// The CPU twin of the snow depth reads it; this component holds the map and the terrain
    /// bounds and hands no second copy outside.
    ///
    /// The read is bilinear and mip 0 — so is `SampleSurfaceMapsFast` on the shader side.
    public float SlopeAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return surfaceMaps.GetPixelBilinear((worldPos.x - origin.x) / span,
                                            (worldPos.z - origin.z) / span).a;
    }

    public void Bind(TerrainMaterialSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, AtmosphereController atmosphereController,
        Texture2D maps, Texture2D windMap,
        Texture2D normals, Texture2DArray horizonMap, Texture2D heightMap,
        Shader shader)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        atmosphere = atmosphereController;
        surfaceMaps = maps;
        windWeight = windMap;
        groundNormals = normals;
        horizon = horizonMap;
        terrainHeights = heightMap;
        surfaceShader = shader;

        // THE OLD MATERIAL IS DESTROYED. Merely dropping the reference was a leak: the setup
        // script rebinds on every compile and one more material was left ownerless each time.
        // `hideFlags = DontSave` hides them from the scene, not from memory.
        DestroyOwned(material);
        material = null;          // rebinding should refresh the material too
        appliedRevision = -1;     // the settings are written to the new material from scratch
    }

    void OnDisable()
    {

        DestroyOwned(material);
        material = null;
    }

    /// `Destroy` at runtime, `DestroyImmediate` in the editor. In the editor `Destroy` is
    /// deferred to the next frame and that frame may never come.
    static void DestroyOwned(UnityEngine.Object owned)
    {
        if (owned == null) return;

        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    void Update()
    {
        EnsureMaterial();
        ApplySettings();

        float precipitation = weather != null ? weather.Precipitation : 0f;

        // Wetting is fast, drying is slow: when the rain stops the rock stays dark for a while
        float target = precipitation;
        float duration = target > wetness ? 8f : Mathf.Max(1f, settings.dryingSeconds);
        wetness = Mathf.Lerp(wetness, target, 1f - Mathf.Exp(-Time.deltaTime / duration));
        material.SetFloat(WetnessId, wetness);

        // THE WETNESS IS PUBLISHED AS A GLOBAL TOO. The snow on objects
        // (`SnowCoverObject`) is on another material; if it does not see the same wetness,
        // the same snow comes out dry on a rock and wet on the ground.
        Shader.SetGlobalFloat(WetnessId, wetness);

        // The PREVAILING direction, not the instantaneous speed. The surface pattern sits on
        // this axis; when the axis moved with the gust the pattern dragged across the world
        // (the field is built on `dot(worldXZ, windAxis)`, see WindField).
        Vector3 windDir = wind != null ? wind.PrevailingDirection : Vector3.right;
        // The severity is in w: the pattern wants not only a direction but a strength — in calm
        // weather the surface stays uncombed, in a storm it is streaked.
        material.SetVector(WindDirId, new Vector4(windDir.x, windDir.y, windDir.z,
            wind != null ? wind.Strength : 0f));

        // The noon sun, not the instantaneous one: lichen settles according to the yearly
        // sunlight and does not blink through the day
        material.SetVector(SunDirId, time != null ? time.NoonSunDirection : Vector3.up);

        ApplyAlpenglow();
    }

    /// A shift on three axes from the seed. The same seed → the same surface; there is nothing
    /// to synchronize in co-op because there is no shared state.
    static Vector4 PatternOffset(int seed)
    {
        // A small integer mixer, so consecutive seeds give unrelated shifts.
        uint h = (uint)seed * 2654435761u;
        float x = (h & 0xFFu) * 2f;
        float y = ((h >> 8) & 0xFFu) * 2f;
        float z = ((h >> 16) & 0xFFu) * 2f;
        return new Vector4(x, y, z, 0f);
    }

    /// The red light coming from the horizon at dawn and dusk. Its colour and timing come from
    /// TimeOfDay; a separate timer would contradict the sky.
    void ApplyAlpenglow()
    {
        if (time == null)
        {
            material.SetFloat(DawnStrengthId, 0f);

            // The direction is written too: without it, whatever was last left in the material
            // stays, and everything reading this direction (surface sparkle, night
            // dulling) takes it for daytime. Below the horizon = no source.
            material.SetVector(DawnDirId, Vector3.down);
            return;
        }

        // The horizon factor already peaks at dawn and sunset. Squaring it compresses the glow
        // into those two moments; left wide it lasts until noon.
        float horizon = time.HorizonFactor * time.HorizonFactor;

        // The red lingers a while as the sun drops below the horizon, then ends.
        // The window narrowed: the limit of the lighting is now drawn by the Earth's shadow
        // (h ≈ R·θ²/2 in the shader). The summit is ~2100 m and the shadow crosses that
        // elevation with the sun at −1.5° (SunHeight ≈ −0.026); after that there is no surface
        // left to light. The old −0.18 limit carried power through the night for nothing.
        float alive = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.05f, time.SunHeight));

        // THE CLOUD COVER CUTS THE DIRECT PHASE.
        //
        // The strength used to be `horizon x alive x setting` with no weather term at all:
        // at dawn IN A STORM the mountain face still burned red behind a thick cloud mass,
        // while the fog palette in exactly the same state was deliberately going pale
        // (`AtmosphereController`, `duskOvercast`). Two derivations of one sky contradicting
        // each other — the thing `CLAUDE.md` forbids.
        //
        // IT IS NOT ZEROED. An alpenglow has two phases: the direct beam grazing the face,
        // and the afterglow the face takes from a sky painted red. Cloud cover kills the
        // first and only dims the second, so the floor stays well above zero.
        //
        // The coverage comes from the SAME place the sky, the fog and the clouds read
        // (`AtmosphereController.Coverage`) — no second mapping.
        float overcast = atmosphere != null ? Mathf.Clamp01(atmosphere.Coverage) : 0f;
        float weatherGate = Mathf.Lerp(1f, 0.25f, overcast);

        material.SetColor(DawnColorId, time.CurrentSunColor);
        material.SetVector(DawnDirId, time.SunDirection);
        material.SetFloat(DawnStrengthId,
            horizon * alive * weatherGate * settings.alpenglowStrength);
        material.SetFloat(AlpenglowFacingId, settings.alpenglowFacing);
    }

    /// A recompile in Play mode can drop the material; it is verified at the point of use.
    /// The dependency check is here too: on an ExecuteAlways component `OnEnable` runs the
    /// moment the component is added to the scene, i.e. before `Bind`.
    ///
    /// THE REFERENCE SURVIVING IS NOT ENOUGH, IT HAS TO BE FULL. When the shader is
    /// reimported the material object stays alive but every value written to it is erased.
    /// When `_TerrainSize` falls to zero the surface uv becomes `(pos - origin) / 0` and the
    /// WHOLE terrain prints NaN — measured: 162674 of 162674 pixels. On screen the terrain is
    /// pitch black and the snow mesh is normal.
    /// `ApplySettings` does not save it either, it skips because `appliedRevision` stayed equal.
    void EnsureMaterial()
    {
        if (material != null && material.HasVector(TerrainSizeId)) return;

        if (settings == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(settings)} is not assigned.");
        if (surfaceShader == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(surfaceShader)} is not assigned.");
        if (surfaceMaps == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: the surface maps are not assigned.");
        var terrain = GetComponent<Terrain>();
        appliedRevision = -1;
        material = new Material(surfaceShader) { hideFlags = HideFlags.DontSave };
        terrain.materialTemplate = material;

        material.SetTexture(SurfaceMapsId, surfaceMaps);
        material.SetVector(SurfaceMapsSizeId, new Vector4(surfaceMaps.width, surfaceMaps.height,
            1f / surfaceMaps.width, 1f / surfaceMaps.height));
        material.SetTexture(GroundNormalsId, groundNormals);
        material.SetTexture(HorizonId, horizon);
        material.SetVector(TerrainOriginId, transform.position);
        material.SetVector(TerrainSizeId, terrain.terrainData.size);

        // The snow mesh should read the same shadow (the reasoning sits next to the field definitions).
        terrainSpan = terrain.terrainData.size.x;

        // The terrain height is a GLOBAL: the fog and the sky read it too, it is not only the
        // surface's setting. xy is the corner position, z the span, w the height scale — the
        // 0-1 value in the texture is converted to metres with it, and the conversion lives in one place.
        Vector3 size = terrain.terrainData.size;
        Shader.SetGlobalTexture(TerrainHeightMapId, terrainHeights);
        Shader.SetGlobalVector(TerrainHeightAreaId, new Vector4(
            transform.position.x, transform.position.z, size.x, size.y));
    }

    void ApplySettings()
    {
        if (appliedRevision == settings.revision) return;
        appliedRevision = settings.revision;

        material.SetColor(RockPrimaryId, settings.rockPrimary);
        material.SetColor(RockSecondaryId, settings.rockSecondary);
        material.SetColor(LowlandTintId, settings.lowlandTint);
        material.SetColor(AlpineTintId, settings.alpineTint);
        material.SetColor(LichenColorId, settings.lichenColor);
        material.SetColor(OxideColorId, settings.oxideColor);
        material.SetColor(ScreeColorId, settings.screeColor);

        material.SetFloat(GrainScaleId, settings.grainScale);
        material.SetFloat(GrainStrengthId, settings.grainStrength);
        material.SetFloat(RockSmoothnessId, settings.rockSmoothness);
        material.SetFloat(BandThicknessId, settings.bandThickness);
        material.SetFloat(BandWarpId, settings.bandWarp);
        material.SetFloat(BandWarpScaleId, settings.bandWarpScale);
        material.SetFloat(BandContrastId, settings.bandContrast);
        material.SetFloat(LowlandCeilingId, settings.lowlandCeiling);
        material.SetFloat(AlpineFloorId, settings.alpineFloor);
        material.SetFloat(AltitudeTintStrengthId, settings.altitudeTintStrength);
        material.SetFloat(LichenAmountId, settings.lichenAmount);
        material.SetFloat(LichenCeilingId, settings.lichenCeiling);
        material.SetFloat(LichenMoistureBiasId, settings.lichenMoistureBias);
        material.SetFloat(LichenSunSensitivityId, settings.lichenSunSensitivity);
        material.SetFloat(OxideAmountId, settings.oxideAmount);
        material.SetFloat(OxideScaleId, settings.oxideScale);
        material.SetFloat(ScreeAmountId, settings.screeAmount);
        material.SetVector(ScreeRangeId, settings.screeRange);
        // THE SEED IS A GLOBAL, NOT A MATERIAL FIELD: the two hash roots are in two separate
        // files and one of them is read in the displacement stage. Written to the material,
        // that stage would not see it.
        //
        // The shift is DERIVED from the seed, not entered by hand: the three axes are
        // independent of each other and stay inside the 512 hash wrap (`fmod(..., 512)` in `MountainHash`).
        Shader.SetGlobalVector(PatternSeedId, PatternOffset(settings.patternSeed));

        material.SetFloat(ScreeSlopeLimitId, settings.screeSlopeLimit);

        ApplySand();

        material.SetFloat(WetDarkeningId, settings.wetDarkening);
        material.SetFloat(WetSmoothnessId, settings.wetSmoothness);
        material.SetFloat(BumpStrengthId, settings.bumpStrength);
        material.SetFloat(BumpScaleId, settings.bumpScale);
        material.SetFloat(CavityStrengthId, settings.cavityStrength);
    }

    /// The shore sand. The maps come from the settings asset; the elevation of the band does
    /// NOT — the shader reads `_SeaLevelY`, the global the sea publishes. Copying the level
    /// into the material here would give the beach a second source and the two could diverge
    /// the moment the sea level was moved.
    void ApplySand()
    {
        // WITHOUT MAPS THE SAND IS OFF, AND IT IS SILENT. A missing texture reads as black in
        // the shader; the shore would go dark and the cause would be hunted for in the light.
        bool ready = settings.sandAlbedo != null && settings.sandNormal != null
                  && settings.sandRoughness != null && settings.sandAO != null;

        material.SetFloat(SandAmountId, ready ? settings.sandAmount : 0f);
        if (!ready) return;

        material.SetTexture(SandAlbedoId, settings.sandAlbedo);
        material.SetTexture(SandNormalId, settings.sandNormal);
        material.SetTexture(SandRoughId, settings.sandRoughness);
        material.SetTexture(SandAOId, settings.sandAO);

        material.SetColor(SandTintId, settings.sandTint);
        material.SetFloat(SandTexScaleId, settings.sandTexScale);
        material.SetFloat(SandNormalStrengthId, settings.sandNormalStrength);
        material.SetFloat(SandBandAboveId, settings.sandBandAbove);
        material.SetFloat(SandBandBelowId, settings.sandBandBelow);
        material.SetFloat(SandFadeId, settings.sandFade);
        // THE SLOPE WINDOW IS SENT AS TWO COSINES. Built in the shader as `cos(limit) ± 0.08`
        // — the way the rock and gravel masks do it — it breaks at a shallow limit:
        // `cos(6°) + 0.06` is 1.05, which no surface reaches, and the mask saturated at 0.73
        // even on dead flat ground. Here the window is ±3° of angle wherever the limit sits.
        material.SetVector(SandSlopeCosId, new Vector4(
            Mathf.Cos((settings.sandSlopeLimit + 3f) * Mathf.Deg2Rad),
            Mathf.Cos(Mathf.Max(0f, settings.sandSlopeLimit - 3f) * Mathf.Deg2Rad), 0f, 0f));
        material.SetFloat(SandPatchScaleId, settings.sandPatchScale);

        // COVERAGE IS A SHARE, THE SHADER WANTS A THRESHOLD. `MountainFbm` gathers its mass
        // between roughly 0.30 and 0.70. The shader's transition is ±0.12 wide, so the two ends
        // are placed a full transition OUTSIDE that mass: at 0 coverage the whole band sits below
        // the transition (no sand anywhere), at 1 it sits above it (the whole shore is sand).
        material.SetFloat(SandPatchThresholdId,
            Mathf.Lerp(0.85f, 0.15f, settings.sandCoverage));
    }
}
