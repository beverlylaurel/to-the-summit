using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Deterministic guards for celestial motion and shared lighting contracts.</summary>
public static class LightingContinuityTest
{
    const string TimePath = "Assets/Scripts/Environment/TimeOfDay.cs";
    const string SkyPath = "Packages/com.jiaozi158.unity-physically-based-sky-urp/Runtime/PhysicallyBasedSkyURP.cs";
    const string RainPath = "Assets/Shaders/Precipitation.shader";
    const string SnowPath = "Assets/Snow/Shaders/SnowfallParticle.shader";
    const string SnowVfxPath = "Assets/Snow/VFX/VFX_Snowfall.vfx";
    const string SeaPath = "Assets/Sea/Shaders/SeaLit.shader";
    const string FlarePath = "Assets/Settings/SunLensFlare.asset";

    [MenuItem("To The Summit/Lighting/Continuity Test", false, 90)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder(2048);
        report.AppendLine("# Lighting Continuity Test");

        bool astronomy = Astronomy(report);
        bool sourceContract = SourceContract(report);
        bool receivers = ReceiverContract(report);
        bool lens = LensContract(report);
        ok = astronomy && sourceContract && receivers && lens;

        report.AppendLine();
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static bool Astronomy(StringBuilder report)
    {
        const int year = 2026;
        const float latitude = 39f, longitude = 35f, utc = 3f;
        float summerNoon = Elevation(CelestialEphemeris.Evaluate(
            year, 172, 0.5f, latitude, longitude, utc, 0f).SunDirection);
        float winterNoon = Elevation(CelestialEphemeris.Evaluate(
            year, 355, 0.5f, latitude, longitude, utc, 0f).SunDirection);

        float summerDay = DaylightHours(year, 172, latitude, longitude, utc);
        float winterDay = DaylightHours(year, 355, latitude, longitude, utc);

        float minPhase = 1f, maxPhase = 0f;
        float minSeparation = 180f, maxSeparation = 0f;
        for (int day = 220; day < 255; day++)
        {
            var sample = CelestialEphemeris.Evaluate(
                year, day, 0.5f, latitude, longitude, utc, 0f);
            minPhase = Mathf.Min(minPhase, sample.MoonIlluminatedFraction);
            maxPhase = Mathf.Max(maxPhase, sample.MoonIlluminatedFraction);
            float separation = Vector3.Angle(sample.SunDirection, sample.MoonDirection);
            minSeparation = Mathf.Min(minSeparation, separation);
            maxSeparation = Mathf.Max(maxSeparation, separation);
        }

        bool season = summerNoon > winterNoon + 35f && summerDay > winterDay + 4f;
        bool lunar = minPhase < 0.05f && maxPhase > 0.95f
                  && maxSeparation - minSeparation > 150f;

        report.AppendLine("  [" + Mark(season) + "] seasonal sun: noon "
                        + summerNoon.ToString("F1") + "/" + winterNoon.ToString("F1")
                        + " deg, daylight " + summerDay.ToString("F1") + "/"
                        + winterDay.ToString("F1") + " h");
        report.AppendLine("  [" + Mark(lunar) + "] lunar cycle: illumination "
                        + minPhase.ToString("F3") + "-" + maxPhase.ToString("F3")
                        + ", separation " + minSeparation.ToString("F1") + "-"
                        + maxSeparation.ToString("F1") + " deg");
        return season && lunar;
    }

    static bool SourceContract(StringBuilder report)
    {
        string time = File.ReadAllText(TimePath);
        string sky = File.ReadAllText(SkyPath);
        bool intensityHandover = time.Contains("Light wanted = PrimaryLight;")
                              && !time.Contains("NightHandoverHeight");
        bool singleFade = time.Contains("float extinction = Mathf.Max(beam.x")
                       && !time.Contains("* sunFade;");
        bool independentBodies = sky.Contains("public static Light SunLight")
                              && sky.Contains("public static Color? SkyMoonRadiance")
                              && sky.Contains("Light sunForPhase = sunBody;");

        report.AppendLine("  [" + Mark(intensityHandover) + "] main light follows current energy");
        report.AppendLine("  [" + Mark(singleFade) + "] low-sun extinction is applied once");
        report.AppendLine("  [" + Mark(independentBodies) + "] sky keeps independent sun and moon");
        return intensityHandover && singleFade && independentBodies;
    }

    static bool ReceiverContract(StringBuilder report)
    {
        string rain = File.ReadAllText(RainPath);
        string snow = File.ReadAllText(SnowPath);
        string snowVfx = File.ReadAllText(SnowVfxPath);
        string sea = File.ReadAllText(SeaPath);

        bool rainShadow = rain.Contains("MainLightRealtimeShadow")
                       && rain.Contains("SampleMainLightCookie(IN.worldPos)");
        bool snowLight = snow.Contains("SampleMainLightCookie(IN.positionWS)")
                      && snow.Contains("SampleSH(half3(0, 1, 0))")
                      && !snow.Contains("half3 N = (half3)forward;")
                      && snowVfx.Contains("receiveShadows: 1")
                      && snowVfx.Contains("useEmissive: 0");
        bool seaLight = sea.Contains("float3 directLightColor = mainLight.color * mainLight.shadowAttenuation;")
                     && !sea.Contains("spec *= saturate(_SeaSunElevation01")
                     && !sea.Contains("float3 overcast = dot(skyRefl");

        report.AppendLine("  [" + Mark(rainShadow) + "] rain receives terrain and cloud shadow");
        report.AppendLine("  [" + Mark(snowLight) + "] snow ambient is view independent and cookie-aware");
        report.AppendLine("  [" + Mark(seaLight) + "] sea direct terms share shadow; moon glitter survives");
        return rainShadow && snowLight && seaLight;
    }

    static bool LensContract(StringBuilder report)
    {
        var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(FlarePath);
        bool valid = data != null && data.elements != null && data.elements.Length == 3;
        if (valid)
        {
            bool hasVeiling = false, hasGhost = false;
            foreach (var element in data.elements)
            {
                hasVeiling |= element.flareType == SRPLensFlareType.Circle
                           && element.uniformScale >= 5f;
                hasGhost |= element.flareType == SRPLensFlareType.Ring
                         && element.localIntensity <= 0.03f;
            }
            valid = hasVeiling && hasGhost;
        }

        report.AppendLine("  [" + Mark(valid) + "] subtle flare includes veiling glare and restrained ghost");
        return valid;
    }

    static float Elevation(Vector3 direction) =>
        Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;

    static float DaylightHours(int year, int day, float latitude, float longitude, float utc)
    {
        const int steps = 288; // five-minute samples
        int above = 0;
        for (int i = 0; i < steps; i++)
            if (CelestialEphemeris.Evaluate(year, day, (i + 0.5f) / steps,
                    latitude, longitude, utc, 0f).SunDirection.y > 0f)
                above++;
        return 24f * above / steps;
    }

    static string Mark(bool value) => value ? "+" : "-";
}
