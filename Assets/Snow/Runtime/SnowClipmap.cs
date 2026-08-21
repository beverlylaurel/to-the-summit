// ROL: kar yüzeyini çizen iç içe halkalar. Her halka kendi quad boyutuna snap'lenir,
// vertex shader durumdan derinliği okuyup yüzeyi kaldırır.
// Çağıran: kimse — kendi LateUpdate'inde çiziyor. SnowManager'dan bağımsız çalışır,
// yalnız onun yazdığı global'leri okur.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowClipmap : MonoBehaviour
{
    /// En içteki halkanın kenarı, metre (§7.1). Dışa doğru her halka 3 katı.
    const float InnerExtent = 6f;

    /// Halka başına büyüme oranı. İç delik ızgaranın tam üçte biri olduğu için
    /// bu sayı 3'ten başka bir şey olamaz — delik bir sonraki halkanın kenarına
    /// birebir oturmak zorunda, yoksa ya çatlak ya bindirme kalır.
    const int RingScale = 3;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;
    [SerializeField] Material material;

    [Tooltip("Halkaların merkezinde duracak nesne. Normalde oyuncu.")]
    [SerializeField] Transform followTarget;

    [Tooltip("Zemin yüksekliği. Pişmeden çizim yapılmıyor.")]
    [SerializeField] SnowGroundHeight groundHeight;

    Mesh[] meshes;
    float[] quadSizes;
    int ringCount;

    /// BÜTÜN HALKALAR TEK ADIMA snap'leniyor: en kaba halkanın quad'ının iki katı.
    ///
    /// §7.1 her halkanın KENDİ quad boyuna snap'lenmesini söylüyor. Sonucu kağıtta
    /// hesaplayınca tutmuyor: halka 0 adımı 5 cm, halka 1 adımı 15 cm. İkisi ayrı
    /// yuvarlanınca merkezleri 7.5 cm'ye kadar ayrışıyor ve halka 1'in deliği halka 0'ın
    /// kenarına oturmuyor — aradan zemin görünüyor. Kabul kriterinin "halkalar arası
    /// çatlak yok" maddesi tam bunu yasaklıyor.
    ///
    /// Tek adım ikisini birden çözüyor çünkü adım her halkanın quad'ının TAM KATI:
    /// 1.35 m = 54 x 2.5 cm = 18 x 7.5 cm = 6 x 22.5 cm = 2 x 67.5 cm. Yani hem
    /// merkezler aynı hem her halkanın ızgarası dünyaya çapalı kalıyor.
    float snapStep;

    /// En dış halkanın yarım kenarı. Arazi bu yarıçap içinde kendi kar birikintisiyle
    /// kabarmıyor — iki yüzey kesişmesin diye.
    float outerHalfExtent;

    public int RingCount => ringCount;
    public int TriangleCount { get; private set; }

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException("SnowClipmap: SnowSettings atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (material == null)
            throw new System.InvalidOperationException("SnowClipmap: materyal atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (followTarget == null)
            throw new System.InvalidOperationException("SnowClipmap: takip hedefi atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        BuildRings();
    }

    void OnDisable()
    {
        if (meshes == null) return;

        for (int i = 0; i < meshes.Length; i++)
            if (meshes[i] != null) DestroyImmediate(meshes[i]);

        meshes = null;
        quadSizes = null;
        ringCount = 0;
        TriangleCount = 0;
    }

    void BuildRings()
    {
        SnowQualityData quality = settings.QualityData;

        // IZGARA ÜÇE BÖLÜNEBİLİR OLMAK ZORUNDA. İç delik ızgaranın üçte biri ve tam
        // olarak bir içteki halkanın kenarını kaplıyor; bölünmezse delik ya küçük kalıp
        // iki yüzey bindiriyor ya büyük olup çatlak açıyor.
        int grid = quality.Ring0Grid / RingScale * RingScale;
        int hole = grid / RingScale;

        ringCount = quality.RingCount;
        meshes = new Mesh[ringCount];
        quadSizes = new float[ringCount];

        TriangleCount = 0;

        for (int i = 0; i < ringCount; i++)
        {
            float extent = InnerExtent * Mathf.Pow(RingScale, i);
            float quadSize = extent / grid;

            // En içteki halkanın deliği yok: oyuncunun tam altı dolu olmalı.
            int ringHole = i == 0 ? 0 : hole;

            quadSizes[i] = quadSize;
            meshes[i] = SnowMeshBuilder.BuildRing(grid, quadSize, ringHole, "Snow Clipmap Ring " + i);

            TriangleCount += (grid * grid - ringHole * ringHole) * 2;
        }

        snapStep = quadSizes[ringCount - 1] * 2f;
        outerHalfExtent = InnerExtent * Mathf.Pow(RingScale, ringCount - 1) * 0.5f;
    }

    void LateUpdate()
    {
        if (meshes == null) return;

        // ZEMİN PIŞMEDEN ÇİZİLMİYOR. İlk karelerde yükseklik dokusu boş ve yüzey
        // Y = 0'a düşüyor; oyuncu 400 m'deyken ayaklarının çok altında beyaz bir kare
        // asılı kalıyor.
        if (groundHeight != null && groundHeight.HeightTexture == null) return;

        Vector3 follow = followTarget.position;

        // Snap YAPILMAZSA vertex'ler oyuncu yürüdükçe kayar ve yüzey dalgalanır.
        float x = Mathf.Floor(follow.x / snapStep) * snapStep;
        float z = Mathf.Floor(follow.z / snapStep) * snapStep;

        var offset = new Vector3(x, 0f, z);

        // ARAZİYE BİLDİR. Kabarması en dış halkanın içinde sönüyor; sönüm son
        // halkanın genişliğine yayılıyor ki sınırda basamak olmasın.
        float fadeStart = outerHalfExtent - InnerExtent * Mathf.Pow(RingScale, ringCount - 2) * 0.5f;
        Shader.SetGlobalVector(SnowShaderIDs.SnowClipRegion,
                               new Vector4(x, z, fadeStart, outerHalfExtent));

        for (int i = 0; i < ringCount; i++)
        {
            // GÖLGE ATMIYOR. Arazi bu halkaların altında hâlâ çiziliyor ve neredeyse
            // aynı kotta gölge atıyor; ikisi birden atınca yüzeyde düzenli şeritler
            // çıkıyor. Aynı belirti eski kar yamasında ölçüldü.
            Graphics.DrawMesh(meshes[i], Matrix4x4.Translate(offset),
                              material, gameObject.layer, null, 0, null, false, true);
        }
    }
}
