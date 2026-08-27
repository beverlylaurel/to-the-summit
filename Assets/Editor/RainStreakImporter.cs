using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// GARG-NAYAR İZ VERİTABANINI UNITY'YE ALIR — `rain-spec.md` §10.2 Aşama 1.
///
/// Kaynak 15 000 adet 16-bit tek kanal PNG ve proje dışında duruyor (Kirmse arazi
/// verisiyle aynı kural: ham veri repoya girmez). `Tools/rain/pack_streaks.py` onları
/// normalize edip float16 bloklara paketliyor; bu araç blokları `Texture2DArray`'e
/// çeviriyor.
///
/// NEDEN UNITY PNG'LERİ DOĞRUDAN OKUMUYOR: `ImageConversion` 16-bit gri PNG'yi 8-bit'e
/// indiriyor. Spec §5.4.4'ün uyardığı şey tam da bu — her dokunun kendi max çarpanı var
/// ve çarpanlar 0.002 ile 0.65 arasında değişiyor; hassasiyet kaybı sönük izleri
/// tamamen siliyor ve "tüm izler eşit parlak" sonucunu veriyor.
public static class RainStreakImporter
{
    /// Paketlenmiş bloklar PROJE AĞACINDA AMA `Assets/` DIŞINDA. İçeride olsalardı
    /// Unity onları `TextAsset` olarak import ederdi — editöre ve build'e 67 MB, oysa
    /// yalnız içe aktarma anında okunuyorlar. Ham PNG'ler repoya hiç girmiyor (Kirmse
    /// arazi verisiyle aynı kural).
    const string SourceFolder = "Tools/rain/packed";
    const string AssetPath = "Assets/Rain/RainStreakDatabase.asset";

    [MenuItem("To The Summit/Rain/Set Up Streak Database", false, 40)]
    static void Import()
    {
        var indexFiles = Directory.GetFiles(SourceFolder, "*.index.txt");
        if (indexFiles.Length == 0)
            throw new FileNotFoundException(
                $"{SourceFolder} içinde .index.txt yok. Önce Tools/rain/pack_streaks.py çalıştırılmalı.");

        var db = ScriptableObject.CreateInstance<RainStreakDatabase>();
        var sizes = new SortedSet<int>();
        var dcams = new SortedSet<int>();

        // Önce eksenleri ve seviyeleri topla: hangi boyut, hangi kamera açısı var.
        var parsed = new Dictionary<string, Index>();
        foreach (var file in indexFiles)
        {
            var index = Index.Read(file);
            parsed[Path.GetFileNameWithoutExtension(file).Replace(".index", "")] = index;
            sizes.Add(index.Width);
            dcams.Add(index.Dcam);
        }

        db.Sizes = sizes.ToArray();
        db.Vertical = parsed.Values.First(i => i.Vertical != null).Vertical;
        db.Horizontal = parsed.Values.First(i => i.Horizontal != null).Horizontal;

        var angles = new List<RainStreakDatabase.CameraAngle>();
        foreach (int dcam in dcams)
        {
            var angle = new RainStreakDatabase.CameraAngle
            {
                Dcam = dcam,
                Point = new Texture2DArray[db.Sizes.Length],
                Ambient = new Texture2DArray[db.Sizes.Length],
            };

            for (int s = 0; s < db.Sizes.Length; s++)
            {
                int size = db.Sizes[s];
                angle.Point[s] = Build($"point_size{size}_dcam{dcam:00}", parsed);
                angle.Ambient[s] = Build($"env_size{size}_dcam{dcam:00}", parsed);
            }

            // Varlık tablosu yalnız (dcam, v, h, osc)'ye bağlı — çözünürlükten
            // bağımsız, bir kez alınıyor.
            angle.Present = parsed[$"point_size{db.Sizes[0]}_dcam{dcam:00}"].Present;
            angles.Add(angle);
        }

        db.Angles = angles.ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        if (AssetDatabase.LoadAssetAtPath<RainStreakDatabase>(AssetPath) != null)
            AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(db, AssetPath);

        foreach (var angle in db.Angles)
        {
            foreach (var t in angle.Point) AssetDatabase.AddObjectToAsset(t, db);
            foreach (var t in angle.Ambient) AssetDatabase.AddObjectToAsset(t, db);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Report(db);
        Selection.activeObject = db;
    }

    /// Blok → `Texture2DArray`. Blok float16 ve dilim dilim sıralı; `SetPixelData`
    /// baytları olduğu gibi alıyor, dönüşüm yok.
    static Texture2DArray Build(string name, Dictionary<string, Index> parsed)
    {
        if (!parsed.TryGetValue(name, out var index))
            throw new KeyNotFoundException($"{name}.index.txt yok");

        string blob = Path.Combine(SourceFolder, name + ".bytes");
        byte[] data = File.ReadAllBytes(blob);

        int sliceBytes = index.Width * index.Height * 2;
        long expected = (long)sliceBytes * index.Slices;
        if (data.LongLength != expected)
            throw new IOException(
                $"{name}.bytes {data.LongLength} bayt, {expected} bekleniyordu " +
                $"({index.Width}×{index.Height}×{index.Slices}, R16F)");

        var array = new Texture2DArray(index.Width, index.Height, index.Slices,
                                       TextureFormat.RHalf, false, true)
        {
            name = name,
            // İz DİKEY tekrar etmiyor: uçları kırpılmış bir doku. Yatayda da
            // tekrar yok — kenarın ötesi hava.
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int slice = 0; slice < index.Slices; slice++)
        {
            var span = new byte[sliceBytes];
            System.Array.Copy(data, (long)slice * sliceBytes, span, 0, sliceBytes);
            array.SetPixelData(span, 0, slice);
        }

        array.Apply(false, true);
        return array;
    }

    static void Report(RainStreakDatabase db)
    {
        long bytes = 0;
        int missing = 0;

        foreach (var angle in db.Angles)
        {
            foreach (var t in angle.Point.Concat(angle.Ambient))
                bytes += (long)t.width * t.height * t.depth * 2;
            missing += angle.Present.Count(p => p == 0);
        }

        var head = db.Angles[0];
        Debug.Log(
            $"İz veritabanı kuruldu: {db.Angles.Length} kamera açısı × "
            + $"{db.Sizes.Length} çözünürlük ({string.Join(", ", db.Sizes)} px)\n"
            + $"  eksen: v {db.Vertical.Length} × h {db.Horizontal.Length} × osc 10\n"
            + $"  dcam0 en yüksek seviye: {head.Point.Last().width}×{head.Point.Last().height}"
            + $" × {head.Point.Last().depth} dilim\n"
            + $"  eksik kombinasyon: {missing} (uç dikey açılarda iz dejenere, spec §5.4.5)\n"
            + $"  bellek: {bytes / 1048576f:F1} MB");
    }

    /// `.index.txt` — paketleyicinin yazdığı boyut ve eksen kaydı.
    class Index
    {
        public int Width, Height, Slices, Dcam;
        public int[] Vertical, Horizontal;
        public byte[] Present;

        public static Index Read(string path)
        {
            var index = new Index();
            string name = Path.GetFileName(path);

            // dcam kimliği dosya adında: <tür>_size<N>_dcam<NN>.index.txt
            int mark = name.IndexOf("_dcam", System.StringComparison.Ordinal);
            if (mark < 0) throw new IOException($"{name}: dosya adında _dcam yok");
            index.Dcam = int.Parse(name.Substring(mark + 5, 2), CultureInfo.InvariantCulture);

            foreach (string line in File.ReadAllLines(path))
            {
                var parts = line.Split(' ');
                switch (parts[0])
                {
                    case "width": index.Width = int.Parse(parts[1]); break;
                    case "height": index.Height = int.Parse(parts[1]); break;
                    case "slices": index.Slices = int.Parse(parts[1]); break;
                    case "format":
                        if (parts[1] != "R16F")
                            throw new IOException($"{name}: format {parts[1]}, R16F bekleniyor");
                        break;
                    case "axis":
                        var values = parts.Skip(2).Select(int.Parse).ToArray();
                        if (parts[1] == "v") index.Vertical = values;
                        else if (parts[1] == "h") index.Horizontal = values;
                        break;
                    case "present":
                        index.Present = parts[1].Select(c => (byte)(c == '1' ? 1 : 0)).ToArray();
                        break;
                }
            }

            return index;
        }
    }
}
