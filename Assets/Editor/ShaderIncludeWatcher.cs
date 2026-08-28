using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Recompiles shaders containing `.hlsl` files when includes change.
///
/// WHY THIS EXISTS: Unity DOES NOT track which `.hlsl` files are included by a `.shader`.
/// When an include file changes, the shader remains stale and nothing changes on screen —
/// code is correct on disk, running version is obsolete. This is the exact same class of
/// silent staleness that caused issues repeatedly (SYMPTOMS.md: PNG cache, `.asset` runtime
/// copy, menu item triggering wrong action).
///
/// The symptom is always identical and misleading: measurement is correct, fix is correct,
/// screen does not change. Then the fix is presumed "ineffective" and reverted, missing the real cause.
///
/// Scope is kept narrow: only shaders in the same root folder are reimported.
/// Scanning the entire project would waste seconds on every include save.
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
            Debug.Log($"Include changed: {count} shader(s) recompiled "
                      + $"({string.Join(", ", touchedRoots)}).");
    }
}
