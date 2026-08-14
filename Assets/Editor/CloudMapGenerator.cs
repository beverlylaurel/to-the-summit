using UnityEditor;
using UnityEngine;

/// Hava haritasını üretir. Kanallar `[H18 s.11]`:
///   R = seyrek kapsama (w_c0) — bulutların nerede olduğu, aralarda gerçek boşluk var
///   G = yoğun kapsama (w_c1) — kapsama sürgüsü 0.5'i geçince göğü kapatan harita
///   B = azami bulut yüksekliği (w_h) — sütun başına tavan
///
/// G, R ile AYNI gürültüden daha düşük eşikle türetiliyor. Böylece kapsama yükselince
/// ikinci bir bağımsız bulut alanı doğmuyor, mevcut bulutlar dışa doğru büyüyor.
///
/// Harita dünya XZ'sinde döşendiği için gürültü de kendi periyodunda sarmalı.
public static class CloudMapGenerator
{
    const string MapPath = "Assets/VolumetricClouds/Textures/CloudMap.asset";

    /// ÜRETİCİ SÜRÜMÜ. Buradaki algoritma veya sabitler değişince ARTTIRILIR; kurulum
    /// diskteki haritanın etiketine bakıp bayatsa kendisi yeniliyor. Elle menüye basmaya
    /// bırakılırsa bayat harita sessizce kullanılıyor — A kanalı eklendiğinde yoğunluk iki
    /// katına çıkmıştı, ekranda "yanlış ayar" gibi görünüyordu.
    const int MapVersion = 2;
    static string VersionLabel => $"CloudMap-v{MapVersion}";
    const int Resolution = 512;
    const int Octaves = 5;
    const int BaseCells = 4;
    const float MinCloudTop = 0.55f;

    // `DA = … × w_d × 2` `[H18 Ek B.3]`: 0.5 nötr çarpan. Aralık [0.35, 0.65] → çarpan
    // [0.70, 1.30]. Yerleşimden ayrı gürültü — geniş bulut ile yoğun bulut aynı şey değil.
    const float MinMapDensity = 0.35f;
    const float MaxMapDensity = 0.65f;

    // Ölçülerek seçildi. fBm kendi aralığına normalize edildikten sonra:
    // 0.50/0.15 → gökyüzünün %47'si bulutlu, %23'ü doygun çekirdek, bulut içi ortalama 0.74.
    // Doğrusal germe (kenar = 1 − eşik) çekirdeği asla 1.0'a taşımıyor: doygun alan binde 2'de
    // kalıyor, shader `coverage²` aldığı için bulut görünmez oluyor. Plato şart.
    const float SparseThreshold = 0.50f;
    const float SparseEdge = 0.15f;
    const float DenseThreshold = 0.20f;
    const float DenseEdge = 0.25f;

    [MenuItem("To The Summit/Bulut/Hava Haritasını Üret", false, 40)]
    public static void Generate()
    {
        CreateOrUpdate();
        AssetDatabase.SaveAssets();
    }

    /// Haritayı yoksa üretir, bayatsa yeniler, güncelse olduğu gibi döndürür.
    public static Texture2D EnsureExists()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        if (existing == null) return CreateOrUpdate();

        foreach (var label in AssetDatabase.GetLabels(existing))
            if (label == VersionLabel) return existing;

        return CreateOrUpdate();
    }

    static Texture2D CreateOrUpdate()
    {
        // Tavan ayrı bir gürültü: geniş bulut alanı ile yüksek bulut aynı şey değil.
        float[] placement = BuildField(0x51ED270B);
        float[] tops = BuildField(0x2F6E1A93);
        float[] densities = BuildField(0x7A19C4E5);

        var pixels = new Color[Resolution * Resolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            float sparse = Plateau(placement[i], SparseThreshold, SparseEdge);
            float dense = Plateau(placement[i], DenseThreshold, DenseEdge);
            float top = Mathf.Lerp(MinCloudTop, 1.0f, tops[i]);
            float density = Mathf.Lerp(MinMapDensity, MaxMapDensity, densities[i]);
            pixels[i] = new Color(sparse, dense, top, density);
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        bool isNew = texture == null;
        if (isNew)
            texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, mipChain: false, linear: true);
        // Harita RGB24 üretilmişti; A kanalı eklenince biçim değişti. `Reinitialize` asset
        // nesnesini koruyor — yeniden oluşturulsa profildeki referans kopardı.
        else if (texture.format != TextureFormat.RGBA32 || texture.width != Resolution)
            texture.Reinitialize(Resolution, Resolution, TextureFormat.RGBA32, hasMipMap: false);

        texture.name = "CloudMap";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false);

        // Yerinde yazılıyor: sahnedeki ve profildeki referanslar kopmasın.
        if (isNew) AssetDatabase.CreateAsset(texture, MapPath);
        else EditorUtility.SetDirty(texture);

        // Sürüm etikette duruyor. Asset adı `CreateAsset` tarafından dosya adına
        // eziliyor, oraya yazılamıyor.
        AssetDatabase.SetLabels(texture, new[] { VersionLabel });

        return texture;
    }

    /// fBm alanını üretip kendi aralığına normalize eder. Normalizasyon olmadan eşiklerin
    /// anlamı çekirdeğe göre kayıyor: ham fBm [0.17, 0.83] aralığında kalıyor.
    static float[] BuildField(uint seed)
    {
        var field = new float[Resolution * Resolution];
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float value = Fbm(x / (float)Resolution, y / (float)Resolution, seed);
                field[y * Resolution + x] = value;
                if (value < min) min = value;
                if (value > max) max = value;
            }
        }

        float range = max - min;
        for (int i = 0; i < field.Length; i++)
            field[i] = (field[i] - min) / range;

        return field;
    }

    /// Eşiğin altı sıfır, eşikten `edge` kadar sonrası 1.0 ve orada kalıyor. Kenar yumuşak,
    /// çekirdek doygun — kapsama haritasının olması gereken biçim.
    static float Plateau(float value, float threshold, float edge)
    {
        return Mathf.Clamp01((value - threshold) / edge);
    }

    static float Fbm(float u, float v, uint seed)
    {
        float sum = 0.0f;
        float amplitude = 1.0f;
        float normalization = 0.0f;
        int cells = BaseCells;

        for (int octave = 0; octave < Octaves; octave++)
        {
            sum += ValueNoise(u * cells, v * cells, cells, seed + (uint)octave * 0x9E3779B1u) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            cells *= 2;
        }

        return sum / normalization;
    }

    /// Sarmalı değer gürültüsü. Hücre koordinatı periyoda göre mod alınıyor ki doku döşenirken
    /// dikiş oluşmasın.
    static float ValueNoise(float x, float y, int period, uint seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3.0f - 2.0f * fx);
        fy = fy * fy * (3.0f - 2.0f * fy);

        float v00 = Hash(x0, y0, period, seed);
        float v10 = Hash(x0 + 1, y0, period, seed);
        float v01 = Hash(x0, y0 + 1, period, seed);
        float v11 = Hash(x0 + 1, y0 + 1, period, seed);

        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    /// Tamsayı bit karıştırıcı. Sinüs tabanlı hash küçük tamsayı hücrelerde ilişkili değer
    /// üretip kafes deseni yaratıyordu; ölçülüp bulundu, bir daha kullanılmıyor.
    static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        h ^= h >> 16;
        return h;
    }

    static float Hash(int x, int y, int period, uint seed)
    {
        uint cx = (uint)(((x % period) + period) % period);
        uint cy = (uint)(((y % period) + period) % period);
        uint h = Mix(cx * 0x9E3779B1u) ^ Mix(cy * 0x85EBCA77u) ^ Mix(seed);
        return Mix(h) / 4294967296.0f;
    }
}
