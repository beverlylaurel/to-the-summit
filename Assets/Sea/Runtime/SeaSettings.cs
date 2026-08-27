// ROL: deniz sisteminin ayarlari. Kodda gomulu sayi yok; asset olarak
// disaridan veriliyor (CLAUDE.md — ayarlar ScriptableObject'e tasinir).
// Cagiran: SeaManager, SeaSimulation, SeaBathymetry, SeaMeshBuilder.

using UnityEngine;

/// KADEME BOYUTLARI SABİT DEĞİL AYAR.
///
/// Spec §6.6 üç kademeyi `[KALİBRASYON]` işaretliyor ve kalite presetine göre
/// değişiyorlar; `SeaConstants`'a konmadılar. İkinin kuvveti kuralı ise
/// `[KAYNAK]` — çözünürlük burada değil, `SeaConstants.FftSize`'da.
[CreateAssetMenu(menuName = "To The Summit/Deniz Ayarları", fileName = "SeaSettings")]
public class SeaSettings : ScriptableObject
{
    [Header("Kalite")]
    /// KALITE KADEMESI FFT ÇÖZÜNÜRLÜĞÜNÜ, KADEME SAYISINI VE MESH'İ SÜRER.
    /// Sayılar `SeaQuality.Of` tablosunda (spec §15.3); burada yalnız hangi
    /// kademede olduğumuz duruyor.
    [Tooltip("Kalite kademesi. Sayılar SeaQuality tablosunda.")]
    public SeaQualityPreset quality = SeaQualityPreset.Medium;

    [Header("Deniz seviyesi")]
    /// DENİZ SEVİYESİ ÖLÇÜLEREK SEÇİLDİ.
    ///
    /// Arazinin batı kenarında kıyı profili Z boyunca on iki kesitte ölçüldü.
    /// 30 m'de su şeridi z ∈ [−9000, +2000] bandında 2.7–4.1 km — istenen
    /// aralık. 10 m'de 2.0 km (dar), 60 m'de 4.0 km ama dağın eteği fazla
    /// suya giriyor.
    [Tooltip("Deniz yüzeyinin dünya Y koordinatı (m).")]
    public float seaLevelY = 30f;

    [Tooltip("Arazi dışında varsayılan su derinliği (m). Ufka kadar açık deniz.")]
    [Min(10f)] public float deepWaterDepth = 200f;

    [Header("Spektrum")]
    /// FFT tek bir derinlik değeriyle çalışıyor; yerel derinlik değişimi
    /// mesh üzerinde uygulanıyor (spec §6.4).
    [Tooltip("Spektrumun varsaydığı ortalama derinlik (m).")]
    [Min(1f)] public float spectrumDepth = 60f;

    /// Rüzgârın su üzerinde estiği mesafe. Deniz alanı ~250 km², yani
    /// karakteristik uzunluk ~15 km; fetch onun mertebesinde.
    [Tooltip("Fetch — rüzgârın su üzerinde estiği mesafe (m).")]
    [Min(100f)] public float fetch = 12000f;

    /// Kıyıdan bakılan denizde düzenli dalga trenleri isteniyor.
    /// [KAYNAK: Horvath 2015 "swell" parametresi]
    [Tooltip("Ölü dalga payı. 0 = yerel çırpıntı, 1 = uzak fırtına dalgası.")]
    [Range(0f, 1f)] public float swell = 0.72f;

    /// [KAYNAK: Tessendorf 2004 denklem 41]
    [Tooltip("Küçük dalga kesme uzunluğu (m).")]
    [Min(0.01f)] public float smallWaveCutoff = 0.15f;

    /// Tüm frekanslar bunun katına yuvarlanıyor. ZORUNLU: uzun oyun
    /// oturumlarında `t` büyüdükçe float hassasiyeti kaybını engelliyor.
    /// [KAYNAK: Tessendorf 2004 §4.2]
    [Tooltip("Döngü periyodu (s). Simülasyon bu sürede tekrar ediyor.")]
    [Min(10f)] public float loopPeriod = 200f;

    [Header("Kademeler (spec §6.6)")]
    /// ÜÇ KADEME. Tek bir yama hem 200 m'lik ölü dalgayı hem 20 cm'lik
    /// çırpıntıyı taşıyamaz. `dx` değerleri `U²/g`'den 10–1000 kat küçük
    /// olmalı (spec §6.6); U = 8 m/s için U²/g = 6.52 m.
    ///
    ///   kademe 0: 512 m / 256 = 2.00 m  → oran  3.3 (bilerek düşük, yalnız
    ///                                     uzun dalga taşıyor)
    ///   kademe 1: 128 m / 256 = 0.50 m  → oran 13.0
    ///   kademe 2:  24 m / 256 = 0.094 m → oran 69.4
    [Tooltip("Her kademenin dünyada kapladığı kare (m).")]
    public Vector3 patchSizes = new Vector3(512f, 128f, 24f);

    [Tooltip("Kademelerin toplama ağırlığı.")]
    public Vector3 tierWeights = new Vector3(1f, 1f, 1f);

    /// Choppy displacement ölçeği. Dalga tepelerini keskinleştirip çukurları
    /// genişletiyor — FFT temsilini gerçekçi kılan doğrusal olmayan davranış.
    /// [KAYNAK: Tessendorf 2004 denklem 44]
    [Tooltip("Choppy displacement ölçeği.")]
    [Range(0f, 2f)] public float choppiness = 1.1f;

    /// KADEME BAŞINA FARKLI. Yüksek dalga sayılarında tam choppiness
    /// çırpıntıyı sıkıştırıp düğümlenmeye yol açıyor (spec §6.7).
    [Tooltip("Kademe başına choppiness çarpanı.")]
    public Vector3 choppinessPerTier = new Vector3(1f, 0.85f, 0.45f);

    [Header("Sığ su (spec §8)")]
    /// Green yasası çok sığ suda sınırsıza gidiyor; gerçekte kırılma devreye
    /// giriyor. [KALİBRASYON]
    [Tooltip("Sığlaşma kazancının üst sınırı.")]
    [Range(1f, 5f)] public float maxShoalingGain = 2.2f;

    /// Kabarma bandının su seviyesini yükselttiği en büyük değer (m).
    /// Islak kum bandı bununla nefes alıyor. [KALİBRASYON]
    [Tooltip("Kabarma (run-up) bandının derinlik katkısı (m).")]
    [Range(0f, 2f)] public float runupMaxDepth = 0.45f;

    [Header("Optik (spec §12)")]
    /// Kırmızı en hızlı, mavi en yavaş sönümleniyor — suyun mavi
    /// görünmesinin sebebi. Kıyı suyu için. [KALİBRASYON]
    [Tooltip("Sönüm katsayısı, kanal başına (1/m).")]
    public Vector3 extinctionRgb = new Vector3(0.30f, 0.08f, 0.05f);

    /// [KAYNAK: Tessendorf 2004 §6.3 örnek shader — upwelling = (0, 0.2, 0.3)]
    [Tooltip("Yukarı ışıma rengi.")]
    public Color upwellingColor = new Color(0.00f, 0.20f, 0.30f);

    [Tooltip("Refraksiyon saptırma gücü.")]
    [Range(0f, 2f)] public float refractionStrength = 0.35f;

    /// Sakin ve fırtınalı yüzey pürüzlülüğü; rüzgâr hızıyla harmanlanıyor.
    [Range(0f, 0.5f)] public float roughnessCalm = 0.02f;
    [Range(0f, 0.5f)] public float roughnessRough = 0.14f;

    [Header("Köpük (spec §13)")]
    [Tooltip("Kıyı köpüğünün göründüğü derinlik (m).")]
    [Min(0.1f)] public float shoreFoamDepth = 1.2f;

    /// Köpük saçan bir yüzey, parlak değil. [KALİBRASYON]
    public Color foamColor = new Color(0.92f, 0.94f, 0.95f);
    [Range(0f, 1f)] public float foamRoughness = 0.85f;

    /// Tepe köpüğü deseninin dünya ölçeği (1/m). Desen katlanma yönünde
    /// uzatılıyor. [KALİBRASYON]
    [Tooltip("Tepe köpüğü deseni ölçeği (1/m).")]
    [Range(0.05f, 4f)] public float foamTiling = 0.8f;

    /// KIYI KÖPÜĞÜNÜN KENARI GÜRÜLTÜYLE KIRILIYOR. Kırılmazsa köpük bandı
    /// düz bir çizgi olur ve kıyı çizgisi çizilmiş gibi durur
    /// (spec §18 tuzak tablosu). [KAYNAK: Crest, SIGGRAPH 2017]
    [Tooltip("Kıyı köpüğü kenar gürültüsünün ölçeği (1/m).")]
    [Range(0.05f, 2f)] public float foamBreakupTiling = 0.35f;
}
