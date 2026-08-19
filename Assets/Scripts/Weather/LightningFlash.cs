using System;
using UnityEngine;

/// Şimşeği dünyaya yerleştirir ve aydınlatmasını sürer: kendi yönlü ışığını ve gökyüzü
/// ile bulutun okuduğu parlama değerlerini yazar.
///
/// Ne zaman çakacağını ve ne kadar uzakta olduğunu bilmez — `ThunderPlayer` söyler.
/// Havayı, rüzgârı, saati okumaz; tetikleyen taraf zaten okuyor. Buranın rastgeleliği
/// yalnızca çakmanın hangi yönde olduğu ve kaç geri vuruş yaptığı.
///
/// Işık yönlü kalıyor. Çakma bulutun içinde, yani iki kilometrenin üstünde: beş yüz metre
/// ötede çakan bir şimşeğin ayağının dibindeki kayaya uzaklığı 2550 m, üç kilometre
/// ötedekine 3900 m — arazi boyunca yalnızca 2.3 kat fark. Buna karşılık menzili tüm
/// sahneyi kaplayan bir nokta ışık Forward+ kümelemesini işlevsiz bırakırdı. Baskın ipucu
/// olan "yakın çakma kör eder, uzak çakma soluk kalır" mesafenin karesinden geliyor ve o
/// bedava; yön de artık gerçek konumdan türüyor. Yere inen kolun değme noktasındaki nokta
/// ışık `LightningBolt`'un işi — orası gerçekten yakın olduğu için menzili dar tutulabiliyor.
[RequireComponent(typeof(Light))]
public class LightningFlash : MonoBehaviour
{
    static readonly int FlashId = Shader.PropertyToID("_LightningFlash");
    static readonly int PositionId = Shader.PropertyToID("_LightningPosition");
    static readonly int ScatterLutId = Shader.PropertyToID("_LightningScatterLut");
    static readonly int ScatterTId = Shader.PropertyToID("_LightningScatterT");
    static readonly int SourcesId = Shader.PropertyToID("_LightningSources");
    static readonly int SourceCountId = Shader.PropertyToID("_LightningSourceCount");

    /// KANAL BOYUNCA KAÇ NOKTA KAYNAK. Makale 50 kullanıyor ama çevrimdışı render için
    /// `[Dobashi 2001, §5.1]`; bizde piksel başına o kadar tablo örneklemesi pahalı.
    ///
    /// Sekiz, kanalın şeklini taşımaya yetiyor: tek kaynakta parlama küre gibi duruyor
    /// ve kolun nereye uzandığını anlatmıyor. Sayı arttıkça kazanç hızla azalıyor çünkü
    /// tablo zaten yumuşak.
    const int SourceCount = 8;

    /// Bir çakmanın taşıyabileceği en fazla geri vuruş sayısı
    const int MaxStrokes = 3;

    [SerializeField] ThunderPlayer thunder;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] CloudLayerProbe cloudLayer;
    [SerializeField] Transform observer;
    [SerializeField] LightningSettings settings;

    /// Atmosferik saçılma tablosu (`LightningLutBaker` pişiriyor). Sis ve gök buradan
    /// okuyor; tablo statik olduğu için bir kez yazılıyor, kare kare değil.
    /// Kanalın nerede bittiğini bilmek için: kaynaklar buluttan YERE dağılıyor.
    /// Sabit bir boy kullanmak, dik yamaçta kanalı arazinin içine gömüyordu.
    [SerializeField] Terrain terrain;

    [SerializeField] Texture2D scatterLut;
    [SerializeField] float scatterCutoff = 9000f;

    /// Çakmanın dünyadaki yeri ve ne kadar sürdüğü. Kolu çizen taraf buradan besleniyor:
    /// konumu ikinci kez seçseydi ışık bir yerde, kol başka bir yerde olurdu.
    public event Action<LightningStrike> Placed;

    /// Çakmayı tepe değerinde dondurur. Bir çakma 0.1 saniye sürüyor; "şurası aydınlandı
    /// mı" sorusunu altı karede cevaplamak mümkün değil. Test anahtarı.
    public bool Held { get; set; }

    /// Son çakmanın uzaklığı (metre). Hiç çakmadıysa -1.
    public float LastDistance { get; private set; } = -1f;

    /// Şu andaki ışık şiddeti ve bulut parlaması. Panelde okunuyor: bir çakmanın
    /// görünmemesi ya olayın hiç gelmemesinden ya da çizilmemesinden olabilir, ikisi
    /// dışarıdan aynı görünüyor.
    public float Intensity => flash != null ? flash.intensity : 0f;
    public float Glow { get; private set; }

    Light flash;

    readonly float[] strokeTime = new float[MaxStrokes];
    readonly float[] strokeAmplitude = new float[MaxStrokes];
    int strokeCount;

    bool active;
    float elapsed;
    float duration;
    float decayTau;
    float peakIntensity;
    float peakGlow;
    Vector3 origin;
    readonly Vector4[] sources = new Vector4[SourceCount];

    /// Sahne kurulumu ışığı da buradan yapılandırır: biçimi bileşenin kendi işi,
    /// kurulum betiğine dağılmamalı.
    public void Bind(ThunderPlayer source, AtmosphereController air, Transform eye,
        LightningSettings tuning, CloudLayerProbe layer, Texture2D lut, float cutoff,
        Terrain ground)
    {
        terrain = ground;
        thunder = source;
        atmosphere = air;
        cloudLayer = layer;
        observer = eye;
        settings = tuning;
        scatterLut = lut;
        scatterCutoff = cutoff;

        PublishLut();

        var light = GetComponent<Light>();
        light.type = LightType.Directional;

        // Gölge kapalı. URP ana yönlü ışığı en parlak olana göre seçiyor; çakma anında
        // güneşten parlak olan bu ışık ana ışığı devralır ve dağın gölgeleri bir kare
        // boyunca yer değiştirir. Gölgesiz kalınca yalnızca ek ışık olarak toplanıyor.
        light.shadows = LightShadows.None;
        light.color = tuning.flashColor;
        light.intensity = 0f;
    }

    void OnEnable()
    {
        if (thunder == null || atmosphere == null || observer == null || settings == null)
            throw new InvalidOperationException(
                $"{nameof(LightningFlash)}: bağımlılıklar atanmadı.");

        flash = GetComponent<Light>();
        flash.color = settings.flashColor;

        thunder.Struck += OnStruck;

        PublishLut();
        Apply(0f);
    }

    void OnDisable()
    {
        thunder.Struck -= OnStruck;
        active = false;

        Apply(0f);
    }

    void Update()
    {
        if (Held)
        {
            if (LastDistance >= 0f) Apply(1f);
            return;
        }

        if (!active) return;

        elapsed += Time.deltaTime;

        if (elapsed >= duration)
        {
            active = false;
            Apply(0f);
            return;
        }

        Apply(Envelope(elapsed));
    }

    /// distance: çakmanın metre cinsinden uzaklığı
    void OnStruck(float distance)
    {
        float nearness = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(settings.nearDistance, settings.farDistance, distance));

        // Yön oyuncunun baktığı tarafa ağırlıklı. Tamamen rastgele dağıtmak "doğru" ama
        // görüş açısı gökyüzünün beşte birini gördüğü için çakmaların çoğu arkada kalıyor
        // ve fırtına boş görünüyordu. Kaçırılan bir şimşek hiç çakmamış demektir.
        float look = Mathf.Atan2(observer.forward.z, observer.forward.x);
        float spread = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.28f, settings.forwardBias);
        float bearing = look + UnityEngine.Random.Range(-spread, spread);

        Vector3 eye = observer.position;
        Vector3 strike = new Vector3(eye.x + Mathf.Cos(bearing) * distance, 0f,
                                     eye.z + Mathf.Sin(bearing) * distance);

        // Şimşek bulutun içinde boşalır. Katmanın alt çeyreğine yerleşiyor: kanal aşağı
        // doğru büyüdüğü için görünen boşalma tabana yakın oluyor. Tepe ÇAKMA SÜTUNUNDAN
        // okunuyor; sütun boşsa katmanın azami tepesi kullanılıyor.
        float top = cloudLayer.TopAt(strike);
        if (float.IsPositiveInfinity(top)) top = cloudLayer.MaxTop;
        strike.y = Mathf.Lerp(cloudLayer.Bottom, top, 0.25f);
        origin = strike;

        // KAYNAKLAR KANAL BOYUNCA. Tek kaynak bütün kolu temsil edince parlama küre
        // gibi duruyor ve kolun nereye uzandığını anlatmıyor; makalenin yöntemi zaten
        // kolları nokta kaynak dizisine çevirmek (§3.2).
        //
        // Uç noktalar: boşalma noktası (bulutun alt çeyreği) ve yamacın kendisi.
        // Enerji kaynaklara BÖLÜNÜYOR (shader toplamı sayıya bölüyor), yani toplam
        // parlaklık değişmiyor — değişen yalnız nereye dağıldığı.
        float groundY = terrain != null
            ? terrain.SampleHeight(strike) + terrain.transform.position.y
            : cloudLayer.Bottom - 1000f;

        Vector3 foot = new Vector3(strike.x, groundY, strike.z);

        for (int i = 0; i < SourceCount; i++)
        {
            Vector3 p = Vector3.Lerp(origin, foot, (i + 0.5f) / SourceCount);
            sources[i] = new Vector4(p.x, p.y, p.z, 0f);
        }

        // ÇAKMA BAŞINA BİR KEZ. Konum çakma boyunca değişmiyor; her karede sekiz vektör
        // yazmak boşuna. `Apply` kare kare yalnız ŞİDDETİ güncelliyor.
        Shader.SetGlobalVectorArray(SourcesId, sources);
        Shader.SetGlobalFloat(SourceCountId, SourceCount);

        // Ters kare sönüm: şiddet referans mesafede verilmiş, gerçek mesafeye taşınıyor.
        // Yakınında patlayan şimşeğin gözü kamaştırması bundan — ton eşleme orada beyaza
        // doyuyor, ki bakan göz de öyle yapıyor.
        float reach = Vector3.Distance(eye, origin);
        float reference = Mathf.Max(1f, settings.referenceDistance);
        peakIntensity = settings.intensityAtReference * (reference * reference)
                        / Mathf.Max(1f, reach * reach);

        peakGlow = Mathf.Lerp(settings.distantGlow, settings.closeGlow, nearness);
        decayTau = Mathf.Max(0.001f,
            Mathf.Lerp(settings.distantDecay, settings.closeDecay, nearness));

        // Işık çakmadan göze gelir; yönlü ışığın baktığı yön o yolun kendisi.
        transform.rotation = Quaternion.LookRotation((eye - origin).normalized);

        // Tek bir sönümlü parlama plastik duruyor: gerçek şimşek aynı kanaldan birkaç
        // kez boşalır ve göze çırpınan bir ışık olarak ulaşır.
        strokeCount = UnityEngine.Random.Range(1, MaxStrokes + 1);

        float when = 0f;
        for (int i = 0; i < strokeCount; i++)
        {
            strokeTime[i] = when;

            // İlk boşalma en güçlüsü, sonrakiler zayıflar
            strokeAmplitude[i] = i == 0 ? 1f : UnityEngine.Random.Range(0.35f, 0.9f);
            when += UnityEngine.Random.Range(settings.strokeGap.x, settings.strokeGap.y);
        }

        elapsed = 0f;
        duration = strokeTime[strokeCount - 1] + decayTau * 5f;
        active = true;
        LastDistance = distance;

        Placed?.Invoke(new LightningStrike(origin, cloudLayer.Bottom, distance, nearness, duration));
    }

    /// Vuruşların örtüşen sönümleri. Toplamak yerine en güçlüsü alınıyor: üst üste
    /// binen iki vuruş toplandığında tepe değeri aşıp beyaza doyuruyor.
    float Envelope(float t)
    {
        float value = 0f;

        for (int i = 0; i < strokeCount; i++)
        {
            float age = t - strokeTime[i];
            if (age < 0f) continue;

            float rise = Mathf.Clamp01(age / Mathf.Max(0.0001f, settings.riseSeconds));
            value = Mathf.Max(value, strokeAmplitude[i] * rise * Mathf.Exp(-age / decayTau));
        }

        return value;
    }

    /// TABLO BİR KEZ YAZILIYOR. Statik bir asset; her karede global yazmak boşuna.
    /// Yazılmazsa sis örneklemesi boş dokudan okur ve parlama tamamen kaybolur — bu
    /// yüzden `Bind` ve `OnEnable` ikisinden de çağrılıyor (bileşen sahnede hazır
    /// dururken `Bind` çalışmayabiliyor).
    void PublishLut()
    {
        if (scatterLut != null) Shader.SetGlobalTexture(ScatterLutId, scatterLut);
        Shader.SetGlobalFloat(ScatterTId, scatterCutoff);
    }

    void Apply(float value)
    {
        flash.intensity = peakIntensity * value;

        // rgb önceden çarpılmış: gökyüzü ve bulut aynı değeri okuyor, rengi ayrıca
        // seçmiyorlar. w gerekirse şiddeti tek başına verir.
        Glow = peakGlow * value;

        Color glow = settings.flashColor * (peakGlow * value);
        Shader.SetGlobalVector(FlashId, new Vector4(glow.r, glow.g, glow.b, peakGlow * value));

        // Konum ve lekenin yarıçapı. Bulut, ışın yönünü katmanla kesip bulduğu dünya
        // noktasının buraya uzaklığına göre parlıyor — yani denizde gerçekten bir yer
        // aydınlanıyor, bir yön değil.
        Shader.SetGlobalVector(PositionId,
            new Vector4(origin.x, origin.y, origin.z, Mathf.Max(1f, settings.glowRadius)));

    }
}

/// Bir çakmanın dünyadaki karşılığı. Konumu tek yer seçiyor, kolu çizen taraf onu okuyor.
public readonly struct LightningStrike
{
    /// Buluttaki boşalma noktası. Parlamanın kaynağı burası.
    public readonly Vector3 Origin;

    /// Bulut tabanının kotu. Görünür kanal buradan aşağıda başlar: kütlenin içindeki
    /// bölüm zaten buluttan görünmez, oradan başlatmak kanalı bulutun önüne asıyor.
    public readonly float CloudBase;

    /// Çakmanın **yer** uzaklığı (metre): fırtınanın kaç metre ötede olduğu.
    ///
    /// Buradaki sayı gözle boşalma noktası arasındaki üç boyutlu mesafe **değil**. İkisi
    /// karıştırılınca kol hiç çizilmedi: çakma bulutun içinde, yani iki buçuk kilometrenin
    /// üstünde duruyor, dolayısıyla üç boyutlu mesafe yatayda sıfıra gelse bile o
    /// yüksekliğin altına inmiyor ve "kolu şu mesafeye kadar çiz" koşulu daima aşılıyordu.
    public readonly float Distance;

    /// 0 uzak, 1 yakın
    public readonly float Nearness;

    /// Parlamanın toplam süresi (saniye)
    public readonly float Duration;

    public LightningStrike(Vector3 origin, float cloudBase, float distance, float nearness,
        float duration)
    {
        Origin = origin;
        CloudBase = cloudBase;
        Distance = distance;
        Nearness = nearness;
        Duration = duration;
    }
}
