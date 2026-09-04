using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class VintagePhotoModeTest
{
    const string ProfilePath = "Assets/Settings/VintageDslrProfile.asset";
    const string ShaderPath = "Assets/Shaders/VintagePhoto.shader";
    const string SourcePath = "Assets/Scripts/Photography/VintagePhotoMode.cs";

    [MenuItem("To The Summit/Photography/System Test", false, 70)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        report.AppendLine("# Vintage Photo Mode Test");

        VintageDslrProfile profile = AssetDatabase.LoadAssetAtPath<VintageDslrProfile>(ProfilePath);
        bool profileValid = profile != null
                         && profile.captureWidth == 1944 && profile.captureHeight == 1296
                         && profile.outputWidth == 3888 && profile.outputHeight == 2592
                         && Mathf.Abs(profile.viewfinderCoverage - 0.95f) < 0.001f;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        bool shaderValid = shader != null && shader.isSupported;
        bool shaderBlit = ShaderBlitTest(shader, out Color whiteResponse);
        string shaderSource = File.ReadAllText(ShaderPath);
        bool sensor = shaderSource.Contains("4095.0")
                   && shaderSource.Contains("shotSigma")
                   && shaderSource.Contains("fixedPattern")
                   && shaderSource.Contains("MeterLogLuminance");

        string source = File.ReadAllText(SourcePath);
        bool controls = source.Contains("digit4Key.wasPressedThisFrame")
                     && source.Contains("mouse.rightButton.wasPressedThisFrame")
                     && source.Contains("mouse.leftButton.wasPressedThisFrame")
                     && source.Contains("keyboard.gKey.wasPressedThisFrame");
        bool noScreenshot = !source.Contains("ScreenCapture.CaptureScreenshot")
                         && source.Contains("data.renderPostProcessing = false")
                         && source.Contains("GraphicsFormat.R16G16B16A16_SFloat");
        bool persistence = source.Contains("ImageConversion.EncodeToJPG")
                        && source.Contains("WriteMetadata")
                        && source.Contains("VintagePhotoLibrary");

        VintagePhotoMode mode = Object.FindAnyObjectByType<VintagePhotoMode>(FindObjectsInactive.Include);
        bool scene = mode != null;

        report.AppendLine($"  [{Mark(profileValid)}] 1944x1296 capture, 3888x2592 output, 95% viewfinder");
        report.AppendLine($"  [{Mark(shaderValid)}] processing shader imported and supported");
        report.AppendLine($"  [{Mark(shaderBlit)}] Graphics.Blit source is bound: "
                        + $"{whiteResponse.r:F3}/{whiteResponse.g:F3}/{whiteResponse.b:F3}");
        report.AppendLine($"  [{Mark(sensor)}] metering, shot/read noise, fixed pattern and 12-bit quantisation");
        report.AppendLine($"  [{Mark(controls)}] 4/right-click/left-click/gallery controls");
        report.AppendLine($"  [{Mark(noScreenshot)}] scene-linear HDR capture bypasses gameplay post processing");
        report.AppendLine($"  [{Mark(persistence)}] JPEG and metadata are persisted");
        report.AppendLine($"  [{Mark(scene)}] photo mode is bound in Game scene");

        ok = profileValid && shaderValid && shaderBlit && sensor && controls
          && noScreenshot && persistence && scene;
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static string Mark(bool value) => value ? "+" : "-";

    static bool ShaderBlitTest(Shader shader, out Color response)
    {
        response = Color.clear;
        if (shader == null || !shader.isSupported) return false;

        var material = new Material(shader);
        material.SetFloat("_Exposure", 0.83f);
        material.SetFloat("_IsoScale", 2f);
        material.SetFloat("_FrameSeed", 12.3f);
        material.SetFloat("_VignetteStrength", 0.58f);
        material.SetFloat("_ChromaticAberration", 0.00115f);
        material.SetFloat("_Distortion", -0.018f);
        material.SetFloat("_Contrast", 0.35f);
        material.SetFloat("_Sharpen", 0.32f);
        material.SetFloat("_GrainStrength", 0.32f);
        material.SetFloat("_PurpleFringe", 0.22f);
        material.SetVector("_WhiteBalance", new Vector4(1.035f, 1f, 0.965f, 0f));

        var target = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        target.Create();
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(Texture2D.whiteTexture, target, material, 0);

        RenderTexture.active = target;
        var pixels = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
        pixels.ReadPixels(new Rect(0f, 0f, 16f, 16f), 0, 0, false);
        pixels.Apply(false, false);
        response = pixels.GetPixel(8, 8);
        RenderTexture.active = previous;

        Object.DestroyImmediate(pixels);
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(material);

        return response.r > 0.5f && response.g > 0.5f && response.b > 0.5f;
    }
}
