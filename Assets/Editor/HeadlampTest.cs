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
                   && settings.colorTemperatureKelvin <= 4500f;
        bool hierarchy = controller != null && camera != null && mount != null
                      && mount.parent == camera.transform
                      && hotspot != null && hotspot.transform.parent == mount
                      && spill != null && spill.transform.parent == mount;
        bool optics = hotspot != null && spill != null
                   && hotspot.type == LightType.Spot && spill.type == LightType.Spot
                   && hotspot.lightUnit == LightUnit.Lumen && spill.lightUnit == LightUnit.Lumen
                   && !hotspot.useColorTemperature && !spill.useColorTemperature
                   && hotspot.color.b < hotspot.color.r && spill.color.b < spill.color.r
                   && hotspot.shadows == LightShadows.Soft
                   && spill.shadows == LightShadows.None;

        controller?.SetOn(true);
        bool switchOn = hotspot != null && spill != null
                     && hotspot.enabled && spill.enabled
                     && settings != null
                     && Mathf.Approximately(hotspot.intensity, settings.hotspotLumens)
                     && Mathf.Approximately(spill.intensity, settings.spillLumens);
        controller?.SetOn(false);
        bool switching = switchOn && hotspot != null && spill != null
                      && !hotspot.enabled && !spill.enabled
                      && Mathf.Approximately(hotspot.intensity, 0f)
                      && Mathf.Approximately(spill.intensity, 0f);

        report.AppendLine($"  [{Mark(tuning)}] output is restrained and beam angles form hotspot plus spill");
        report.AppendLine($"  [{Mark(hierarchy)}] mount inherits the rendered camera's head and gait motion");
        report.AppendLine($"  [{Mark(optics)}] physical lumen units, explicit warm LED tint and one soft shadow cone are active");
        report.AppendLine($"  [{Mark(switching)}] LED output switches fully within the input frame");
        ok = tuning && hierarchy && optics && switching;
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static string Mark(bool value) => value ? "+" : "-";
}
