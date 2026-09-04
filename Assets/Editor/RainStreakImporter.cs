using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// IMPORTS GARG-NAYAR STREAK DATABASE INTO UNITY — `rain-spec.md` §10.2 Phase 1.
///
/// Source consists of 15,000 16-bit single-channel PNGs stored outside the project
/// (same rule as DEM data: raw data does not enter repository). `Tools/rain/pack_streaks.py`
/// normalizes and packs them into float16 blobs; this tool converts blobs into `Texture2DArray`.
///
/// WHY UNITY CANNOT READ PNGs DIRECTLY: `ImageConversion` downsamples 16-bit grayscale PNGs to 8-bit.
/// Spec §5.4.4 warns of this exact problem — each texture has its own max multiplier varying
/// between 0.002 and 0.65; precision loss completely erases faint streaks, rendering "all streaks equally bright".
public static class RainStreakImporter
{
    /// Packed blobs reside IN THE PROJECT TREE BUT OUTSIDE `Assets/`. If inside,
    /// Unity would import them as `TextAsset` — adding 67 MB to editor and build, whereas
    /// they are only read during import. Raw PNGs never enter repository.
    const string SourceFolder = "Tools/rain/packed";
    const string AssetPath = "Assets/Rain/RainStreakDatabase.asset";

    [MenuItem("To The Summit/Rain/Set Up Streak Database", false, 40)]
    static void Import() => Rebuild();

    /// Rebuilds the database in place so scene references keep the same asset GUID.
    public static void Rebuild()
    {
        var indexFiles = Directory.GetFiles(SourceFolder, "*.index.txt");
        if (indexFiles.Length == 0)
            throw new FileNotFoundException(
                $"No .index.txt found in {SourceFolder}. Run Tools/rain/pack_streaks.py first.");

        var db = AssetDatabase.LoadAssetAtPath<RainStreakDatabase>(AssetPath);
        if (db == null)
            db = ScriptableObject.CreateInstance<RainStreakDatabase>();
        else
        {
            foreach (var child in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                if (child != db) Object.DestroyImmediate(child, true);
        }
        var sizes = new SortedSet<int>();
        var dcams = new SortedSet<int>();

        // Gather axes and levels first: determine available resolutions and camera angles.
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
                Mask = new Texture2DArray[db.Sizes.Length],
            };

            for (int s = 0; s < db.Sizes.Length; s++)
            {
                int size = db.Sizes[s];
                angle.Point[s] = Build($"point_size{size}_dcam{dcam:00}", parsed);
                angle.Ambient[s] = Build($"env_size{size}_dcam{dcam:00}", parsed);
                angle.Mask[s] = BuildMask($"env_size{size}_dcam{dcam:00}", parsed);
            }

            // Occupancy table depends only on (dcam, v, h, osc) — resolution-independent, sampled once.
            angle.Present = parsed[$"point_size{db.Sizes[0]}_dcam{dcam:00}"].Present;
            angles.Add(angle);
        }

        db.Angles = angles.ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        if (!AssetDatabase.Contains(db)) AssetDatabase.CreateAsset(db, AssetPath);

        foreach (var angle in db.Angles)
        {
            foreach (var t in angle.Point) AssetDatabase.AddObjectToAsset(t, db);
            foreach (var t in angle.Ambient) AssetDatabase.AddObjectToAsset(t, db);
            foreach (var t in angle.Mask) AssetDatabase.AddObjectToAsset(t, db);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Report(db);
        Selection.activeObject = db;
    }

    /// Blob → `Texture2DArray`. Blob is float16 ordered slice by slice; `SetPixelData`
    /// ingests raw bytes directly without conversion.
    static Texture2DArray Build(string name, Dictionary<string, Index> parsed)
    {
        if (!parsed.TryGetValue(name, out var index))
            throw new KeyNotFoundException($"Missing {name}.index.txt");

        string blob = Path.Combine(SourceFolder, name + ".bytes");
        byte[] data = File.ReadAllBytes(blob);

        int sliceBytes = index.Width * index.Height * 2;
        long expected = (long)sliceBytes * index.Slices;
        if (data.LongLength != expected)
            throw new IOException(
                $"{name}.bytes has {data.LongLength} bytes, expected {expected} " +
                $"({index.Width}x{index.Height}x{index.Slices}, R16F)");

        var array = new Texture2DArray(index.Width, index.Height, index.Slices,
                                       TextureFormat.RHalf, false, true)
        {
            name = name,
            // Streak does not repeat vertically: texture has clipped endpoints.
            // No horizontal repeat either — beyond edge is empty air.
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

    /// Recovers the normalized streak image from the packed ambient radiance. Every packed
    /// slice is `normalized PNG * scalar`; dividing by that slice's maximum removes illumination
    /// while preserving the geometric footprint. This keeps the import reproducible without the
    /// external 15,000-file source database.
    static Texture2DArray BuildMask(string name, Dictionary<string, Index> parsed)
    {
        if (!parsed.TryGetValue(name, out var index))
            throw new KeyNotFoundException($"Missing {name}.index.txt");

        byte[] source = File.ReadAllBytes(Path.Combine(SourceFolder, name + ".bytes"));
        int pixelsPerSlice = index.Width * index.Height;
        int sliceBytes = pixelsPerSlice * 2;
        var normalized = new byte[source.Length];

        for (int slice = 0; slice < index.Slices; slice++)
        {
            int start = slice * sliceBytes;
            float maximum = 0f;

            for (int pixel = 0; pixel < pixelsPerSlice; pixel++)
            {
                int offset = start + pixel * 2;
                ushort bits = (ushort)(source[offset] | source[offset + 1] << 8);
                maximum = Mathf.Max(maximum, Mathf.HalfToFloat(bits));
            }

            if (maximum <= 1e-8f) continue;

            for (int pixel = 0; pixel < pixelsPerSlice; pixel++)
            {
                int offset = start + pixel * 2;
                ushort bits = (ushort)(source[offset] | source[offset + 1] << 8);
                ushort mask = Mathf.FloatToHalf(Mathf.Clamp01(Mathf.HalfToFloat(bits) / maximum));
                normalized[offset] = (byte)(mask & 0xff);
                normalized[offset + 1] = (byte)(mask >> 8);
            }
        }

        var array = new Texture2DArray(index.Width, index.Height, index.Slices,
                                       TextureFormat.RHalf, false, true)
        {
            name = name.Replace("env_", "mask_"),
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int slice = 0; slice < index.Slices; slice++)
        {
            var span = new byte[sliceBytes];
            System.Array.Copy(normalized, slice * sliceBytes, span, 0, sliceBytes);
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
            foreach (var t in angle.Point.Concat(angle.Ambient).Concat(angle.Mask))
                bytes += (long)t.width * t.height * t.depth * 2;
            missing += angle.Present.Count(p => p == 0);
        }

        var head = db.Angles[0];
        Debug.Log(
            $"Streak database built: {db.Angles.Length} camera angle(s) x "
            + $"{db.Sizes.Length} resolution(s) ({string.Join(", ", db.Sizes)} px)\n"
            + $"  axes: v {db.Vertical.Length} x h {db.Horizontal.Length} x osc 10\n"
            + $"  dcam0 top level: {head.Point.Last().width}x{head.Point.Last().height}"
            + $" x {head.Point.Last().depth} slices\n"
            + $"  missing combinations: {missing} (streak degenerates at extreme vertical angles, spec §5.4.5)\n"
            + $"  memory: {bytes / 1048576f:F1} MB");
    }

    /// `.index.txt` — dimensions and axis metadata recorded by packer.
    class Index
    {
        public int Width, Height, Slices, Dcam;
        public int[] Vertical, Horizontal;
        public byte[] Present;

        public static Index Read(string path)
        {
            var index = new Index();
            string name = Path.GetFileName(path);

            // dcam ID in filename: <type>_size<N>_dcam<NN>.index.txt
            int mark = name.IndexOf("_dcam", System.StringComparison.Ordinal);
            if (mark < 0) throw new IOException($"{name}: missing _dcam in filename");
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
                            throw new IOException($"{name}: format {parts[1]}, expected R16F");
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
