using UnityEngine;

/// Şimşek çakmasının parlaklığı, rengi, zarfı ve görünür kolu.
///
/// `ThunderSettings` ile aynı sebeple ayrı bir dosyada: bileşenin üstünde durunca sahne
/// kopyası kodu eziyor ve Unity o kopyayı istediği an diske yeniden yazıyor.
///
/// Çakmanın ne kadar uzakta olduğu burada yok — onu `ThunderPlayer` seçiyor ve olayla
/// birlikte taşıyor. Buradaki mesafeler yalnızca "ne kadarı yakın sayılır" sorusunun
/// cevabı; çakmanın kendi yerine karışmıyorlar.
[CreateAssetMenu(menuName = "To The Summit/Lightning", fileName = "LightningSettings")]
public class LightningSettings : ScriptableObject
{
    [Header("Yakınlık eşiği")]
    [Tooltip("Bu mesafenin berisi tam 'yakın' sayılır (metre).")]
    public float nearDistance = 800f;
    [Tooltip("Bu mesafenin ötesi tam 'uzak' sayılır (metre).")]
    public float farDistance = 5000f;

    [Tooltip("Çakmanın oyuncunun baktığı yöne düşme eğilimi. 0 tamamen rastgele — " +
             "gökyüzünün her yönü eşit, ama görüş açısı dar olduğu için çakmaların " +
             "çoğunu kaçırırsın. 1 neredeyse hep önde.")]
    [Range(0f, 1f)] public float forwardBias = 0.65f;

    [Header("Işık")]
    [Tooltip("Referans mesafedeki ışık şiddeti. Gerçek şiddet mesafenin karesiyle söner: " +
             "iki kat uzaktaki çakma dörtte bir aydınlatır. Yakınında patlayan bir " +
             "şimşeğin gözü kamaştırması bundan. Öğlen güneşi 1.4 civarında.")]
    public float intensityAtReference = 9f;
    [Tooltip("Yukarıdaki şiddetin ölçüldüğü mesafe (metre). Bulut katmanının oyuncuya " +
             "olan yüksekliğine yakın olmalı: çakma katmanın içinde olduğu için gerçek " +
             "mesafe yatayda sıfıra gelse bile o yüksekliğin altına inemiyor. Bin metre " +
             "verilince yakın çakma bile güneşin yarısı kadar kalıyordu.")]
    public float referenceDistance = 3000f;
    [Tooltip("Şimşek soğuk ve maviye çalan bir ışıktır; güneşin sıcaklığı yoktur.")]
    public Color flashColor = new(0.80f, 0.87f, 1f);

    [Header("Gökyüzü ve bulut")]
    [Tooltip("Uzak çakmada bulut kütlesinin aldığı parlama.")]
    [Range(0f, 3f)] public float distantGlow = 0.4f;
    [Tooltip("Yakın çakmada bulut kütlesinin aldığı parlama.")]
    [Range(0f, 3f)] public float closeGlow = 1.6f;
    [Tooltip("Bulut denizinde aydınlanan lekenin yarıçapı (metre). Çakma noktasından " +
             "bu kadar uzakta parlama yarıya iner.")]
    public float glowRadius = 2500f;

    [Header("Zarf")]
    [Tooltip("Sıfırdan tam parlaklığa çıkma süresi (saniye). Şimşek anlık açılır.")]
    public float riseSeconds = 0.015f;
    [Tooltip("Yakın çakmanın sönüm zaman sabiti (saniye). Keskin ve kısa — ama gerçek " +
             "şimşeğin süresi kadar kısa değil. Altmış milisaniyede sönen bir parlama " +
             "altı kare sürüyor ve gözden kaçıyordu; gerçekte fark edilmesinin sebebi " +
             "çevresinden yüz bin kat parlak olması, burada ise yalnızca yedi kat.")]
    public float closeDecay = 0.18f;
    [Tooltip("Uzak çakmanın sönüm zaman sabiti (saniye). Işık bulut kütlesinin içinde " +
             "dağıldığı için uzaktan daha uzun ve yayvan bir parlama olarak görünür.")]
    public float distantDecay = 0.35f;
    [Tooltip("İki geri vuruş arası aralık (saniye).")]
    public Vector2 strokeGap = new(0.04f, 0.13f);

    [Header("Görünür kol")]
    [Tooltip("Kolun çizileceği en uzak mesafe (metre). Ötesinde yalnızca bulut parlar — " +
             "gerçekte de uzak şimşek kolunu göstermez, denizi aydınlatır.")]
    public float boltDistance = 2500f;
    [Tooltip("Kanalın kaç parçaya bölüneceği. Azı köşeli, fazlası ince kıvrım.")]
    [Range(4, 64)] public int boltSegments = 28;
    [Tooltip("Kanalın geniş salınımı, kendi uzunluğunun oranı olarak. Bu bir yürüyüş: " +
             "kıvrımlar birbirini sürdürüyor, bağımsız sıçramalar değil. " +
             "Metre yerine oran, çünkü çatallar ana kanaldan kat kat kısa: mutlak bir " +
             "sapma onlarda oransal olarak iki katına çıkıyor ve keskin kırılma düğüm " +
             "aralığına yaklaşarak testereye dönüyordu.")]
    [Range(0f, 0.15f)] public float boltWaviness = 0.045f;
    [Tooltip("Geniş salınımın üstüne binen keskin kırılmaların payı. Sıfırda kanal " +
             "yumuşak bir yay olarak iniyor ve cansız duruyor; birde yalnızca testere " +
             "dişi kalıyor. Gerçek kanalda iki ölçek birden var.")]
    [Range(0f, 1f)] public float boltKink = 0.35f;
    [Tooltip("Kanalın kalınlığı (metre).")]
    public float boltWidth = 14f;
    [Tooltip("Ana kanaldan ayrılan çatal sayısı.")]
    [Range(0, 8)] public int boltBranches = 5;
    [Tooltip("Çatalın ana kanala oranla uzunluğu.")]
    [Range(0.1f, 0.8f)] public float boltBranchLength = 0.6f;

    [Header("Değme noktası")]
    [Tooltip("Kolun yere değdiği yerdeki nokta ışığın şiddeti. Yönlü ışıktan farklı " +
             "olarak burası gerçekten yakında, o yüzden menzili dar tutulabiliyor.")]
    public float groundIntensity = 600f;
    [Tooltip("O ışığın menzili (metre).")]
    public float groundRange = 700f;
}
