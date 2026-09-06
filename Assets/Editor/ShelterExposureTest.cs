using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ShelterExposureTest
{
    [MenuItem("To The Summit/Weather/Shelter Exposure Test", false, 44)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder(2048);
        report.AppendLine("# Shelter Exposure Test");
        var root = new GameObject("ZZ_ShelterTest");
        var listener = new GameObject("ZZ_ShelterListener");
        listener.transform.position = new Vector3(0f, 1.65f, 0f);
        var sensor = root.AddComponent<ShelterExposure>();
        sensor.Bind(listener.transform);

        try
        {
            // Four solid walls and roof: floor is intentionally absent because the sensor must
            // answer from overhead and openings, not from a trigger volume.
            GameObject roof = Box(root.transform, "Roof", new Vector3(0, 3, 0), new Vector3(6, .25f, 6));
            Box(root.transform, "North", new Vector3(0, 1.5f, 3), new Vector3(6, 3, .25f));
            Box(root.transform, "South", new Vector3(0, 1.5f, -3), new Vector3(6, 3, .25f));
            GameObject east = Box(root.transform, "East", new Vector3(3, 1.5f, 0), new Vector3(.25f, 3, 6));
            Box(root.transform, "West", new Vector3(-3, 1.5f, 0), new Vector3(.25f, 3, 6));
            Physics.SyncTransforms();
            sensor.EditorSampleNow();

            bool sealedInterior = sensor.IsIndoors && sensor.Opening01 < 0.01f
                               && sensor.PrecipitationExposure < 0.05f
                               && sensor.WindTransmission < 0.05f
                               && sensor.RainTransmission > 0.05f
                               && sensor.LightningDirectTransmission < 0.05f;
            report.AppendLine("  [" + M(sealedInterior) + "] sealed room: cover="
                + sensor.Cover01.ToString("F2") + ", opening=" + sensor.Opening01.ToString("F2")
                + ", dry radius=" + sensor.DryRadius.ToString("F2") + " m");

            Object.DestroyImmediate(east);
            Physics.SyncTransforms();
            sensor.EditorSampleNow();
            bool openDoor = sensor.IsIndoors && sensor.Opening01 > 0.05f
                         && sensor.RainTransmission > 0.10f
                         && sensor.RainTransmission < 0.55f
                         && sensor.WindTransmission < 0.25f;
            report.AppendLine("  [" + M(openDoor) + "] open side admits restrained sound: opening="
                + sensor.Opening01.ToString("F2") + ", rain=" + sensor.RainTransmission.ToString("F2")
                + ", wind=" + sensor.WindTransmission.ToString("F2"));

            Object.DestroyImmediate(roof);
            Physics.SyncTransforms();
            sensor.EditorSampleNow();
            bool outdoors = !sensor.IsIndoors && sensor.PrecipitationExposure > 0.95f
                         && sensor.RainTransmission > 0.95f && sensor.WindTransmission > 0.95f;
            report.AppendLine("  [" + M(outdoors) + "] no roof restores exterior weather");

            bool integrations = IntegrationContracts(report);
            ok = sealedInterior && openDoor && outdoors && integrations;
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(listener);
            Shader.SetGlobalVector("_ShelterCenterRadius", Vector4.zero);
            Shader.SetGlobalFloat("_ShelterVisualBlock", 0f);
        }

        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.AddComponent<BoxCollider>();
        return go;
    }

    static bool IntegrationContracts(StringBuilder report)
    {
        string rainShader = File.ReadAllText("Assets/Shaders/Precipitation.shader");
        string snowShader = File.ReadAllText("Assets/Snow/Shaders/SnowfallParticle.shader");
        string snowLayers = File.ReadAllText("Assets/Snow/Runtime/SnowfallLayers.cs");
        string audio = File.ReadAllText("Assets/Scripts/Weather/WeatherAudio.cs");
        string thunder = File.ReadAllText("Assets/Scripts/Weather/ThunderPlayer.cs");
        string temperature = File.ReadAllText("Assets/Scripts/Weather/TemperatureField.cs");

        bool visuals = rainShader.Contains("_ShelterCenterRadius")
                    && rainShader.Contains("shelterOutside")
                    && snowShader.Contains("_ShelterCenterRadius")
                    && snowLayers.Contains("shelter.PrecipitationExposure")
                    && snowLayers.Contains("nearLayer.Reinit()");
        bool acoustics = audio.Contains("shelter.RainTransmission")
                      && audio.Contains("shelter.WindTransmission")
                      && thunder.Contains("shelter.ThunderTransmission");
        bool body = temperature.Contains("shelter.WindTransmission");

        report.AppendLine("  [" + M(visuals) + "] rain and both snow render paths use shelter exposure");
        report.AppendLine("  [" + M(acoustics) + "] rain, wind and thunder use opening-aware acoustics");
        report.AppendLine("  [" + M(body) + "] felt temperature uses sheltered wind");
        return visuals && acoustics && body;
    }

    static string M(bool value) => value ? "+" : "-";
}
