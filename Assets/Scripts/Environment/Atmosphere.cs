using UnityEngine;

/// Atmosferin ışığa ne yaptığını hesaplar — SALT MATEMATİK, Unity bağımlılığı yok.
///
/// Renk burada SEÇİLMEZ. Şafağın turuncusu, alacakaranlığın moru, gecenin maviliği
/// hep aynı üç bileşenin optik derinliğinden çıkar:
///
///   Rayleigh — hava molekülleri. λ⁻⁴ ile gider, yani maviyi kırmızıdan ~7 kat fazla
///              saçar. Gündüz göğü mavi yapan, batışta huzmeyi turuncuya çeviren bu.
///   Mie      — aerosol. Dalga boyundan bağımsız, ileri yönde keskin saçar (g = 0.8).
///              Güneşin çevresindeki beyaz hâle ve puslu havanın süt rengi buradan.
///   Ozon     — Chappuis bandı. 500-700 nm'yi (yeşil-turuncu) yutar, maviyi neredeyse
///              hiç tutmaz. Alacakaranlığın MORU bundandır; Rayleigh değil.
///
/// Kritik ayrım: güneş ufkun altındayken huzme atmosfere 20-25 km'den TEĞET girer.
/// Orada hava seyrek (Rayleigh ölçek yüksekliği 8 km) ama ozon katmanı tam oradadır
/// (tepe 25 km). Rayleigh doyarken ozon yolu uzamaya devam eder — denge devrilir ve
/// renk turuncudan pembeye, oradan mora geçer. Sıra kodlanmaz, bu geometriden doğar.
///
/// Katsayılar: Bruneton & Neyret, Precomputed Atmospheric Scattering (2008).
public static class Atmosphere
{
    /// Ham gök radyansını sahne birimlerine taşıyan TEK kazanç. İki ayrı sabit vardı
    /// (`AtmosphereController` gökyüzü rengi için, `TimeOfDay` pozlama seviyesi için)
    /// ve aynı adı taşıdıkları hâlde farklı işler yapıyorlardı: biri değişince öteki
    /// yerinde kalıyor, gökyüzü ile ondan türeyen değer birbirinden ayrışıyordu.
    /// Değeri zenit parlaklığına göre kalibre edildi (bkz. SkyRadiance).
    public const float SceneGain = 3.6f;

    public const float PlanetRadius = 6360000f;
    public const float AtmosphereRadius = 6420000f;

    // Deniz seviyesinde saçılma/soğurma katsayıları (1/m).
    // Rayleigh: 1.24062e-6 / λ⁴, λ = 680 / 550 / 440 nm.
    static readonly Vector3 RayleighBeta = new(5.80e-6f, 13.56e-6f, 33.10e-6f);
    const float RayleighScaleHeight = 8000f;

    // Mie üç kanalda eşit: aerosol partikülü dalga boyundan büyük, ayrım yapmaz.
    // Sönüm saçılmadan büyük (albedo 0.9): aerosol bir miktar da yutar.
    const float MieBeta = 3.996e-6f;
    const float MieExtinction = MieBeta / 0.9f;
    const float MieScaleHeight = 1200f;

    /// Ozon TEPE yoğunluğundaki soğurma. Yeşil en yüksek — Chappuis bandı 600 nm
    /// civarında tepe yapıyor. Maviyi tutmaması alacakaranlığı mora çeviren şey.
    static readonly Vector3 OzoneBeta = new(0.650e-6f, 1.881e-6f, 0.085e-6f);
    const float OzonePeak = 25000f;    // katmanın tepe kotu
    const float OzoneWidth = 15000f;   // çadır profilinin yarı genişliği

    /// Verilen kottaki bağıl yoğunluklar. Ozon çadır profili: tepede 1, ±15 km'de 0.
    static void Densities(float altitude, out float rayleigh, out float mie, out float ozone)
    {
        rayleigh = Mathf.Exp(-altitude / RayleighScaleHeight);
        mie = Mathf.Exp(-altitude / MieScaleHeight);
        ozone = Mathf.Max(0f, 1f - Mathf.Abs(altitude - OzonePeak) / OzoneWidth);
    }

    /// Işının küresel atmosferden çıkana (ya da yere çarpana) kadar kat ettiği yol
    /// boyunca optik derinlik. Yere çarpıyorsa sonsuz sayılır: o yönden ışık gelmez.
    ///
    /// Küresel geometri şart: düzlem yaklaşımı ufka yakın açılarda hava kütlesini
    /// kat kat yanlış veriyor ve batış rengi tam orada belirleniyor.
    static bool OpticalDepth(float startAltitude, Vector3 direction, int steps,
                             out Vector3 depth)
    {
        depth = Vector3.zero;

        Vector3 origin = new(0f, PlanetRadius + startAltitude, 0f);
        float top = RaySphere(origin, direction, AtmosphereRadius);
        if (top <= 0f) return false;

        // Yere çarpan ışın: kaynak görünmüyor.
        if (BelowHorizon(startAltitude, direction)) return false;

        float step = top / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 p = origin + direction * (step * (i + 0.5f));
            float altitude = Mathf.Max(0f, p.magnitude - PlanetRadius);

            Densities(altitude, out float r, out float m, out float o);

            depth += (RayleighBeta * r + Vector3.one * (MieExtinction * m) + OzoneBeta * o)
                     * step;
        }

        return true;
    }

    /// Işın yerin altına mı bakıyor?
    ///
    /// Küre kesişimi esastır — ufka yakın saatlerdeki görünüm ona göre kalibre edildi.
    /// Ama o hesap gezegen ölçeğinde güvenilmez: `|origin|² − R²` iki 4·10¹³ sayının
    /// farkı ve float32'nin o büyüklükteki adımı ~4·10⁶. Gözlemci deniz seviyesindeyken
    /// sonuç yuvarlama gürültüsü ve işareti güneşin yüksekliğine göre değişiyordu —
    /// güneş 27°'de çalışıyor, 29°'de "yere çarptı" sayılıp huzme sıfırlanıyor, sahne
    /// kararıyordu. Kesintili olduğu için eşik gibi de görünmüyordu.
    ///
    /// Çözüm: küre hesabı tamamen bırakılır, soru AÇIYLA sorulur — sadeleşme yok,
    /// sonuç donanımdan ve derleyiciden bağımsız.
    ///
    /// Eşiğe pay EKLENMEZ. Eklenmişti ve sert bir aç/kapa üretiyordu: güneş 5°'yi
    /// geçtiği anda huzme ve ufuk örnekleri topluca sıfırlanıp geri geliyordu, ekranda
    /// 17:37 civarında bıçak gibi bir sıçrama bırakıyordu. Alçak güneşin kısılması
    /// ayrı ve SÜREKLİ bir çarpanın işi (`LowSunFade`).

    static bool BelowHorizon(float altitude, Vector3 direction)
        => direction.y < HorizonDipSine(altitude);

    /// Ufkun çöküş açısının sinüsü (negatif: gözlemci yükseldikçe ufuk aşağı kayar).
    static float HorizonDipSine(float altitude)
    {
        float ratio = Mathf.Clamp01(PlanetRadius / (PlanetRadius + altitude));
        return -Mathf.Sqrt(Mathf.Max(0f, 1f - ratio * ratio));
    }

    /// ALÇAK GÜNEŞ KISICISI — görünüm kararı. Ufukta sıfır, beş derecede tam.
    ///
    /// Fizik ufka yakınken güçlü ve kızıl bir gök veriyor; istenen şafak ondan ölçülü.
    /// Kısma tek yerden gelir ve HUZME, GÜNEŞ RENGİ ve UFUK ÖRNEKLERİ üçüne birden
    /// uygulanır — biri kısılıp öteki kısılmayınca bulutlar bir anda pembeleşiyordu
    /// (`Tint()` normalize ettiği için renk, huzme sönse bile tam doygun kalıyor).
    ///
    /// Sürekli olmak zorunda: eşik olarak kurulduğunda güneş o açıyı geçtiği anda
    /// sahne toptan sıçrıyordu.
    public static float LowSunFade(float altitude, Vector3 direction)
    {
        float dip = HorizonDipSine(altitude);
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(dip, dip + LowSunFadeSine, direction.y));
    }

    /// Kısıcının tam güce ulaştığı yükseklik (sinüs). 0.0872 ≈ 5°.
    public const float LowSunFadeSine = 0.0872f;

    /// Işının küreye girdiği ilk pozitif uzaklık; kesişmiyorsa -1.
    ///
    /// FELAKET SADELEŞME KORUMASI. `c = |origin|² - radius²` gezegen ölçeğinde iki
    /// 4·10¹³ sayının farkı ve float32'nin o büyüklükteki adımı ~4·10⁶ — kaynak tam
    /// yüzeydeyken (deniz seviyesindeki gözlemci, yer küresi) c teorik olarak sıfır ama
    /// hesabın kendisi gürültü. Ardından `t1 = -b + sqrt(b·b - c)` de teorik olarak
    /// sıfır; `sqrt(b·b)` bir ulp yukarı yuvarlanınca t1 küçük bir POZİTİF sayı çıkıyor
    /// ve yukarı giden ışın "yere çarptı" sayılıyordu. Yuvarlamanın yönü b'ye, yani
    /// güneşin yüksekliğine bağlı olduğu için hata kesintili: güneş 27°'de çalışıyor,
    /// 29°'de huzme sıfırlanıp sahne kararıyordu. Bulut kotundaki çağrılar etkilenmiyor
    /// çünkü orada c ≈ 3·10¹⁰, gürültünün çok üstünde.
    ///
    /// Çözüm hesapla değil geometriyle: kaynak küre yüzeyinde ya da dışındaysa (c ≥ 0)
    /// ışın ancak küreye DOĞRU giderse kesişebilir. b ≥ 0 ise uzaklaşıyordur, kesişme
    /// yoktur — köke hiç girmeye gerek yok.
    static float RaySphere(Vector3 origin, Vector3 direction, float radius)
    {
        float b = Vector3.Dot(origin, direction);
        float c = Vector3.Dot(origin, origin) - radius * radius;

        if (c >= 0f && b >= 0f) return -1f;

        float d = b * b - c;
        if (d < 0f) return -1f;

        d = Mathf.Sqrt(d);
        float t0 = -b - d, t1 = -b + d;
        return t0 > 0f ? t0 : (t1 > 0f ? t1 : -1f);
    }

    /// Gözlemciye ULAŞAN doğrudan ışığın çarpanı, kanal başına. Güneş ufkun altındaysa
    /// ya da ışın yere çarpıyorsa sıfır. Normalizasyon YOK: hem renk hem sönüm burada,
    /// birlikte. Parlaklığı geri kazanmak için en parlak kanalı 1'e çekmek — eski hâl —
    /// batışı sönmeyen bir kızıla kilitliyor ve göz alıyordu.
    public static Vector3 BeamTransmittance(float altitude, Vector3 sunDirection, int steps = 24)
    {
        float visible = DiscVisibility(altitude, sunDirection);
        if (visible <= 0f) return Vector3.zero;

        Vector3 direction = sunDirection;

        // Işın yere çarpıyor ama disk hâlâ kısmen görünüyorsa (kırılma) yolu teğetten
        // ölçeriz: geometrik ufku sıyıran ışının rengi, batmakta olan güneşin rengidir.
        if (BelowHorizon(altitude, direction))
            direction = GrazingDirection(altitude, sunDirection);

        if (!OpticalDepth(altitude, direction, steps, out Vector3 depth))
            return Vector3.zero;

        return new Vector3(Mathf.Exp(-depth.x), Mathf.Exp(-depth.y), Mathf.Exp(-depth.z))
             * (visible * LowSunFade(altitude, sunDirection));
    }


    /// Atmosferik kırılma ufukta ışığı yaklaşık 0.57° yukarı büker: güneş gerçekte
    /// battıktan sonra da bir süre görünür. Diskin kendisi de 0.53° geniştir, yani
    /// batış bir an değil bir geçiştir.
    const float HorizonRefraction = 0.00995f;   // 0.57° radyan
    const float SunDiscRadius = 0.00463f;       // 0.265° radyan

    /// Diskin ufuk üstünde kalan payı, 0-1. Ölçü GEOMETRİK ufka göre alınır: gözlemci
    /// yükseldikçe ufuk çöker (bulut kotunda 1.64°) ve güneş oradan daha erken görünür.
    ///
    /// Eski hâlde `OpticalDepth` yere çarpan ışını sıfırlıyordu ve geçiş genişliği TAM
    /// SIFIRDI: bir karede huzme 0.106, sonrakinde 0. Huzme yalnız ışığın şiddetini
    /// değil rengini de taşıdığı için güneş diski, arazinin şafak rengi, bulutun dusk
    /// tonu ve palet aynı anda siyaha atlıyordu.
    static float DiscVisibility(float altitude, Vector3 sunDirection)
    {
        float dip = HorizonDip(altitude);
        float elevation = Mathf.Asin(Mathf.Clamp(sunDirection.y, -1f, 1f));

        // Ufkun ÜSTÜNDEKİ pay: pozitif = disk merkezi ufkun üstünde.
        float margin = elevation - dip;

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            -HorizonRefraction - SunDiscRadius,
            -HorizonRefraction + SunDiscRadius, margin));
    }

    /// Gözlemcinin kotundan geometrik ufkun yükseklik açısı (negatif: aşağı çöker).
    static float HorizonDip(float altitude)
        => -Mathf.Acos(Mathf.Clamp01(PlanetRadius / (PlanetRadius + altitude)));

    /// Aynı azimutta, ufku tam sıyıran yön. Yere çarpan ışının yerine bu kullanılır;
    /// teğet yol atmosferin en uzun kesitidir, batışın kızılı oradan gelir.
    static Vector3 GrazingDirection(float altitude, Vector3 sunDirection)
    {
        Vector3 flat = new(sunDirection.x, 0f, sunDirection.z);
        flat = flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.forward;

        // Küçük bir pay yukarı: tam teğette küre kesişimi sayısal olarak bıçak sırtında.
        float dip = HorizonDip(altitude) + 1e-4f;
        return flat * Mathf.Cos(dip) + Vector3.up * Mathf.Sin(dip);
    }

    /// Gökyüzünün gözlemciye saçtığı ışık. Güneş battıktan sonra manzarayı aydınlatan
    /// şey budur — doğrudan huzme değil. Alpenglow'un kaynağı da bu: zirve, kızıla
    /// boyanmış gökten ışık alır.
    ///
    /// İki bileşen toplanır. TEK saçılma: her nokta için güneşe giden yolun
    /// geçirgenliği, oradaki saçılma katsayısı ve faz fonksiyonu. ÇOK saçılma:
    /// ikinci ve sonraki saçılmalar yönü unutur, izotropik gelir ve `MultipleScattering`
    /// tablosundan okunur.
    ///
    /// Çok saçılma olmadan batış ufkunun doygunluğu 0.98'e çıkıyordu — mavi kanal
    /// 0.005'te kalıyor, ekrandaki renk turuncu değil neredeyse saf kırmızı oluyordu.
    /// Gerçek batış ufku 0.5-0.7 doygunluktadır; maviyi geri dolduran şey tam olarak
    /// çok saçılmadır. Eksikliği ayrıca gökyüzünü topyekûn karartıyor ve kaybı telafi
    /// eden kazanç, en parlak yeri — güneş tarafındaki ufku — 1.0'ın iki katına
    /// taşıyıp ton eşlemede kırptırıyordu.
    public static Vector3 SkyRadiance(float altitude, Vector3 viewDirection,
                                      Vector3 sunDirection, int steps = 16)
    {
        Vector3 origin = new(0f, PlanetRadius + altitude, 0f);
        float top = RaySphere(origin, viewDirection, AtmosphereRadius);
        if (top <= 0f) return Vector3.zero;

        if (BelowHorizon(altitude, viewDirection))
        {
            float ground = RaySphere(origin, viewDirection, PlanetRadius);
            if (ground > 0f) top = ground;
        }

        float cosTheta = Vector3.Dot(viewDirection, sunDirection);
        float rayleighPhase = 3f / (16f * Mathf.PI) * (1f + cosTheta * cosTheta);
        float miePhase = MiePhase(cosTheta, 0.8f);

        float step = top / steps;
        Vector3 accumulated = Vector3.zero;
        Vector3 viewDepth = Vector3.zero;

        for (int i = 0; i < steps; i++)
        {
            Vector3 p = origin + viewDirection * (step * (i + 0.5f));
            float h = Mathf.Max(0f, p.magnitude - PlanetRadius);

            Densities(h, out float r, out float m, out float o);

            Vector3 extinction = RayleighBeta * r + Vector3.one * (MieExtinction * m)
                               + OzoneBeta * o;
            viewDepth += extinction * step;

            Vector3 viewTransmittance = new(
                Mathf.Exp(-viewDepth.x), Mathf.Exp(-viewDepth.y), Mathf.Exp(-viewDepth.z));

            // Bu noktaya güneşten DOĞRUDAN ışık ulaşıyor mu? Yere çarpıyorsa hiç —
            // ama bu noktayı tamamen atlamak yanlıştı: gölgedeki hava, komşusundan
            // saçılan ışıkla hâlâ parlar. Alacakaranlığın simsiyah olmasının sebebi
            // o atlamaydı; çok saçılma orada da katkı verir.
            Vector3 scatteringIsotropic = RayleighBeta * r + Vector3.one * (MieBeta * m);

            if (OpticalDepth(h, sunDirection, 8, out Vector3 sunDepth))
            {
                Vector3 transmittance = new(
                    Mathf.Exp(-sunDepth.x - viewDepth.x),
                    Mathf.Exp(-sunDepth.y - viewDepth.y),
                    Mathf.Exp(-sunDepth.z - viewDepth.z));

                Vector3 scattering = RayleighBeta * (r * rayleighPhase)
                                   + Vector3.one * (MieBeta * m * miePhase);

                accumulated += Vector3.Scale(transmittance, scattering) * step;
            }

            // Çok saçılma izotropik: faz fonksiyonu yok, güneş yolu Ψ'nin içinde.
            Vector3 psi = MultipleScattering(h, sunDirection);
            accumulated += Vector3.Scale(viewTransmittance,
                                         Vector3.Scale(scatteringIsotropic, psi)) * step;
        }

        return accumulated;
    }

    // --- Çok saçılma (Hillaire 2020, "A Scalable and Production Ready Sky and
    //     Atmosphere Rendering Technique") ---
    //
    // Işık atmosferde bir kez saçılıp durmaz. İkinci saçılmadan sonra geldiği yönü
    // unutur; toplamı izotropik bir kaynak gibi davranır. Sonsuz mertebe, geometrik
    // seri olarak kapanır: Ψ = L₂ / (1 − f), f = bir saçılmanın geri dönen payı.
    //
    // Tablo (kot × güneş yüksekliği) bir kez kurulur. İki eksen de yeter: Ψ izotropik
    // olduğu için BAKIŞ yönünden bağımsızdır — asıl ucuzluk buradan gelir.
    const int MsAltitudes = 16;

    /// AÇI EKSENİ İNCE OLMAK ZORUNDA. Eksen sin(güneş yüksekliği) üzerinde düzgün
    /// bölünüyor; 24 kutuda kutu başına 5° düşüyor. Alacakaranlık derecede ~2.24 kat
    /// söndüğü için bir kutu 28 katlık değişimi kapsıyor ve bilineer ara değer orada
    /// çaresiz kalıyordu: güneş −6°'deyken gök, doğuştaki değerin 0.041'i çıkıyordu —
    /// gerçek 0.0085, yani 4.8 kat fazla parlak. 48'de 0.0039'a iniyor.
    ///
    /// Ölçüldü ve ayrıştırıldı: yön sayısı (16→32) ve adım sayısı (12→24) hiçbir şey
    /// değiştirmiyor, kot ekseni (16→24) de öyle. Kazancın TAMAMI bu eksende. 48'in
    /// ötesi de kazandırmıyor — 64 ve 96'daki oynama yakınsama değil, 16 yönlü
    /// örneklemenin gürültüsü.
    ///
    /// Gündüz etkilenmiyor: güneş +5° ve üstünde fark %2.5'in altında.
    const int MsAngles = 48;
    const float MsTopAltitude = 60000f;
    static Vector3[] msTable;

    /// Küre üzerine düzgün dağılmış yönler (Fibonacci sarmalı). Rastgele örnekleme
    /// aynı sayıda yönle daha gürültülü çıkıyor ve tablo kotlar arası zıplıyor.
    static Vector3 SphereDirection(int index, int count)
    {
        float y = 1f - 2f * (index + 0.5f) / count;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        float phi = (index + 0.5f) * Mathf.PI * (3f - Mathf.Sqrt(5f));
        return new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi));
    }

    static void BuildMultipleScattering()
    {
        msTable = new Vector3[MsAltitudes * MsAngles];
        const int Directions = 16;
        const int Steps = 12;
        const float Isotropic = 1f / (4f * Mathf.PI);

        for (int a = 0; a < MsAngles; a++)
        {
            // Güneşin dikey bileşeni −1..1; yatayı kalanı tamamlar.
            float sinSun = MsAngles > 1 ? -1f + 2f * a / (MsAngles - 1f) : 0f;
            Vector3 sun = new(Mathf.Sqrt(Mathf.Max(0f, 1f - sinSun * sinSun)), sinSun, 0f);

            for (int k = 0; k < MsAltitudes; k++)
            {
                float altitude = MsTopAltitude * k / (MsAltitudes - 1f);
                Vector3 origin = new(0f, PlanetRadius + altitude, 0f);

                Vector3 second = Vector3.zero;   // L₂ — ikinci mertebe
                Vector3 transfer = Vector3.zero; // f  — geri dönen pay

                for (int d = 0; d < Directions; d++)
                {
                    Vector3 dir = SphereDirection(d, Directions);

                    float top = RaySphere(origin, dir, AtmosphereRadius);
                    if (top <= 0f) continue;

                    if (BelowHorizon(altitude, dir))
                    {
                        float ground = RaySphere(origin, dir, PlanetRadius);
                        if (ground > 0f) top = ground;
                    }

                    float step = top / Steps;
                    Vector3 depth = Vector3.zero;

                    for (int i = 0; i < Steps; i++)
                    {
                        Vector3 p = origin + dir * (step * (i + 0.5f));
                        float h = Mathf.Max(0f, p.magnitude - PlanetRadius);

                        Densities(h, out float r, out float m, out float o);

                        depth += (RayleighBeta * r + Vector3.one * (MieExtinction * m)
                                  + OzoneBeta * o) * step;

                        Vector3 scattering = RayleighBeta * r + Vector3.one * (MieBeta * m);
                        Vector3 travelled = new(Mathf.Exp(-depth.x), Mathf.Exp(-depth.y),
                                                Mathf.Exp(-depth.z));

                        Vector3 common = Vector3.Scale(travelled, scattering)
                                         * (Isotropic * step);
                        transfer += common;

                        if (OpticalDepth(h, sun, 6, out Vector3 sunDepth))
                        {
                            second += Vector3.Scale(common, new Vector3(
                                Mathf.Exp(-sunDepth.x), Mathf.Exp(-sunDepth.y),
                                Mathf.Exp(-sunDepth.z)));
                        }
                    }
                }

                float weight = 4f * Mathf.PI / Directions;
                second *= weight;
                transfer *= weight;

                msTable[k * MsAngles + a] = new Vector3(
                    second.x / (1f - Mathf.Min(0.98f, transfer.x)),
                    second.y / (1f - Mathf.Min(0.98f, transfer.y)),
                    second.z / (1f - Mathf.Min(0.98f, transfer.z)));
            }
        }
    }

    /// Verilen kotta ve güneş yüksekliğinde izotropik çok saçılma kaynağı.
    /// İki eksende bilineer: tablo kaba olduğu için ara değer şart, yoksa
    /// gökyüzü güneş yükseldikçe kademe kademe zıplıyor.
    static Vector3 MultipleScattering(float altitude, Vector3 sunDirection)
    {
        if (msTable == null) BuildMultipleScattering();

        float fk = Mathf.Clamp01(altitude / MsTopAltitude) * (MsAltitudes - 1);
        float fa = Mathf.Clamp01((sunDirection.y + 1f) * 0.5f) * (MsAngles - 1);

        int k0 = Mathf.FloorToInt(fk), a0 = Mathf.FloorToInt(fa);
        int k1 = Mathf.Min(k0 + 1, MsAltitudes - 1), a1 = Mathf.Min(a0 + 1, MsAngles - 1);
        float tk = fk - k0, ta = fa - a0;

        Vector3 lower = Vector3.Lerp(msTable[k0 * MsAngles + a0], msTable[k0 * MsAngles + a1], ta);
        Vector3 upper = Vector3.Lerp(msTable[k1 * MsAngles + a0], msTable[k1 * MsAngles + a1], ta);
        return Vector3.Lerp(lower, upper, tk);
    }

    /// Henyey-Greenstein: aerosol ışığı ileri yönde keskin saçar. Güneşin çevresindeki
    /// beyaz hâlenin ve puslu havada batışın "patlamasının" sebebi.
    static float MiePhase(float cosTheta, float g)
    {
        float g2 = g * g;
        float denom = 1f + g2 - 2f * g * cosTheta;
        return (1f - g2) / (4f * Mathf.PI * denom * Mathf.Sqrt(Mathf.Max(1e-4f, denom)));
    }
}
