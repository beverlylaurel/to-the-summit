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
    /// HÜCRE BOYU = PARILTININ EKRANDAKİ BOYU. Bir hücre en fazla bir kristal
    /// parlatıyor, dolayısıyla nokta hücre kadar büyük görünüyor. 4 mm'de
    /// noktalar iri kalıyordu (kullanıcı bildirdi: "noktacıklar çok büyük,
    /// daha minik olmalı"). 1.5 mm gerçek kar kristali ölçeğine yakın.
    [Tooltip("Parıltı hücresinin dünya boyu, metre.")]
    [SerializeField] float sparkleCellSize = 0.0008f;

    [Tooltip("Piksel başına hedeflenen parıltı olasılığı. Mesafeden bağımsız tutuyor.")]
    [SerializeField] float sparkleDensity = 0.002f;

    [Tooltip("Parıltı konisinin keskinliği.")]
    [SerializeField] float sparkleSharpness = 8f;

    /// YOĞUNLUK VE PARLAKLIK ÖLÇÜLÜ TUTULUYOR. Önce 0.06 / 12 kullanıldı;
    /// kar yüzeyi sürekli kıvılcım saçıyordu ve kullanıcı "çok abartı, yapay
    /// duruyor" diye bildirdi. Gerçek karda parıltı seyrektir: kristallerin
    /// yalnız güneşi tam yansıtan azınlığı göze çarpar.
    [Tooltip("Parıltının parlaklığı.")]
    [SerializeField] float sparkleIntensity = 7f;

    [Header("Tessellation")]
    /// KAR YÜZEYİ GEOMETRİ, NORMAL HARİTASI DEĞİL.
    ///
    /// Terrain üçgenleri kameraya göre bölünüp kar yüzeyinin yüksekliği kadar
    /// kaydırılıyor. Normal haritası silüete ve örtüşmeye katkı vermiyor;
    /// sıyırtma açıda bir yüzeyin görünümünü tamamen o ikisi belirliyor.
    [Tooltip("En yüksek bölme faktörü. Donanım tavanı 64; Terrain köşe " +
             "aralığı 7.32 m olduğu için 64'te en ince geometri 11.4 cm.")]
    [SerializeField, Range(1f, 64f)] float tessMax = 64f;

    [Tooltip("Faktörün tam olduğu mesafe (m).")]
    [SerializeField, Min(1f)] float tessNear = 15f;

    [Tooltip("Faktörün 1'e indiği mesafe (m). Ötesinde bölme yok.")]
    [SerializeField, Min(2f)] float tessFar = 60f;

    [Header("Yüzey dokuları")]
    /// DÖRT KAR YÜZEYİ, DÖRT FİZİKSEL DURUM.
    ///
    /// Kar rengi eskiden yalnız yoğunluktan türeyen iki sabit arasında
    /// lerp'leniyordu; yüzeyin kendi dokusu yoktu ve izin prosedürel kenarı
    /// çevresindeki boş renge oturmuyordu (kullanıcı bildirdi).
    ///
    /// Dokular GLOBAL yayınlanıyor: kar mesh'i ve arazi AYNI dokuyu görmek
    /// zorunda. Mesh yalnız yerel sapmayı çiziyor, düz alanı arazi çiziyor;
    /// ikisi farklı doku kullansaydı sınır yine kendini gösterirdi.
    [Tooltip("Taze düşmüş örtü — düz, özelliksiz.")]
    [SerializeField] Texture2D surfTazeColor, surfTazeNormal, surfTazeRough;

    [Tooltip("Kuru soğuk toz kar — ince taneli.")]
    [SerializeField] Texture2D surfTozColor, surfTozNormal, surfTozRough;

    [Tooltip("Yerleşmiş / sıkışmış kar — topaklı.")]
    [SerializeField] Texture2D surfYerlesmisColor, surfYerlesmisNormal, surfYerlesmisRough;

    [Tooltip("Rüzgârın işlediği yüzey — oluklu, sastrugi.")]
    [SerializeField] Texture2D surfRuzgarColor, surfRuzgarNormal, surfRuzgarRough;

    [Tooltip("Bir döşemenin kapladığı metre.")]
    [SerializeField] float surfTileMeters = 2.5f;

    [Tooltip("Doku katkısının gücü. 0 = eski düz renk.")]
    [SerializeField, Range(0f, 1f)] float surfStrength = 0.35f;

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
    // UZAKTAKİ TANE 1.3 PİKSELDE FAZLA KESKİN.
    //
    // Taban yol doğru: tane asgari boya büyütülüp `subPixel` ile aynı oranda
    // söndürülüyor, yani enerji korunuyor. Ama 1.3 piksellik bir leke dokunun
    // alfa kenarını tek pikselde bitiriyor ve uzaktaki kar KESKİN NOKTA olarak
    // okunuyor (kullanıcı bildirdi). Gerçekte uzak kar puslanır.
    //
    // Ayak izi büyütülüp ışık aynı oranda kısılınca leke yumuşuyor; toplam
    // parlaklık değişmiyor çünkü `subPixel` boyun karesiyle bölüyor.
    [SerializeField] float minPixelSize = 2.4f;

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
    public Texture2D SurfRuzgarColor => surfRuzgarColor;
    public Texture2D SurfRuzgarNormal => surfRuzgarNormal;
    public Texture2D SurfRuzgarRough => surfRuzgarRough;
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
}
