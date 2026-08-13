using UnityEngine;

/// Dağın tüm üretim parametreleri. Bootstrap ve tuner penceresi aynı asset'e bakar.
[CreateAssetMenu(fileName = "MountainSettings", menuName = "To The Summit/Mountain Settings")]
public class MountainSettings : ScriptableObject
{
    [Header("Boyut")]
    [Tooltip("2^n + 1 olmalı: 513, 1025, 2049, 4097 (Unity maksimumu).")]
    public int heightmapResolution = 4097;
    [Tooltip("Metre. Tabanın bir kenarı.")]
    public float terrainSize = 8192f;
    [Tooltip("Metre. Zirvenin maksimum yüksekliği.")]
    public float terrainHeight = 2100f;
    public int seed = 1;

    [Header("Genel Form")]
    [Tooltip("Dağ eteğinin haritaya oranı. Dışarısı düz başlangıç arazisi.")]
    [Range(0.1f, 0.48f)] public float mountainRadius = 0.38f;
    [Tooltip("Dağın silüeti. X: merkezden uzaklık (0 zirve, 1 etek). Y: yükseklik.")]
    public AnimationCurve heightProfile;
    [Tooltip("Dağın etrafındaki zemin seviyesi (0-1).")]
    [Range(0f, 0.2f)] public float baseHeight = 0.03f;
    [Tooltip("Tabanın daireden sapması. 0 = kusursuz daire, halka desenleri belirginleşir.")]
    [Range(0f, 0.6f)] public float radialDistortion = 0.3f;
    [Tooltip("Taban bozulmasının açısal frekansı. Küçük = birkaç geniş çıkıntı.")]
    [Range(0.5f, 8f)] public float radialFrequency = 2.5f;

    [Header("İkincil Zirveler")]
    [Tooltip("Ana zirvenin çevresine eklenen omuz ve yan tepe sayısı.")]
    [Range(0, 8)] public int secondaryPeaks = 3;
    [Tooltip("Yan tepelerin merkeze uzaklığı, dağ yarıçapının oranı olarak.")]
    [Range(0.1f, 0.9f)] public float peakSpread = 0.45f;
    [Tooltip("Yan tepelerin yükseklik aralığı, ana zirvenin oranı olarak.")]
    public Vector2 peakHeightRange = new(0.45f, 0.75f);
    [Tooltip("Yan tepelerin genişlik aralığı, dağ yarıçapının oranı olarak.")]
    public Vector2 peakRadiusRange = new(0.25f, 0.45f);

    [Header("Sırtlar (Ridged Noise)")]
    [Tooltip("Oktav sayısı ayar değil: çözünürlüğün taşıyabileceği kadarı otomatik kullanılır. " +
             "Fazlası aliasing, yani benek ve çukur üretir.")]
    [Range(0.5f, 8f)] public float baseFrequency = 2.5f;
    [Range(1.5f, 3f)] public float lacunarity = 2.1f;
    [Range(0.2f, 0.8f)] public float gain = 0.42f;
    [Tooltip("0 = pürüzsüz koni, 1 = tamamen sırtlarla parçalanmış.")]
    [Range(0f, 1f)] public float ridgeInfluence = 0.65f;
    [Tooltip("Etekte sırt etkisinin çarpanı. Düşük = etek yumuşak, zirve sivri.")]
    [Range(0f, 1f)] public float ridgeFootDamping = 0.35f;
    [Tooltip("Sırtların keskinliği. 1 = yuvarlak tepeler, 3 = bıçak sırtı.")]
    [Range(0.5f, 3f)] public float ridgeSharpness = 2f;

    [Header("Domain Warp (organik bozma)")]
    [Range(0f, 0.6f)] public float warpStrength = 0.35f;
    [Range(0.5f, 8f)] public float warpFrequency = 2.5f;
    [Tooltip("İkinci kademe bozma. Büyük ölçekli kıvrımların üstüne ince kıvrım ekler.")]
    [Range(0f, 0.3f)] public float warpDetailStrength = 0.08f;
    [Range(2f, 20f)] public float warpDetailFrequency = 9f;

    [Header("Kaya Bantları / Sahanlıklar")]
    [Tooltip("Geniş sahanlıklar.")]
    [Range(0f, 1f)] public float coarseTerraceStrength = 0.4f;
    [Range(2, 40)] public int coarseTerraceBands = 8;
    [Tooltip("Orta ölçek çentikler.")]
    [Range(0f, 1f)] public float fineTerraceStrength = 0.22f;
    [Range(4, 120)] public int fineTerraceBands = 40;
    [Tooltip("Büyük = bantlar daha dik, sahanlıklar daha düz.")]
    [Range(1f, 4.5f)] public float terraceSharpness = 3f;
    [Tooltip("Bant kotlarının yere göre kayması. 0 = her yerde aynı kotta (halka deseni).")]
    [Range(0f, 1f)] public float terraceOffsetAmount = 0.8f;
    [Range(0.5f, 8f)] public float terraceOffsetFrequency = 2.5f;
    [Tooltip("Teras gücünün yere göre değişimi. 1 = bazı yamaçlar tamamen basamaksız.")]
    [Range(0f, 1f)] public float terraceVariation = 0.6f;
    [Range(0.5f, 8f)] public float terraceVariationFrequency = 3.5f;

    [Header("Tepe törpüsü")]
    [Tooltip("Sivri uçların her turda ne kadar yumuşatılacağı. 0 = kapalı. Oransal: " +
             "büyük diş çok, küçük diş az iner, düzensizlik korunur. Eşikli törpü " +
             "denendi ve yanlıştı — her dişi eşiğin hemen altına tıraşlayıp eşit boyda " +
             "mini piramitlerden bir tarla bırakıyordu; tekdüzelik desen olarak okunur.")]
    [Range(0f, 1f)] public float crestSoftening = 0.4f;

    [Header("Termal Erozyon")]
    [Tooltip("0 = kapalı. Keskin kırıkları moloz yamacına çevirir, büyük formu bozmaz.")]
    [Range(0, 40)] public int erosionIterations = 12;
    [Tooltip("Malzemenin durabildiği en dik açı. Bunu aşan yamaçlar aşağı akar.")]
    [Range(30f, 70f)] public float talusAngle = 48f;
    [Tooltip("Her iterasyonda taşan malzemenin ne kadarı hareket etsin.")]
    [Range(0.1f, 0.9f)] public float erosionRate = 0.5f;

    [Header("Zirve")]
    [Tooltip("Zirvenin düzleştiği yükseklik oranı. 1 = düzleştirme yok.")]
    [Range(0.5f, 1f)] public float summitPlateauStart = 1f;
    [Tooltip("Zirve platosunun düzlüğü. 0 = sivri, 1 = tamamen düz.")]
    [Range(0f, 1f)] public float summitFlatness = 0.5f;

    /// Parametreler değişti mi anlamak için. Bootstrap gereksiz yeniden üretimi bununla atlar.
    /// Üretim tarifinin sürümü. Tarif yalnızca ayarlardan ibaret değil: kodun kendisi de
    /// tarifin parçası. Törpü penceresi bir sabitte genişletildiğinde imza değişmedi ve
    /// dağ yeniden üretilmedi — düzeltme diskte, ekranda eski dağ. Üretim kodu her
    /// değiştiğinde bu sayı artırılır.
    [Header("Ova (dağın önü)")]
    [Tooltip("Ovanın dağdan uzaklaştıkça alçalma miktarı (metre). Dağın önü düz değildir: " +
             "dereler malzemeyi aşağı taşır ve etekten dışarı doğru hafif bir yelpaze " +
             "eğimi bırakır. Sıfır = dümdüz tabla.")]
    [Range(0f, 200f)] public float forelandFanDrop = 60f;

    [Tooltip("Moren sırtlarının yüksekliği (metre). Buzulun geri çekilirken bıraktığı " +
             "yaylar; dağa göre konsantrik dizilirler.")]
    [Range(0f, 40f)] public float moraineHeight = 20f;

    [Tooltip("İki moren sırtı arası mesafe (metre).")]
    [Range(100f, 1500f)] public float moraineSpacing = 420f;

    [Tooltip("Dere yataklarının derinliği (metre). Dağdan dışarı doğru inen oyuklar; " +
             "yolun köprü ya da geçit istediği yerler bunlar.")]
    [Range(0f, 30f)] public float channelDepth = 14f;

    [Tooltip("Tepeciklerin yüksekliği (metre). Yürürken yanından geçilen kabartılar; "
             + "tırmanılacak engel değil, zeminin karakteri. Üç ölçekte dağılır (90/42/21 m) ve "
             + "yamalı uygulanır: bir yer pürüzlü, yanı düz.")]
    [Range(0f, 15f)] public float hummockHeight = 8f;

    const int RecipeVersion = 14;

    public string BuildSignature()
    {
        return string.Join("|",
            RecipeVersion,
            heightmapResolution, terrainSize, terrainHeight, seed,
            mountainRadius, baseHeight, radialDistortion, radialFrequency,
            secondaryPeaks, peakSpread, peakHeightRange, peakRadiusRange,
            baseFrequency, lacunarity, gain,
            ridgeInfluence, ridgeFootDamping, ridgeSharpness,
            warpStrength, warpFrequency, warpDetailStrength, warpDetailFrequency,
            coarseTerraceStrength, coarseTerraceBands,
            fineTerraceStrength, fineTerraceBands,
            terraceSharpness, terraceOffsetAmount, terraceOffsetFrequency,
            terraceVariation, terraceVariationFrequency,
            erosionIterations, talusAngle, erosionRate, crestSoftening,
            summitPlateauStart, summitFlatness,
            forelandFanDrop, moraineHeight, moraineSpacing, channelDepth, hummockHeight,
            CurveSignature());
    }

    string CurveSignature()
    {
        var keys = heightProfile.keys;
        var parts = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            parts[i] = $"{keys[i].time:F3},{keys[i].value:F3},{keys[i].outTangent:F2}";

        return string.Join(";", parts);
    }

    /// Form parametrelerini makul aralıklarda rastgeleler. Boyut, yükseklik ve
    /// çözünürlük korunur — onlar tasarım kararı, varyasyon konusu değil.
    public void Randomize(System.Random rng)
    {
        seed = rng.Next(1, 100000);

        mountainRadius = Range(rng, 0.30f, 0.45f);
        radialDistortion = Range(rng, 0.15f, 0.45f);
        radialFrequency = Range(rng, 1.5f, 4.5f);

        secondaryPeaks = rng.Next(0, 6);
        peakSpread = Range(rng, 0.30f, 0.65f);
        float peakLow = Range(rng, 0.35f, 0.60f);
        peakHeightRange = new Vector2(peakLow, peakLow + Range(rng, 0.10f, 0.30f));
        float radiusLow = Range(rng, 0.18f, 0.35f);
        peakRadiusRange = new Vector2(radiusLow, radiusLow + Range(rng, 0.05f, 0.20f));

        baseFrequency = Range(rng, 1.8f, 3.5f);
        lacunarity = Range(rng, 1.9f, 2.3f);
        gain = Range(rng, 0.36f, 0.50f);
        ridgeInfluence = Range(rng, 0.45f, 0.80f);
        ridgeFootDamping = Range(rng, 0.20f, 0.60f);
        ridgeSharpness = Range(rng, 1.2f, 2.6f);

        warpStrength = Range(rng, 0.20f, 0.50f);
        warpFrequency = Range(rng, 1.5f, 4f);
        warpDetailStrength = Range(rng, 0.03f, 0.14f);
        warpDetailFrequency = Range(rng, 6f, 14f);

        coarseTerraceStrength = Range(rng, 0.20f, 0.55f);
        coarseTerraceBands = rng.Next(5, 13);
        fineTerraceStrength = Range(rng, 0.10f, 0.35f);
        fineTerraceBands = rng.Next(20, 46);
        terraceSharpness = Range(rng, 2f, 3.5f);
        terraceOffsetAmount = Range(rng, 0.5f, 1f);
        terraceOffsetFrequency = Range(rng, 1.5f, 4f);
        terraceVariation = Range(rng, 0.40f, 0.85f);
        terraceVariationFrequency = Range(rng, 2f, 5f);

        erosionIterations = rng.Next(6, 25);
        talusAngle = Range(rng, 38f, 60f);
        erosionRate = Range(rng, 0.35f, 0.7f);

        // Çoğu dağın zirvesi sivridir; plato ara sıra çıksın
        summitPlateauStart = rng.NextDouble() < 0.3 ? Range(rng, 0.80f, 0.95f) : 1f;
        summitFlatness = Range(rng, 0.2f, 0.7f);
    }

    static float Range(System.Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);

    /// Etekte yayvan, yukarı çıktıkça dikleşen, zirvede sivri varsayılan silüet.
    /// Teğetler hesaplanarak verilir; nokta sürüklenince eğri şişmesin diye
    /// tuner'daki "Eğriyi düzelt" düğmesi teğetleri Auto moduna alır.
    public static AnimationCurve DefaultProfile()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.1f, 0.78f),
            new Keyframe(0.3f, 0.48f),
            new Keyframe(0.55f, 0.24f),
            new Keyframe(0.8f, 0.08f),
            new Keyframe(1f, 0f));

        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);

        return curve;
    }

    void Reset() => heightProfile = DefaultProfile();
}
