using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// Adds the `URP_PBSKY` define to the project.
///
/// The package's own asmdef generates this define ONLY for the `PBSkyURP` assembly. Our
/// cloud port and scene setup live in `Assembly-CSharp`; the `#if URP_PBSKY` blocks there
/// remained inactive even when the package was installed — all sky integration (aerial
/// perspective for clouds, shared planetary radius, shared ambient probe) lives inside those blocks.
///
/// The define is added if the package exists and removed if it is missing, preventing compilation errors if the package is removed.
[InitializeOnLoad]
static class SkyPackageDefine
{
    const string Define = "URP_PBSKY";
    const string PackagePath = "Packages/com.jiaozi158.unity-physically-based-sky-urp/package.json";

    static SkyPackageDefine()
    {
        bool installed = System.IO.File.Exists(PackagePath);

        var target = NamedBuildTarget.FromBuildTargetGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        string current = PlayerSettings.GetScriptingDefineSymbols(target);
        var symbols = current.Split(';').Where(s => s.Length > 0).ToList();

        bool present = symbols.Contains(Define);
        if (present == installed) return;

        if (installed) symbols.Add(Define);
        else symbols.Remove(Define);

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
    }
}
