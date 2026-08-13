using UnityEngine;

/// BİSİKLET AYARLARI. Bütün sayılar burada; kod hiçbir değeri gömülü tutmuyor. Aynı
/// kontrolcü farklı ayar asset'iyle başka bir bisiklet olur — yüklü sefer bisikleti,
/// hafif şehir bisikleti, çocuk bisikleti.
///
/// SAYILAR FİZİKSEL. Hız tablosu yok: sürücünün gücü, aracın kütlesi, lastiğin
/// yuvarlanma direnci ve havanın sürtünmesi veriliyor, hız bunlardan ÇIKIYOR. Tablo
/// yazılsaydı her yeni eğim için yeni satır gerekirdi ve arazi değişince yalan olurdu.
///
/// Denge hızı: P = v * (Crr*m*g + m*g*sin(eğim) + 0.5*rho*CdA*v^2)
[CreateAssetMenu(menuName = "To The Summit/Bisiklet Ayarları", fileName = "BikeSettings")]
public class BikeSettings : ScriptableObject
{
    [Header("Kütle ve güç")]
    [Tooltip("Sürücü, bisiklet ve yükün toplam kütlesi (kg). Sefer yüküyle 90-100 kg.")]
    [Range(40f, 160f)] public float mass = 95f;

    [Tooltip("Sürekli pedal gücü (watt). Antrenmanlı bir sürücü 200-250 W'ı saatlerce " +
             "verebilir; 400 W ancak dakikalarca sürer.")]
    [Range(60f, 500f)] public float steadyPower = 230f;

    [Tooltip("Kısa süreli sprint gücü (watt). Dayanıklılık sistemi geldiğinde onunla " +
             "sınırlanacak; şimdilik serbest.")]
    [Range(100f, 900f)] public float sprintPower = 400f;

    [Tooltip("Tekerleğin zemine aktarabildiği en büyük itme (newton). Güç formülü düşük " +
             "hızda sonsuza gidiyor: duruştan kalkarken 230 W bölü sıfır hız. Sınır " +
             "olmazsa bisiklet yerinden fırlıyordu. 400 N, tırtıklı lastiğin toprakta " +
             "tutabildiği mertebe.")]
    [Range(50f, 1200f)] public float maxDriveForce = 400f;

    [Header("Direnç")]
    [Tooltip("Yuvarlanma direnci katsayısı. Asfalt 0.005, toprak yol 0.022, " +
             "çakıl ve patika 0.035, gevşek kum 0.06. Zemin sistemi varsa çalışma " +
             "anında değiştirilebiliyor.")]
    [Range(0.003f, 0.12f)] public float rollingResistance = 0.025f;

    [Tooltip("Sürükleme alanı CdA (m²). Dik oturuş ve sırt yüküyle 0.6-0.7; yarış " +
             "duruşunda 0.3.")]
    [Range(0.15f, 1.2f)] public float dragArea = 0.65f;

    [Tooltip("Hava yoğunluğu (kg/m³). Deniz seviyesinde 1.225; irtifada düşer ve " +
             "sürükleme azalır. Sabit bırakmak yaklaşma için yeterli.")]
    [Range(0.4f, 1.4f)] public float airDensity = 1.2f;

    [Header("Fren")]
    [Tooltip("Tam frende yavaşlama (m/s²). Bisiklette 4-6 m/s²; fazlası ön takla.")]
    [Range(1f, 12f)] public float brakeDeceleration = 5f;

    [Tooltip("Serbest bırakınca inişte ulaşılan en yüksek hız (m/s). Fizik %10 inişte " +
             "64 km/h veriyor — gerçek ama oynanabilir değil, sürücü zaten fren yapar. " +
             "Bu tavan o refleksin yerine geçiyor.")]
    [Range(4f, 25f)] public float comfortMaxSpeed = 12.5f;

    [Header("Direksiyon")]
    [Tooltip("En büyük yatma açısı (derece). Viraj yarıçapı buradan çıkıyor: " +
             "r = v² / (g·tan(yatma)). Otuz derece toprakta tutunabilen sınır.")]
    [Range(10f, 45f)] public float maxLean = 30f;

    [Tooltip("Düşük hızda dönüş hızı tavanı (derece/saniye). Fizik neredeyse duran " +
             "bisikleti yerinde döndürmeye izin veriyor; gerçekte gidon açısı sınırlı.")]
    [Range(30f, 360f)] public float maxYawRate = 120f;

    [Tooltip("Yatmanın görsel yumuşaması (saniye). Sıfır olursa bisiklet virajda " +
             "anında yatıyor ve oyuncak gibi duruyor.")]
    [Range(0.02f, 1f)] public float leanSmoothing = 0.25f;

    [Header("Zemin")]
    [Tooltip("Zemin sayılan katmanlar. Işın buraya atılıyor; oyuncunun kendi " +
             "çarpışması bu katmanda OLMAMALI.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Yerçekimi (m/s²). Havadayken ve zemine oturturken kullanılıyor.")]
    public float gravity = -9.81f;

    [Tooltip("Tekerlek yarıçapı (metre). Dönüş hızı buradan: ω = v / r. " +
             "29 inç tekerlek tırtıklı lastikle yaklaşık 0.37 m.")]
    [Range(0.15f, 0.6f)] public float wheelRadius = 0.37f;
}
