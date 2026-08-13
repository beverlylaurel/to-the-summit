using UnityEngine;

/// Yüksekliğe bağlı hava sürücüsünün ayarları: şiddet eğrisi, dalgalanma, açık pencere,
/// yumuşatma ve iniş davranışı.
///
/// Bileşenin üstünde `[SerializeField]` olarak durdukları sürece değerin üç kopyası
/// oluyordu: koddaki varsayılan, sahnedeki serileştirilmiş kopya ve gerçekte çalışan.
/// Sahne kazanıyor, üstelik Unity sahneyi kendi belleğinden istediği an diske yeniden
/// yazıyor — koddan yapılan bir düzeltme sessizce geri alınıyordu.
///
/// Kuşak kotları burada yok ve olmamalı: onlar ayar değil, dağdan türeyen ölçü. Kurulum
/// betiği dağın zeminiyle zirvesinden hesaplayıp bileşene yazıyor, dağ değişince kendi
/// kendilerine kayıyorlar.
[CreateAssetMenu(menuName = "To The Summit/Weather Driver", fileName = "WeatherDriverSettings")]
public class WeatherDriverSettings : ScriptableObject
{
    [Header("Açılış")]
    [Tooltip("Açılışın bittiği yükseklik (zeminden metre). Buraya kadar hava dingin kalır.")]
    public float openingRise = 400f;
    [Tooltip("Açılıştaki şiddet. Çok hafif olmalı.")]
    /// SIFIR: koşu AÇIK havada başlıyor. 0.12'ydi ve tabanda sürekli hafif yağış
    /// bırakıyordu; iklim kışa çekilince o yağış kara döndü ve oyun ilk saniyeden
    /// karlı başlıyordu. Oysa tasarım "başlangıçta açık, yaklaşmanın ortasında cephe"
    /// (bkz. DECISIONS.md).
    [Range(0f, 1f)] public float openingIntensity;

    [Header("Şiddet eğrisi")]
    [Tooltip("Yağmurun tavandaki şiddeti.")]
    [Range(0f, 1f)] public float rainPeak = 0.65f;
    [Tooltip("Kar yerleştiğinde şiddet. Yağmur tavanından düşük: kar sakin başlar.")]
    [Range(0f, 1f)] public float snowBase = 0.4f;
    [Tooltip("Zirve fırtınasına girmeden hemen önceki şiddet.")]
    [Range(0f, 1f)] public float snowPeak = 0.9f;

    [Header("Dalgalanma")]
    [Tooltip("Yağmur bölgesindeki dalgalanma genliği.")]
    [Range(0f, 1f)] public float rainVariation = 0.4f;
    [Tooltip("Kar bölgesindeki dalgalanma genliği. Asıl çeşitlilik burada.")]
    [Range(0f, 1f)] public float snowVariation = 0.55f;
    [Tooltip("Havanın genel halinin değişme hızı. 0.005 ≈ 3.5 dakika.")]
    public float slowFrequency = 0.005f;
    [Tooltip("Kısa esintilerin hızı. 0.02 ≈ 50 saniye.")]
    public float fastFrequency = 0.02f;

    [Tooltip("Zirve kuşağında genliğin kalan payı. 0 yapılırsa yukarıda hava tek bir " +
             "sabit sağanağa çakılır ve saatlerce hiç değişmez. 0.3 ile aralık 0.70-1.00: " +
             "zirve hâlâ acımasız ama ölü değil.")]
    [Range(0f, 1f)] public float summitVariation = 0.3f;

    [Header("Donma seviyesi")]
    [Tooltip("Sınırın hedefe varma süresi (saniye). Sıcaklığın kendisi anında " +
             "değişebilir ama havanın o sıcaklığa oturması saatler alır; kısa tutulursa " +
             "kar sınırı esintiyle zıplar. Sınırın NEREDE olduğu artık " +
             "`TemperatureField`'den geliyor, burada yalnız varış hızı var.")]
    public float freezingSmoothSeconds = 240f;

    [Header("Açık pencere")]
    [Tooltip("Nadiren hava tamamen açılır: bulutlar aralanır, zirve görünür.")]
    [Range(0f, 1f)] public float clearWindowStrength = 0.8f;
    [Tooltip("Açık pencerelerin sıklığı. 0.0025 ≈ 7 dakikada bir dener.")]
    public float clearWindowFrequency = 0.0025f;
    [Tooltip("Pencere tam açıldığında yağıştan geriye kalan pay. Kendi yavaş " +
             "gürültüsüyle 0 ile bu değer arasında gezer: çoğu pencerede yağış tamamen " +
             "kesilir, bazılarında çiselemeye devam eder. Sabit tutulunca her açılma " +
             "birbirinin aynısı oluyordu.")]
    [Range(0f, 0.5f)] public float clearWindowResidue = 0.22f;
    [Tooltip("Kalan payın değişme hızı. Pencerenin kendi sıklığından bağımsız olmalı; " +
             "aynı frekansta ikisi kilitlenip her pencere aynı derinlikte açılıyor.")]
    public float clearWindowResidueFrequency = 0.0009f;

    [Header("Yumuşatma")]
    [Tooltip("Şiddetin hedefe varma süresi (saniye). Sağanaktan sakin kara ani geçişi engeller.")]
    public float smoothingSeconds = 25f;
    [Tooltip("Bulut kütlesinin hedefe varma süresi (saniye). Yağışınkinden çok uzun " +
             "olmalı: bulut yağış kesildikten sonra da bir süre durur. Eşitlenirse " +
             "yağışın durduğu karede gökyüzü açılır.")]
    public float cloudLagSeconds = 150f;
    [Tooltip("Ulaşılan seviyenin bu kadar altına inmek havayı etkilemez (metre). " +
             "Boyun geçişleri ve rota sapmaları fırtınayı geri almasın.")]
    public float descentDeadband = 250f;
    [Tooltip("Ölü bandı da aşarak inildiğinde havanın gerileme süresi (saniye).")]
    public float descentSeconds = 90f;

    [Header("Rüzgâr")]
    [Tooltip("Açılıştaki rüzgâr şiddeti.")]
    [Range(0f, 1f)] public float windAtBase = 0.2f;
}
