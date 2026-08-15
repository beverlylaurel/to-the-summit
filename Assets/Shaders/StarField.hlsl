#ifndef TTS_STAR_FIELD_INCLUDED
#define TTS_STAR_FIELD_INCLUDED

/// YILDIZ ALANI — PROSEDÜREL. Önceden küp harita olarak üretiliyordu; 512'lik yüzde bir
/// teksel 0.176°, ekranda 1920px/90° FOV'da bir piksel 0.047°. Yani her yıldız zorunlu
/// olarak dört piksel genişliğindeydi ve bilineer süzme onu 2×2 tekseline yayıp yumuşak
/// bir lekeye çeviriyordu. Bir piksele inmek için yüz 2048 olmalıydı: RGBAHalf'ta 201 MB.
/// Doku yolu bu işi çözemiyor.
///
/// Burada yıldız ekran-uzayı türevinden çiziliyor, yani çözünürlükten bağımsız olarak
/// hep ~1 piksel. Titreme de ancak burada mümkün: durağan bir doku titreyemez.
///
/// GÜRÜLTÜ TAMSAYI KARIŞTIRICIDAN. `frac(sin(...))` küçük tamsayı girdilerde korele
/// çıkıyor ve düzenli desen üretiyor (`CLOUDS_REBUILD.md`, ders 9).

/// Yüz başına ızgara. Hücre 90/128 = 0.70°, ekranda ~15 piksel.
#define STAR_GRID 128.0

/// Çıplak gözle görülen yıldız ~6000. Hücre sayısı 6 × 128² = 98304, yani hücre başına
/// düşme olasılığı 0.061.
#define STAR_DENSITY 0.061

/// Kadir aralığı. Her kadir bir öncekinin 10^(−0.4) katı.
#define STAR_FAINTEST_MAGNITUDE 6.0

/// x = güneşin yüksekliğinin sinüsü. Gündüz solması buradan; `SkyWeatherDriver` sürüyor.
float4 _StarFieldParams;

uint StarMix(uint x)
{
    x ^= x >> 17; x *= 0xed5ad4bbu;
    x ^= x >> 11; x *= 0xac4c1b51u;
    x ^= x >> 15; x *= 0x31848babu;
    x ^= x >> 14;
    return x;
}

float StarHash01(uint seed, uint channel)
{
    uint h = StarMix(seed * 747796405u + channel * 2891336453u + 1u);
    return (h & 0x00FFFFFFu) / 16777215.0;
}

/// Yön → küp yüzü ve yüz içi koordinat. Yüzler arası tutarlılık aranmıyor: her yüz kendi
/// ızgarasını taşıyor, dikişte yalnız hücre sınırı değişiyor ve yıldız hücre ortasında
/// durduğu için kırpılmıyor.
void StarDirectionToFace(float3 d, out uint face, out float2 uv)
{
    float3 a = abs(d);
    float major;
    float2 st;

    if (a.x >= a.y && a.x >= a.z)
    {
        major = a.x;
        face = d.x > 0.0 ? 0u : 1u;
        st = float2(d.x > 0.0 ? -d.z : d.z, -d.y);
    }
    else if (a.y >= a.z)
    {
        major = a.y;
        face = d.y > 0.0 ? 2u : 3u;
        st = float2(d.x, d.y > 0.0 ? d.z : -d.z);
    }
    else
    {
        major = a.z;
        face = d.z > 0.0 ? 4u : 5u;
        st = float2(d.z > 0.0 ? d.x : -d.x, -d.y);
    }

    uv = 0.5 * (st / max(major, 1e-6) + 1.0);
}

/// Yıldız rengi sıcaklığından: sıcak olan mavi-beyaz, soğuk olan turuncu. Çoğunluk beyaza
/// yakın — seçim ortaya doğru büzülüyor ki uçlar azınlıkta kalsın.
float3 StarColor(float pick)
{
    float t = (pick - 0.5) * 2.0;
    t = sign(t) * t * t;

    return t < 0.0
        ? lerp(float3(1.0, 1.0, 1.0), float3(0.72, 0.80, 1.00), -t)
        : lerp(float3(1.0, 1.0, 1.0), float3(1.00, 0.84, 0.68), t);
}

/// GÜNDÜZ SOLMASI GÜNEŞ YÜKSEKLİĞİNDEN, KADİRE GÖRE AYRI AYRI.
///
/// Eskiden solma yoktu: paket yıldızları `(1 − skyOpacity)` ile çarpıyor ve bunun gündüzü
/// halledeceği varsayılmıştı. ÖLÇÜLDÜ, YANLIŞ — zenitte gündüz optik derinlik ~0.2, yani
/// opaklık ~0.2 ve yıldızların %80'i geçiyordu; sabah 8'de gökyüzü yıldızlıydı. Gerçekte
/// yıldızları saklayan şey opaklık değil, gök parlaklığının 10⁵ kat büyük olması; bizim
/// yıldızlar gece görünsün diye yükseltildiği için gündüz de hayatta kalıyorlardı.
///
/// Eşik gerçek alacakaranlık tanımlarından: parlak yıldız güneş −3°'nin altına inince
/// görünür, en sönüğü −18°'yi (astronomik alacakaranlığın sonu) bekler.
float StarDaylightFade(float magnitude)
{
    float faint = magnitude / STAR_FAINTEST_MAGNITUDE;
    float threshold = lerp(-0.052, -0.309, faint); // sin(−3°) … sin(−18°)

    return saturate((threshold - _StarFieldParams.x) * 20.0);
}

/// SİNTİLASYON HAVA KÜTLESİNE BAĞLI. Işık ufka yakınken çok daha kalın bir hava
/// katmanından geçiyor, kırılma indisi oynamaları birikiyor; zenitte yıldız neredeyse
/// sabit durur. Kendi zamanlayıcısı yok, `_Time` ve hash fazı kullanılıyor.
float StarTwinkle(uint seed, float altitudeSin)
{
    float airmass = 1.0 / max(altitudeSin, 0.08);
    float amplitude = saturate((airmass - 1.0) * 0.30) * StarHash01(seed, 5u);

    float phase = StarHash01(seed, 6u) * 6.2831853;
    float speed = 3.0 + StarHash01(seed, 7u) * 5.0;

    // İki frekans: tek sinüs düzenli nabız gibi okunuyor, sintilasyon düzensizdir.
    float wave = sin(_Time.y * speed + phase) * 0.6
               + sin(_Time.y * speed * 1.73 + phase * 2.1) * 0.4;

    return 1.0 + amplitude * wave;
}

/// `dir` uzay dönüşü uygulanmış bakış yönü, `altitudeSin` dünya uzayındaki yüksekliğin
/// sinüsü (hava kütlesi onunla hesaplanıyor, yıldız alanının dönüşüyle değil).
float3 EvaluateStarField(float3 dir, float altitudeSin)
{
    uint face;
    float2 uv;
    StarDirectionToFace(dir, face, uv);

    float2 cellUV = uv * STAR_GRID;

    // Hücre birimi / piksel. Dikişte türev patlıyor; kırpılmazsa oradaki yıldız tüm
    // hücreye yayılır.
    float2 footprint = fwidth(cellUV);
    float pixel = clamp(max(footprint.x, footprint.y), 1e-5, 0.25);

    int2 cell = int2(floor(cellUV));
    uint seed = StarMix(face + StarMix((uint)cell.x + StarMix((uint)cell.y)));

    if (StarHash01(seed, 0u) > STAR_DENSITY) return 0.0;

    // Yıldız hücrenin ORTA %70'İNE konuyor. Böylece komşu hücrelere bakmaya gerek
    // kalmıyor: kenara 15% pay var, hücre ~15 piksel, yıldız ~1 piksel.
    float2 starPos = float2(StarHash01(seed, 1u), StarHash01(seed, 2u)) * 0.7 + 0.15;

    // Sönük yıldız çok, parlak yıldız az. Küp kökü, kadir başına ~2.5 kat artan gerçek
    // sayıma yakın bir dağılım veriyor.
    float magnitude = STAR_FAINTEST_MAGNITUDE * pow(StarHash01(seed, 3u), 1.0 / 3.0);
    float brightness = pow(10.0, -0.4 * magnitude);

    float fade = StarDaylightFade(magnitude);
    if (fade <= 0.0) return 0.0;

    // Yarıçap PİKSEL cinsinden, yani çözünürlükten bağımsız. Parlak yıldız biraz büyük:
    // gözde de öyle okunur, nokta kaynak olmasına rağmen parlaklıkla yayılır.
    float radius = lerp(0.75, 1.35, 1.0 - magnitude / STAR_FAINTEST_MAGNITUDE);
    float distance = length(frac(cellUV) - starPos) / (pixel * radius);
    float core = exp(-distance * distance * 1.6);

    return StarColor(StarHash01(seed, 4u))
         * brightness * core * fade * StarTwinkle(seed, altitudeSin);
}

#endif // TTS_STAR_FIELD_INCLUDED
