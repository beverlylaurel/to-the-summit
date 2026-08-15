using System;
using UnityEngine;

/// Günün saatini tutar ve güneşi döndürür. Havayı tanımaz.
/// Işık, sis ve renk düzenlemesi ikisini birden tüketen taraftır.
[ExecuteAlways]
public class TimeOfDay : MonoBehaviour
{
    [SerializeField] Light sun;

    [Tooltip("0 = gece yarısı, 0.25 = şafak, 0.5 = öğle, 0.75 = gün batımı.")]
    [SerializeField, Range(0f, 1f)] float normalized = 0.3f;
    [Tooltip("Tam bir günün gerçek süresi (dakika). 0 = zaman akmaz.")]
    [SerializeField] float dayLengthMinutes = 40f;
    [Tooltip("Yayın güney/kuzey eğimi. 0 = tam tepeden geçer, büyüdükçe alçak yay.")]
    [SerializeField, Range(0f, 60f)] float arcTilt = 28f;
    [Tooltip("Doğu yönünün pusula açısı (derece). Yay buna göre döner.")]
    [SerializeField] float eastHeading;

    [Header("Işık")]
    [Tooltip("Atmosfer dışındaki ham güneş rengi. Şafak tonu bundan türetilir, ayrıca " +
             "seçilmez — süzülme hesabı yapar.")]
    [SerializeField] Color sunColor = new(1f, 0.97f, 0.92f);
    [SerializeField] Color moonColor = new(0.62f, 0.70f, 0.92f);
    // 3.030782 gökyüzü paketinin kalibrasyonu: 100000 lux yer aydınlığı. Sahne kurulumu
    // da bunu yazıyor, ikisi ayrışmasın diye varsayılan burada da güncellendi.
    [SerializeField] float sunIntensity = 3.030782f;
    // Ortam probe'u donmuşken gece sahte bir maviyle doluyordu ve ay gereksiz
    // görünüyordu. Probe dürüstleşince gece gerçek değerine indi ve gökyüzünü aydınlatan
    // tek kaynak ay kaldı. Değer göz kararı bulundu.
    [SerializeField] float moonIntensity = 0.204f;

    /// Güneşin tepe şiddeti. Gökyüzü paketi kendi parlaklığını ana ışıktan türettiği için
    /// gök ile sahnenin göreli parlaklığı buradan ayarlanıyor; F1 paneli bunu sürüyor.
    /// Gök cisminin ufka göre payı, 0-1. ATMOSFERİK DEĞİL, GEOMETRİK: soğurma ve
    /// kızıllık gökyüzü paketinin işi, bu yalnız cismin ufkun üstünde olup olmadığını
    /// yumuşak bir geçişle söylüyor. sin(3°) ≈ 0.0523.
    const float HorizonBand = 0.0523f;

    static float HorizonBlend(float directionY) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-HorizonBand, HorizonBand, directionY));

    public float SunIntensity
    {
        get => sunIntensity;
        set => sunIntensity = value;
    }

    /// Ayın tepe şiddeti. Güneşle AYNI ışığa yazılıyor, yani gökyüzü paketi geceleyin ayı
    /// güneş yerine koyup atmosferi ondan aydınlatıyor. Değer ışık zinciri süzülüyorken
    /// ayarlanmıştı; ham ışığa geçince `LowSunFade` çarpanı düştü ve gece parlaklaştı.
    public float MoonIntensity
    {
        get => moonIntensity;
        set => moonIntensity = value;
    }



    public event Action<TimeOfDay> Changed;

    public float Normalized => normalized;

    /// Test için zamanı dondurur.
    public bool Paused { get; set; }

    /// Saat:dakika biçiminde okunabilir zaman.
    public string Clock
    {
        get
        {
            float hours = normalized * 24f;
            return $"{Mathf.FloorToInt(hours):00}:{Mathf.FloorToInt(hours % 1f * 60f):00}";
        }
    }

    /// Güneşin ufuk üstündeki yüksekliği: 1 tepe noktası, 0 ufuk, negatif gece.
    public float SunHeight { get; private set; }

    /// 0 tam gece, 1 tam gündüz. Gökyüzü rengi, sis rengi ve renk düzenlemesi bunu okur.
    ///
    /// Aşağıdaki `sunOverMoon` ile karıştırılmamalı: o, ışığın kaynağının güneş mi ay mı
    /// olduğunu söyleyen bir anahtar ve bilerek çok daha dar bir kuşakta döner. Bu ise
    /// "ortalık ne kadar gündüz" sorusunun cevabı ve geniş olmak zorunda. İkisi farklı
    /// sorular; birini diğerine uydurmak ya ışık kaynağını ufukta yarım saat boyunca
    /// ikiye böler ya da sabah 8 ile öğle 12'yi aynı parlaklıkta gösterir.
    public float DayFactor { get; private set; }

    /// Güneşe doğru bakan birim vektör. Gökyüzü kadranı bunu kullanır.
    static readonly int SunHeightId = Shader.PropertyToID("_SunHeight");

    public Vector3 SunDirection { get; private set; } = Vector3.up;

    /// 1 = güneş tam ufukta (şafak veya batım), 0 = tepede ya da derin gece.
    /// Sıcak turuncu tonlar buna göre karışır.
    public float HorizonFactor { get; private set; }

    /// Güneşin o andaki rengi. Şafakta turuncu, tepede beyaza yakın.
    public Color CurrentSunColor { get; private set; } = Color.white;

    public Color MoonTint => moonColor;

    /// Ay güneşin karşısındadır.
    public Vector3 MoonDirection => -SunDirection;

    /// Huzmenin atmosferden geçen payı (0-1). Renk değil ŞİDDET taşır.
    public float BeamLevel { get; private set; }
    public float MoonLevel { get; private set; }

    /// Gök ışığı: güneş battıktan sonra manzarayı aydınlatan kaynak.
    public float SkyLevel { get; private set; }

    /// Ay ışığının rengi — sabit değil, ay da ufukta kızarır.
    public Color MoonLight { get; private set; } = Color.white;

    /// TEŞHİS — yönlü ışığın o andaki şiddeti. Zincirin hangi halkasının koptuğunu
    /// ekrandan okumak için: huzme mi sıfırlanıyor, renk mi siyaha düşüyor, yoksa
    /// şiddet mi kayboluyor.
    public float LightIntensity => sun != null ? sun.intensity : 0f;



    /// Rengi tona indirger: en parlak kanal 1 olur, sönüm şiddete devredilir.
    static Color Tint(Vector3 v)
    {
        float peak = Mathf.Max(v.x, Mathf.Max(v.y, v.z));
        return peak <= 1e-6f ? Color.black
             : new Color(v.x / peak, v.y / peak, v.z / peak, 1f);
    }

    /// Öğle vakti güneşin yönü. Yüzeyin kalıcı özellikleri buna bakar: liken yıllık
    /// güneşlenmeye göre yerleşir, anlık güneş konumuna bağlanırsa gün içinde yanıp söner.
    public Vector3 NoonSunDirection => DirectionAt(0.5f);

    public void Bind(Light directional)
    {
        sun = directional;
        MarkAsSun();
    }

    /// URP ana yönlü ışığı en parlak olana göre seçiyor. Şimşek çakması güneşten
    /// parlak olduğu için o anda ana ışığı devralır ve dağın gölgeleri bir kare
    /// boyunca yer değiştirir. Güneş açıkça işaretlenince seçim sabitleniyor.
    void MarkAsSun()
    {
        // Eşitlik kontrolü gereksiz yazımı önlüyor: bu bir sahne ayarı ve her karede
        // yazılınca sahne sürekli kirleniyor.
        if (sun != null && RenderSettings.sun != sun) RenderSettings.sun = sun;
    }

    /// Test ve önizleme için saati doğrudan verir
    public void SetNormalized(float value)
    {
        normalized = Mathf.Repeat(value, 1f);
        Apply();
    }

    void OnEnable()
    {
        MarkAsSun();
        Apply();
    }

    void Update()
    {
        if (Application.isPlaying && !Paused && dayLengthMinutes > 0f)
            normalized = Mathf.Repeat(normalized + Time.deltaTime / (dayLengthMinutes * 60f), 1f);

        Apply();
    }

    /// Verilen saatte güneşin yönü. Güneş bir yay çizer: doğudan doğar, güneye eğik
    /// tepe noktasından geçer, batıdan batar. Yalnızca eğim değiştirilirse aynı noktadan
    /// doğup aynı noktaya iner — yay olmaz.
    Vector3 DirectionAt(float clock)
    {
        float angle = (clock - 0.25f) * 360f;
        var local = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);

        // Yayı güneye yatır, sonra pusulaya göre çevir
        Vector3 direction = Quaternion.Euler(0f, eastHeading, 0f)
                            * (Quaternion.AngleAxis(arcTilt, Vector3.right) * local);

        return direction.normalized;
    }

    void Apply()
    {
        SunDirection = DirectionAt(normalized);
        float elevation = SunDirection.y;

        SunHeight = elevation;

        // Geniş bir kuşakta yumuşasın: alacakaranlık aniden bitmesin.
        // Dar tutulunca sabah 8 ile öğle 12 aynı parlaklıkta görünüyordu.
        DayFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.22f, 0.45f, elevation));

        // Ufka yakınlık: şafak ve gün batımının sıcak tonlarını bu sürer
        HorizonFactor = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(Mathf.Abs(elevation) / 0.32f));

        // Işığın rengi ışık nesnesinden bağımsız bir bilgi: bulutlar, sis ve dağ yüzeyi
        // de bunu okuyor. if (sun != null) içinde kalınca hiç güncellenmiyor ve beyazda
        // donuyordu — şafak kızıllığı bu yüzden hiçbir yerde görünmüyordu.
        // ATMOSFERDEN GEÇEN HUZME. Renk seçilmiyor: Rayleigh maviyi tüketiyor, Mie
        // beyaz hâleyi kuruyor, ozon alacakaranlıkta yeşili yutup moru bırakıyor.
        // Normalizasyon YOK — huzme kızarırken SÖNMEK zorunda. Eski hâl en parlak
        // kanalı hep 1'e çekiyordu: batış sönmeyen bir kızılda kilitleniyor, göz
        // alıyordu.
        Vector3 beam = Atmosphere.BeamTransmittance(0f, SunDirection);
        Vector3 moonBeam = Atmosphere.BeamTransmittance(0f, MoonDirection);

        // Gök ışığı: güneş battıktan sonra manzarayı aydınlatan şey. Zenit yönü
        // temsilî alınıyor — tek renk yeter, yön dağılımı gökyüzü shader'ının işi.
        Vector3 sky = Atmosphere.SkyRadiance(0f, Vector3.up, SunDirection)
                    * Atmosphere.SceneGain;

        BeamLevel = (beam.x + beam.y + beam.z) / 3f;
        SkyLevel = (sky.x + sky.y + sky.z) / 3f;

        // Renk ve şiddet ayrı taşınır: tüketicilerin çoğu rengi bir TON olarak
        // kullanıyor, sönümü ışık şiddeti taşıyor. Çarpımları gerçek huzmeye eşit.
        // KISICI RENGE DE UYGULANIR. `Tint()` en parlak kanalı 1'e çektiği için huzme
        // sönerken renk tam doygun kalıyor: kısıcı yalnız şiddete uygulanınca bulutlar
        // alçak güneşte bir anda pembeleşiyordu. Renk ve şiddet aynı eğriyi izlemeli.
        float sunFade = Atmosphere.LowSunFade(0f, SunDirection);
        CurrentSunColor = Tint(Vector3.Scale(beam,
            new Vector3(sunColor.r, sunColor.g, sunColor.b))) * sunFade;

        // Ay ayrı bir fizik değil: güneş ışığının aydan yansıyıp AYNI atmosferden
        // geçmesi. Sabit mavi bir renk yapaydı — ay da ufukta kızarır.
        MoonLight = Tint(Vector3.Scale(moonBeam,
            new Vector3(moonColor.r, moonColor.g, moonColor.b)))
                  * Atmosphere.LowSunFade(0f, MoonDirection);
        MoonLevel = (moonBeam.x + moonBeam.y + moonBeam.z) / 3f;

        if (sun != null)
        {
            // KAYNAK PAYI GEOMETRİDEN, ESKİ MODELDEN DEĞİL. Önce `BeamLevel`'den
            // türetiliyordu ve o ufukta hızlı yükseliyor: güneş 3.03, ay 0.4 olduğu için
            // oran daha güneş ufkun dibindeyken 0'dan 1'e fırlıyor, ışık da onunla
            // sıçrıyordu (05:59 → 06:00 bir anda aydınlanma).
            //
            // ŞİDDET TOPLAM, HARMANLAMA DEĞİL. İki kaynağın katkısı toplanır; `Lerp`
            // kullanılırsa pay sıçradığında şiddet de sıçrar. Toplam sürekli, çünkü iki
            // terim de sürekli.
            //
            // BANT ±3°. Ham ışıkta atmosferik sönüm yok, yani şafağın TEK rampası bu.
            // ±1° (~8 dk) fazla dardı. ±3° ~24 dakikalık geçiş veriyor; ufukta (y=0)
            // güneş yarım şiddette, diskin yarısı görünüyor demek.
            float sunAbove = HorizonBlend(SunDirection.y);
            float moonAbove = HorizonBlend(MoonDirection.y);

            float sunPower = sunIntensity * sunAbove;
            float moonPower = moonIntensity * moonAbove;
            float sunShare = sunPower / Mathf.Max(1e-5f, sunPower + moonPower);

            Vector3 lightSource = sunShare > 0.5f ? SunDirection : MoonDirection;
            sun.transform.rotation = Quaternion.LookRotation(-lightSource);

            sun.color = Color.Lerp(moonColor, sunColor, sunShare);
            sun.intensity = sunPower + moonPower;
        }

        // Güneş yüksekliği GLOBAL olarak da yayınlanır. Materyal property'si olarak
        // taşınan sürüm arazi shader'ında gece kapısını kapatmadı (kar pırıltısı gece
        // boyunca çizilmeye devam etti); global yol aynı karede etkisini gösterdi.
        // Sahne geneli tek bir güneş var, değerin materyal başına anlamı da yok.
        Shader.SetGlobalFloat(SunHeightId, SunHeight);

        Changed?.Invoke(this);
    }
}
