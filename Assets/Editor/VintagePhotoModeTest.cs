using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class VintagePhotoModeTest
{
    const string ProfilePath = "Assets/Settings/VintageDslrProfile.asset";
    const string ShaderPath = "Assets/Shaders/VintagePhoto.shader";
    const string SourcePath = "Assets/Scripts/Photography/VintagePhotoMode.cs";
    const string RegularFontPath = "Assets/UI/Fonts/Inconsolata/Inconsolata-Regular.ttf";
    const string MediumFontPath = "Assets/UI/Fonts/Inconsolata/Inconsolata-Medium.ttf";
    const string SemiboldFontPath = "Assets/UI/Fonts/Inconsolata/Inconsolata-SemiBold.ttf";
    const string IconSetPath = "Assets/UI/Icons/ThinTriple/ThinTripleIconSet.asset";
    const string HudSourcePath = "Assets/Scripts/Photography/UI/VintagePhotoHud.cs";
    const string HeldSystemSourcePath = "Assets/Scripts/Items/HeldItemSystem.cs";
    const string HeldHudSourcePath = "Assets/Scripts/Items/UI/HeldItemHud.cs";
    const string IconSourcePath = "Assets/Scripts/UI/Icons/ThinTripleIconSet.cs";
    const string RainGlassSourcePath = "Assets/Scripts/UI/Style/RainGlassUi.cs";

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
                         && profile.previewWidth >= 648 && profile.meterIntervalSeconds > 0f;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        bool shaderValid = shader != null && shader.isSupported;
        bool shaderBlit = ShaderBlitTest(shader, out Color whiteResponse);
        string shaderSource = File.ReadAllText(ShaderPath);
        bool liveShader = LiveShaderTest(shader);
        bool sensor = shaderSource.Contains("4095.0")
                   && shaderSource.Contains("shotSigma")
                   && shaderSource.Contains("fixedPattern")
                   && shaderSource.Contains("MeterLogLuminance");

        string source = File.ReadAllText(SourcePath);
        bool controls = !source.Contains("digit4Key.wasPressedThisFrame")
                     && source.Contains("public sealed class VintagePhotoMode : EquippableItem")
                     && source.Contains("HeldItemInput.ForPointer")
                     && source.Contains("mouse.rightButton.wasPressedThisFrame")
                     && source.Contains("mouse.leftButton.wasPressedThisFrame")
                     && source.Contains("keyboard.gKey.wasPressedThisFrame")
                     && source.Contains("Time.frameCount == sharedActionFrame");
        bool noScreenshot = !source.Contains("ScreenCapture.CaptureScreenshot")
                         && source.Contains("data.renderPostProcessing = false")
                         && source.Contains("GraphicsFormat.R16G16B16A16_SFloat");
        bool persistence = source.Contains("ImageConversion.EncodeToJPG")
                        && source.Contains("WriteMetadata")
                        && source.Contains("VintagePhotoLibrary");

        Font regularFont = AssetDatabase.LoadAssetAtPath<Font>(RegularFontPath);
        Font mediumFont = AssetDatabase.LoadAssetAtPath<Font>(MediumFontPath);
        Font semiboldFont = AssetDatabase.LoadAssetAtPath<Font>(SemiboldFontPath);
        ThinTripleIconSet iconSet = AssetDatabase.LoadAssetAtPath<ThinTripleIconSet>(IconSetPath);
        ThinTripleIconSet.Icon cameraIcon = iconSet != null
            ? iconSet.Get(ThinTripleIconId.Camera) : null;
        bool iconTiers = cameraIcon != null
                      && cameraIcon.small != null && cameraIcon.small.width == 20
                      && cameraIcon.medium != null && cameraIcon.medium.width == 32
                      && cameraIcon.large != null && cameraIcon.large.width == 48;
        string hudSource = File.ReadAllText(HudSourcePath);
        string heldSystemSource = File.ReadAllText(HeldSystemSourcePath);
        string heldHudSource = File.ReadAllText(HeldHudSourcePath);
        string iconSource = File.ReadAllText(IconSourcePath);
        string rainGlassSource = File.ReadAllText(RainGlassSourcePath);
        bool uiSource = source.Contains("VintagePhotoHud")
                     && hudSource.Contains("DrawCameraReadout")
                     && hudSource.Contains("DrawControls")
                     && hudSource.Contains("RainGlassUi.DrawSurface")
                     && !hudSource.Contains("DrawEquipped(int remaining)")
                     && heldHudSource.Contains("TransitionSeconds = 0.22f")
                     && heldHudSource.Contains("OffsetPixels = -4f")
                     && heldHudSource.Contains("IReadOnlyList<HeldItemAction> actions")
                     && heldSystemSource.Contains("activeItem.SharedActions")
                     && hudSource.Contains("ThinTripleIconId.MouseRight, \"KAPAT\"")
                     && !hudSource.Contains("A / D  GEZİN")
                     && iconSource.Contains("ThinTripleIconId")
                     && rainGlassSource.Contains("public static class RainGlassUi")
                     && rainGlassSource.Contains("BorderSoft")
                     && !source.Contains("DrawThinTripleIcon");

        VintagePhotoMode mode = Object.FindAnyObjectByType<VintagePhotoMode>(FindObjectsInactive.Include);
        var serializedMode = mode != null ? new SerializedObject(mode) : null;
        HeldItemSystem itemSystem = Object.FindAnyObjectByType<HeldItemSystem>(FindObjectsInactive.Include);
        var serializedItems = itemSystem != null ? new SerializedObject(itemSystem) : null;
        SerializedProperty registeredItems = serializedItems?.FindProperty("items");
        bool scene = serializedMode != null
                  && serializedMode.FindProperty("previewFeature").objectReferenceValue != null
                  && registeredItems != null && registeredItems.arraySize == 1
                  && registeredItems.GetArrayElementAtIndex(0).objectReferenceValue == mode;
        bool typography = regularFont != null && mediumFont != null && semiboldFont != null
                       && iconSet != null && iconTiers
                       && serializedMode != null
                       && serializedMode.FindProperty("regularFont").objectReferenceValue == regularFont
                       && serializedMode.FindProperty("mediumFont").objectReferenceValue == mediumFont
                       && serializedMode.FindProperty("semiboldFont").objectReferenceValue == semiboldFont
                       && serializedMode.FindProperty("iconSet").objectReferenceValue == iconSet;

        report.AppendLine($"  [{Mark(profileValid)}] 1944x1296 capture, 3888x2592 output, live metering profile");
        report.AppendLine($"  [{Mark(shaderValid)}] processing shader imported and supported");
        report.AppendLine($"  [{Mark(shaderBlit)}] Graphics.Blit source is bound: "
                        + $"{whiteResponse.r:F3}/{whiteResponse.g:F3}/{whiteResponse.b:F3}");
        report.AppendLine($"  [{Mark(sensor)}] metering, shot/read noise, fixed pattern and 12-bit quantisation");
        report.AppendLine($"  [{Mark(controls)}] common equip plus camera modal controls");
        report.AppendLine($"  [{Mark(noScreenshot)}] scene-linear HDR capture bypasses gameplay post processing");
        report.AppendLine($"  [{Mark(persistence)}] JPEG and metadata are persisted");
        report.AppendLine($"  [{Mark(scene)}] photo mode is registered in the common item system");
        report.AppendLine($"  [{Mark(typography)}] Inconsolata and native 20/32/48 px icon tiers are bound");
        report.AppendLine($"  [{Mark(uiSource)}] camera HUD uses shared Rain Glass and icon layers");

        report.AppendLine($"  [{Mark(liveShader)}] linear meter, live exposure and focus zero/active controls");

        ok = profileValid && shaderValid && shaderBlit && sensor && controls
          && noScreenshot && persistence && scene && typography && uiSource && liveShader;
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
    static bool LiveShaderTest(Shader shader)
    {
        if (shader == null || !shader.isSupported) return false;
        var material = new Material(shader);
        var source = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        var target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBFloat,
            RenderTextureReadWrite.Linear);
        var read = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            var colors = new Color[64 * 64];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.25f, 0.25f, 0.25f, 1f);
            source.SetPixels(colors);
            source.Apply();
            Graphics.Blit(source, target, material, 1);
            ReadTarget(target, read);
            bool meter = Mathf.Abs(read.GetPixel(32, 32).r + 2f) < 0.002f;

            material.SetVector("_WhiteBalance", Vector4.one);
            material.SetFloat("_IsoScale", 1f);
            material.SetFloat("_Exposure", 0.5f);
            Graphics.Blit(source, target, material, 0);
            ReadTarget(target, read);
            float low = read.GetPixel(32, 32).r;
            material.SetFloat("_Exposure", 1f);
            Graphics.Blit(source, target, material, 0);
            ReadTarget(target, read);
            bool exposure = read.GetPixel(32, 32).r > low * 1.5f;

            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                colors[y * 64 + x] = (x / 4) % 2 == 0 ? Color.white : Color.black;
            source.SetPixels(colors);
            source.Apply();
            material.SetVector("_FocusStep", Vector4.zero);
            Graphics.Blit(source, target, material, 2);
            ReadTarget(target, read);
            bool clear = Mathf.Abs(read.GetPixel(32, 32).r - 1f) < 0.002f
                      && read.GetPixel(36, 32).r < 0.002f;
            material.SetVector("_FocusStep", new Vector4(8f / 64f, 0f, 0f, 0f));
            Graphics.Blit(source, target, material, 2);
            ReadTarget(target, read);
            float softened = read.GetPixel(32, 32).r;
            bool focus = softened > 0.15f && softened < 0.85f;
            Debug.Log($"Live shader controls: meter={meter}, exposure={exposure}, clear={clear}, focus={focus} ({softened:F3})");
            return meter && exposure && clear && focus;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(read);
            Object.DestroyImmediate(material);
        }
    }

    static void ReadTarget(RenderTexture target, Texture2D pixels)
    {
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
    }

}
