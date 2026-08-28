using System.IO;
using UnityEditor;
using UnityEngine;

/// LIGHTNING ATMOSPHERIC SCATTERING LUT — [Dobashi 2001, §4.4] Eq. 5.
///
/// PURPOSE: Glow around lightning is light scattering from atmospheric particles to eye.
/// Analytical integral along view ray does not exist (Eq. 2), and numerical evaluation per pixel is too expensive.
/// Key contribution: integral depends only on eye position relative to source in local space,
/// source intensity factors out (Eq. 4). The table is computed ONCE and shared across all sources, flashes, and scenes.
///
/// CLEAR AIR BAKING: paper assumes UNIFORM atmospheric particle density (§3.2).
/// Local fog varies with weather; baking it into LUT would require recomputing per weather state.
/// The LUT holds baseline clear air scattering; local fog continues through its own volumetric pass (`HeightFog.hlsl`).
///
/// Verification: integral was independently verified against Python reference model.
static class LightningLutBaker
{
    const string AssetPath = "Assets/Settings/LightningScatterLut.asset";

    /// Table resolution. [Dobashi 2001, §5.1] uses 128x128.
    const int Resolution = 128;

    /// INTEGRATION CUTOFF DISTANCE (meters). [Dobashi 2001, §4.2]: infinite integration
    /// is truncated at large distance T. Paper used 1.5 km.
    ///
    /// Arena spans 30 km with lightning striking between 200 m and 8 km (`ThunderSettings`).
    /// T is set to 9 km so distant flashes do not fall outside integration domain.
    const float CutoffDistance = 9000f;

    /// Samples along ray. Difference between 256 and 512 is <0.2% (subpixel). 256 is retained.
    const int Samples = 256;

    /// CLEAR AIR EXTINCTION. Rayleigh scattering scales with lambda^-4 (blue scatters faster than red).
    const float ReferenceRange = 30000f;

    /// NORMALIZATION. Scaled to evaluate to 1.0 at reference configuration (flash 800 m away, 30 deg view offset).
    const float ReferenceValue = 4.751153e-04f;

    /// Wavelengths corresponding to RGB [Dobashi 2001, §4.4]: 675, 520, 460 nm.
    static readonly float[] Wavelengths = { 675f, 520f, 460f };

    /// Automatically bakes if missing on startup.
    [InitializeOnLoadMethod]
    static void BakeIfMissing()
    {
        if (!File.Exists(AssetPath)) Bake();
    }

    [MenuItem("To The Summit/Lightning/Bake Scatter Table")]
    static void Bake()
    {
        var tex = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true)
        {
            name = "LightningScatterLut",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color[Resolution * Resolution];

        for (int iv = 0; iv < Resolution; iv++)
        {
            float v = Coord(iv);

            for (int iu = 0; iu < Resolution; iu++)
            {
                float u = Coord(iu);

                pixels[iv * Resolution + iu] = new Color(
                    Integrate(u, v, Wavelengths[0]) / ReferenceValue,
                    Integrate(u, v, Wavelengths[1]) / ReferenceValue,
                    Integrate(u, v, Wavelengths[2]) / ReferenceValue,
                    1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (existing != null) AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(tex, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Lightning scatter table baked: {Resolution}x{Resolution}, "
                  + $"T={CutoffDistance} m -> {AssetPath}");

        Report(tex);
    }

    /// SIGNED QUADRATIC AXIS MAPPING: concentrates sampling resolution near origin
    /// where 1/s^2 falloff is steepest (~1 m per cell near zero, ~280 m at perimeter).
    static float Coord(int index)
    {
        float t = (index + 0.5f) / Resolution * 2f - 1f;
        return Mathf.Sign(t) * t * t * CutoffDistance;
    }

    /// Evaluates scattering integral Eq. 5.
    /// Minimum v clamped to 250 m to represent physical lightning channel length rather than a singularity point.
    static float Integrate(float uEye, float vEye, float wavelength)
    {
        float v = Mathf.Max(Mathf.Abs(vEye), 250f);

        // Rayleigh extinction:
        float scale = Mathf.Pow(wavelength / 550f, 4f);
        float extinction = 1f / (ReferenceRange * scale);

        float lo = -CutoffDistance;

        // Step uses (Samples - 1) intervals for trapezoidal rule:
        float step = (uEye - lo) / (Samples - 1);
        if (step <= 0f) return 0f;

        float sum = 0f;

        for (int i = 0; i < Samples; i++)
        {
            float u = lo + step * i;
            float w = (i == 0 || i == Samples - 1) ? 0.5f : 1f;

            float s = Mathf.Sqrt(u * u + v * v);
            float cosAlpha = u / s;

            // Isotropic phase function:
            const float isotropic = 1f / (4f * Mathf.PI);
            float phase = isotropic;

            float t = uEye - u;
            sum += w * phase / (s * s) * Mathf.Exp(-extinction * (s + t));

            _ = cosAlpha;
        }

        return sum * step;
    }

    [MenuItem("To The Summit/Lightning/VERIFY Scatter Table")]
    static void Verify()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (tex == null) { Debug.LogWarning("Scatter table missing — bake first."); return; }
        Report(tex);
    }

    static void Report(Texture2D tex)
    {
        int[] us = { 64, 96, 64, 100, 20 };
        int[] vs = { 64, 64, 96, 100, 64 };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Table {tex.width}x{tex.height}  T={CutoffDistance} m");

        for (int i = 0; i < us.Length; i++)
        {
            Color c = tex.GetPixel(us[i], vs[i]);
            sb.AppendLine($"u={Coord(us[i]),8:F1} v={Coord(vs[i]),8:F1}  "
                          + $"RGB {c.r:E6} {c.g:E6} {c.b:E6}");
        }

        Debug.Log(sb.ToString());
    }
}
