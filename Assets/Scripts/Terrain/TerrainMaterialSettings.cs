using UnityEngine;

/// Dağ yüzeyinin görünümü. Katmanların "nerede olacağı" dağın kendi biçiminden okunur
/// (bkz. SurfaceMapBaker); buradaki değerler yalnızca ne kadar ve hangi renkte olacağını
/// belirler.
[CreateAssetMenu(menuName = "To The Summit/Terrain Material", fileName = "TerrainMaterialSettings")]
public class TerrainMaterialSettings : ScriptableObject
{
    /// Ayar her değiştiğinde artan sayaç. `TerrainSurface` bunu okuyup materyale
    /// yazmayı yalnız gerçekten değişince yapıyor: kırk küsur alan her kare tekrar
    /// gönderiliyordu ve hiçbiri kendiliğinden değişmiyor.
    ///
    /// Inspector ve ayar penceresi `OnValidate`'i tetikliyor, yani canlı ayar
    /// yapılırken geri bildirim anında geliyor.
    [System.NonSerialized] public int revision;

    void OnValidate() => revision++;

    [Header("Kaya")]
    public Color rockPrimary = new(0.13f, 0.14f, 0.16f);
    [Tooltip("Jeolojik bantlarda görünen ikincil kaya.")]
    public Color rockSecondary = new(0.27f, 0.26f, 0.24f);
    [Tooltip("Yüzey tanesinin ölçeği. Büyük değer ince tane.")]
    public float grainScale = 0.08f;
    [Range(0f, 1f)] public float grainStrength = 0.35f;
    [Tooltip("Kayanın parlaklığı. Islak kaya bunun üstüne çıkar.")]
    [Range(0f, 1f)] public float rockSmoothness = 0.12f;

    [Header("Jeolojik bantlar")]
    [Tooltip("Bir bandın kalınlığı (metre).")]
    public float bandThickness = 130f;
    [Tooltip("Bantların tektonikle bükülme miktarı (metre). Sıfır = yapay düz çizgiler.")]
    public float bandWarp = 150f;
    public float bandWarpScale = 0.0016f;
    [Range(0f, 1f)] public float bandContrast = 0.5f;

    [Header("Rakım tonu")]
    [Tooltip("Alçak rakımda kayaya karışan sıcak toprak tonu.")]
    public Color lowlandTint = new(0.30f, 0.26f, 0.20f);
    [Tooltip("Yüksek rakımda buzul aşındırmasının bıraktığı soğuk ton.")]
    public Color alpineTint = new(0.29f, 0.32f, 0.37f);
    [Tooltip("Toprak tonunun tamamen bittiği yükseklik (metre).")]
    public float lowlandCeiling = 1400f;
    [Tooltip("Soğuk tonun başladığı yükseklik (metre).")]
    public float alpineFloor = 3200f;
    [Range(0f, 1f)] public float altitudeTintStrength = 0.5f;

    [Header("Liken — konkavlık, gölge ve rakım")]
    public Color lichenColor = new(0.33f, 0.35f, 0.21f);
    [Range(0f, 1f)] public float lichenAmount = 0.5f;
    [Tooltip("Likenin yaşayabildiği en yüksek kot (metre).")]
    public float lichenCeiling = 2600f;
    [Tooltip("Nemin ne kadar oyuk gerektirdiği. Yüksek değer yalnızca derin yarıklara koyar.")]
    [Range(0f, 1f)] public float lichenMoistureBias = 0.55f;
    [Tooltip("Güneş gören yüzlerde likenin ne kadar kuruduğu.")]
    [Range(0f, 1f)] public float lichenSunSensitivity = 0.7f;

    [Header("Oksit — jeolojik bantları izler")]
    public Color oxideColor = new(0.40f, 0.20f, 0.10f);
    [Tooltip("Demirli katmanların payı. Lekeler bantların dışına çıkmaz.")]
    [Range(0f, 1f)] public float oxideAmount = 0.3f;
    [Tooltip("Leke ölçeği. Küçük değer geniş lekeler.")]
    public float oxideScale = 0.004f;

    [Header("Çakıl — birikim haritasından")]
    public Color screeColor = new(0.30f, 0.29f, 0.27f);
    [Range(0f, 1f)] public float screeAmount = 0.6f;
    [Tooltip("Birikim haritasında çakılın başladığı ve tamamlandığı eşikler. " +
             "Aralık dar tutulunca yalnızca en yoğun oluklar seçilir.")]
    public Vector2 screeRange = new(0.62f, 0.88f);
    [Tooltip("Çakılın tutunabildiği en dik açı (derece).")]
    [Range(10f, 60f)] public float screeSlopeLimit = 38f;

    /// Prosedürel yüzeyin tohumu. Kaya bandı, oksit, liken, tanecik, kırılma ve
    /// birikinti şeklinin tamamı dünya koordinatına bağlı; tohum değişmeden arazi
    /// baştan üretilse bile aynı koordinatta aynı desen çıkıyor.
    ///
    /// Dağ yeniden üretildiğinde bu da artırılır, yoksa eski dağdan yerler tanıdık
    /// gelir — bir kez yaşandı ve ölçüldü.
    public int patternSeed = 2;

    [Header("Islaklık")]
    [Tooltip("Yağışta kayanın ne kadar koyulaşacağı.")]
    [Range(0f, 1f)] public float wetDarkening = 0.45f;
    [Tooltip("Islak yüzeyin kazandığı parlaklık.")]
    [Range(0f, 1f)] public float wetSmoothness = 0.6f;
    [Tooltip("Yağış dindikten sonra kuruma süresi (saniye).")]
    public float dryingSeconds = 120f;

    [Header("Yüzey kabartısı")]
    [Tooltip("Prosedürel normalin gücü. Sıfır = plastik görünüm.")]
    [Range(0f, 2f)] public float bumpStrength = 0.9f;
    public float bumpScale = 0.3f;

    [Header("Alpenglow — şafak ve gün batımı")]
    // 0.9 -> 0.35: eskiden parlama irtifa rampasıyla (tabanda ×0.35) kısılıyordu.
    // Rampa Dünya gölgesine devredilince o kısma kalktı ve her yüzey tam gücü aldı —
    // batışta sahne düz kırmızıya boyanıyordu.
    [Range(0f, 4f)] public float alpenglowStrength = 0.35f;
    [Tooltip("Güneşe bakan yüzlerin ne kadar öne çıkacağı. Sıfırda her yön eşit parlar. " +
             "Yalnız güneş ufkun üstündeyken çalışır: battıktan sonra aydınlatan şey " +
             "noktasal bir kaynak değil, kızıla boyanmış bütün gökyüzüdür.")]
    [Range(0f, 1f)] public float alpenglowFacing = 0.7f;

    [Header("Gölgelenme")]
    [Tooltip("Maruziyet haritasının ambient ışığı ne kadar kısacağı. Vadi dipleri koyulaşır.")]
    [Range(0f, 1f)] public float cavityStrength = 0.55f;
}
