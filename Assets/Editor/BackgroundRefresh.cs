using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

/// REFRESHES EVEN WHEN UNITY IS IN THE BACKGROUND. Unity only scans for file changes
/// when the window gains focus; for externally written code, clicking into Unity or
/// pressing Ctrl+R was required every time.
///
/// A trigger file's timestamp is monitored: the moment it changes, import and
/// compilation are requested. Watching a single file instead of continuously scanning the
/// filesystem eliminates scanning overhead in large projects.
///
/// Untouched during Play mode and active compilation: initiating a reload mid-run
/// interrupts gameplay and corrupts incomplete compilation.
[InitializeOnLoad]
public static class BackgroundRefresh
{
    const string TriggerPath = "Logs/refresh.trigger";

    /// Interval between checks. Reading a file timestamp once per second is negligibly cheap;
    /// reading every frame is unnecessary.
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
