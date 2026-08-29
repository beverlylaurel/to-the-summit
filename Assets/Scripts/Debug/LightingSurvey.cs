using System.Collections;
using System.IO;
using UnityEngine;

/// [TEMPORARY — delete once the lighting defects are named] THE SAME FRAME AT EVERY HOUR.
///
/// Lighting is judged by eye and eyes disagree, so the argument is settled the way everything
/// else was today: one frame, one camera, the clock as the only variable. Weather is pinned too
/// — the storm shots are a second pass, not a drift.
///
/// THE SCENE IS PINNED EVERY FRAME, not once. `AltitudeWeatherDriver` and the cloud driver both
/// write their state continuously; set once, they walk it back before the shot is taken and the
/// series ends up comparing different skies.
public class LightingSurvey : MonoBehaviour
{
    [Tooltip("Saat değiştikten sonra kaç saniye beklenecek — ortam probu ve zamansal birikim otursun.")]
    [SerializeField] float settleSeconds = 5f;

    IEnumerator Start()
    {
        var time = FindFirstObjectByType<TimeOfDay>();
        var driver = FindFirstObjectByType<AltitudeWeatherDriver>();

        if (time == null || driver == null)
        {
            Debug.LogError($"{nameof(LightingSurvey)}: bileşenler bulunamadı.");
            yield break;
        }

        // THE WEATHER IS DRIVEN THROUGH THE DRIVER, NOT AROUND IT.
        //
        // The first version wrote `WeatherState.Precipitation` and DISABLED the driver. That
        // measured nothing: the cloud coverage comes from `AltitudeWeatherDriver.CloudMass`, and
        // a disabled driver freezes it. The storm shots came out with a blue sky and the probe
        // reported a bug that was its own.
        //
        // `IntensityOverride` is the driver's own test path — the same one the F1 panel uses.
        // `Instant` removes the transition, otherwise the cloud mass lags by tens of seconds and
        // the shot photographs the way there rather than the destination.
        driver.Instant = true;
        time.Paused = true;

        Directory.CreateDirectory("Logs/Lighting");

        (string name, float hour, float precipitation)[] shots =
        {
            ("01_gece_03",        3f,    0f),
            ("02_safak_05",       5.5f,  0f),
            ("03_gundogumu_06",   6.2f,  0f),
            ("04_sabah_08",       8f,    0f),
            ("05_oglen_12",      12f,    0f),
            ("06_ikindi_16",     16f,    0f),
            ("07_altin_saat_175",17.6f,  0f),
            ("08_gunbatimi_18",  18.1f,  0f),
            ("09_alacakaranlik", 18.8f,  0f),
            ("10_firtina_oglen", 12f,    0.9f),
            ("11_firtina_batim", 17.8f,  0.9f),
        };

        foreach (var s in shots)
        {
            float until = Time.realtimeSinceStartup + settleSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                // Pinned EVERY frame — see the note above.
                time.SetNormalized(s.hour / 24f);
                time.Paused = true;
                driver.IntensityOverride = s.precipitation;
                yield return null;
            }

            string path = $"Logs/Lighting/{s.name}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[LightingSurvey] {s.name}  saat {s.hour:0.0}  yağış {s.precipitation:0.0} -> {path}");

            for (int i = 0; i < 5; i++) yield return null;
        }

        time.Paused = false;
        driver.IntensityOverride = -1f;
        driver.Instant = false;

        Debug.Log("[LightingSurvey] bitti -> Logs/Lighting/");
    }
}
