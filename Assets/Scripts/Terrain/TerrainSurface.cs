using System;
using UnityEngine;

/// Dağ yüzeyi materyalini sürer. Görünüm kararlarını vermez — onlar ayarlarda ve
/// shader'da. Buradaki tek iş, paylaşılan atmosfer durumunu materyale aktarmak.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainSurface : MonoBehaviour
{
    [SerializeField] TerrainMaterialSettings settings;
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;

    [SerializeField] Texture2D surfaceMaps;
    [Tooltip("Arazinin rüzgârı hızlandırma/yavaşlatma ağırlığı. Hâkim rüzgâr yönüne "
             + "göre pişiyor; rüzgâr korunaklılığı bunu okuyor.")]
    [SerializeField] Texture2D windWeight;
    [SerializeField] Texture2D groundNormals;
    [SerializeField] Texture2DArray horizon;
    [Tooltip("Arazi yüksekliği dokusu. Sis katmanları yerden yüksekliği buradan okuyor.")]
    [SerializeField] Texture2D terrainHeights;
    [SerializeField] Shader surfaceShader;

    static readonly int SurfaceMapsId = Shader.PropertyToID("_SurfaceMaps");
    static readonly int SurfaceMapsSizeId = Shader.PropertyToID("_SurfaceMapsSize");
    static readonly int GroundNormalsId = Shader.PropertyToID("_GroundNormals");
    static readonly int HorizonId = Shader.PropertyToID("_HorizonMap");
    static readonly int TerrainOriginId = Shader.PropertyToID("_TerrainOrigin");
    static readonly int TerrainSizeId = Shader.PropertyToID("_TerrainSize");

    static readonly int RockPrimaryId = Shader.PropertyToID("_RockPrimary");
    static readonly int RockSecondaryId = Shader.PropertyToID("_RockSecondary");
    static readonly int LowlandTintId = Shader.PropertyToID("_LowlandTint");
    static readonly int AlpineTintId = Shader.PropertyToID("_AlpineTint");
    static readonly int LichenColorId = Shader.PropertyToID("_LichenColor");
    static readonly int OxideColorId = Shader.PropertyToID("_OxideColor");
    static readonly int ScreeColorId = Shader.PropertyToID("_ScreeColor");

    static readonly int GrainScaleId = Shader.PropertyToID("_GrainScale");
    static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
    static readonly int RockSmoothnessId = Shader.PropertyToID("_RockSmoothness");
    static readonly int BandThicknessId = Shader.PropertyToID("_BandThickness");
    static readonly int BandWarpId = Shader.PropertyToID("_BandWarp");
    static readonly int BandWarpScaleId = Shader.PropertyToID("_BandWarpScale");
    static readonly int BandContrastId = Shader.PropertyToID("_BandContrast");
    static readonly int LowlandCeilingId = Shader.PropertyToID("_LowlandCeiling");
    static readonly int AlpineFloorId = Shader.PropertyToID("_AlpineFloor");
    static readonly int AltitudeTintStrengthId = Shader.PropertyToID("_AltitudeTintStrength");
    static readonly int LichenAmountId = Shader.PropertyToID("_LichenAmount");
    static readonly int LichenCeilingId = Shader.PropertyToID("_LichenCeiling");
    static readonly int LichenMoistureBiasId = Shader.PropertyToID("_LichenMoistureBias");
    static readonly int LichenSunSensitivityId = Shader.PropertyToID("_LichenSunSensitivity");
    static readonly int OxideAmountId = Shader.PropertyToID("_OxideAmount");
    static readonly int OxideScaleId = Shader.PropertyToID("_OxideScale");
    static readonly int ScreeAmountId = Shader.PropertyToID("_ScreeAmount");
    static readonly int ScreeRangeId = Shader.PropertyToID("_ScreeRange");
    static readonly int ScreeSlopeLimitId = Shader.PropertyToID("_ScreeSlopeLimit");
    static readonly int PatternSeedId = Shader.PropertyToID("_PatternSeed");
    /// Yüzey başına altı doku. Son ekler shader'daki DECLARE_SURFACE_DETAIL
    /// makrosunun ürettikleriyle birebir; iki yerde ayrı yazılsaydı bir harita
    /// eklendiğinde biri sessizce boş kalırdı.
    static readonly string[] SurfaceMapSuffixes =
        { "Normal", "NormalLut", "Rough", "RoughLut", "Height", "HeightLut" };
    static readonly int WetDarkeningId = Shader.PropertyToID("_WetDarkening");
    static readonly int WetSmoothnessId = Shader.PropertyToID("_WetSmoothness");
    static readonly int BumpStrengthId = Shader.PropertyToID("_BumpStrength");
    static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    static readonly int CavityStrengthId = Shader.PropertyToID("_CavityStrength");

    static readonly int DawnColorId = Shader.PropertyToID("_SurfaceDawnColor");
    static readonly int DawnDirId = Shader.PropertyToID("_SurfaceDawnDir");
    static readonly int DawnStrengthId = Shader.PropertyToID("_SurfaceDawnStrength");
    static readonly int AlpenglowFacingId = Shader.PropertyToID("_AlpenglowFacing");

    static readonly int TerrainHeightMapId = Shader.PropertyToID("_TerrainHeightMap");
    static readonly int TerrainHeightAreaId = Shader.PropertyToID("_TerrainHeightArea");
    /// Atmosfer yazıyor, burada yalnız OKUNUYOR: rüzgâr eşiği geçildi mi ve ne kadar.
    /// Eşik kuralı atmosferin ayarında duruyor; burada ikinci kez kurmak iki sistemi
    /// ayırırdı. `PrecipitationRenderer` de aynı sebeple globalden okuyor.
    static readonly int WetnessId = Shader.PropertyToID("_SurfaceWetness");
    static readonly int WindDirId = Shader.PropertyToID("_SurfaceWindDir");
    static readonly int SunDirId = Shader.PropertyToID("_SurfaceSunDir");

    Material material;
    int appliedRevision = -1;
    float wetness;

    /// Arazinin yatay genişliği (metre). Yüzey haritası UV'si buradan türüyor;
    /// her sorguda bileşen aranmasın diye saklanıyor.
    float terrainSpan;

    /// Genişlik materyal kurulumunda yazılıyor ama okuyanlar daha erken çalışabiliyor
    /// (rüzgâr sığınağı ilk karede soruyor). Sıfırsa arazinin kendi verisinden alınır.
    float TerrainSpan
    {
        get
        {
            if (terrainSpan <= 0f) terrainSpan = GetComponent<Terrain>().terrainData.size.x;
            return terrainSpan;
        }
    }

    public TerrainMaterialSettings Settings => settings;

    /// Yüzey haritasının EĞİM kanalı (1 düz, 0 dik) verilen dünya konumunda.
    /// Kar derinliğinin CPU ikizi buradan okuyor; haritayı ve arazi sınırlarını bu
    /// bileşen tutuyor, dışarı ikinci bir kopya çıkarmıyor.
    ///
    /// Okuma bilinear ve mip 0 — shader tarafındaki `SampleSurfaceMapsFast` de öyle.
    /// Arazinin rüzgâr ağırlığı verilen dünya konumunda, 0.67-2.0. 1 nötr.
    /// Rüzgârüstü ve dışbükey yüzeyde rüzgâr hızlanır, rüzgâraltı ve içbükeyde yavaşlar
    /// (Liston & Sturm). Harita bayta sığsın diye yarıya bölünmüş saklanıyor.
    public float WindWeightAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return windWeight.GetPixelBilinear((worldPos.x - origin.x) / span,
                                           (worldPos.z - origin.z) / span).r * 2f;
    }

    public float SlopeAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return surfaceMaps.GetPixelBilinear((worldPos.x - origin.x) / span,
                                            (worldPos.z - origin.z) / span).a;
    }

    public void Bind(TerrainMaterialSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, Texture2D maps, Texture2D windMap,
        Texture2D normals, Texture2DArray horizonMap, Texture2D heightMap,
        Shader shader)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        surfaceMaps = maps;
        windWeight = windMap;
        groundNormals = normals;
        horizon = horizonMap;
        terrainHeights = heightMap;
        surfaceShader = shader;

        // Eski materyal YOK EDİLİYOR. Sadece referansı bırakmak sızıntıydı: kurulum
        // betiği her derlemede yeniden bağlıyor ve her seferinde bir materyal daha
        // sahipsiz kalıyordu. `hideFlags = DontSave` onları sahneden gizliyor, bellekten
        // değil.
        DestroyOwned(material);
        material = null;          // yeniden bağlanınca materyal de yenilensin
        appliedRevision = -1;     // yeni materyale ayarlar baştan yazılır
    }

    void OnDisable()
    {

        DestroyOwned(material);
        material = null;
    }

    /// Çalışma anında `Destroy`, editörde `DestroyImmediate`. Editörde `Destroy` bir
    /// sonraki kareye erteleniyor ve o kare hiç gelmeyebiliyor.
    static void DestroyOwned(UnityEngine.Object owned)
    {
        if (owned == null) return;

        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    void Update()
    {
        EnsureMaterial();
        ApplySettings();

        float precipitation = weather != null ? weather.Precipitation : 0f;

        // Islanma hızlı, kuruma yavaş: yağmur dininde kaya bir süre koyu kalır
        float target = precipitation;
        float duration = target > wetness ? 8f : Mathf.Max(1f, settings.dryingSeconds);
        wetness = Mathf.Lerp(wetness, target, 1f - Mathf.Exp(-Time.deltaTime / duration));
        material.SetFloat(WetnessId, wetness);

        // HÂKİM yön, anlık hız değil. Yüzey deseni bu eksene
        // oturuyor; eksen esintiyle oynayınca desen dünyada sürükleniyordu (alan
        // `dot(worldXZ, windAxis)` üzerinden kuruluyor, bkz. WindField).
        Vector3 windDir = wind != null ? wind.PrevailingDirection : Vector3.right;
        // Şiddet w'de: desen yalnızca yön değil güç de istiyor — dingin havada
        // yüzey taranmamış kalır, fırtınada çizgilenir.
        material.SetVector(WindDirId, new Vector4(windDir.x, windDir.y, windDir.z,
            wind != null ? wind.Strength : 0f));

        // Anlık güneş değil öğle güneşi: liken yıllık güneşlenmeye göre yerleşir,
        // gün içinde yanıp sönmez
        material.SetVector(SunDirId, time != null ? time.NoonSunDirection : Vector3.up);

        ApplyAlpenglow();
    }

    /// Tohumdan üç eksende kaydırma. Aynı tohum → aynı yüzey; co-op'ta senkronlanacak
    /// bir şey yok çünkü paylaşılan durum yok.
    static Vector4 PatternOffset(int seed)
    {
        // Küçük tamsayı karıştırıcı; ardışık tohumlar ilişkisiz kaydırma versin diye.
        uint h = (uint)seed * 2654435761u;
        float x = (h & 0xFFu) * 2f;
        float y = ((h >> 8) & 0xFFu) * 2f;
        float z = ((h >> 16) & 0xFFu) * 2f;
        return new Vector4(x, y, z, 0f);
    }

    /// Şafak ve batımda ufuktan gelen kızıl ışık. Rengi ve zamanlaması TimeOfDay'den
    /// gelir; ayrı bir zamanlayıcı kurulsaydı gökyüzüyle çelişirdi.
    void ApplyAlpenglow()
    {
        if (time == null)
        {
            material.SetFloat(DawnStrengthId, 0f);

            // Yön de yazılır: yazılmazsa materyalde en son ne kaldıysa o durur ve
            // bu yönü okuyan her şey (yüzey pırıltısı, gece
            // matlaşması) gündüz sanır. Ufkun altı = kaynak yok.
            material.SetVector(DawnDirId, Vector3.down);
            return;
        }

        // Ufuk çarpanı zaten şafak ve batımda tepe yapıyor. Kareyle daraltmak
        // parlamayı o iki ana sıkıştırıyor; geniş bırakılırsa öğlene kadar sürüyor.
        float horizon = time.HorizonFactor * time.HorizonFactor;

        // Güneş ufkun altına indikçe kızıllık bir süre daha sürer, sonra biter
        // Pencere daraldı: aydınlanmanın sınırını artık Dünya'nın gölgesi çiziyor
        // (shader'da h ≈ R·θ²/2). Zirve ~2100 m ve gölge o kotu güneş −1.5°'deyken
        // (SunHeight ≈ −0.026) geçiyor; ondan sonra ortada aydınlanacak yüzey
        // kalmıyor. Eski −0.18 sınırı gece boyunca boşuna güç taşıyordu.
        float alive = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.05f, time.SunHeight));

        material.SetColor(DawnColorId, time.CurrentSunColor);
        material.SetVector(DawnDirId, time.SunDirection);
        material.SetFloat(DawnStrengthId, horizon * alive * settings.alpenglowStrength);
        material.SetFloat(AlpenglowFacingId, settings.alpenglowFacing);
    }

    /// Play mode'da yeniden derleme materyali düşürebilir; kullanım anında doğrulanır.
    /// Bağımlılık kontrolü de burada: ExecuteAlways bileşende OnEnable, bileşen sahneye
    /// eklendiği anda yani Bind'den önce çalışıyor.
    ///
    /// REFERANSIN YAŞAMASI YETMİYOR, İÇİ DE DOLU OLMALI. Shader yeniden içe
    /// aktarıldığında materyal nesnesi ayakta kalıyor ama üzerine yazılmış tüm
    /// değerler siliniyor. `_TerrainSize` sıfıra düşünce yüzey uv'si
    /// `(pos - origin) / 0` oluyor ve arazinin TAMAMI NaN basıyor — ölçüldü:
    /// 162674 pikselin 162674'ü. Ekranda arazi simsiyah, kar mesh'i normal.
    /// `ApplySettings` de kurtarmıyor, `appliedRevision` eşit kaldığı için atlıyor.
    void EnsureMaterial()
    {
        if (material != null && material.HasVector(TerrainSizeId)) return;

        if (settings == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(settings)} atanmadı.");
        if (surfaceShader == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(surfaceShader)} atanmadı.");
        if (surfaceMaps == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: yüzey haritaları atanmadı.");
        var terrain = GetComponent<Terrain>();
        appliedRevision = -1;
        material = new Material(surfaceShader) { hideFlags = HideFlags.DontSave };
        terrain.materialTemplate = material;

        material.SetTexture(SurfaceMapsId, surfaceMaps);
        material.SetVector(SurfaceMapsSizeId, new Vector4(surfaceMaps.width, surfaceMaps.height,
            1f / surfaceMaps.width, 1f / surfaceMaps.height));
        material.SetTexture(GroundNormalsId, groundNormals);
        material.SetTexture(HorizonId, horizon);
        material.SetVector(TerrainOriginId, transform.position);
        material.SetVector(TerrainSizeId, terrain.terrainData.size);

        // Kar mesh'i de aynı gölgeyi okusun (gerekçe alan tanımlarının yanında).
        terrainSpan = terrain.terrainData.size.x;

        // Arazi yüksekliği GLOBAL: sis ve gökyüzü de okuyor, yalnız yüzeyin ayarı değil.
        // xy köşe konumu, z genişlik, w yükseklik ölçeği — dokudaki 0-1 değer bununla
        // metreye çevriliyor, dönüşüm tek yerde duruyor.
        Vector3 size = terrain.terrainData.size;
        Shader.SetGlobalTexture(TerrainHeightMapId, terrainHeights);
        Shader.SetGlobalVector(TerrainHeightAreaId, new Vector4(
            transform.position.x, transform.position.z, size.x, size.y));
    }

    void ApplySettings()
    {
        if (appliedRevision == settings.revision) return;
        appliedRevision = settings.revision;

        material.SetColor(RockPrimaryId, settings.rockPrimary);
        material.SetColor(RockSecondaryId, settings.rockSecondary);
        material.SetColor(LowlandTintId, settings.lowlandTint);
        material.SetColor(AlpineTintId, settings.alpineTint);
        material.SetColor(LichenColorId, settings.lichenColor);
        material.SetColor(OxideColorId, settings.oxideColor);
        material.SetColor(ScreeColorId, settings.screeColor);

        material.SetFloat(GrainScaleId, settings.grainScale);
        material.SetFloat(GrainStrengthId, settings.grainStrength);
        material.SetFloat(RockSmoothnessId, settings.rockSmoothness);
        material.SetFloat(BandThicknessId, settings.bandThickness);
        material.SetFloat(BandWarpId, settings.bandWarp);
        material.SetFloat(BandWarpScaleId, settings.bandWarpScale);
        material.SetFloat(BandContrastId, settings.bandContrast);
        material.SetFloat(LowlandCeilingId, settings.lowlandCeiling);
        material.SetFloat(AlpineFloorId, settings.alpineFloor);
        material.SetFloat(AltitudeTintStrengthId, settings.altitudeTintStrength);
        material.SetFloat(LichenAmountId, settings.lichenAmount);
        material.SetFloat(LichenCeilingId, settings.lichenCeiling);
        material.SetFloat(LichenMoistureBiasId, settings.lichenMoistureBias);
        material.SetFloat(LichenSunSensitivityId, settings.lichenSunSensitivity);
        material.SetFloat(OxideAmountId, settings.oxideAmount);
        material.SetFloat(OxideScaleId, settings.oxideScale);
        material.SetFloat(ScreeAmountId, settings.screeAmount);
        material.SetVector(ScreeRangeId, settings.screeRange);
        // TOHUM GLOBAL, materyal alanı DEĞİL: iki hash kökü iki ayrı dosyada ve biri
        // yer değiştirme geçişinde okunuyor. Materyale yazılırsa o geçiş göremez.
        //
        // Kaydırma tohumdan TÜRETİLİYOR, elle girilmiyor: üç eksen birbirinden bağımsız
        // ve 512'lik hash sarmasının içinde kalıyor (`MountainHash`'te `fmod(..., 512)`).
        Shader.SetGlobalVector(PatternSeedId, PatternOffset(settings.patternSeed));

        material.SetFloat(ScreeSlopeLimitId, settings.screeSlopeLimit);

        material.SetFloat(WetDarkeningId, settings.wetDarkening);
        material.SetFloat(WetSmoothnessId, settings.wetSmoothness);
        material.SetFloat(BumpStrengthId, settings.bumpStrength);
        material.SetFloat(BumpScaleId, settings.bumpScale);
        material.SetFloat(CavityStrengthId, settings.cavityStrength);
    }
}
