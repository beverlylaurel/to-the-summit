using System;
using UnityEditor;
using UnityEngine;

/// Volumetrik bulutların 3B gürültü dokularını üretir.
///
/// Taban dokusu (RGBA): R Perlin-Worley karışımı — bulutun genel kütlesi.
/// G, B, A artan frekansta Worley — kabarcıklı yapı.
/// Detay dokusu (RGB): artan frekansta Worley — siluet aşındırma.
///
/// Dokular döngüsel (tileable) üretilir; sonsuz gökyüzünde dikiş görünmez.
public static class CloudNoiseGenerator
{
    public const string BasePath = "Assets/Settings/CloudBaseNoise.asset";
    public const string DetailPath = "Assets/Settings/CloudDetailNoise.asset";
    public const string CurlPath = "Assets/Settings/CloudCurlNoise.asset";
    /// Sürüm DOSYA ADINDA. Nesnenin adına yazmak işe yaramıyor: AssetDatabase
    /// .CreateAsset ana nesnenin adını dosya adına çeviriyor, dolayısıyla bir sonraki
    /// açılışta sürüm etiketi kaybolmuş oluyor ve doku HER SEFERİNDE yeniden
    /// üretiliyordu. Yol sürümü taşıyınca varlık kontrolü yeterli.
    public const string HighPath = "Assets/Settings/CloudHighNoise_v3.asset";

    /// PDF'in ölçüsü: 128³. 96³'te bir texel 29 metre ediyordu (2.9 km periyot),
    /// 128³'te 22 metre — Perlin-Worley'nin tomurcukları ve Worley oktavları daha
    /// ince çözülüyor, ayrıca aynı dünya periyodunda daha çok çeşitlilik olduğu için
    /// döşeme tekrarı da geç fark ediliyor. Pişirme bir kez, ~2.4 kat daha uzun.
    const int BaseResolution = 128;
    const int DetailResolution = 32;
    const int CurlResolution = 128;
    const int HighResolution = 256;

    /// Curl gürültüsü: ıraksamasız (divergence-free) vektör alanı — akışkan hareketinin
    /// ucuz taklidi. Aşındırma dokusunun okunduğu koordinatı büker; bulut kenarlarına
    /// burgulu, türbülanslı biçim verir (Guerrilla'nın imza dokusu). Iraksamasızlık
    /// önemli: sıradan gürültüyle bükmek alanı şişirip söndürür, curl yalnız kaydırır.
    public static Texture2D LoadOrCreateCurl()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(CurlPath);
        if (existing != null) return existing;

        int n = CurlResolution;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[n * n];
        float inv = 1f / n;
        const float eps = 1f / 256f;

        try
        {
            for (int y = 0; y < n; y++)
            {
                EditorUtility.DisplayProgressBar("Curl gürültüsü", $"{y + 1}/{n}", (y + 1f) / n);

                for (int x = 0; x < n; x++)
                {
                    var p = new Vector3(x * inv, y * inv, 0f);

                    // Üç ayrı potansiyel alanın curl'ü — sonlu farkla.
                    float3x3 curl = Curl(p, eps);
                    pixels[y * n + x] = new Color(
                        curl.a * 0.5f + 0.5f, curl.b * 0.5f + 0.5f, curl.c * 0.5f + 0.5f, 1f);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        AssetDatabase.CreateAsset(texture, CurlPath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    /// Yüksek irtifa bulut dokusu: hacimsel katmanın ÜSTÜNDE, ışın yürüyüşü olmadan
    /// çizilen sirrus/alto katmanı. Üç kanal üç bulut cinsi:
    ///   R sirrus — rüzgârla taranmış uzun tüy çizgileri (anizotropik fbm)
    ///   G altokümülüs — düzenli benek tarlası (Worley)
    ///   B altostratus — geniş, yumuşak levha (düşük frekans fbm)
    /// Katman kilometrelerce yukarıda ve ince; hacimsel çözüm oraya harcanmaz —
    /// PDF'in de tercihi bu.
    /// Sürüm adı: üretim değişince eski asset kendiliğinden yeniden pişer.

    public static Texture2D LoadOrCreateHigh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(HighPath);
        if (existing != null) return existing;

        // Eski sürümler temizlenir: sürüm yolda taşındığı için artık dosya olarak
        // birikirlerdi.
        AssetDatabase.DeleteAsset("Assets/Settings/CloudHighNoise.asset");

        int n = HighResolution;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[n * n];
        float inv = 1f / n;

        try
        {
            for (int y = 0; y < n; y++)
            {
                EditorUtility.DisplayProgressBar("Yüksek bulut dokusu", $"{y + 1}/{n}", (y + 1f) / n);

                for (int x = 0; x < n; x++)
                {
                    var p = new Vector3(x * inv, y * inv, 0f);

                    // Sirrus: alan CURL ile bükülür — PDF'in türbülans mekanizması
                    // (aynı curl dokusunu zaten üretiyoruz). Iraksamasız büküm
                    // fbm'i filamentlere ayırır: rüzgâr makaslamasının taradığı
                    // ince tüy demetleri. Kendi uydurduğum eksene hizalı sıkıştırma
                    // (x*0.35, y*3.2) düz ve sert çizgiler veriyordu — gökyüzünde
                    // uçak izi gibi okunuyordu; üstelik tamsayı olmayan çarpanlar
                    // döşemeyi de bozuyordu.
                    float3x3 flow = Curl(p, 1f / 256f);
                    var swept = new Vector3(p.x + flow.a * 0.6f, p.y + flow.b * 0.6f, 11.7f);
                    float streak = PerlinFbm(swept, 3f, 4);
                    float cirrus = Mathf.Clamp01(Remap(streak, 0.44f, 0.78f, 0f, 1f));
                    cirrus *= cirrus;   // uçlar seyrelir, gövde kalır

                    // Altokümülüs: düzenli benek tarlası — ters Worley.
                    float dapple = WorleyFbm(new Vector3(p.x, p.y, 31.3f), 10f);
                    float alto = Mathf.Clamp01(Remap(dapple, 0.35f, 0.8f, 0f, 1f));

                    // Altostratus: geniş yumuşak levha.
                    float sheet = PerlinFbm(new Vector3(p.x, p.y, 57.9f), 2f, 3);

                    pixels[y * n + x] = new Color(cirrus, alto, Mathf.Clamp01(sheet), 1f);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        AssetDatabase.CreateAsset(texture, HighPath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    struct float3x3 { public float a, b, c; }

    /// Vektör potansiyelin curl'ü: ∇×P. Bileşenler farklı frekanslardan alınır ki
    /// üç kanal bağımsız burgu taşısın.
    static float3x3 Curl(Vector3 p, float eps)
    {
        float px0 = Potential(p - new Vector3(eps, 0, 0), 4f, 11.3f);
        float px1 = Potential(p + new Vector3(eps, 0, 0), 4f, 11.3f);
        float py0 = Potential(p - new Vector3(0, eps, 0), 4f, 11.3f);
        float py1 = Potential(p + new Vector3(0, eps, 0), 4f, 11.3f);

        float qx0 = Potential(p - new Vector3(eps, 0, 0), 8f, 37.7f);
        float qx1 = Potential(p + new Vector3(eps, 0, 0), 8f, 37.7f);
        float qy0 = Potential(p - new Vector3(0, eps, 0), 8f, 37.7f);
        float qy1 = Potential(p + new Vector3(0, eps, 0), 8f, 37.7f);

        float rx0 = Potential(p - new Vector3(eps, 0, 0), 16f, 71.9f);
        float ry1 = Potential(p + new Vector3(0, eps, 0), 16f, 71.9f);
        float rx1 = Potential(p + new Vector3(eps, 0, 0), 16f, 71.9f);
        float ry0 = Potential(p - new Vector3(0, eps, 0), 16f, 71.9f);

        float scale = 1f / (2f * eps);
        return new float3x3
        {
            a = Mathf.Clamp((py1 - py0) * scale, -1f, 1f),
            b = Mathf.Clamp(-(px1 - px0) * scale, -1f, 1f),
            c = Mathf.Clamp(((qy1 - qy0) - (rx1 - rx0) + (ry1 - ry0) - (qx1 - qx0)) * scale * 0.5f,
                            -1f, 1f)
        };
    }

    static float Potential(Vector3 p, float frequency, float offset)
        => PerlinFbm(p + new Vector3(offset, offset, offset), frequency, 3) * 0.35f;

    public static Texture3D LoadOrCreateBase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(BasePath);
        if (existing != null && existing.width == BaseResolution) return existing;
        if (existing != null) AssetDatabase.DeleteAsset(BasePath);

        var texture = Create(BaseResolution, BaseVoxel, "Bulut taban dokusu");
        AssetDatabase.CreateAsset(texture, BasePath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    public static Texture3D LoadOrCreateDetail()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(DetailPath);
        if (existing != null) return existing;

        var texture = Create(DetailResolution, DetailVoxel, "Bulut detay dokusu");
        AssetDatabase.CreateAsset(texture, DetailPath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    static Texture3D Create(int resolution, Func<Vector3, Color> voxel, string title)
    {
        var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[resolution * resolution * resolution];
        float inv = 1f / resolution;

        try
        {
            // Dilimler paralel: 128³ tek iş parçacığında editörü dakikalarca
            // kilitliyor. Voxel fonksiyonu saf (durumsuz) olduğu için dilimler
            // birbirinden bağımsız; ilerleme çubuğu ana iş parçacığında kalır.
            for (int block = 0; block < resolution; block += 8)
            {
                EditorUtility.DisplayProgressBar(title, $"{block}/{resolution}",
                                                 block / (float)resolution);

                int last = Mathf.Min(block + 8, resolution);
                System.Threading.Tasks.Parallel.For(block, last, z =>
                {
                    for (int y = 0; y < resolution; y++)
                    for (int x = 0; x < resolution; x++)
                        pixels[x + y * resolution + z * resolution * resolution] =
                            voxel(new Vector3(x * inv, y * inv, z * inv));
                });
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    static Color BaseVoxel(Vector3 p)
    {
        // Perlin-Worley: yumuşak dalgalanma kabarcıklı yapıya oturtulur.
        // Guerrilla'nın Nubis modelinde bulutun genel kütlesini bu kanal taşır.
        float perlin = PerlinFbm(p, 4f, 5);
        float worleyLow = WorleyFbm(p, 6f);
        float perlinWorley = Remap(perlin, worleyLow - 1f, 1f, 0f, 1f);

        return new Color(
            perlinWorley,
            WorleyFbm(p, 6f),
            WorleyFbm(p, 12f),
            WorleyFbm(p, 24f));
    }

    static Color DetailVoxel(Vector3 p)
    {
        return new Color(
            WorleyFbm(p, 8f),
            WorleyFbm(p, 16f),
            WorleyFbm(p, 32f),
            1f);
    }

    /// Ters Worley oktavları: 1'e yakın değerler öbek merkezleri, kabarcıklı doku
    static float WorleyFbm(Vector3 p, float frequency)
    {
        return Worley(p, frequency) * 0.625f
             + Worley(p, frequency * 2f) * 0.25f
             + Worley(p, frequency * 4f) * 0.125f;
    }

    /// Döngüsel Worley: hücre indeksleri sarmalandığı için doku dikişsiz tekrarlar
    static float Worley(Vector3 p, float frequency)
    {
        Vector3 scaled = p * frequency;
        Vector3 cell = new(Mathf.Floor(scaled.x), Mathf.Floor(scaled.y), Mathf.Floor(scaled.z));
        float nearest = 1f;
        int period = Mathf.Max(1, Mathf.RoundToInt(frequency));

        for (int z = -1; z <= 1; z++)
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            Vector3 offset = new(x, y, z);
            Vector3 neighbour = cell + offset;

            Vector3 wrapped = new(
                Mathf.Repeat(neighbour.x, period),
                Mathf.Repeat(neighbour.y, period),
                Mathf.Repeat(neighbour.z, period));

            Vector3 feature = neighbour + Hash3(wrapped);
            nearest = Mathf.Min(nearest, (feature - scaled).sqrMagnitude);
        }

        return 1f - Mathf.Clamp01(Mathf.Sqrt(nearest));
    }

    static float PerlinFbm(Vector3 p, float frequency, int octaves)
    {
        float sum = 0f;
        float norm = 0f;
        float amplitude = 1f;

        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(p, frequency) * amplitude;
            norm += amplitude;
            frequency *= 2f;
            amplitude *= 0.5f;
        }

        return sum / norm;
    }

    /// Döngüsel değer gürültüsü: köşe değerleri sarmalanmış hücre indeksinden türer
    static float ValueNoise(Vector3 p, float frequency)
    {
        Vector3 scaled = p * frequency;
        Vector3 cell = new(Mathf.Floor(scaled.x), Mathf.Floor(scaled.y), Mathf.Floor(scaled.z));
        Vector3 f = scaled - cell;

        f = new Vector3(Smooth(f.x), Smooth(f.y), Smooth(f.z));
        int period = Mathf.Max(1, Mathf.RoundToInt(frequency));

        float c000 = Corner(cell, 0, 0, 0, period);
        float c100 = Corner(cell, 1, 0, 0, period);
        float c010 = Corner(cell, 0, 1, 0, period);
        float c110 = Corner(cell, 1, 1, 0, period);
        float c001 = Corner(cell, 0, 0, 1, period);
        float c101 = Corner(cell, 1, 0, 1, period);
        float c011 = Corner(cell, 0, 1, 1, period);
        float c111 = Corner(cell, 1, 1, 1, period);

        float x00 = Mathf.Lerp(c000, c100, f.x);
        float x10 = Mathf.Lerp(c010, c110, f.x);
        float x01 = Mathf.Lerp(c001, c101, f.x);
        float x11 = Mathf.Lerp(c011, c111, f.x);

        return Mathf.Lerp(Mathf.Lerp(x00, x10, f.y), Mathf.Lerp(x01, x11, f.y), f.z);
    }

    static float Corner(Vector3 cell, int dx, int dy, int dz, int period)
    {
        Vector3 corner = new(
            Mathf.Repeat(cell.x + dx, period),
            Mathf.Repeat(cell.y + dy, period),
            Mathf.Repeat(cell.z + dz, period));

        return Hash1(corner);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static float Hash1(Vector3 p)
    {
        float h = Vector3.Dot(p, new Vector3(127.1f, 311.7f, 74.7f));
        return Frac(Mathf.Sin(h) * 43758.5453f);
    }

    static Vector3 Hash3(Vector3 p)
    {
        return new Vector3(
            Frac(Mathf.Sin(Vector3.Dot(p, new Vector3(127.1f, 311.7f, 74.7f))) * 43758.5453f),
            Frac(Mathf.Sin(Vector3.Dot(p, new Vector3(269.5f, 183.3f, 246.1f))) * 43758.5453f),
            Frac(Mathf.Sin(Vector3.Dot(p, new Vector3(113.5f, 271.9f, 124.6f))) * 43758.5453f));
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    static float Remap(float value, float low, float high, float newLow, float newHigh)
        => newLow + (value - low) / Mathf.Max(0.0001f, high - low) * (newHigh - newLow);
}
