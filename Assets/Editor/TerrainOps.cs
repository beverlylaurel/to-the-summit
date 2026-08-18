using UnityEngine;

/// Yükseklik alanı üzerinde çalışan İŞLEMLER. Arayüz yok, durum yok — hepsi
/// `float[]` alıp `float[]` değiştiriyor.
///
/// NEDEN AYRI DOSYA: pencere fırçaları ve önizlemeyi yönetiyor; buradakiler tüm
/// araziye (ya da bir maskeye) uygulanan toplu işlemler. İkisi karışınca pencere
/// okunmaz hâle geliyordu.
///
/// KOT BİRİMİ METRE. Bütün eşikler (duruş açısı, çökelme, teras yüksekliği) metre
/// cinsinden ve hücre boyu `cell` parametresinden geliyor; ızgara çözünürlüğü
/// değişince sonuç değişmiyor.
public static class TerrainOps
{
    // ============================================================ yardımcılar

    /// Ayrılabilir kutu bulanıklaştırma. Kayan toplam: maliyet yarıçapla büyümüyor.
    public static void Blur(float[] h, int n, int radius)
    {
        if (radius < 1) return;
        var tmp = new float[n * n];
        int w = radius * 2 + 1;

        for (int z = 0; z < n; z++)
        {
            float sum = 0f;
            for (int x = -radius; x <= radius; x++) sum += h[z * n + Mathf.Clamp(x, 0, n - 1)];
            for (int x = 0; x < n; x++)
            {
                tmp[z * n + x] = sum / w;
                sum -= h[z * n + Mathf.Clamp(x - radius, 0, n - 1)];
                sum += h[z * n + Mathf.Clamp(x + radius + 1, 0, n - 1)];
            }
        }
        for (int x = 0; x < n; x++)
        {
            float sum = 0f;
            for (int z = -radius; z <= radius; z++) sum += tmp[Mathf.Clamp(z, 0, n - 1) * n + x];
            for (int z = 0; z < n; z++)
            {
                h[z * n + x] = sum / w;
                sum -= tmp[Mathf.Clamp(z - radius, 0, n - 1) * n + x];
                sum += tmp[Mathf.Clamp(z + radius + 1, 0, n - 1) * n + x];
            }
        }
    }

    /// Maskeyle harmanlama. Bütün işlemler sonucu buradan geçiriyor: maske yoksa
    /// tamamı, varsa yalnız maskenin gösterdiği yer değişiyor.
    static void Blend(float[] h, float[] result, float[] mask, float amount)
    {
        for (int i = 0; i < h.Length; i++)
        {
            float w = amount * (mask != null ? mask[i] : 1f);
            if (w > 0f) h[i] = Mathf.Lerp(h[i], result[i], w);
        }
    }

    // ============================================================ aşınma

    /// TERMAL AŞINMA. Gevşek malzeme duruş açısının üstünde durmaz, kayar.
    ///
    /// Fiziksel: eğim eşiği aşan fark komşulara paylaştırılıyor. Bulanıklaştırmadan
    /// farkı sırt hatlarını ve vadi ağını KORUMASI — yalnız taşıyamayacağı kadar dik
    /// yüzleri indiriyor.
    public static void Thermal(float[] h, int n, float cell, float talusDeg, float rate,
                               int iters, float[] mask = null)
    {
        float maxStep = Mathf.Tan(talusDeg * Mathf.Deg2Rad) * cell;
        var d = new float[n * n];

        for (int it = 0; it < iters; it++)
        {
            System.Array.Clear(d, 0, d.Length);

            for (int z = 1; z < n - 1; z++)
            for (int x = 1; x < n - 1; x++)
            {
                int i = z * n + x;
                float c = h[i];
                float e0 = Mathf.Max(c - h[i - 1] - maxStep, 0f);
                float e1 = Mathf.Max(c - h[i + 1] - maxStep, 0f);
                float e2 = Mathf.Max(c - h[i - n] - maxStep, 0f);
                float e3 = Mathf.Max(c - h[i + n] - maxStep, 0f);
                float tot = e0 + e1 + e2 + e3;
                if (tot <= 0f) continue;

                float low = Mathf.Min(Mathf.Min(h[i - 1], h[i + 1]), Mathf.Min(h[i - n], h[i + n]));
                float moved = Mathf.Min(tot, (c - low) * 0.5f) * rate;
                float share = moved / tot;

                d[i] -= moved;
                d[i - 1] += e0 * share;
                d[i + 1] += e1 * share;
                d[i - n] += e2 * share;
                d[i + n] += e3 * share;
            }

            for (int i = 0; i < h.Length; i++)
                h[i] += d[i] * (mask != null ? mask[i] : 1f);
        }
    }

    /// HİDROLİK AŞINMA — damlacık modeli.
    ///
    /// Her damla rastgele bir yere düşüyor, eğim yönünde akıyor, hızlandıkça malzeme
    /// çözüyor, yavaşlayınca çökeltiyor. Termal aşınmanın veremediği şeyi veriyor:
    /// **vadi ağı** — dallanan oluklar, birikinti yelpazeleri, sırtların keskinleşmesi.
    ///
    /// Termal olan malzemeyi komşuya taşır ve yüzeyi düzler; hidrolik olan malzemeyi
    /// UZAĞA taşır ve yüzeyi oyar. İkisi ayrı olgu, biri diğerinin yerine geçmiyor.
    public static void Hydraulic(float[] h, int n, float cell, int droplets, int maxSteps,
                                 float inertia, float capacity, float erode, float deposit,
                                 float evaporate, float gravity, int seed,
                                 int brushRadius = 4, float[] mask = null)
    {
        var rnd = new System.Random(seed);

        // YÜKSEKLİK NORMALİZE EDİLİYOR — SABİTLER ONU VARSAYIYOR.
        //
        // Referans uygulama (SebLague/Hydraulic-Erosion, MIT) yükseklik alanını 0-1
        // aralığında tutuyor ve bütün katsayılar o ölçeğe göre seçilmiş. Bizim alanımız
        // METRE: aynı sabitlerle `speed² += -dh · gravity` her adımda yüzlerce metrelik
        // bir farkla besleniyor, hız ve taşıma kapasitesi ~1000 kat şişiyor. Damla dev
        // miktarda malzeme koparıp bıraktığı yere kule dikiyordu — ekranda sivri uçlar.
        //
        // Ölçüldü: metre uzayında tek geçiş 6001 m'lik dağı 8000 m tavanına dayadı.
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < h.Length; i++) { if (h[i] < lo) lo = h[i]; if (h[i] > hi) hi = h[i]; }
        float span = Mathf.Max(hi - lo, 1f);
        for (int i = 0; i < h.Length; i++) h[i] = (h[i] - lo) / span;

        for (int k = 0; k < droplets; k++)
        {
            float px = (float)(rnd.NextDouble() * (n - 2)) + 1f;
            float pz = (float)(rnd.NextDouble() * (n - 2)) + 1f;
            float dx = 0f, dz = 0f, speed = 1f, water = 1f, sediment = 0f;

            for (int s = 0; s < maxSteps; s++)
            {
                int ix = (int)px, iz = (int)pz;
                if (ix < 1 || iz < 1 || ix >= n - 2 || iz >= n - 2) break;

                float fx = px - ix, fz = pz - iz;
                int i = iz * n + ix;

                // İki doğrusal yükseklik ve gradyan.
                float h00 = h[i], h10 = h[i + 1], h01 = h[i + n], h11 = h[i + n + 1];
                float gx = (h10 - h00) * (1f - fz) + (h11 - h01) * fz;
                float gz = (h01 - h00) * (1f - fx) + (h11 - h10) * fx;
                float hOld = h00 * (1 - fx) * (1 - fz) + h10 * fx * (1 - fz)
                           + h01 * (1 - fx) * fz + h11 * fx * fz;

                // ATALET. Damla yönünü bir anda değiştirmiyor; yoksa her adımda en dik
                // yöne sıçrayıp ızgaraya hizalı zikzak oluklar açıyor.
                dx = dx * inertia - gx * (1f - inertia);
                dz = dz * inertia - gz * (1f - inertia);
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 1e-5f) break;
                dx /= len; dz /= len;

                px += dx; pz += dz;
                int nx = (int)px, nz = (int)pz;
                if (nx < 1 || nz < 1 || nx >= n - 2 || nz >= n - 2) break;

                float hNew = h[nz * n + nx];
                float dh = hNew - hOld;

                // Taşıma kapasitesi hıza ve eğime bağlı; yokuş yukarı giderse sıfır.
                float cap = Mathf.Max(-dh, 0.01f) * speed * water * capacity;

                if (sediment > cap || dh > 0f)
                {
                    // Çökelme: çukura girdiyse yalnız çukuru dolduracak kadar.
                    float amt = dh > 0f ? Mathf.Min(dh, sediment) : (sediment - cap) * deposit;
                    sediment -= amt;
                    Deposit(h, n, i, fx, fz, amt, mask);
                }
                else
                {
                    float amt = Mathf.Min((cap - sediment) * erode, -dh);
                    sediment += amt;
                    Erode(h, n, ix, iz, amt, brushRadius, mask);
                }

                speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + -dh * gravity));
                water *= 1f - evaporate;
                if (water < 0.01f) break;
            }
        }

        for (int i = 0; i < h.Length; i++) h[i] = h[i] * span + lo;
    }

    static void Deposit(float[] h, int n, int i, float fx, float fz, float amt, float[] mask)
    {
        if (amt <= 0f) return;
        float m0 = mask == null ? 1f : mask[i];
        h[i] += amt * (1 - fx) * (1 - fz) * m0;
        h[i + 1] += amt * fx * (1 - fz) * m0;
        h[i + n] += amt * (1 - fx) * fz * m0;
        h[i + n + 1] += amt * fx * fz * m0;
    }

    /// Aşınma bir FIRÇAYA yayılıyor: tek hücreden çekmek iğne deliği açıyor.
    ///
    /// YARIÇAP TARAKLANMAYI BELİRLİYOR. Küçük yarıçapta her damla kendi tek hücrelik
    /// oluğunu kazıyor; pürüzsüz bir yamaçta bütün düşüş hatları paralel olduğu için
    /// ekranda dikey taranmış çizgiler kalıyor. Geniş fırça komşu damlaların oluklarını
    /// birleştiriyor ve dallanan vadi ağı çıkıyor.
    static void Erode(float[] h, int n, int cx, int cz, float amt, int radius, float[] mask)
    {
        if (amt <= 0f) return;
        radius = Mathf.Max(1, radius);

        float total = 0f;
        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            if (d <= radius) total += 1f - d / radius;
        }
        if (total <= 0f) return;

        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            if (d > radius) continue;
            int ix = cx + x, iz = cz + z;
            if (ix < 0 || iz < 0 || ix >= n || iz >= n) continue;
            int i = iz * n + ix;
            h[i] -= amt * ((1f - d / radius) / total) * (mask == null ? 1f : mask[i]);
        }
    }

    // ============================================================ biçim

    /// FRAKTAL GÜRÜLTÜ. Her oktav döndürülüyor — hizalı oktavlar ızgara artefaktı
    /// üretiyor.
    ///
    /// NYQUIST SINIRI ZORUNLU: ızgara 2 hücreden kısa dalgayı taşıyamaz, istenirse
    /// geri katlanıp hücre-hücre zikzağa döner. Bu proje o hatayla bir gün kaybetti;
    /// sınır burada, çağıran yerde değil.
    public static void FractalNoise(float[] h, int n, float cell, float wavelengthM,
                                    int octaves, float amplitudeM, float persistence,
                                    float lacunarity, int seed, float[] mask = null)
    {
        var rnd = new System.Random(seed);
        float nyquist = 2f * cell;

        var acc = new float[n * n];
        float amp = 1f, wl = wavelengthM, norm = 0f;

        for (int o = 0; o < octaves; o++)
        {
            if (wl < nyquist) break;

            float rot = (float)(rnd.NextDouble() * Mathf.PI);
            float ox = (float)(rnd.NextDouble() * 1000.0);
            float oz = (float)(rnd.NextDouble() * 1000.0);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float f = cell / wl;

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float u = (x * cs - z * sn) * f + ox;
                float v = (x * sn + z * cs) * f + oz;
                acc[z * n + x] += (Mathf.PerlinNoise(u, v) - 0.5f) * amp;
            }

            norm += amp;
            amp *= persistence;
            wl /= lacunarity;
        }

        if (norm <= 0f) return;
        float k = amplitudeM / norm;
        for (int i = 0; i < h.Length; i++)
            h[i] += acc[i] * k * (mask == null ? 1f : mask[i]);
    }

    /// WARP — alanı kendi gürültüsüyle yatayda bükme. Düzgün biçimleri organikleştirir:
    /// dairesel etek dalgalı kıyıya, düz sırt kıvrımlı sırta döner.
    public static void Warp(float[] h, int n, float cell, float wavelengthM, float strengthM,
                            int seed, float[] mask = null)
    {
        var src = (float[])h.Clone();
        var rnd = new System.Random(seed);
        float ox = (float)(rnd.NextDouble() * 1000.0), oz = (float)(rnd.NextDouble() * 1000.0);
        float f = cell / Mathf.Max(wavelengthM, cell * 2f);
        float amp = strengthM / cell;

        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float wx = (Mathf.PerlinNoise(x * f + ox, z * f + oz) - 0.5f) * 2f * amp;
            float wz = (Mathf.PerlinNoise(x * f + ox + 57.3f, z * f + oz + 13.9f) - 0.5f) * 2f * amp;

            float sx = Mathf.Clamp(x + wx, 0, n - 1.001f);
            float sz = Mathf.Clamp(z + wz, 0, n - 1.001f);
            int x0 = (int)sx, z0 = (int)sz;
            float tx = sx - x0, tz = sz - z0;
            int x1 = Mathf.Min(x0 + 1, n - 1), z1 = Mathf.Min(z0 + 1, n - 1);

            float a = Mathf.Lerp(src[z0 * n + x0], src[z0 * n + x1], tx);
            float b = Mathf.Lerp(src[z1 * n + x0], src[z1 * n + x1], tx);
            float v = Mathf.Lerp(a, b, tz);

            int i = z * n + x;
            h[i] = Mathf.Lerp(h[i], v, mask == null ? 1f : mask[i]);
        }
    }

    /// TERAS / STRATİFİKASYON. Kotu basamaklara oturtuyor — tortul kaya katmanları,
    /// aşınmış plato kenarları.
    ///
    /// `sharpness` basamağın keskinliği: 0'da hiç görünmez, 1'de dik duvar. Sert
    /// kırpma kullanılmıyor, `smoothstep` ile — dik duvarın türevi kırılınca ekranda
    /// Mach bandı çıkıyor.
    public static void Terrace(float[] h, int n, float stepM, float sharpness,
                               float[] mask = null)
    {
        if (stepM <= 0.01f) return;
        for (int i = 0; i < h.Length; i++)
        {
            float t = h[i] / stepM;
            float baseLevel = Mathf.Floor(t);
            float frac = t - baseLevel;
            float shaped = Mathf.Lerp(frac, Mathf.SmoothStep(0f, 1f, frac), sharpness);
            float v = (baseLevel + shaped) * stepM;
            h[i] = Mathf.Lerp(h[i], v, mask == null ? 1f : mask[i]);
        }
    }

    /// KESKİNLEŞTİR / YUMUŞAT. Yüksek geçiren kazanç: 1'in üstü kabartıyı güçlendirir,
    /// altı söndürür. Yarıçap metre cinsinden ve Nyquist'in üstünde tutuluyor.
    public static void Sharpen(float[] h, int n, float cell, float radiusM, float gain,
                               float[] mask = null)
    {
        var smooth = (float[])h.Clone();
        Blur(smooth, n, Mathf.Max(1, Mathf.RoundToInt(radiusM / cell)));
        for (int i = 0; i < h.Length; i++)
        {
            float v = smooth[i] + (h[i] - smooth[i]) * gain;
            h[i] = Mathf.Lerp(h[i], v, mask == null ? 1f : mask[i]);
        }
    }

    /// Kot aralığını yeniden eşler. Dağın boyunu değiştirmenin doğru yolu: bütün
    /// kabartı oranını koruyor.
    public static void Remap(float[] h, float newMin, float newMax, float[] mask = null)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < h.Length; i++) { if (h[i] < lo) lo = h[i]; if (h[i] > hi) hi = h[i]; }
        if (hi - lo < 1e-3f) return;

        float k = (newMax - newMin) / (hi - lo);
        for (int i = 0; i < h.Length; i++)
        {
            float v = newMin + (h[i] - lo) * k;
            h[i] = Mathf.Lerp(h[i], v, mask == null ? 1f : mask[i]);
        }
    }

    /// RADYAL KONİ DAMGASI. Boş düzlemde işe başlamak için: merkeze bir kütle koyup
    /// üstünde çalışmaya devam ediliyor. Kesit `(1-r)²` — ovaya TEĞET iner, açıyla
    /// çarpmaz; dağ ile ova arasındaki "diz" o çarpmadan doğuyor.
    public static void Cone(float[] h, int n, float cell, float cxCell, float czCell,
                            float radiusM, float heightM, float[] mask = null)
    {
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x - cxCell) * cell, dz = (z - czCell) * cell;
            float r = Mathf.Sqrt(dx * dx + dz * dz) / radiusM;
            if (r >= 1f) continue;
            float t = (1f - r) * (1f - r);
            int i = z * n + x;
            h[i] += heightM * t * (mask == null ? 1f : mask[i]);
        }
    }

    /// ETEK GEÇİŞİ. Kütleye dokunmadan, arazinin dış bandındaki kabartıyı ovaya
    /// indirir. Dağı küçültmez — yalnız eteğin kenara çarpmadan inmesini sağlar.
    ///
    /// MESAFE ÇEBİŞEV (kare), YARIÇAP DEĞİL. Arena kare; dairesel bir halka köşeleri
    /// yüksek bırakır ve oyuncu köşeye yürüyünce duvar bulur. Aynı ders eski maskede
    /// de ölçülmüştü.
    ///
    /// GEÇİŞ SMOOTHSTEP: iki ucunda da türevi sıfır, yani ne dağın eteğinde ne ovanın
    /// başında diz oluşuyor. Doğrusal bir rampa tam da "dağ absürt bir anda yükseliyor"
    /// hissini veren kırılmayı bırakıyor.
    ///
    /// `keepRelief` dış uçta hayatta kalan kabartı payı: 0 ise ova cam gibi düz olur
    /// ve yapay durur, 0.1-0.2 arası hafif tepecikli düz bırakıyor.
    public static void Apron(float[] h, int n, float cell, float innerM, float outerM,
                             float plainLevel, float keepRelief)
    {
        float centre = (n - 1) * 0.5f;
        float inner = Mathf.Max(innerM, 1f);
        float outer = Mathf.Max(outerM, inner + 1f);

        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float dz = Mathf.Abs(z - centre) * cell;
            float dx = Mathf.Abs(x - centre) * cell;
            float d = Mathf.Max(dx, dz);                 // Çebişev
            if (d <= inner) continue;

            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
            float keep = Mathf.Lerp(1f, keepRelief, t);

            int i = z * n + x;
            h[i] = plainLevel + (h[i] - plainLevel) * keep;
        }
    }

    /// OVAYI YUMUŞATIR. Belirli bir kotun altındaki kabartıyı yerel ortalamaya doğru
    /// çeker — düzleştirmez, ALÇALTIR: tepecikler kalır ama boyları küçülür.
    ///
    /// Sabit bir kota oturtmak ovayı masa gibi yapıyor; yerel ortalamaya çekmek
    /// biçimi koruyup genliği düşürüyor.
    public static void CalmLowland(float[] h, int n, float cell, float belowM,
                                   float featherM, float keepRelief, float scaleM)
    {
        var smooth = (float[])h.Clone();
        Blur(smooth, n, Mathf.Max(1, Mathf.RoundToInt(scaleM / cell)));

        for (int i = 0; i < h.Length; i++)
        {
            // Yalnız alçak yerler: eşiğin üstü hiç etkilenmiyor.
            float w = 1f - Mathf.SmoothStep(0f, 1f,
                          Mathf.InverseLerp(belowM - featherM, belowM + featherM, h[i]));
            if (w <= 0.001f) continue;

            float target = smooth[i] + (h[i] - smooth[i]) * keepRelief;
            h[i] = Mathf.Lerp(h[i], target, w);
        }
    }

    // ============================================================ maskeler

    /// Kot bandı maskesi. Kenarlar yumuşak: sert eşik arazide kontur çizgisi bırakıyor.
    public static float[] MaskByHeight(float[] h, int n, float lo, float hi, float feather)
    {
        var m = new float[n * n];
        for (int i = 0; i < h.Length; i++)
        {
            float a = Mathf.SmoothStep(lo - feather, lo + feather, h[i]);
            float b = 1f - Mathf.SmoothStep(hi - feather, hi + feather, h[i]);
            m[i] = Mathf.Clamp01(Mathf.Min(a, b));
        }
        return m;
    }

    /// Eğim maskesi (derece).
    public static float[] MaskBySlope(float[] h, int n, float cell, float lo, float hi,
                                      float feather)
    {
        var m = new float[n * n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, n - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, n - 1);
            float gx = (h[z * n + xp] - h[z * n + xm]) / ((xp - xm) * cell);
            float gz = (h[zp * n + x] - h[zm * n + x]) / ((zp - zm) * cell);
            float deg = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;

            float a = Mathf.SmoothStep(lo - feather, lo + feather, deg);
            float b = 1f - Mathf.SmoothStep(hi - feather, hi + feather, deg);
            m[z * n + x] = Mathf.Clamp01(Mathf.Min(a, b));
        }
        return m;
    }

    /// Dışbükeylik maskesi: pozitif taraf SIRT, negatif taraf VADİ.
    ///
    /// Ölçüt eğim değil `h − bulanık h`. Eğim tabanlı maske bütün yamacı seçiyor;
    /// dışbükeylik yalnız sırtı ya da yalnız vadiyi seçiyor.
    public static float[] MaskByCurvature(float[] h, int n, float cell, float radiusM,
                                          bool ridges, float strength)
    {
        var smooth = (float[])h.Clone();
        Blur(smooth, n, Mathf.Max(1, Mathf.RoundToInt(radiusM / cell)));

        var m = new float[n * n];
        for (int i = 0; i < h.Length; i++)
        {
            float d = h[i] - smooth[i];
            if (!ridges) d = -d;
            m[i] = Mathf.Clamp01(d / Mathf.Max(strength, 0.01f));
        }
        return m;
    }

    public static float[] Combine(float[] a, float[] b)
    {
        if (a == null) return b;
        if (b == null) return a;
        var m = new float[a.Length];
        for (int i = 0; i < a.Length; i++) m[i] = a[i] * b[i];
        return m;
    }
}
