using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// `.hlsl` değişince onu içeren shader'ları yeniden derletir.
///
/// NEDEN VAR: Unity `.shader` dosyasının hangi `.hlsl`'leri içerdiğini takip ETMİYOR.
/// Include dosyası değiştiğinde shader bayat kalıyor ve ekranda hiçbir şey değişmiyor —
/// kod diskte doğru, çalışan sürüm eski. Bu, projenin tekrar tekrar yandığı sessiz
/// bayatlık sınıfının aynısı (`SYMPTOMS.md`: PNG önbelleği, `.asset` çalışma-zamanı
/// kopyası, menü düğmesinin yanlış işi yapması).
///
/// Belirti hep aynı ve aldatıcı: ölçüm doğru, düzeltme doğru, ekran değişmiyor. Sonra
/// düzeltme "işe yaramadı" sanılıp geri alınıyor ve gerçek sebep bir tur daha kaçıyor.
///
/// Kapsam dar tutuluyor: yalnız aynı kök klasördeki shader'lar yeniden içe aktarılıyor.
/// Tüm projeyi taramak her include kaydında saniyeler yakardı.
class ShaderIncludeWatcher : AssetPostprocessor
{
    static readonly string[] Roots =
    {
        "Assets/Shaders",
        "Assets/VolumetricClouds",
    };

    static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                       string[] moved, string[] movedFrom)
    {
        var touchedRoots = new HashSet<string>();

        foreach (string path in imported)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".hlsl" && ext != ".cginc") continue;

            foreach (string root in Roots)
                if (path.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                    touchedRoots.Add(root);
        }

        if (touchedRoots.Count == 0) return;

        int count = 0;
        foreach (string root in touchedRoots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { root }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                                          ImportAssetOptions.ForceUpdate);
                count++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ComputeShader", new[] { root }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                                          ImportAssetOptions.ForceUpdate);
                count++;
            }
        }

        if (count > 0)
            Debug.Log($"Include değişti: {count} shader yeniden derlendi "
                      + $"({string.Join(", ", touchedRoots)}).");
    }
}
