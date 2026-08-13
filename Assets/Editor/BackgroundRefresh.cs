using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

/// UNITY ARKA PLANDAYKEN DE YENİLER. Unity dosya değişikliğini ancak pencere odağı
/// aldığında tarıyor; dışarıdan yazılan kod için her seferinde Unity'ye tıklamak ya da
/// Ctrl+R'a basmak gerekiyordu.
///
/// Tetikleyici bir dosyanın zaman damgası izleniyor: değiştiği anda içe aktarma ve
/// derleme isteniyor. Dosya sistemini sürekli taramak yerine tek dosyaya bakmak, büyük
/// projede taramanın maliyetini ortadan kaldırıyor.
///
/// Play sırasında ve derleme sürerken dokunulmuyor: ortada yeniden yükleme başlatmak
/// oyunu kesiyor ve yarım kalmış derlemeyi bozuyor.
[InitializeOnLoad]
public static class BackgroundRefresh
{
    const string TriggerPath = "Logs/refresh.trigger";

    /// İki bakış arası. Saniyede bir dosya damgası okumak ölçülemeyecek kadar ucuz;
    /// her karede okumak gereksiz.
    const double Interval = 1.0;

    static DateTime stamp;
    static double next;

    static BackgroundRefresh()
    {
        stamp = Stamp();
        EditorApplication.update += Tick;
    }

    static DateTime Stamp() => File.Exists(TriggerPath)
        ? File.GetLastWriteTimeUtc(TriggerPath)
        : DateTime.MinValue;

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + Interval;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        DateTime current = Stamp();
        if (current == stamp) return;

        stamp = current;

        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
    }
}
