using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Scene integration checks for the shared visual/audio precipitation source and wall clearance.
public static class EnvironmentIntegrationTest
{
    [MenuItem("To The Summit/Tests/Environment Integration")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Run this integration check in Play mode.");
        var rain = UnityEngine.Object.FindAnyObjectByType<PrecipitationRenderer>();
        var audio = UnityEngine.Object.FindAnyObjectByType<WeatherAudio>();
        if (rain == null || audio == null ||
            new SerializedObject(audio).FindProperty("precipitationSource").objectReferenceValue != rain)
            throw new InvalidOperationException("Weather audio is not bound to the active precipitation source.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var intensity = typeof(PrecipitationRenderer).GetField("precipitation", flags);
        var coverage = typeof(PrecipitationRenderer).GetField("localFactor", flags);
        var density = typeof(PrecipitationRenderer).GetField("density", flags);
        object oldIntensity = intensity.GetValue(rain), oldCoverage = coverage.GetValue(rain), oldDensity = density.GetValue(rain);
        try
        {
            intensity.SetValue(rain, 0.6f); density.SetValue(rain, 0.6f); coverage.SetValue(rain, 0f);
            Check(Mathf.Approximately(rain.LocalRainIntensity, 0f), "cloudless column is silent at 0.60 global rain");
            coverage.SetValue(rain, 0.5f);
            Check(Mathf.Approximately(rain.LocalRainIntensity, 0.3f), "half-covered column supplies local rain");
            density.SetValue(rain, 0.00001f);
            Check(Mathf.Approximately(rain.LocalRainIntensity, 0f), "culled drops do not drive rain audio");
            density.SetValue(rain, 0.6f); intensity.SetValue(rain, 0f);
            Check(Mathf.Approximately(rain.LocalRainIntensity, 0f), "snow-only phase has no rain audio");
        }
        finally
        {
            intensity.SetValue(rain, oldIntensity); coverage.SetValue(rain, oldCoverage); density.SetValue(rain, oldDensity);
        }
        var camera = Camera.main;
        var controller = camera.GetComponentInParent<CharacterController>();
        float eyeY = controller.transform.InverseTransformPoint(camera.transform.position).y;
        float capCenter = controller.center.y + controller.height * 0.5f - controller.radius;
        float capOffset = Mathf.Max(0f, eyeY - capCenter);
        float clearance = Mathf.Sqrt(Mathf.Max(0f, controller.radius * controller.radius - capOffset * capOffset)) - controller.skinWidth;
        float halfY = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float nearCorner = Mathf.Sqrt(camera.nearClipPlane * camera.nearClipPlane + halfY * halfY * (1f + camera.aspect * camera.aspect));
        Check(nearCorner < clearance, "near-plane corners fit inside standing capsule clearance");
        Debug.Log("Environment integration: PASSED");
    }

    static void Check(bool passed, string description)
    {
        if (!passed) throw new InvalidOperationException(description);
        Debug.Log("[+] " + description);
    }
}
