// ROL: kar sisteminin FİZİKSEL sabitleri. Sanatsal ayarlar burada değil,
// SnowSettings ScriptableObject'inde (spec §0.10).
// Çağıran: bütün kar bileşenleri; SnowConstantsTest bu değerlerin
// SnowConstants.hlsl ile birebir aynı olduğunu doğrular.

/// SABİTLER İKİ YERDE YAŞIYOR ve birebir aynı olmak zorunda: burası CPU tarafı,
/// `Shaders/SnowConstants.hlsl` GPU tarafı. Tek yerde tutmanın yolu yok — HLSL
/// C# okuyamıyor, C# de `#define` göremiyor. Ayrışma sessizdir: simülasyon
/// GPU'da bir eşikle, CPU'daki karar başka bir eşikle çalışır ve belirti
/// "bazen oluyor bazen olmuyor" olur.
///
/// `SnowConstantsTest` her ikisini de metin olarak okuyup karşılaştırıyor.
/// Yeni bir sabit eklenince İKİSİNE de eklenecek, yoksa test kırmızı yanar.
public static class SnowConstants
{
    // --- Yoğunluk (spec §6.3) ---

    /// Taze toz kar, kg/m³.
    public const float RhoMin = 50f;

    /// Çiğnenmiş, buzlu kar, kg/m³.
    public const float RhoMax = 550f;

    /// Suyun yoğunluğu, kg/m³. SWE → derinlik dönüşümünün çarpanı.
    public const float RhoWater = 1000f;

    // --- Bölge takibi (spec §6.4) ---

    /// Bölge merkezinin snap'lendiği ızgara, metre. Snap yapılmazsa izler teksel
    /// altı kayar ve titrer; spec bunu "en zor teşhis edilen hata" diye işaretliyor.
    public const float SnapStep = 0.25f;

    // --- Kar / arazi çakışması (spec §8.1) ---

    /// Bu derinliğin altındaki kar hiç çizilmiyor, metre. Z-fighting'i tamamen
    /// kaldırıyor ve karın araziye kaybolarak karışmasını sağlıyor.
    public const float MinVisibleHeight = 0.004f;

    // --- Yakalama (spec §9.1, §9.4) ---

    /// Yakalama kamerasının oyuncunun altına indiği mesafe, metre.
    public const float CaptureBelow = 3f;

    /// Kameranın yukarı doğru gördüğü mesafe, metre. Far plane ikisinin toplamı.
    public const float CaptureAbove = 3f;

    /// Poisson blur yarıçapı, teksel (spec §9.4).
    public const float BlurRadiusTexels = 1.5f;

    // --- İz oluşumu (spec §10.1) ---

    /// Gevşek karın normalize yoğunluğu.
    public const float LooseN = 0.10f;

    /// Sıkışmış (patika) karın normalize yoğunluğu.
    public const float PackedN = 0.55f;

    /// Tam sıkışmış karda batmanın kaç katına indiği. Taze karda 1.0, patikada bu.
    public const float PackedSinkScale = 0.18f;

    /// Bir basışta yoğunluğun ne kadar arttığı.
    public const float CompactRate = 0.12f;

    // --- Kenar yığılması (spec §10.2) ---

    /// Sırt hesabında hız yönünde kaydırma, saniye.
    public const float RimVelocityBias = 0.04f;

    /// `blur(carve) − carve` farkının sırta çevrilme katsayısı.
    public const float RimStrength = 1.8f;

    /// Sırtın en fazla yüksekliği, metre.
    public const float RimMax = 0.10f;

    /// Sırt yüksekliğinin ölçekleneceği referans kar derinliği, metre.
    public const float RimRefDepth = 0.25f;

    /// Sırt blur'unun yarıçapı, teksel. Büyütülürse sırt izden uzaklaşıp yüzer.
    public const float RimBlurTexels = 7f;

    // --- İzlerin dolması (spec §10.3) ---

    /// Yağış hızının doldurma hızına çevrilme katsayısı.
    public const float FillGain = 900f;

    /// 4 m/s üstündeki her m/s'nin doldurma hızına eklediği, m/s.
    public const float WindFill = 0.0012f;

    // --- Birikme, oturma, erime (spec §11) ---

    /// Oturmanın zaman sabiti, saniye (6 saat).
    public const float SettleTau = 21600f;

    /// Tazelik kanalının sönüm zaman sabiti, saniye.
    public const float DisturbTau = 900f;

    /// Derece-gün erime katsayısı, m/(°C·s). 4 mm/(°C·gün) — standart kar DDF'i.
    public const float MeltDdf = 4.63e-8f;

    /// Rüzgâr yönlü yeniden dağıtımın şiddeti.
    public const float DriftBias = 0.45f;

    /// Yağmur karın üstüne yağarken erimenin kaç katına çıktığı.
    public const float RainMeltBoost = 2.5f;

    /// SWE'nin tavanı, metre.
    public const float SweMax = 0.60f;

    // --- Yağış (spec §3.4, §17.2) ---

    /// Bu sıcaklığın altında kar başlar, °C.
    public const float SnowOnBelow = 0.5f;

    /// Bu sıcaklığın üstünde kar durur, °C. İkisi arasındaki bant histerezis.
    public const float SnowOffAbove = 2f;

    /// Tam şiddette yağışın SWE hızı, m/s (5 mm/saat).
    public const float MaxSweRate = 1.39e-6f;

    /// Tam şiddette saniyedeki tane doğum hızı.
    public const float MaxFlakeRate = 16000f;

    // --- Gökyüzü görünürlüğü (spec §12.1) ---

    /// Gökyüzü haritasının kapsadığı alan, metre.
    public const float SkyAreaSize = 96f;

    /// Harita bu kadar kayınca yenileniyor, metre. Her frame değil.
    public const float SkyMoveThreshold = 4f;

    // --- Rüzgâr taşınımı (spec §18.0, §18.1) ---

    /// Rüzgâr-etki yüzeyinin düşey ivmesi, m/s². Cordonnier ve ark., EG 2018.
    public const float WindShadowC = 0.7f;

    /// Aşınma hızı, m/(s·s). Makaledeki 0.1 m/(s·gün)'ün saniyeye çevrilmişi.
    public const float ErosionRate = 1.16e-6f;

    /// Gevşek kar için savrulma eşiği, 10 m'deki rüzgâr hızı (m/s). PBSM varsayılanı.
    public const float DriftU10Loose = 5f;

    /// Sıkışmış kar için savrulma eşiği, m/s. Li & Pomeroy 1997 üst sınırı.
    public const float DriftU10Packed = 11f;

    // --- Isı kaynakları (spec §18.2) ---

    /// Aynı anda hesaba katılan en fazla ısı kaynağı. Uniform dizi boyutu.
    public const int MaxHeatSources = 16;

    /// Isı alanının SWE'yi eritme hızı, m SWE / (m θ · s).
    public const float HeatMeltRate = 0.0009f;

    /// Isı alanının ıslaklığı artırma hızı, 1 / (m θ · s).
    public const float HeatWetRate = 0.25f;

    // --- Kabuk (spec §18.3) ---

    /// Karın çok dengesizleştiği sıcaklık, °C.
    public const float TWarm = 5f;

    /// Erime-donma çimentolanmasının en hızlı olduğu sıcaklık, °C.
    public const float TCool = -5f;

    /// Karın yalnız kendi ağırlığıyla sıkıştığı sıcaklık, °C.
    public const float TFreeze = -20f;

    /// Kabuğun büyüme hızı, 1/s.
    public const float CrustGain = 1.4e-4f;

    /// Rüzgâr levhasının kabuğa katkısı, 1/s.
    public const float CrustWindGain = 6.0e-5f;

    /// Sıcakta kabuğun erime zaman sabiti, saniye.
    public const float CrustMeltTau = 1200f;

    /// Taze karın kabuğu örtme katsayısı.
    public const float CrustBury = 220f;

    /// Bu değerin üstündeki kabuk sağlam sayılıyor.
    public const float CrustSolid = 0.55f;

    /// Bu batmanın üstünde kabuk kırılıyor, metre.
    public const float CrustBreakPen = 0.05f;

    /// Sağlam kabuğun üstünde batmanın kaç katına indiği.
    public const float CrustSinkScale = 0.04f;

    // --- Sastrugi (spec §18.4) ---

    /// Sastrugi genliğinin zaman sabiti, saniye.
    public const float SastrugiTau = 900f;

    /// Taze karın sastrugiyi örtme katsayısı.
    public const float SastrugiBury = 260f;

    /// Sırtların yüksekliği, metre.
    public const float SastrugiHeight = 0.035f;

    /// Rüzgâr yönündeki dalga boyu, metre.
    public const float SastrugiLength = 0.35f;

    /// Rüzgâra dik sırt uzunluğu, metre. Length ile karıştırılırsa desen 90° yanlış olur.
    public const float SastrugiWidth = 1.20f;

    /// Rüzgâr yönünün yumuşatma zaman sabiti, saniye. Ham yön gust'larla titrer.
    public const float SastrugiWindTau = 120f;

    // --- İz içi AO (spec §18.5) ---

    /// Ufuk taramasının yarıçapı, metre.
    public const float AoRadius = 0.10f;

    /// AO'nun şiddeti. 0 = kapalı.
    public const float AoStrength = 1f;

    // --- Süspansiyon perdeleri (spec §18.7) ---

    /// Süspansiyon katmanının ölçek yüksekliği, metre.
    public const float SuspScaleH = 1.1f;

    /// Perde alfasının tabanı.
    public const float SuspAlphaBase = 0.16f;

    /// Süspansiyonun üst sınırı, metre. PBSM.
    public const float SuspMaxHeight = 5f;

    // --- Püskürtme (spec §18.6) ---

    /// Yerinden edilen metreküp başına parçacık sayısı.
    public const float SprayParticlesPerM3 = 40000f;

    // --- Hesaplama (spec §20) ---

    /// Compute thread group boyutu. Her zaman 8×8×1.
    public const int GroupSize = 8;
}
