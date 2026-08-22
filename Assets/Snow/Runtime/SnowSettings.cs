// ROL: kar sisteminin SANATSAL ayarları (spec §0.10). Fiziksel sabitler burada
// değil, SnowConstants'ta. Bir asset olarak dışarıdan veriliyor.
// Çağıran: SnowManager ve alt bileşenleri, Inspector'dan enjekte edilerek.

using UnityEngine;

/// Zemin yüksekliğinin nereden geleceği (spec §7). Kullanıcı seçer; sistem
/// kendisi karar vermez.
public enum SnowGroundSource
{
    /// Sahnedeki tek Unity Terrain'in heightmap'i bir kez RHalf dokuya pişirilir.
    UnityTerrain,

    /// Mesh tabanlı arazi: ayrı bir ortografik kamerayla bir kez bake edilir.
    MeshBake,
}

[CreateAssetMenu(menuName = "To The Summit/Kar/Kar Ayarları", fileName = "SnowSettings")]
public class SnowSettings : ScriptableObject
{
    [Header("Kalite")]
    [Tooltip("Çözünürlük, halka sayısı, detay katmanı ve VFX kapasitesini birlikte sürer.")]
    [SerializeField] SnowQualityPreset quality = SnowQualityPreset.Medium;

    [Header("Zemin")]
    [Tooltip("Zemin yüksekliğinin kaynağı. Kullanıcı seçer; sistem kendisi karar vermez.")]
    [SerializeField] SnowGroundSource groundSource = SnowGroundSource.UnityTerrain;

    [Tooltip("MeshBake yolunda bake kamerasının kapsayacağı alan, metre.")]
    [SerializeField] float groundBakeArea = 512f;

    [Tooltip("MeshBake yolunda hangi katmanların zemin sayılacağı.")]
    [SerializeField] LayerMask groundLayerMask = ~0;

    [Header("Yağış")]
    [Tooltip("Kar yüzeyinin kenarını kıran gürültü dokusu (spec §8.2, §16).")]
    [SerializeField] Texture2D breakupNoise;

    [Header("Görünüm")]
    [Tooltip("Gölgede kalan karın aldığı mavimsi ton (spec §14.3).")]
    [SerializeField] Color shadowTint = new(0.66f, 0.76f, 0.95f);

    [Tooltip("İnce karın ışığı geçirme şiddeti.")]
    [SerializeField, Range(0f, 2f)] float translucencyStrength = 0.6f;

    [Header("Parıltı (spec §14.4)")]
    [Tooltip("Parıltı hücresinin dünya boyu, metre.")]
    [SerializeField] float sparkleCellSize = 0.004f;

    [Tooltip("Piksel başına hedeflenen parıltı olasılığı. Mesafeden bağımsız tutuyor.")]
    [SerializeField] float sparkleDensity = 0.06f;

    [Tooltip("Parıltı konisinin keskinliği.")]
    [SerializeField] float sparkleSharpness = 8f;

    [Tooltip("Parıltının parlaklığı.")]
    [SerializeField] float sparkleIntensity = 12f;

    [Header("Nesne üstü kar (spec §16)")]
    [Tooltip("Bu eğimin altındaki yüzeylerde kar tutmaz.")]
    [SerializeField, Range(0f, 1f)] float coverSlopeThreshold = 0.25f;

    [Tooltip("Eğim maskesinin keskinliği.")]
    [SerializeField] float coverSlopeSharpness = 1.6f;

    [Tooltip("Kenar gürültüsünün dünya ölçeği.")]
    [SerializeField] float coverBreakupScale = 1.8f;

    [Tooltip("Kenar gürültüsünün ağırlığı.")]
    [SerializeField, Range(0f, 1f)] float coverBreakupStrength = 0.55f;

    [Tooltip("Kenarın ne kadar keskin bittiği.")]
    [SerializeField] float coverEdgeSharpness = 4f;

    [Tooltip("Nesne üstündeki karın kalınlığı, metre.")]
    [SerializeField] float coverThickness = 0.04f;

    [Tooltip("Kenarın yuvarlanma payı.")]
    [SerializeField] float coverEdgeBulge = 0.35f;

    [Header("Kar yağışı (spec §17)")]
    [Tooltip("Uzaktaki tanenin asgari ekran boyu, piksel. Altında kar kaybolur ve TAA'da titrer.")]
    [SerializeField] float minPixelSize = 1.3f;

    [Tooltip("Tanenin takla frekansı, rad/s.")]
    [SerializeField] float flutterFrequency = 5.5f;

    [Tooltip("Taklanın genliği, metre.")]
    [SerializeField] float flutterAmplitude = 0.35f;

    [Tooltip("Gece lambaların altında görünsünler diye küçük bir yayınım.")]
    [SerializeField] float flakeEmissive = 1f;

    [Tooltip("Yer savrulmasının doğum hızı, tane/saniye.")]
    [SerializeField] float spindriftRate = 6000f;

    [Header("Kar durumu başlangıcı")]
    [Tooltip("Bölge dışındaki ve yeni açılan şeritteki SWE, metre.")]
    [SerializeField] float defaultSwe = 0f;

    [Tooltip("Bölge dışındaki ve yeni açılan şeritteki normalize yoğunluk.")]
    [SerializeField, Range(0f, 1f)] float defaultRhoN = 0.12f;

    [Header("Kar çizgisi")]
    [Tooltip("Donma seviyesinin kaç metre üstünde kar TAM kalınlığa ulaşır.")]
    [SerializeField] float snowLineBand = 450f;

    [Tooltip("Kar çizgisinin üstündeki kalıcı kar (m SWE). 0.05 ≈ 25 cm derinlik.")]
    [SerializeField] float snowLineSwe = 0.05f;

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

    /// SINAMA GEÇERSİZ KILMASI. `NonSerialized`: asset'e hiç yazılmıyor,
    /// Play'den çıkınca ve her derlemede kendiliğinden sıfırlanıyor. Geri
    /// almayı unutmak MÜMKÜN DEĞİL — ayar dosyasına elle sayı yazmanın
    /// yerine bunun için var.
    [System.NonSerialized] float testSweOverride = -1f;

    public bool HasTestSnow => testSweOverride >= 0f;

    public void SetTestSnow(float swe) => testSweOverride = Mathf.Max(0f, swe);
    public void ClearTestSnow() => testSweOverride = -1f;

    public float DefaultSwe => testSweOverride >= 0f ? testSweOverride : defaultSwe;
    public float DefaultRhoN => defaultRhoN;
    public float SnowLineBand => snowLineBand;
    public float SnowLineSwe => snowLineSwe;
}
