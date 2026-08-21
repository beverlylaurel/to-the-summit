using System;
using UnityEngine;

/// DERİN KAR YÜZEYİ. Oyuncuyu izleyen yoğun mesh; karın görünen yüzeyini arazi
/// mesh'inden AYRI çizer.
///
/// NEDEN AYRI MESH. Arazi üçgeni 7.32 m ve donanım tessellation tavanı 64, yani en iyi
/// ihtimalle 11.4 cm. Elli santimlik bir izin karşısına dört üçgen düşüyor ve iz
/// fasetli bir oluk olarak okunuyor — ölçüldü, ekranda görüldü. RDR2 de bunu araziyle
/// yapmıyor: ayrı bir "deep snow surface" var
/// (`imgeself.github.io/posts/2020-06-19-graphics-study-rdr2`).
///
/// Burada yama 24 m kenarlı, 256×256 hücre → **9.4 cm dörtgen**. Deformasyon dokusunun
/// texel'i 4.7 cm, yani iki texel bir dörtgene düşüyor: doku geometriden ince, tersi
/// değil.
///
/// YÜKSEKLİK ARAZİNİN KENDİ HEIGHTMAP'İNDEN. `Terrain.terrainData.heightmapTexture`
/// doğrudan bağlanıyor; CPU'da `SampleHeight` çağırmak 66 bin köşe için kare başına
/// milisaniyeler eder. Yama arazi yüzeyine oturuyor, üstüne kar derinliği biniyor,
/// izler çıkarılıyor.
///
/// SNAP TEXEL IZGARASINA. Yama oyuncuyla sürekli kaysaydı köşeler arazi örneklemesi
/// içinde yüzer ve yüzey kaynardı. Konum deformasyon texel'inin katına yuvarlanıyor.
public class SnowPatch : MonoBehaviour
{
    /// İKİ HALKA. Tek yama yetmiyordu: 24 metrede ize yakın plan çözünürlüğü
    /// veriyor ama ötesinde iz kayboluyordu (kullanıcı bildirdi). Aynı hücreyle 96
    /// metreyi örtmek dört milyon köşe eder.
    ///
    /// Çözüm iki halka, ikisi de 512×512:
    ///   YAKIN  24 m → 4.7 cm hücre. Deformasyon dokusunun texel'iyle birebir.
    ///   UZAK   96 m → 18.75 cm hücre. Pencerenin tamamını örtüyor.
    ///
    /// Uzak halka yakının kapsadığı yerde kendini kesiyor, yoksa iki yüzey aynı kotta
    /// çakışırdı.
    [Tooltip("Yamanın kenarı, metre.")]
    [SerializeField] float extent = 24f;
    [Tooltip("Kenar başına hücre. Deformasyon dokusuyla hizalı olmalı.")]
    [SerializeField] int cells = 512;
    [Tooltip("İç halka mı? İç halka `_PatchHalf`'ı yazar, dış halka onu okuyup keser.")]
    [SerializeField] bool inner = true;

    float CellSize => extent / cells;

    /// Snap adımı HÜCRE BOYU.
    ///
    /// Adım hücreden küçük olduğunda köşeler her karede hücrenin içinde kayıyor,
    /// örnekleme noktası değişiyor ve yüzey titriyor. Kullanıcı "iz çok fena titriyor"
    /// dedi; sebep buydu.
    float SnapStep => CellSize;

    [Tooltip("Yamanın malzemesi (SnowPatch shader).")]
    [SerializeField] Material material;
    [Tooltip("Yüksekliğin okunduğu arazi.")]
    [SerializeField] Terrain terrain;
    [Tooltip("Yamanın izlediği gövde.")]
    [SerializeField] Transform follow;

    public void Bind(Material materialRef, Terrain terrainRef, Transform followRef,
                     float extentMetres, int cellCount, bool isInner)
    {
        material = materialRef;
        terrain = terrainRef;
        follow = followRef;
        extent = extentMetres;
        cells = cellCount;
        inner = isInner;
    }

    Mesh mesh;

    /// Arazi malzemesinin özellikleri kopyalandı mı. Yama arazinin gölgelendirme
    /// fonksiyonlarını çağırıyor ve o fonksiyonlar arazinin dokularını okuyor; yama
    /// kendi malzemesinde onlar olmadan derlenmiyor ("Unrecognized sampler").
    ///
    /// Kopya BİR KEZ: arazi malzemesi çalışma zamanında `TerrainSurface` tarafından
    /// kuruluyor, yani `OnEnable`'da henüz yok.
    bool copied;

    static readonly int PatchCenterId = Shader.PropertyToID("_PatchCenter");
    static readonly int TerrainHeightmapId = Shader.PropertyToID("_TerrainHeightmap");
    static readonly int PatchCellId = Shader.PropertyToID("_PatchCell");
    static readonly int PatchHalfId = Shader.PropertyToID("_PatchHalf");
    static readonly int HeightScaleId = Shader.PropertyToID("_TerrainHeightScale");
    static readonly int HeightUvId = Shader.PropertyToID("_TerrainHeightUv");
    static readonly int TerrainOriginId = Shader.PropertyToID("_TerrainOrigin");
    static readonly int TerrainSizeId = Shader.PropertyToID("_TerrainSize");
    static readonly int RingCenterId = Shader.PropertyToID("_RingCenter");
    static readonly int RingInnerId = Shader.PropertyToID("_RingIsOuter");

    void OnEnable()
    {
        if (material == null || terrain == null || follow == null)
            throw new InvalidOperationException($"{nameof(SnowPatch)}: bağımlılıklar atanmadı.");

        mesh = BuildMesh();
        copied = false;
    }

    void OnDisable()
    {
        // Yama gidince arazi kendini kesmeyi bırakmalı, yoksa zeminde delik kalır.
        if (inner) Shader.SetGlobalFloat(PatchHalfId, 0f);

        if (mesh == null) return;
        Destroy(mesh);
        mesh = null;
    }

    void Update()
    {
        Vector3 p = follow.position;

        // Snap: texel ızgarasının katına yuvarla. Yuvarlanmazsa köşeler arazi
        // örneklemesi içinde yüzer ve yüzey kaynar.
        float cx = Mathf.Round(p.x / SnapStep) * SnapStep;
        float cz = Mathf.Round(p.z / SnapStep) * SnapStep;

        // KOPYA ÖNCE, GLOBAL SONRA — sıra önemli.
        //
        // Arazi shader'ı `_PatchHalf` görünce kendini kesiyor. Global, yama daha
        // çizilmeden yazılsaydı arazi kendini keser ama üstünü örtecek yama olmazdı:
        // ilk karelerde zeminde delik. Arazi malzemesi çalışma zamanında kuruluyor,
        // yani bu erken çıkış gerçekten oluyor.
        var source = terrain.materialTemplate;
        if (source == null) return;

        if (!copied)
        {
            // Arazinin dokuları yamaya geçiyor: yama arazinin gölgelendirme
            // fonksiyonlarını çağırıyor ve onlar o dokuları okuyor.
            material.CopyPropertiesFromMaterial(source);
            copied = true;
        }

        // Arazi köşesi ve boyu HER KARE tazeleniyor: kopya bir kezlik ve dağ yeniden
        // üretilirse bu ikisi kayar, yama araziden kopar.
        material.SetVector(TerrainOriginId, source.GetVector(TerrainOriginId));
        material.SetVector(TerrainSizeId, source.GetVector(TerrainSizeId));

        // Global'i YALNIZ İÇ HALKA yazıyor: arazi onun kapsadığı yerde kesiliyor.
        // Dış halka da aynı değeri okuyup kendini kesiyor.
        if (inner)
        {
            Shader.SetGlobalVector(PatchCenterId, new Vector4(cx, 0f, cz, 0f));
            Shader.SetGlobalFloat(PatchHalfId, extent * 0.5f);
        }

        // Halkanın kendi merkezi ve yarım kenarı malzemede: iki halka farklı adımlarla
        // yapıştığı için merkezleri de farklı.
        material.SetVector(RingCenterId, new Vector4(cx, 0f, cz, 0f));
        material.SetFloat(RingInnerId, inner ? 0f : 1f);

        material.SetFloat(PatchCellId, CellSize);

        var data = terrain.terrainData;
        material.SetTexture(TerrainHeightmapId, data.heightmapTexture);

        // YÜKSEKLİK ÖLÇEĞİ AÇIKÇA GEÇİYOR. Unity heightmap dokusunu 0-0.5 aralığında
        // saklıyor; iki katı kotun tam boyunu veriyor. Sayı shader'a gömülseydi
        // Unity'nin iç sözleşmesi değiştiğinde sessizce kayardı.
        material.SetVector(HeightScaleId,
            new Vector4(data.size.y * 2f, terrain.transform.position.y, 0f, 0f));

        // YARIM TEXEL DÜZELTMESİ. Heightmap texel'i `i · size/(res−1)` konumuna denk
        // geliyor, yani doku arazinin köşelerinde biter — `world/size` doğrudan UV
        // değil. Düzeltilmezse yama araziden yarım texel (3.6 m) kayar ve eğimde
        // yüzeyden kopar.
        float res = data.heightmapResolution;
        material.SetVector(HeightUvId,
            new Vector4((res - 1f) / res, 0.5f / res, 0f, 0f));

        // MATRİSLE TAŞINIYOR, shader'da toplanmıyor. Köşeler yerel uzayda duruyor ve
        // dünya konumu vertex'te `_PatchCenter` eklenerek kuruluyordu: Unity o zaman
        // sınırları DÜNYA ORİJİNİNDE sanıyor ve oyuncu uzaklaşınca yamayı görüş
        // hacminden eliyordu — yama hiç çizilmiyordu.
        var transformMatrix = Matrix4x4.Translate(new Vector3(cx, 0f, cz));

        Graphics.DrawMesh(mesh, transformMatrix, material, gameObject.layer,
                          null, 0, null, true, true);
    }

    /// Izgara YEREL uzayda kuruluyor: köşe konumu (-12..12, 0, -12..12). Dünya konumu
    /// vertex shader'da `_PatchCenter` eklenerek çıkıyor, yani mesh bir kez kuruluyor
    /// ve oyuncu yürürken hiç dokunulmuyor.
    Mesh BuildMesh()
    {
        int side = cells + 1;
        var vertices = new Vector3[side * side];
        var indices = new int[cells * cells * 6];

        float half = extent * 0.5f;
        float cell = CellSize;

        for (int z = 0; z < side; z++)
        for (int x = 0; x < side; x++)
            vertices[z * side + x] = new Vector3(x * cell - half, 0f, z * cell - half);

        int t = 0;
        for (int z = 0; z < cells; z++)
        for (int x = 0; x < cells; x++)
        {
            int v = z * side + x;
            indices[t++] = v;
            indices[t++] = v + side;
            indices[t++] = v + 1;
            indices[t++] = v + 1;
            indices[t++] = v + side;
            indices[t++] = v + side + 1;
        }

        var built = new Mesh
        {
            name = "Snow Patch",
            // 513² = 263 169 köşe, 16 bit indeksin (65 536) çok üstünde.
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        built.SetVertices(vertices);
        built.SetTriangles(indices, 0);
        // Sınırlar YEREL. Matris yamayı xz'de taşıyor; y ise vertex shader'da
        // kuruluyor ve matriste yok, o yüzden kutu arazinin bütün kot aralığını
        // kapsayacak kadar yüksek. Dar tutulsaydı yama tepede ya da dipte elenirdi.
        built.bounds = new Bounds(new Vector3(0f, 3000f, 0f),
                                  new Vector3(extent, 7000f, extent));
        return built;
    }
}
