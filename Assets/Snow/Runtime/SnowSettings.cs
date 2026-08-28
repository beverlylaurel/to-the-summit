using UnityEngine;

/// Ground elevation source mode (spec §7).
public enum SnowGroundSource
{
    /// Baked once to RHalf from the primary scene Unity Terrain heightmap.
    UnityTerrain,

    /// Mesh-based terrain: baked once via orthographic camera.
    MeshBake,
}

[CreateAssetMenu(menuName = "To The Summit/Snow/Snow Settings", fileName = "SnowSettings")]
public class SnowSettings : ScriptableObject
{
    [Header("Quality")]
    [Tooltip("Drives simulation resolution, cascade rings, detail layers, and VFX capacity.")]
    [SerializeField] SnowQualityPreset quality = SnowQualityPreset.Medium;

    [Header("Ground")]
    [Tooltip("Source of base ground elevation.")]
    [SerializeField] SnowGroundSource groundSource = SnowGroundSource.UnityTerrain;

    [Tooltip("Area covered by orthographic camera in MeshBake mode, meters.")]
    [SerializeField] float groundBakeArea = 512f;

    [Tooltip("Ground layer mask for MeshBake mode.")]
    [SerializeField] LayerMask groundLayerMask = ~0;

    [Header("Precipitation")]
    [Tooltip("Breakup noise texture for snow margins (spec §8.2, §16).")]
    [SerializeField] Texture2D breakupNoise;

    [Header("Appearance")]
    [Tooltip("Cool ambient shadow tint for snow (spec §14.3).")]
    [SerializeField] Color shadowTint = new(0.66f, 0.76f, 0.95f);

    [Tooltip("Translucency strength for shallow snow layers.")]
    [SerializeField, Range(0f, 2f)] float translucencyStrength = 0.6f;

    [Header("Sparkle (spec §14.4)")]
    [Tooltip("World size of sparkle crystal cell, meters.")]
    [SerializeField] float sparkleCellSize = 0.0008f;

    [Tooltip("Target sparkle probability per pixel.")]
    [SerializeField] float sparkleDensity = 0.002f;

    [Tooltip("Sparkle specular reflection sharpness.")]
    [SerializeField] float sparkleSharpness = 8f;

    [Tooltip("Sparkle intensity multiplier.")]
    [SerializeField] float sparkleIntensity = 7f;

    [Header("Tessellation")]
    [Tooltip("Maximum tessellation factor (hardware max 64).")]
    [SerializeField, Range(1f, 64f)] float tessMax = 64f;

    [Tooltip("Distance where tessellation factor is fully applied, meters.")]
    [SerializeField, Min(1f)] float tessNear = 15f;

    [Tooltip("Distance where tessellation drops to 1, meters.")]
    [SerializeField, Min(2f)] float tessFar = 60f;

    [Header("Surface Textures")]
    [Tooltip("Fresh powder snow surface textures.")]
    [SerializeField] Texture2D surfTazeColor, surfTazeNormal, surfTazeRough;

    [Tooltip("Dry cold powder snow surface textures.")]
    [SerializeField] Texture2D surfTozColor, surfTozNormal, surfTozRough;

    [Tooltip("Settled / compacted snow surface textures.")]
    [SerializeField] Texture2D surfYerlesmisColor, surfYerlesmisNormal, surfYerlesmisRough;

    [Tooltip("Wind-sculpted sastrugi snow surface textures.")]
    [SerializeField] Texture2D surfWindColor, surfWindNormal, surfWindRough;

    [Tooltip("Tiling scale in meters.")]
    [SerializeField] float surfTileMeters = 2.5f;

    [Tooltip("Surface texture blending strength (0 = uniform color).")]
    [SerializeField, Range(0f, 1f)] float surfStrength = 0.35f;

    [Header("Object Snow Coverage (spec §16)")]
    [Tooltip("Slope threshold below which snow accumulates on objects.")]
    [SerializeField, Range(0f, 1f)] float coverSlopeThreshold = 0.25f;

    [Tooltip("Slope coverage mask sharpness.")]
    [SerializeField] float coverSlopeSharpness = 1.6f;

    [Tooltip("World scale of edge breakup noise.")]
    [SerializeField] float coverBreakupScale = 1.8f;

    [Tooltip("Edge breakup noise strength.")]
    [SerializeField, Range(0f, 1f)] float coverBreakupStrength = 0.55f;

    [Tooltip("Coverage boundary edge sharpness.")]
    [SerializeField] float coverEdgeSharpness = 4f;

    [Tooltip("Snow layer thickness on objects, meters.")]
    [SerializeField] float coverThickness = 0.04f;

    [Tooltip("Edge bulge factor.")]
    [SerializeField] float coverEdgeBulge = 0.35f;

    [Header("Snowfall (spec §17)")]
    [Tooltip("Minimum screen size of distant snowflakes, pixels.")]
    [SerializeField] float minPixelSize = 2.4f;

    [Tooltip("Flake flutter frequency, rad/s.")]
    [SerializeField] float flutterFrequency = 5.5f;

    [Tooltip("Flake flutter amplitude, meters.")]
    [SerializeField] float flutterAmplitude = 0.35f;

    [Tooltip("Flake emissive brightness under night lighting.")]
    [SerializeField] float flakeEmissive = 1f;

    [Tooltip("Spindrift blowing snow spawn rate, flakes/second.")]
    [SerializeField] float spindriftRate = 6000f;

    [Header("Initial State")]
    [Tooltip("Default SWE outside active region and on newly scrolled margins, meters.")]
    [SerializeField] float defaultSwe = 0f;

    [Tooltip("Default normalized density outside active region.")]
    [SerializeField, Range(0f, 1f)] float defaultRhoN = 0.12f;

    public SnowQualityPreset Quality => quality;
    public SnowQualityData QualityData => SnowQuality.Get(quality);

    public SnowGroundSource GroundSource => groundSource;
    public float GroundBakeArea => groundBakeArea;
    public LayerMask GroundLayerMask => groundLayerMask;

    public Texture2D BreakupNoise => breakupNoise;

    public Color ShadowTint => shadowTint;
    public float TranslucencyStrength => translucencyStrength;

    public float SparkleCellSize => sparkleCellSize;
    public float SparkleDensity => sparkleDensity;
    public float SparkleSharpness => sparkleSharpness;
    public float SparkleIntensity => sparkleIntensity;

    public float TessMax => tessMax;
    public float TessNear => tessNear;
    public float TessFar => tessFar;

    public Texture2D SurfTazeColor => surfTazeColor;
    public Texture2D SurfTazeNormal => surfTazeNormal;
    public Texture2D SurfTazeRough => surfTazeRough;
    public Texture2D SurfTozColor => surfTozColor;
    public Texture2D SurfTozNormal => surfTozNormal;
    public Texture2D SurfTozRough => surfTozRough;
    public Texture2D SurfYerlesmisColor => surfYerlesmisColor;
    public Texture2D SurfYerlesmisNormal => surfYerlesmisNormal;
    public Texture2D SurfYerlesmisRough => surfYerlesmisRough;
    public Texture2D SurfWindColor => surfWindColor;
    public Texture2D SurfWindNormal => surfWindNormal;
    public Texture2D SurfWindRough => surfWindRough;
    public float SurfTileMeters => surfTileMeters;

    public float SurfStrength => surfStrength;

    public float CoverSlopeThreshold => coverSlopeThreshold;
    public float CoverSlopeSharpness => coverSlopeSharpness;
    public float CoverBreakupScale => coverBreakupScale;
    public float CoverBreakupStrength => coverBreakupStrength;
    public float CoverEdgeSharpness => coverEdgeSharpness;
    public float CoverThickness => coverThickness;
    public float CoverEdgeBulge => coverEdgeBulge;

    public float MinPixelSize => minPixelSize;
    public float FlutterFrequency => flutterFrequency;
    public float FlutterAmplitude => flutterAmplitude;
    public float FlakeEmissive => flakeEmissive;
    public float SpindriftRate => spindriftRate;

    [System.NonSerialized] float testSweOverride = -1f;

    public bool HasTestSnow => testSweOverride >= 0f;

    public void SetTestSnow(float swe) => testSweOverride = Mathf.Max(0f, swe);
    public void ClearTestSnow() => testSweOverride = -1f;

    public float DefaultSwe => testSweOverride >= 0f ? testSweOverride : defaultSwe;
    public float DefaultRhoN => defaultRhoN;
}
