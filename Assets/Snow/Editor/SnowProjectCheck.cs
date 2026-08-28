// Verifies required project settings and scene preconditions for the snow subsystem (spec §1.1, §1.2).
// Invoked by: Menu — To The Summit/Snow/Project Check.

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SnowProjectCheck
{
    public const string DeformerLayer = "SnowDeformer";
    public const string OccluderLayer = "SnowOccluder";

    [MenuItem("To The Summit/Snow/Project Check", false, 48)]
    static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var r = new StringBuilder(4096);
        r.AppendLine("# Snow Subsystem — Project Check");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        bool blocking = false;

        if (PlayerSettings.colorSpace == ColorSpace.Linear)
            Line(r, true, "Color Space", "Linear");
        else
            Line(r, false, "Color Space", PlayerSettings.colorSpace +
                " — Snow shading requires Linear color space. UNMODIFIED.");

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            Line(r, true, "Render pipeline", "URP (" + urp.name + ")");

            if (urp.supportsCameraDepthTexture)
                Line(r, true, "Depth Texture", "Enabled");
            else
                Line(r, false, "Depth Texture", "DISABLED — Required for soft particles. Enable in URP asset. UNMODIFIED.");
        }
        else
        {
            Line(r, false, "Render pipeline", "NOT URP — Snow subsystem requires URP.");
            blocking = true;
        }

        if (SystemInfo.supportsComputeShaders)
            Line(r, true, "Compute shader", "Supported");
        else
        {
            Line(r, false, "Compute shader", "NOT SUPPORTED — Snow subsystem requires compute shaders.");
            blocking = true;
        }

        bool vfx = HasType("UnityEngine.VFX.VisualEffect");
        if (vfx)
            Line(r, true, "VFX Graph", "Installed");
        else
            Line(r, null, "VFX Graph", "NOT INSTALLED — Required for Phase 8 snowfall particles.");

        int free = CountFreeUserLayers();
        bool deformer = LayerMask.NameToLayer(DeformerLayer) >= 0;
        bool occluder = LayerMask.NameToLayer(OccluderLayer) >= 0;

        if (deformer && occluder)
            Line(r, true, "Layers", $"{DeformerLayer} and {OccluderLayer} configured");
        else if (free >= (deformer ? 0 : 1) + (occluder ? 0 : 1))
            Line(r, null, "Layers",
                $"Missing ({(deformer ? "" : DeformerLayer + " ")}{(occluder ? "" : OccluderLayer)}) — " +
                $"{free} available slots.");
        else
        {
            Line(r, false, "Layers", $"Available slots: {free}, required: 2. Free up layer slots.");
            blocking = true;
        }

        if (RenderSettings.ambientMode == AmbientMode.Skybox)
            Line(r, true, "Environment Lighting", "Skybox");
        else
            Line(r, null, "Environment Lighting", RenderSettings.ambientMode +
                " — Skybox ambient recommended for snow scenes. UNMODIFIED.");

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        if (terrains.Length == 0)
            Line(r, null, "Terrain", "Not found in scene — `groundSource = MeshBake` required.");
        else if (terrains.Length == 1)
            Line(r, true, "Terrain", "1 terrain found (" + terrains[0].name + ") — " +
                "`groundSource = UnityTerrain` operational.");
        else
        {
            Line(r, false, "Terrain", terrains.Length + " terrains found — Multi-terrain not supported.");
            blocking = true;
        }

        string parity = SnowConstantsTest.Run(out bool parityOk);
        Line(r, parityOk, "Constant parity", parity.Split('\n')[0]);
        if (!parityOk) r.AppendLine(parity);

        r.AppendLine();
        r.AppendLine("## Manual Setup Tasks");
        r.AppendLine("- Remove `" + DeformerLayer + "` from the main camera's Culling Mask.");
        r.AppendLine("- Ensure precipitation scripts disable rain while `SnowRuntimeState.IsSnowing` is true.");
        r.AppendLine("- Do not assign player characters to the `" + OccluderLayer + "` layer.");

        r.AppendLine();
        r.AppendLine(blocking
            ? "RESULT: BLOCKED — Resolve errors marked above before proceeding."
            : "RESULT: READY to proceed.");

        return r.ToString();
    }

    static void Line(StringBuilder r, bool? ok, string label, string value)
    {
        string mark = ok == null ? "!" : (ok.Value ? "+" : "-");
        r.AppendLine($"  [{mark}] {label.PadRight(22)} {value}");
    }

    static int CountFreeUserLayers()
    {
        int free = 0;
        for (int i = 8; i < 32; i++)
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) free++;
        return free;
    }

    static bool HasType(string fullName)
    {
        foreach (System.Reflection.Assembly a in
                 System.AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetType(fullName, false) != null) return true;

        return false;
    }
}
