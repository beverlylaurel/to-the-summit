using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// `Tools/terrain/synth_l0.py` çıktısını `DivideTree` asset'ine çevirir.
///
/// Dağ pişmiş içerik: üretim editör dışında bir kez çalışıyor, bu araç yalnız içeri
/// alıyor. Çalışma zamanında ne bu araç ne de Python var.
public static class DivideTreeImporter
{
    const string SourcePath = "Assets/Terrain/DivideTree.txt";
    const string AssetPath = "Assets/Terrain/DivideTree.asset";

    [MenuItem("To The Summit/Arazi/Divide Tree'yi İçe Aktar", false, 10)]
    static void Import()
    {
        if (!File.Exists(SourcePath))
            throw new FileNotFoundException(
                $"Divide Tree kaynağı yok: {SourcePath}. Önce " +
                "`python Tools/terrain/synth_l0.py` ve `export_unity.py` çalıştırılır.");

        string[] lines = File.ReadAllLines(SourcePath);
        var tree = LoadOrCreate();
        Parse(lines, tree);

        EditorUtility.SetDirty(tree);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int summit = tree.SummitId;
        ToolLog.Write(
            $"Divide Tree içe alındı: {tree.peaks.Length} zirve, {tree.saddles.Length} boyun, " +
            $"tohum {tree.seed}, bölge {tree.regionSize / 1000f:F0} km. " +
            $"Zirve #{summit}: {tree.peaks[summit].elevation:F0} m, " +
            $"merkezden {Mathf.Sqrt(tree.peaks[summit].east * tree.peaks[summit].east + tree.peaks[summit].north * tree.peaks[summit].north):F0} m.");

        Verify(tree);
    }

    static DivideTree LoadOrCreate()
    {
        var existing = AssetDatabase.LoadAssetAtPath<DivideTree>(AssetPath);
        if (existing != null) return existing;

        var created = ScriptableObject.CreateInstance<DivideTree>();
        AssetDatabase.CreateAsset(created, AssetPath);
        return created;
    }

    /// SATIR SATIR, HATAYI YUTMADAN. Bozuk bir satır sessizce atlanırsa grafik eksik
    /// kalır ve kimlikler kayar — çapa mimarisinin dayandığı şey tam olarak o
    /// kimlikler. Beklenmeyen her şey fırlatılıyor.
    static void Parse(string[] lines, DivideTree tree)
    {
        var ci = CultureInfo.InvariantCulture;
        int i = 0;

        while (i < lines.Length && (lines[i].Length == 0 || lines[i][0] == '#')) i++;

        string Key(string expected)
        {
            string[] parts = lines[i].Split(' ');
            if (parts[0] != expected)
                throw new FormatException($"{SourcePath}:{i + 1} — '{expected}' bekleniyordu, '{parts[0]}' bulundu.");
            i++;
            return parts[1];
        }

        if (Key("format") != "1")
            throw new FormatException($"{SourcePath}: tanınmayan biçim sürümü.");

        tree.seed = int.Parse(Key("seed"), ci);
        tree.regionSize = float.Parse(Key("regionKm"), ci) * 1000f;
        tree.playSize = float.Parse(Key("playKm"), ci) * 1000f;
        tree.summitElevation = float.Parse(Key("summitM"), ci);
        tree.prominenceFloor = float.Parse(Key("promFloorM"), ci);
        tree.elevationScale = float.Parse(Key("elevScale"), ci);

        int peakCount = int.Parse(Key("peaks"), ci);
        tree.peaks = new DivideTree.Peak[peakCount];
        for (int p = 0; p < peakCount; p++, i++)
        {
            string[] f = lines[i].Split(' ');
            if (int.Parse(f[0], ci) != p)
                throw new FormatException($"{SourcePath}:{i + 1} — zirve kimliği sırayı takip etmiyor. " +
                                          "Kimlik = dizi indeksi olmak zorunda, içerik çapaları buna bağlı.");
            tree.peaks[p] = new DivideTree.Peak
            {
                east = float.Parse(f[1], ci),
                north = float.Parse(f[2], ci),
                elevation = float.Parse(f[3], ci),
                prominence = float.Parse(f[4], ci),
            };
        }

        int saddleCount = int.Parse(Key("saddles"), ci);
        tree.saddles = new DivideTree.Saddle[saddleCount];
        for (int s = 0; s < saddleCount; s++, i++)
        {
            string[] f = lines[i].Split(' ');
            if (int.Parse(f[0], ci) != s)
                throw new FormatException($"{SourcePath}:{i + 1} — boyun kimliği sırayı takip etmiyor.");
            tree.saddles[s] = new DivideTree.Saddle
            {
                east = float.Parse(f[1], ci),
                north = float.Parse(f[2], ci),
                elevation = float.Parse(f[3], ci),
                peakA = int.Parse(f[4], ci),
                peakB = int.Parse(f[5], ci),
            };
        }
    }

    /// İçe alma sonrası denetim. Bunlar üretimin sözleşmesi; biri bozulursa dağ
    /// yeniden üretilmeli, elle düzeltilmemeli.
    static void Verify(DivideTree tree)
    {
        if (tree.saddles.Length != tree.peaks.Length - 1)
            Debug.LogWarning($"Divide Tree AĞAÇ DEĞİL: {tree.peaks.Length} zirveye " +
                             $"{tree.saddles.Length} boyun düşüyor, {tree.peaks.Length - 1} olmalıydı. " +
                             "Bijektif zirve↔key saddle eşlemesi bozulmuş.");

        int summit = tree.SummitId;
        var top = tree.peaks[summit];
        float fromCenter = Mathf.Sqrt(top.east * top.east + top.north * top.north);
        if (fromCenter > 500f)
            Debug.LogWarning($"En yüksek zirve merkezde değil: {fromCenter:F0} m ötede. " +
                             "Sabit zirve tutmamış — DECISIONS.md 'L0 girdisi' okunmalı.");

        if (Mathf.Abs(top.elevation - tree.summitElevation) > 1f)
            Debug.LogWarning($"Zirve kotu başlıkla uyuşmuyor: {top.elevation:F0} m / {tree.summitElevation:F0} m.");

        float half = tree.regionSize * 0.5f;
        int outside = 0;
        for (int p = 0; p < tree.peaks.Length; p++)
        {
            var q = tree.peaks[p];
            if (Mathf.Abs(q.east) > half || Mathf.Abs(q.north) > half) outside++;
        }
        if (outside > 0)
            Debug.LogWarning($"{outside} zirve bölge sınırının dışında.");

        int inPlay = 0;
        for (int p = 0; p < tree.peaks.Length; p++)
            if (tree.InPlayArea(tree.peaks[p])) inPlay++;

        ToolLog.Write($"Denetim: oyun alanında {inPlay} zirve, bölge dışında {outside}, " +
                      $"prominence tabanı {tree.prominenceFloor:F0} m.");
    }
}
