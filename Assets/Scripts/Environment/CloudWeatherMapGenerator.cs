using System;
using System.Collections.Generic;
using UnityEngine;

/// Bulut hava haritasını üretir — SALT MATEMATİK, editör bağımlılığı yok.
/// İki yerden çağrılır: editörde CloudWeatherMapBaker (asset olarak kaydeder) ve
/// F1 teşhis panelindeki "Haritayı yeniden pişir" (Play içinde canlı kalibrasyon).
/// İkisi de aynı ayar alanlarını okur: kalibre edilen değer, editör pişirmesiyle
/// birebir aynı haritayı üretir.
///
/// Kanallar: R kapsama (çekirdek-birleşim), G tip, B taban kayması, A tavan
/// (eğim garantili: bütün hâlinde bulanık + kubbe kapağı + iç yarıklar).
public static class CloudWeatherMapGenerator
{
    public const int Resolution = 512;

    public static Texture2D Generate(AtmosphereSettings settings)
    {
        int n = Resolution;
        float texelMeters = settings.weatherMapWorldSize / n;
        var rng = new System.Random(settings.weatherMapSeed);

        // Organizasyon alanı: ham hali (raw) 48 km periyotlu tekrarsız varyans kaynağı
        float[,] raw = ValueField(n, 3, 3, settings.weatherMapSeed * 13 + 1);
        float[,] mask = new float[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float t = Mathf.Clamp01((raw[y, x] - settings.patchWindow) / 0.28f);
            mask[y, x] = t * t * (3f - 2f * t);
        }

        // Yama içi parçalama + dinamik aralık sıkıştırması: taban boşluklara serpinti,
        // tavan istifi (birleşmeyi) sınırlar.
        float[,] breakup = ValueField(n, 12, 2, settings.weatherMapSeed * 17 + 9);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float m = mask[y, x] * (0.5f + 0.5f * breakup[y, x]);
            mask[y, x] = Mathf.Lerp(settings.packingFloor, settings.corePacking,
                                    Mathf.Clamp01(m));
        }

        // Çekirdek serpme: hücre başına RASTGELE deneme (kümeli dağılım), jitter 2.5
        // hücre (satır hizası imkânsız), kabul olasılığı maskeden.
        var cores = new List<Core>();
        const int cell = 22;
        for (int gy = 0; gy < n; gy += cell)
        for (int gx = 0; gx < n; gx += cell)
        {
            int attempts = Mathf.RoundToInt(rng.Next(7) * settings.coreDensity);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float px = (gx + (float)rng.NextDouble() * cell * 2.5f) % n;
                float py = (gy + (float)rng.NextDouble() * cell * 2.5f) % n;
                if (rng.NextDouble() > mask[(int)py, (int)px]) continue;

                // Üstel yarıçap: çok küçük, az büyük. Taban 1400 sabit (altı gürültüyle
                // yontulamıyor, pürüzsüz koni oluyor); tavan sürgüde — dev bulut boyu.
                // Taban 700: en-boy yasası küçüğü zaten basık tuttuğu için eski
                // boru-koruma tabanı (1400) gereksizleşti; yüksek taban yarıçap
                // penceresini daraltıp bütün bulutları AYNI boyda beze ordusuna
                // çeviriyordu. Geniş pencere = boy çeşitliliği.
                float radiusMeters = Mathf.Clamp(
                    -1600f * Mathf.Log(1f - (float)rng.NextDouble()),
                    700f, settings.coreRadiusMax);

                float u = (float)rng.NextDouble();
                float type = (float)rng.NextDouble() < 0.3f
                    ? 0.05f + 0.3f * u
                    : 0.35f + 0.65f * u;

                float vigor = Mathf.Clamp01(0.02f + 0.52f * (radiusMeters / 4500f)
                                            + 0.35f * (float)rng.NextDouble());

                // Boy METRE cinsinden kurulur, katman oranı olarak değil. Oran
                // kullanmak katman kalınlığına kilitliyordu: tavanı yükseltince
                // bütün bulutlar birlikte uzuyordu. Metre, katmandan bağımsız —
                // katman tavanı artık yalnızca kümülonimbusun tavana çarpmaması
                // için var.
                float layer = Mathf.Max(1f, settings.cloudTop - settings.cloudBottom);

                // Cinse göre tipik kalınlık (gerçek değerler): stratus birkaç yüz
                // metre, kümülüs 800-2000, kümülonimbus troposferin tepesine kadar.
                float typicalHeight = Mathf.Lerp(400f, 2000f, type);
                if (type > 0.82f)
                    typicalHeight = Mathf.Lerp(2000f, settings.cumulonimbusHeight,
                                               Mathf.InverseLerp(0.82f, 1f, type));

                // EN-BOY YASASI çapa göre ve cinse bağlı: kümülüs kabaca eni kadar
                // yükselir, kümülonimbus enin iki katını aşabilir (gerçek fırtına
                // bulutu 5 km eninde 10 km boyundadır). Yasa yarıçapa ve tek katsayıya
                // bağlıyken dev bulutlar dikine gelişemiyordu — örs silueti bu yüzden
                // hiç çıkmadı.
                float diameter = radiusMeters * 2f;
                float aspectAllow = Mathf.Lerp(0.9f, 2.2f, Mathf.InverseLerp(0.6f, 1f, type));
                float heightCap = diameter * aspectAllow + 250f;

                // Boy payı çekirdek başına rastgele: hepsi bütçesini tam kullanınca
                // gök aynı biçimli tepelerle doluyordu.
                float heightMeters = Mathf.Min(typicalHeight, heightCap)
                                     * (0.55f + 0.45f * (float)rng.NextDouble())
                                     * (0.5f + 0.5f * vigor);

                float ceiling01 = heightMeters / layer;

                cores.Add(new Core
                {
                    x = px, y = py,
                    radius = radiusMeters / texelMeters,
                    angle = (float)rng.NextDouble() * Mathf.PI,
                    aspect = 1f + 1.2f * (float)rng.NextDouble(),
                    amp = 0.35f + 0.75f * (float)rng.NextDouble(),
                    type = type,
                    baseOffset = (float)rng.NextDouble(),
                    ceiling = Mathf.Clamp01(ceiling01)
                });
            }
        }

        // Splat: eliptik pürüzsüz çekirdekler, olasılıksal birleşim
        var keep = new float[n, n];
        var wG = new float[n, n];
        var wB = new float[n, n];
        var aDome = new float[n, n];
        var wSum = new float[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            keep[y, x] = 1f;

        foreach (var c in cores)
        {
            int reach = Mathf.CeilToInt(c.radius * Mathf.Sqrt(c.aspect));
            int cx = (int)c.x, cy = (int)c.y;
            float ca = Mathf.Cos(c.angle), sa = Mathf.Sin(c.angle);
            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                float fx = cx + dx + 0.5f - c.x;
                float fy = cy + dy + 0.5f - c.y;
                float du = (fx * ca + fy * sa) * c.aspect;
                float dv = -fx * sa + fy * ca;
                float d2 = (du * du + dv * dv) / (c.radius * c.radius * c.aspect);
                if (d2 >= 1f) continue;

                float q = 1f - d2;
                float k = q * q;
                int ix = ((cx + dx) % n + n) % n;
                int iy = ((cy + dy) % n + n) % n;

                keep[iy, ix] *= 1f - c.amp * k;
                float w = c.amp * k;
                wG[iy, ix] += w * c.type;
                wB[iy, ix] += w * c.baseOffset;
                wSum[iy, ix] += w;

                // Tavan: çekirdek başına KUBBE, MAX birleşimi. Ağırlıklı ortalama +
                // bulanıklık, komşunun yüksek tavanını küçük buluta sızdırıyordu
                // (minik ama upuzun borular). Max sızdırmaz: küçük çekirdek alçak
                // kalır, örtüşen bölgede en yüksek kubbe kazanır — birleşik kütlede
                // bile ayrı zirveler ve vadiler. Profil PLATOLU (iç ~%60 düz, omuzda
                // yuvarlanır): çıplak paraboloit her bulutu sivri tepeli beze
                // yapıyordu — gerçek bulut tepesi yayvan kubbedir.
                // 1.25: 1.6'da iç %60 dümdüz tavandı — kolon derinliği ayak izi
                // boyunca sabit kalıp bulutu LEVHA gibi gösteriyordu. Uzun omuz =
                // kenara doğru kademeli incelme, yanal dağılmanın yarısı.
                float plateau = Mathf.Min(1f, (1f - d2) * 1.25f);
                aDome[iy, ix] = Mathf.Max(aDome[iy, ix], c.ceiling * plateau);
            }
        }

        float layerThickness = Mathf.Max(1f, settings.cloudTop - settings.cloudBottom);

        var R = new float[n, n];
        var G = new float[n, n];
        var B = new float[n, n];
        var A = new float[n, n];

        // Boş bölgeler 48 km periyotlu alanlarla boyanır (düz 0.5 kalırsa fırtına
        // dolgusunda tek doku değişkeni döşenen gürültü kalıyor: kafes deseni).
        float[,] paintG = ValueField(n, 4, 2, settings.weatherMapSeed * 7 + 3);
        float[,] paintB = ValueField(n, 5, 2, settings.weatherMapSeed * 11 + 5);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            R[y, x] = 1f - keep[y, x];
            float s = wSum[y, x];
            G[y, x] = s > 1e-6f ? wG[y, x] / s : 0.30f + 0.40f * paintG[y, x];
            B[y, x] = s > 1e-6f ? wB[y, x] / s : 0.5f + (paintB[y, x] - 0.5f) * 0.8f;
            // Tavan doğrudan kubbe alanından; taban payı fırtına dolgusunun
            // boşluklara serdiği bulutlara sığ ama tekrarsız (48 km periyot) rölyef
            // verir — küçük kubbelerin (≥~0.3) altında kalır, boyu bozamaz.
            // Dolgu tabanı da METRE cinsinden: katman tavanı yükselince oranla
            // yazılmış taban bütün gökyüzünü şişiriyordu.
            float fillMeters = 300f + 700f * raw[y, x];
            A[y, x] = Mathf.Max(aDome[y, x], fillMeters / layerThickness);
        }

        // Dev kütlelere iç yarıklar: yalnız kenardan uzak iç bölgelere (ön mesafe
        // taraması) ~700 m'lik koridorlar — küçük/orta bulutlara dokunmaz.
        {
            var big = new bool[n, n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                big[y, x] = R[y, x] > 0.25f;
            var interior = (bool[,])big.Clone();
            var nextI = new bool[n, n];
            var depth = new float[n, n];
            for (int pass = 0; pass < 8; pass++)
            {
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    nextI[y, x] = interior[y, x]
                                  && interior[(y + 1) % n, x] && interior[(y + n - 1) % n, x]
                                  && interior[y, (x + 1) % n] && interior[y, (x + n - 1) % n];
                    if (nextI[y, x]) depth[y, x] += 1f;
                }
                (interior, nextI) = (nextI, interior);
            }

            float[,] crackField = ValueField(n, 10, 2, settings.weatherMapSeed * 23 + 7);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float ridge = 1f - Mathf.Abs(2f * crackField[y, x] - 1f);
                float crack = Mathf.Clamp01((ridge - 0.86f) / 0.10f);
                // Kapı DERİN (7+ texel ≈ 650 m iç derinlik = ~1.5 km+ kütle): sığ
                // kapı orta boy bulutların ortasından koridor geçirip at nalı /
                // C harfi şekiller bırakıyordu. Yarık yalnız koridorun iki yakası
                // ayrı bulut okunacak kadar geniş kütlede anlamlı.
                float interiorOnly = Mathf.Clamp01((depth[y, x] - 7f) / 4f);
                R[y, x] *= 1f - 0.85f * crack * interiorOnly;
            }
        }

        // Eğim garantisi artık kubbe geometrisinin kendisinde (boy ≤ 1.1·yarıçap,
        // paraboloit profil) — eski mesafe-kapağı + ağır bulanıklık zinciri gereksiz.
        // A'ya yalnız texel cilası: kubbe zirveleri komşudan miras almaz.
        BlurPeriodic(A, n, 2f);
        BlurPeriodic(B, n, 6f);
        BlurPeriodic(G, n, 3f);

        var texture = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            name = "CloudWeatherMap (canlı)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };

        // SANAT YÖNÜ (HZD): elle boyanmış harita üretilenin üstüne harmanlanır. Kanal
        // anlamları aynı — boyayan kişi "buraya kule koy" derken üretici de aynı dili
        // konuşuyor. Harman burada, pişirmede yapılır: sıçrama haritası bu sonuçtan
        // türüyor, çalışma zamanında harmanlansa sıçrama boyanmış bulutun üstünden
        // atlardı. Çözünürlük serbest — normalize koordinattan bilineer okunur.
        var art = settings.artDirectionMap;
        float artBlend = art == null ? 0f : Mathf.Clamp01(settings.artDirectionBlend);
        if (artBlend > 0f && !art.isReadable)
            throw new InvalidOperationException(
                $"Sanat yönü haritası '{art.name}' CPU'dan okunamıyor. "
                + "Doku import ayarlarında Read/Write Enabled açılmalı.");

        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var produced = new Color(
                Mathf.Clamp01(R[y, x]), Mathf.Clamp01(G[y, x]),
                Mathf.Clamp01(B[y, x]), Mathf.Clamp01(A[y, x]));

            pixels[y * n + x] = artBlend > 0f
                ? Color.Lerp(produced,
                             art.GetPixelBilinear((x + 0.5f) / n, (y + 0.5f) / n), artBlend)
                : produced;
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    /// Sıçrama haritası: kaba ızgarada kapsamanın EN BÜYÜK değeri. Işın yürüyüşü
    /// boş gökte bunu okuyup büyük adımlarla atlar — tek küçük doku okuması, tam
    /// yoğunluk değerlendirmesi yok.
    ///
    /// Genişletme (dilation) şart: shader haritayı bükülmüş koordinattan okuyor
    /// (kıyı dişlemesi ±650 m, rüzgâraltı dili 1000 m). Kaba texel "boş" derken o
    /// bükümün ulaşabileceği her yeri kapsamalı, yoksa sıçrama gerçek bir bulutun
    /// üstünden atlar. 4 texel genişletme 3 km'lik güvenlik payı bırakır; sıçrama
    /// mesafesi (1200 m) bunun altında kalır.
    public const int SkipResolution = 64;

    public static Texture2D GenerateSkipMap(Texture2D weatherMap)
    {
        int n = SkipResolution;
        int src = weatherMap.width;
        int ratio = Mathf.Max(1, src / n);
        var pixels = weatherMap.GetPixels();

        var coarse = new float[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float peak = 0f;
            for (int dy = 0; dy < ratio; dy++)
            for (int dx = 0; dx < ratio; dx++)
                peak = Mathf.Max(peak, pixels[(y * ratio + dy) * src + x * ratio + dx].r);
            coarse[y, x] = peak;
        }

        const int dilate = 4;
        var grown = new float[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float peak = 0f;
            for (int dy = -dilate; dy <= dilate; dy++)
            for (int dx = -dilate; dx <= dilate; dx++)
                peak = Mathf.Max(peak, coarse[((y + dy) % n + n) % n, ((x + dx) % n + n) % n]);
            grown[y, x] = peak;
        }

        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };

        var output = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            output[y * n + x] = new Color(grown[y, x], 0f, 0f, 1f);

        texture.SetPixels(output);
        texture.Apply(false);
        return texture;
    }

    struct Core
    {
        public float x, y, radius, angle, aspect, amp, type, baseOffset, ceiling;
    }

    /// Döngüsel değer gürültüsü fbm, 0-1'e normalize
    static float[,] ValueField(int n, int baseCells, int octaves, int seed)
    {
        var field = new float[n, n];
        float amp = 1f, total = 0f;
        var rng = new System.Random(seed);

        for (int octave = 0; octave < octaves; octave++)
        {
            int cells = baseCells << octave;
            var grid = new float[cells, cells];
            for (int y = 0; y < cells; y++)
            for (int x = 0; x < cells; x++)
                grid[y, x] = (float)rng.NextDouble();

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n * cells;
                float v = (float)y / n * cells;
                int i0 = (int)u, j0 = (int)v;
                float fu = Smooth(u - i0), fv = Smooth(v - j0);
                int i1 = (i0 + 1) % cells, j1 = (j0 + 1) % cells;

                float value = Mathf.Lerp(
                    Mathf.Lerp(grid[j0, i0], grid[j0, i1], fu),
                    Mathf.Lerp(grid[j1, i0], grid[j1, i1], fu), fv);
                field[y, x] += amp * value;
            }

            total += amp;
            amp *= 0.5f;
        }

        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in field) { min = Mathf.Min(min, v); max = Mathf.Max(max, v); }

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            field[y, x] = Mathf.InverseLerp(min, max, field[y, x]);
        return field;
    }

    /// Ayrılabilir döngüsel gauss bulanıklığı, yerinde
    static void BlurPeriodic(float[,] field, int n, float sigma)
    {
        int radius = Mathf.CeilToInt(sigma * 3f);
        var kernel = new float[radius * 2 + 1];
        float sum = 0f;
        for (int i = -radius; i <= radius; i++)
        {
            kernel[i + radius] = Mathf.Exp(-(i * i) / (2f * sigma * sigma));
            sum += kernel[i + radius];
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        var temp = new float[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float acc = 0f;
            for (int i = -radius; i <= radius; i++)
                acc += field[y, ((x + i) % n + n) % n] * kernel[i + radius];
            temp[y, x] = acc;
        }
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float acc = 0f;
            for (int i = -radius; i <= radius; i++)
                acc += temp[((y + i) % n + n) % n, x] * kernel[i + radius];
            field[y, x] = acc;
        }
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);
}
