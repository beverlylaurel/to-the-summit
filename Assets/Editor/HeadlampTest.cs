using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class HeadlampTest
{
    const string SettingsPath = "Assets/Settings/HeadlampSettings.asset";

    [MenuItem("To The Summit/Player/Headlamp Test", false, 61)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder("# Headlamp Test\n");
        HeadlampSettings settings = AssetDatabase.LoadAssetAtPath<HeadlampSettings>(SettingsPath);
        HeadlampController controller = Object.FindAnyObjectByType<HeadlampController>(
            FindObjectsInactive.Include);
        Camera camera = Camera.main;

        SerializedObject serialized = controller != null ? new SerializedObject(controller) : null;
        Transform mount = serialized?.FindProperty("mount").objectReferenceValue as Transform;
        Light hotspot = serialized?.FindProperty("hotspot").objectReferenceValue as Light;
        Light spill = serialized?.FindProperty("spill").objectReferenceValue as Light;

        bool tuning = settings != null
                   && settings.hotspotLumens + settings.spillLumens <= 500f
                   && settings.hotspotOuterAngle < settings.spillOuterAngle
                   && settings.hotspotInnerAngle < settings.hotspotOuterAngle
                   && settings.spillInnerAngle < settings.spillOuterAngle
                   && settings.switchResponseSeconds <= 0.1f;
        bool hierarchy = controller != null && camera != null && mount != null
                      && mount.parent == camera.transform
                      && hotspot != null && hotspot.transform.parent == mount
                      && spill != null && spill.transform.parent == mount;
        bool optics = hotspot != null && spill != null
                   && hotspot.type == LightType.Spot && spill.type == LightType.Spot
                   && hotspot.lightUnit == LightUnit.Lumen && spill.lightUnit == LightUnit.Lumen
                   && hotspot.useColorTemperature && spill.useColorTemperature
                   && hotspot.shadows == LightShadows.Soft
                   && spill.shadows == LightShadows.None;

        MethodInfo step = typeof(HeadlampController).GetMethod(
            "StepLevel", BindingFlags.Static | BindingFlags.NonPublic);
        float sixty = 0f;
        float thirty = 0f;
        if (step != null && settings != null)
        {
            for (int i = 0; i < 60; i++)
                sixty = (float)step.Invoke(null,
                    new object[] { sixty, 1f, settings.switchResponseSeconds, 1f / 60f });
            for (int i = 0; i < 30; i++)
                thirty = (float)step.Invoke(null,
                    new object[] { thirty, 1f, settings.switchResponseSeconds, 1f / 30f });
        }
        bool response = step != null && sixty > 0.999f && Mathf.Abs(sixty - thirty) < 0.0001f;

        report.AppendLine($"  [{Mark(tuning)}] output is restrained and beam angles form hotspot plus spill");
        report.AppendLine($"  [{Mark(hierarchy)}] mount inherits the rendered camera's head and gait motion");
        report.AppendLine($"  [{Mark(optics)}] physical lumen units, color temperature and one soft shadow cone are active");
        report.AppendLine($"  [{Mark(response)}] switch response is smooth and frame-rate independent");
        ok = tuning && hierarchy && optics && response;
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static string Mark(bool value) => value ? "+" : "-";
}
