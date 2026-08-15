using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// `URP_PBSKY` tanımını projeye ekler.
///
/// Paketin kendi asmdef'i bu tanımı YALNIZ `PBSkyURP` derlemesi için üretiyor. Bizim
/// bulut portumuz ve sahne kurulumu `Assembly-CSharp`'ta duruyor; oradaki
/// `#if URP_PBSKY` blokları paket kurulu olsa bile kapalı kalıyordu — gökyüzüyle
/// entegrasyonun tamamı (buluta aerial perspective, ortak gezegen yarıçapı, ortak
/// ambient probe) o blokların içinde.
///
/// Tanım paket varsa ekleniyor, yoksa siliniyor: paket kaldırıldığında proje derlenmez
/// hâle gelmesin diye.
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
