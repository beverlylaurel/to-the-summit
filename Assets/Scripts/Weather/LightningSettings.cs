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
    [Tooltip("Kolun çizileceği en uzak mesafe (metre). Ötesinde yalnızca bulut parlar.")]
    public float boltDistance = 7000f;
    [Tooltip("Kolun tam parlaklıkta göründüğü mesafe (metre). Buradan `boltDistance`'a " +
             "kadar sönerek kaybolur.\n\n" +
             "SERT KESME YERİNE SÖNÜM: eskiden tek sınır vardı ve 2499 m'de kol tamamen " +
             "görünüyor, 2501 m'de hiç görünmüyordu. Gerçekte uzak şimşek görünür, " +
             "yalnızca ince ve sönük olur; araya giren yağmur ve hava kanalı yutar.")]
    public float boltFullDistance = 1800f;
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
    // ---- DALLANMA: Reed & Wyvill 1994 ----
    //
    // Makalenin tek gerçek fiziksel gözlemi: dallar ana koldan ortalama 16 derece sapar
    // ve açılar bu değer etrafında NORMAL dağılır. Sabit bir açı kullanmak (eski hâl,
    // ~35 derece) her çatalı aynı yöne savuruyordu.
    //
    // Dallanma ÖZYİNELEMELİ: dalın dalı olur. Eski hâl tek kademeydi ve ana kanal
    // gövdeden çıkan beş çubuk gibi duruyordu; gerçek boşalma her kuşakta incelerek
    // ağaçlanıyor.
    [Tooltip("Dalın ana koldan sapma açısı — ortalama (derece). Reed & Wyvill'in " +
             "gözlemi 16 derece; bu makalenin tek ampirik sabiti.")]
    [Range(2f, 40f)] public float boltBranchAngle = 16f;
    [Tooltip("Sapma açısının dağılımı (derece). Normal dağılımın standart sapması.")]
    [Range(0f, 20f)] public float boltBranchSpread = 7f;
    [Tooltip("Sapmanın tavanı (derece). Kuyruktaki uç değerler kolu geri yukarı " +
             "savurmasın diye.")]
    [Range(10f, 80f)] public float boltBranchAngleMax = 50f;

    // DÜĞÜM BAŞINA OLASILIK DEĞİL, BEKLENEN DAL SAYISI.
    //
    // Olasılık düğüm sayısına bağlıydı ve ölçek tutmuyordu: 27 aday düğüm × 0.2 = ana
    // kanaldan 5.4 dal, her biri 4.3 tane daha → ikinci kuşakta 23 dal. Ağaç bütçe
    // tavanına dayanıyordu ve ekranda kök gibi görünüyordu (ölçüldü).
    //
    // Beklenen sayı verilince `boltSegments` değişse de dal sayısı sabit kalıyor.
    [Tooltip("Ana kanaldan doğması BEKLENEN dal sayısı. Düğüm sayısından bağımsız.")]
    [Range(0f, 8f)] public float boltBranchCount = 2.2f;
    [Tooltip("Her kuşakta beklenen sayının çarpanı. Ağacın patlamamasını bu sağlıyor.")]
    [Range(0.1f, 0.9f)] public float boltBranchCountDecay = 0.45f;
    [Tooltip("Dalın ebeveynine oranla uzunluğu. Her kuşakta tekrar uygulanıyor, yani " +
             "0.3'te ikinci kuşak ebeveynin onda biri — ağacın sonlanmasını bu sağlıyor. " +
             "Yüksek değerde dallar ana kanalla birlikte yere iniyor ve kol kök gibi " +
             "görünüyor; gerçek dal havada biter.")]
    [Range(0.1f, 0.6f)] public float boltBranchLength = 0.3f;
    [Tooltip("Her kuşakta kalınlığın çarpanı.")]
    [Range(0.2f, 0.9f)] public float boltWidthDecay = 0.5f;
    [Tooltip("Her kuşakta kıvrımlılığın çarpanı. Reed & Wyvill'de dal ebeveyninden " +
             "DAHA kıvrımlı: gücü azaldıkça yol daha çok savruluyor.")]
    [Range(0.5f, 2f)] public float boltWavinessGrowth = 1.3f;

    [Tooltip("En fazla kaç kuşak dal. 0 = yalnız ana kanal.")]
    [Range(0, 5)] public int boltGenerations = 3;
    [Tooltip("Aynı anda çizilebilecek en fazla çizgi. Ağacın bütçe tavanı; " +
             "aşılırsa dallanma kesilir.")]
    [Range(1, 64)] public int boltMaxLines = 24;

    [Header("Değme noktası")]
    [Tooltip("Kolun yere değdiği yerdeki nokta ışığın şiddeti. Yönlü ışıktan farklı " +
             "olarak burası gerçekten yakında, o yüzden menzili dar tutulabiliyor.")]
    public float groundIntensity = 600f;
    [Tooltip("O ışığın menzili (metre).")]
    public float groundRange = 700f;
}
