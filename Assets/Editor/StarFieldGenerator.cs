using UnityEditor;
using UnityEngine;

/// YILDIZ ALANI. Gökyüzü paketinin `spaceEmissionTexture` girişine verilen küp harita.
///
/// Paket bunu YALNIZ ışın hiçbir şeye çarpmadığında (uzaya bakarken) ekliyor ve sonucu
/// `(1 − skyOpacity)` ile çarpıyor — yani gündüz atmosfer opaklaştıkça yıldızlar
/// kendiliğinden yıkanıyor. Eski sistemdeki `(1−gündüz) × (1−kapsama)` kuralına gerek
/// yok: gündüzü opaklık, bulut örtüsünü de hacimsel bulutların kendisi hallediyor.
///
/// GÜRÜLTÜ TAMSAYI KARIŞTIRICIDAN. `frac(sin(...))` küçük tamsayı girdilerde korele
/// çıkıyor ve düzenli desen üretiyor — bulut gürültüsünde ölçülmüştü
/// (`CLOUDS_REBUILD.md`, ders 9).
static class StarFieldGenerator
{
    const string MapPath = "Assets/Settings/StarField.asset";

    const int MapVersion = 1;
    static string VersionLabel => $"StarField-v{MapVersion}";

    /// Bir yüzün kenar uzunluğu. 512'de bir teksel ~0.35°, yani yıldız küçük bir nokta
    /// olarak okunuyor. 256'da ~0.7° oluyor ve nokta değil leke görünüyor.
    const int FaceSize = 512;

    /// Çıplak gözle görülen yıldız sayısı gökyüzünün tamamında ~6000, ama en sönükleri
    /// zaten seçilemez. Brief "az sayıda, farklı parlaklıkta, küçük ve keskin" diyor.
    const int StarCount = 1500;

    /// Kadir aralığı. Her kadir bir öncekinin 10^(−0.4) katı, yani 6. kadir 0. kadirin
    /// yüzde 0.4'ü.
    const float BrightestMagnitude = 0f;
    const float FaintestMagnitude = 6f;

    /// Haritayı yoksa üretir, bayatsa yeniler, güncelse olduğu gibi döndürür.
    public static Cubemap EnsureExists()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(MapPath);
        if (existing == null) return CreateOrUpdate();

        foreach (var label in AssetDatabase.GetLabels(existing))
            if (label == VersionLabel) return existing;

        return CreateOrUpdate();
    }

    static Cubemap CreateOrUpdate()
    {
        var faces = new Color[6][];
        for (int f = 0; f < 6; f++)
        {
            faces[f] = new Color[FaceSize * FaceSize];
            for (int i = 0; i < faces[f].Length; i++) faces[f][i] = Color.black;
        }

        for (uint i = 0; i < StarCount; i++)
        {
            // Küre üzerinde DÜZGÜN dağılım: `z` doğrudan tekdüze seçiliyor. Açı `acos`
            // ile seçilseydi kutuplarda yığılırdı.
            float z = 1f - 2f * Hash(i, 0u);
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = Hash(i, 1u) * 2f * Mathf.PI;

            var direction = new Vector3(radius * Mathf.Cos(phi), z, radius * Mathf.Sin(phi));

            // Sönük yıldız çok, parlak yıldız az. Küp kökü, kadir başına ~2.5 kat artan
            // gerçek sayıma yakın bir dağılım veriyor.
            float magnitude = Mathf.Lerp(BrightestMagnitude, FaintestMagnitude,
                Mathf.Pow(Hash(i, 2u), 1f / 3f));

            Color color = StarColor(Hash(i, 3u)) * Mathf.Pow(10f, -0.4f * magnitude);

            Splat(faces, direction, color);
        }

        var map = AssetDatabase.LoadAssetAtPath<Cubemap>(MapPath);
        bool isNew = map == null || map.width != FaceSize || map.format != TextureFormat.RGBAHalf;

        if (isNew)
        {
            // HDR: en parlak yıldız 1.0, en sönük 0.004. Sekiz bitte sönük uç tek
            // basamağa düşüp bantlaşıyor.
            map = new Cubemap(FaceSize, TextureFormat.RGBAHalf, mipChain: false);
        }

        map.filterMode = FilterMode.Bilinear;
        map.wrapMode = TextureWrapMode.Clamp;

        for (int f = 0; f < 6; f++) map.SetPixels(faces[f], (CubemapFace)f);
        map.Apply(updateMipmaps: false);

        // Yerinde yazılıyor: profildeki referans kopmasın.
        if (isNew) AssetDatabase.CreateAsset(map, MapPath);
        else EditorUtility.SetDirty(map);

        // Sürüm etikette duruyor. Asset adı `CreateAsset` tarafından dosya adına
        // eziliyor, oraya yazılamıyor.
        AssetDatabase.SetLabels(map, new[] { VersionLabel });

        return map;
    }

    /// Yıldız rengi sıcaklığından gelir: sıcak olan mavi-beyaz, soğuk olan turuncu.
    /// Çoğunluk beyaza yakın — seçim ortaya doğru büzülüyor ki uçlar azınlıkta kalsın.
    static Color StarColor(float pick)
    {
        float t = (pick - 0.5f) * 2f;
        t = Mathf.Sign(t) * t * t;

        return t < 0f
            ? Color.Lerp(Color.white, new Color(0.72f, 0.80f, 1.00f), -t)
            : Color.Lerp(Color.white, new Color(1.00f, 0.84f, 0.68f), t);
    }

    /// Yıldızı ait olduğu yüzün tekseline yazar. Tek teksel bilinear örneklemede yumuşak
    /// bir noktaya dönüşüyor; komşulara yayılırsa nokta değil leke oluyor.
    ///
    /// Aynı tekseli iki yıldız paylaşırsa parlaklıkları TOPLANIYOR — üzerine yazmak sönük
    /// olanı yok sayardı ve sık bölgelerde sayım düşerdi.
    static void Splat(Color[][] faces, Vector3 direction, Color color)
    {
        DirectionToFace(direction, out int face, out float s, out float t);

        int x = Mathf.Clamp(Mathf.FloorToInt(s * FaceSize), 0, FaceSize - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(t * FaceSize), 0, FaceSize - 1);

        faces[face][y * FaceSize + x] += color;
    }

    /// Yön → küp yüzü ve yüz içi koordinat. Unity'nin yüz sırası: +X, −X, +Y, −Y, +Z, −Z.
    static void DirectionToFace(Vector3 d, out int face, out float s, out float t)
    {
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
        float u, w, major;

        if (ax >= ay && ax >= az)
        {
            major = ax;
            face = d.x > 0f ? 0 : 1;
            u = d.x > 0f ? -d.z : d.z;
            w = -d.y;
        }
        else if (ay >= az)
        {
            major = ay;
            face = d.y > 0f ? 2 : 3;
            u = d.x;
            w = d.y > 0f ? d.z : -d.z;
        }
        else
        {
            major = az;
            face = d.z > 0f ? 4 : 5;
            u = d.z > 0f ? d.x : -d.x;
            w = -d.y;
        }

        s = 0.5f * (u / major + 1f);
        t = 0.5f * (w / major + 1f);
    }

    /// Tamsayı bit karıştırıcı. `frac(sin(...))` KULLANILMIYOR: küçük tamsayı girdilerde
    /// korele çıkıyor ve düzenli desen üretiyor.
    static float Hash(uint index, uint channel)
    {
        uint h = index * 747796405u + channel * 2891336453u + 1u;
        h ^= h >> 17; h *= 0xed5ad4bbu;
        h ^= h >> 11; h *= 0xac4c1b51u;
        h ^= h >> 15; h *= 0x31848babu;
        h ^= h >> 14;

        return (h & 0x00FFFFFFu) / 16777215f;
    }
}
