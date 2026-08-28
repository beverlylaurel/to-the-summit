// Automatically handles scene wiring and dependency injection for the snow subsystem.
// Invoked by: Unity Editor (on domain reload and entering Play Mode).

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SnowAutoWire
{
    static readonly string[] Required =
    {
        "settings", "simCompute", "skyShader",
        "groundHeight", "environmentSource", "followTarget", "detailNormal",
    };

    static SnowAutoWire()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) Check();
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= Tick;
        Check();
    }

    static readonly string[] MenuRequired =
    {
        "temperature", "snowfall", "snowManager",
    };

    static readonly string[] CoverageRequired = { "settings" };

    static void Count(SerializedObject so, string[] names, ref int missing, ref string first)
    {
        foreach (string name in names)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null && prop.objectReferenceValue != null) continue;

            missing++;
            first ??= name;
        }
    }

    static void Check()
    {
        if (EditorApplication.isPlaying) return;

        var manager = Object.FindAnyObjectByType<SnowManager>();
        if (manager == null) return;

        int missing = 0;
        string first = null;

        Count(new SerializedObject(manager), Required, ref missing, ref first);

        var menu = Object.FindAnyObjectByType<DebugMenu>();
        if (menu != null) Count(new SerializedObject(menu), MenuRequired, ref missing, ref first);

        var coverage = Object.FindAnyObjectByType<SnowCoverageDriver>();
        if (coverage != null)
            Count(new SerializedObject(coverage), CoverageRequired, ref missing, ref first);

        if (missing == 0) return;

        SnowDebugWindow.SetupScene();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"[Snow] {missing} unassigned references detected (first: `{first}`), auto-wired scene setup.");
    }
}
