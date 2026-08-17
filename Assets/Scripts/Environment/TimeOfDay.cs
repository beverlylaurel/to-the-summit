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
    // AY ALBEDOSU — atmosferden GEÇMEDEN önceki hâli. Doğan ayın fazla turuncu olması
    // bundan geliyordu ve hesabı şu:
    //
    // Zenit optik derinliği hava yoğunluklarımızdan: R 0.046 · G 0.108 · B 0.265.
    // Ay 10°'deyken hava kütlesi ~5.6 kat, geçirgenlik R 0.77 · G 0.55 · B 0.23.
    //
    //   0.62 0.70 0.92 → 10°'de 1.00 0.80 0.43 · zenitte 0.84 0.89 1.00
    //   0.52 0.64 1.00 → 10°'de 1.00 0.87 0.56 · zenitte 0.65 0.75 1.00
    //
    // Tam telafi (0.29 0.42 1.00) turuncuyu bitirir ama tepedeki ayı mora çeker. Yarı
    // yol seçildi: doğan ayın mavisi 0.43'ten 0.56'ya çıkıyor, tepedeki ay ise fazla
    // soğumuyor.
    [SerializeField] Color moonColor = new(0.52f, 0.64f, 1.00f);
    // 3.030782 gökyüzü paketinin kalibrasyonu: 100000 lux yer aydınlığı. Sahne kurulumu
    // da bunu yazıyor, ikisi ayrışmasın diye varsayılan burada da güncellendi.
    [SerializeField] float sunIntensity = 3.030782f;
    // Ortam probe'u donmuşken gece sahte bir maviyle doluyordu ve ay gereksiz
    // görünüyordu. Probe dürüstleşince gece gerçek değerine indi ve gökyüzünü aydınlatan
    // tek kaynak ay kaldı. Değer göz kararı bulundu.
    [SerializeField] float moonIntensity = 0.204f;

    [Tooltip("Ayın kendi yönlü ışığı. Gölge DÜŞÜRMEZ: gökyüzü paketi gölgesiz cismi " +
             "ana ışık saymayıp güneşe düşüyor, böylece gökyüzü hep güneşten sürülüyor.")]
    [SerializeField] Light moon;

    /// Güneşin tepe şiddeti. Gökyüzü paketi kendi parlaklığını ana ışıktan türettiği için
    /// gök ile sahnenin göreli parlaklığı buradan ayarlanıyor; F1 paneli bunu sürüyor.
    /// Gök cisminin ışığa katkısı, 0-1. ATMOSFERİK DEĞİL, GEOMETRİK: soğurma ve kızıllık
    /// gökyüzü paketinin işi.
    ///
    /// GÜNEŞİN BANDI DERİN, AYINKI DAR — ve bu asimetri bilinçli. Paket gökyüzünü ışığın
    /// yönünden ve şiddetinden hesaplıyor: güneş ufkun altına inip şiddeti sıfırlanınca
    /// ALACAKARANLIK DA SÖNÜYOR. Oysa güneş ufkun altındayken gökyüzünü aydınlatmaya
    /// devam eder; sivil alacakaranlık yarım saat sürer. Bant −12°'ye kadar iniyor ki o
    /// saçılım pakete ulaşsın.
    ///
    /// Arazi bundan yanlış aydınlanmıyor: ışık ufkun altındayken neredeyse yatay geliyor,
    /// düz zeminde `N·L` negatif kalıyor. Güneşe bakan dik yamaçlar bir miktar ışık
    /// alıyor — alpenglow tam olarak budur.
    ///
    /// Ayın bandı dar kalıyor: ay ikincil kaynak, onun için bir alacakaranlık modellemiyoruz.
    ///
    /// TABAN −18°: ASTRONOMİK ALACAKARANLIĞIN SONU. −12° denendi ve yetmedi — güneş
    /// −11.5°'deyken (≈18:46) şiddet sıfırlanıyor, gökyüzü sönüyor, ay ise henüz taşıyacak
    /// kadar yükselmemiş oluyordu; 18:38–18:46 arası zifiri karanlık kalıyordu. Gerçekte
    /// gökyüzü −18°'ye kadar aydınlık kalır, gece orada başlar.
    ///
    /// sin(3°) ≈ 0.0523, sin(−18°) ≈ −0.3090.
    const float MoonHorizonBand = 0.0523f;
    const float SunHorizonTop = 0.0523f;
    const float SunTwilightFloor = -0.3090f;

    static float SunBlend(float directionY) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SunTwilightFloor, SunHorizonTop, directionY));

    static float MoonBlend(float directionY) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-MoonHorizonBand, MoonHorizonBand, directionY));

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

    /// Ayın kendi rengi — yüzeyinin albedosu, atmosferden GEÇMEDEN önceki hâli.
    /// Ay ufka yakınken uzun bir atmosfer yolundan geçiyor, mavi soğuruluyor ve disk
    /// sarı-turuncuya kayıyor; bu fiziksel olarak doğru. Daha soğuk bir taban seçilirse
    /// soğurma sonrası sonuç nötre yaklaşıyor.
    public Color MoonColor
    {
        get => moonColor;
        set => moonColor = value;
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

    /// Ay güneşin karşısındadır. Yalnız bu bileşen kullanıyor: dışarıdan okunacak bir
    /// şey kalmadı, ayın kendi ışığı ve gök cismi verisi buradan sürülüyor.
    Vector3 MoonDirection => -SunDirection;

    /// DÜZ ZEMİNE ULAŞAN IŞIK. İki cismin katkısı toplanıyor ve her biri KENDİ
    /// yüksekliğiyle çarpılıyor: ufkun altındaki cismin şiddeti düz zemine ulaşmıyor
    /// (`N·L` negatif). Pozlama uyumu bunu okuyor.
    public float SurfaceLightLevel
    {
        get
        {
            float level = 0f;
            if (sun != null) level += sun.intensity * Mathf.Max(0f, -sun.transform.forward.y);
            if (moon != null) level += moon.intensity * Mathf.Max(0f, -moon.transform.forward.y);
            return level;
        }
    }




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

    /// GÖK KUTBU — yıldız alanının döndüğü eksen. Güneşin yayı da bu eksen etrafında
    /// dönüyor (`DirectionAt`'te `local` XY düzleminde dönüyor, yani eksen +Z'nin aynı
    /// dönüşümden geçmiş hâli). Yıldızlara ayrı bir eksen verilseydi güneşle yıldızlar
    /// farklı yönlerde dönerdi.
    public Vector3 CelestialPole =>
        Quaternion.Euler(0f, eastHeading, 0f)
        * (Quaternion.AngleAxis(arcTilt, Vector3.right) * Vector3.forward);

    public void Bind(Light directional, Light moonLight)
    {
        sun = directional;
        moon = moonLight;
        MarkAsSun();

#if URP_PBSKY
        // Ay gökyüzü paketine İKİNCİ GÖK CİSMİ olarak veriliyor: diski ana ışıktan
        // bağımsız çiziliyor, evresi ve dünya parıltısı paketin kendi hesabından geliyor.
        PhysicallyBasedSkyURP.MoonLight = moonLight;
#endif
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
        // GÜNEŞ HUZMESİ YALNIZ RENK İÇİN. Şiddet artık buradan gelmiyor — soğurmanın
        // sahibi gökyüzü paketi, ışığa ham güneş yazılıyor. `CurrentSunColor` ise hâlâ
        // tüketiliyor: sis rengi, bulut tonu ve arazinin şafak rengi ondan besleniyor.
        Vector3 beam = Atmosphere.BeamTransmittance(0f, SunDirection);

        // Renk ve şiddet ayrı taşınır: tüketicilerin çoğu rengi bir TON olarak
        // kullanıyor, sönümü ışık şiddeti taşıyor. Çarpımları gerçek huzmeye eşit.
        // KISICI RENGE DE UYGULANIR. `Tint()` en parlak kanalı 1'e çektiği için huzme
        // sönerken renk tam doygun kalıyor: kısıcı yalnız şiddete uygulanınca bulutlar
        // alçak güneşte bir anda pembeleşiyordu. Renk ve şiddet aynı eğriyi izlemeli.
        float sunFade = Atmosphere.LowSunFade(0f, SunDirection);
        CurrentSunColor = Tint(Vector3.Scale(beam,
            new Vector3(sunColor.r, sunColor.g, sunColor.b))) * sunFade;

        // İKİ CİSİM, İKİ IŞIK. Tek ışığa sığdırmak yapısal olarak çözülemiyordu: ay
        // güneşin tam karşısında, yön bir tanedir ve devir anında disk 180° atlıyordu.
        // Artık her cisim kendi ışığını sürüyor; gökyüzü paketi ayı ikinci GÖK CİSMİ
        // olarak ayrıca çiziyor (`PhysicallyBasedSkyURP.MoonLight`).
        //
        // Bant asimetrisi duruyor: güneşinki −18°'ye (astronomik alacakaranlık sonu)
        // iner çünkü gökyüzünü o sürüyor; ayınki ±3°, ikincil kaynak için alacakaranlık
        // modellemiyoruz.
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = sunColor;

            // HAVA KÜTLESİ SÖNÜMÜ IŞIĞA DA UYGULANIYOR. Bir dönem uygulanmıyordu ve
            // gerekçe "soğurmanın sahibi gökyüzü paketi"ydi — ama paket bir Unity
            // directional light'ını söndüremez. Sonuç: gök batımda sönerken arazi tam
            // güneş almaya devam ediyordu.
            //
            // `SunBlend` sönüm değil KAPI: `SunHorizonTop` sin(3°), yani 3°'nin üstünde
            // hep 1 dönüyor. Gerçekte 3°'de doğrudan huzme zenit değerinin %5-10'u,
            // 10°'de %30, 40°'de %75.
            //
            // Ölçüldü (renk probu 2, güneşli düz zemin): güneş-gölge farkı 5+ diyafram,
            // açık havada kar için gerçek değer 2.5-3. Kontrastın patlaması buradandı.
            //
            // EN PARLAK KANAL alınıyor, parlaklık değil: `Tint()` rengi aynı kanala
            // göre normalize ediyor. Böylece `CurrentSunColor × intensity` gerçek
            // huzmeye eşit kalıyor — renk ve şiddet aynı eğriyi izliyor.
            float extinction = Mathf.Max(beam.x, Mathf.Max(beam.y, beam.z)) * sunFade;
            sun.intensity = sunIntensity * SunBlend(SunDirection.y) * extinction;
        }

        if (moon != null)
        {
            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
            moon.color = moonColor;
            moon.intensity = moonIntensity * MoonBlend(MoonDirection.y);
        }

        // Güneş yüksekliği GLOBAL olarak da yayınlanır. Materyal property'si olarak
        // taşınan sürüm arazi shader'ında gece kapısını kapatmadı (kar pırıltısı gece
        // boyunca çizilmeye devam etti); global yol aynı karede etkisini gösterdi.
        // Sahne geneli tek bir güneş var, değerin materyal başına anlamı da yok.
        Shader.SetGlobalFloat(SunHeightId, SunHeight);

        Changed?.Invoke(this);
    }
}
