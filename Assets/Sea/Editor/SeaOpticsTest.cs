// ROLE: guards the open-water colour ordering and stable horizon reflection limit.
// CALLED BY: menu - To The Summit/Sea/Test Optics.

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SeaOpticsTest
{
    const string SettingsPath = "Assets/Sea/Settings/SeaSettings.asset";
    const string ShaderPath = "Assets/Sea/Shaders/SeaLit.shader";
    const string TerrainSurfacePath = "Assets/Shaders/MountainSurface.hlsl";
    const string TerrainShaderPath = "Assets/Shaders/MountainSurface.shader";

    [MenuItem("To The Summit/Sea/Test Optics", false, 83)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        SeaSettings settings = AssetDatabase.LoadAssetAtPath<SeaSettings>(SettingsPath);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        string source = File.ReadAllText(ShaderPath);
        string terrainSource = File.ReadAllText(TerrainSurfacePath);
        string terrainShader = File.ReadAllText(TerrainShaderPath);

        bool settingsFound = settings != null;
        bool physicalExtinction = settingsFound
            && settings.extinctionRgb.x > settings.extinctionRgb.y
            && settings.extinctionRgb.y > settings.extinctionRgb.z
            && Approximately(settings.extinctionRgb, new Vector3(0.30f, 0.075f, 0.05f));
        bool blueUpwelling = settingsFound
            && settings.upwellingColor.b > settings.upwellingColor.g
            && settings.upwellingColor.g > settings.upwellingColor.r
            && Approximately(settings.upwellingColor, new Color(0.02f, 0.18f, 0.26f, 1f));
        bool horizonContract = source.Contains(
                "float3 rLookup = normalize(float3(R.x, max(R.y, 0.0), R.z));")
            && source.Contains("A RAY BELOW THE GEOMETRIC HORIZON HITS THE NEXT WATER FACET")
            && !source.Contains("skyRefl = lerp(upwelling, skyRefl")
            && source.Contains("SeaFarGeometryKeep");
        bool shoreContract = source.Contains("fwidth(edgeDepth) * SEA_SHORE_OPTICAL_MIN_PIXELS")
            && source.Contains("fwidth(thickness) * SEA_SHORE_CONTACT_PIXELS")
            && source.Contains("smoothstep(0.0, contactOpticalWidth, thickness)")
            && source.Contains("float contactWash = contactBand")
            && source.Contains("shorePresence")
            && terrainSource.Contains("fwidth(worldPos.y) * 10.0")
            && terrainSource.Contains("float swashEdgeOffset =")
            && terrainSource.Contains("fwidth(localWetHeight) * 10.0")
            && terrainSource.Contains("float waterlineContact = 0.0")
            && terrainSource.Contains("lace = max(lace, waterlineContact)")
            && terrainShader.Contains("half shoreContact = surface.shoreContact")
            && terrainShader.Contains("max(lit, shoreSky)");
        bool shaderClean = shader != null && shader.isSupported
            && ShaderUtil.GetShaderMessages(shader).Length == 0;

        ok = settingsFound && physicalExtinction && blueUpwelling
            && horizonContract && shoreContract && shaderClean;

        var report = new StringBuilder(512);
        report.AppendLine("# Sea Optics Test");
        report.AppendLine("  [" + Mark(settingsFound) + "] settings asset found");
        report.AppendLine("  [" + Mark(physicalExtinction) + "] extinction keeps R > G > B");
        report.AppendLine("  [" + Mark(blueUpwelling) + "] upwelling keeps B > G > R");
        report.AppendLine("  [" + Mark(horizonContract) + "] horizon uses the filtered sky limit without a dark branch");
        report.AppendLine("  [" + Mark(shoreContract) + "] bathymetry and visible terrain contact use separate hand-off widths");
        report.AppendLine("  [" + Mark(shaderClean) + "] shader is supported and has no import messages");
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000001f;

    static bool Approximately(Color a, Color b)
    {
        Vector4 delta = (Vector4)a - (Vector4)b;
        return delta.sqrMagnitude < 0.000001f;
    }

    static string Mark(bool value) => value ? "+" : "-";
}
