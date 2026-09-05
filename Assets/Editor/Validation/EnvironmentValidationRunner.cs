using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnvironmentValidationRunner
{
    const string RunningKey = "TTS.EnvironmentValidation.Running";
    const string ScenarioIdsKey = "TTS.EnvironmentValidation.Scenarios";
    const string ScenarioIndexKey = "TTS.EnvironmentValidation.Index";
    const string OutputKey = "TTS.EnvironmentValidation.Output";
    const string StatusKey = "TTS.EnvironmentValidation.Status";
    const string LastReportKey = "TTS.EnvironmentValidation.LastReport";
    const double DependencyTimeout = 30.0;
    const double SettleSeconds = 2.5;

    enum Stage { WaitingForPlay, WaitingForDependencies, Settling, WaitingForCapture }

    static Stage stage;
    static double stageDeadline;
    static string capturePath;
    static readonly List<string> issues = new();
    static bool listening;

    static GameObject player;
    static Terrain terrain;
    static Camera viewCamera;
    static TimeOfDay timeOfDay;
    static AltitudeWeatherDriver weather;
    static WindField wind;
    static TemperatureField temperature;
    static SnowManager snow;
    static SeaManager sea;
    static SeaSimulation[] seaSimulations;
    static HeadlampController headlamp;
    static VintagePhotoMode photoMode;

    static EnvironmentValidationRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;

        if (IsRunning && EditorApplication.isPlaying)
            BeginPlayRun();
    }

    public static bool IsRunning => SessionState.GetBool(RunningKey, false);
    public static string Status => SessionState.GetString(StatusKey, "Hazır");
    public static string LastReportPath => SessionState.GetString(LastReportKey, string.Empty);

    public static void RunAll()
    {
        var ids = new List<string>();
        foreach (EnvironmentValidationScenario scenario in EnvironmentValidationCatalog.All)
            ids.Add(scenario.id);
        Start(ids);
    }

    public static void Start(IReadOnlyList<string> ids)
    {
        if (IsRunning)
        {
            Debug.LogWarning("Environment validation is already running.");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Environment validation must be started from Edit Mode.");
            return;
        }

        if (ids == null || ids.Count == 0)
        {
            Debug.LogWarning("No environment validation scenario was selected.");
            return;
        }

        foreach (string id in ids)
        {
            if (EnvironmentValidationCatalog.Find(id) == null)
            {
                Debug.LogError($"Unknown environment validation scenario: {id}");
                return;
            }
        }

        string relativeOutput = Path.Combine("Temp", "Validation", "Environment",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        string absoluteOutput = AbsoluteProjectPath(relativeOutput);
        Directory.CreateDirectory(absoluteOutput);

        SessionState.SetBool(RunningKey, true);
        SessionState.SetString(ScenarioIdsKey, string.Join(";", ids));
        SessionState.SetInt(ScenarioIndexKey, 0);
        SessionState.SetString(OutputKey, relativeOutput.Replace('\\', '/'));
        SetStatus("Play Mode başlatılıyor…");

        WriteReportHeader(ids, absoluteOutput);
        stage = Stage.WaitingForPlay;
        EditorApplication.isPlaying = true;
    }

    public static void Cancel()
    {
        if (!IsRunning) return;
        AppendReport("\nKoşu kullanıcı tarafından iptal edildi.\n");
        Finish(false, "İptal edildi");
    }

    public static void RevealLastReport()
    {
        string report = LastReportPath;
        if (!string.IsNullOrEmpty(report) && File.Exists(report))
            EditorUtility.RevealInFinder(report);
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!IsRunning) return;

        if (change == PlayModeStateChange.EnteredPlayMode)
            BeginPlayRun();
        else if (change == PlayModeStateChange.EnteredEditMode)
            Finish(false, "Play Mode beklenmeden kapandı");
    }

    static void BeginPlayRun()
    {
        stage = Stage.WaitingForDependencies;
        stageDeadline = EditorApplication.timeSinceStartup + DependencyTimeout;
        Listen();
        SetStatus("Sahne bağımlılıkları bekleniyor…");
    }

    static void Tick()
    {
        if (!IsRunning || !EditorApplication.isPlaying) return;

        try
        {
            switch (stage)
            {
                case Stage.WaitingForDependencies:
                    if (ResolveDependencies()) ApplyCurrentScenario();
                    else if (EditorApplication.timeSinceStartup > stageDeadline)
                        Fail("Sahne bağımlılıkları 30 saniye içinde hazır olmadı.");
                    break;

                case Stage.Settling:
                    if (EditorApplication.timeSinceStartup >= stageDeadline)
                        CaptureCurrentScenario();
                    break;

                case Stage.WaitingForCapture:
                    if (EditorApplication.timeSinceStartup >= stageDeadline
                        && File.Exists(capturePath) && new FileInfo(capturePath).Length > 0)
                        CompleteCurrentScenario();
                    else if (EditorApplication.timeSinceStartup > stageDeadline + 10.0)
                        Fail("Ekran görüntüsü 10 saniye içinde yazılamadı.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Fail(exception.Message);
        }
    }

    static bool ResolveDependencies()
    {
        player = GameObject.Find("Player");
        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        viewCamera = Camera.main;
        timeOfDay = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>();
        weather = UnityEngine.Object.FindFirstObjectByType<AltitudeWeatherDriver>();
        wind = UnityEngine.Object.FindFirstObjectByType<WindField>();
        temperature = UnityEngine.Object.FindFirstObjectByType<TemperatureField>();
        snow = UnityEngine.Object.FindFirstObjectByType<SnowManager>();
        sea = UnityEngine.Object.FindFirstObjectByType<SeaManager>();
        seaSimulations = UnityEngine.Object.FindObjectsByType<SeaSimulation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        headlamp = UnityEngine.Object.FindFirstObjectByType<HeadlampController>();
        photoMode = UnityEngine.Object.FindFirstObjectByType<VintagePhotoMode>();

        return player != null && terrain != null && viewCamera != null && timeOfDay != null
            && weather != null && wind != null && temperature != null && snow != null
            && sea != null && seaSimulations.Length > 0 && headlamp != null && photoMode != null;
    }

    static void ApplyCurrentScenario()
    {
        EnvironmentValidationScenario scenario = CurrentScenario();
        if (scenario == null)
        {
            Finish(true, "Tamamlandı");
            return;
        }

        issues.Clear();
        SetStatus($"{scenario.title} hazırlanıyor…");

        photoMode.EditorViewfinderForTest(false);
        headlamp.SetOn(false);
        Time.timeScale = 1f;

        PlacePlayer(scenario);
        timeOfDay.Paused = true;
        timeOfDay.SetCalendarDate(2026, 247);
        timeOfDay.SetNormalized(scenario.hour / 24f);

        weather.Instant = true;
        weather.ForceWindow = scenario.forceClearWindow;
        weather.WorldStormOverride = scenario.storm;
        wind.ApplyOverride(scenario.windSeverity, scenario.windAngle);
        wind.EditorTimeOverride = scenario.seaTime;
        temperature.ApplyOverride(scenario.temperatureC);

        snow.SimTimeScale = 0f;
        snow.FillSnowDepth(scenario.snowDepth);

        sea.EditorTimeOverride = scenario.seaTime;
        sea.EditorSwashPhaseOverride = scenario.swashPhase;
        foreach (SeaSimulation simulation in seaSimulations)
            simulation.EditorTimeOverride = scenario.seaTime;

        viewCamera.fieldOfView = scenario.fieldOfView;
        headlamp.SetOn(scenario.headlamp);
        if (scenario.viewfinder) photoMode.EditorViewfinderForTest(true);

        Physics.SyncTransforms();
        stage = Stage.Settling;
        stageDeadline = EditorApplication.timeSinceStartup + SettleSeconds;
    }

    static void PlacePlayer(EnvironmentValidationScenario scenario)
    {
        Vector3 sample = new(scenario.playerXZ.x, 0f, scenario.playerXZ.y);
        sample.y = terrain.SampleHeight(sample) + terrain.transform.position.y + 0.05f;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(sample, Quaternion.Euler(0f, scenario.yaw, 0f));
        if (controller != null) controller.enabled = true;

        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot != null) pivot.localRotation = Quaternion.Euler(scenario.pitch, 0f, 0f);
    }

    static void CaptureCurrentScenario()
    {
        EnvironmentValidationScenario scenario = CurrentScenario();
        string output = AbsoluteProjectPath(SessionState.GetString(OutputKey, string.Empty));
        capturePath = Path.Combine(output, scenario.id + ".png");
        if (File.Exists(capturePath)) File.Delete(capturePath);

        ScreenCapture.CaptureScreenshot(capturePath);
        stage = Stage.WaitingForCapture;
        stageDeadline = EditorApplication.timeSinceStartup + 0.5;
        SetStatus($"{scenario.title} kaydediliyor…");
    }

    static void CompleteCurrentScenario()
    {
        EnvironmentValidationScenario scenario = CurrentScenario();
        float snowReach = Shader.GetGlobalFloat(SeaShaderIDs.SeaSnowReachY);
        float wetLevel = Shader.GetGlobalFloat(SeaShaderIDs.SeaWetLevelY);
        float precipitation = Shader.GetGlobalFloat("_PrecipIntensity01");
        string issueText = issues.Count == 0 ? "temiz" : $"{issues.Count} uyarı/hata";

        var row = new StringBuilder();
        row.AppendLine($"## {scenario.title}");
        row.AppendLine();
        row.AppendLine($"![{scenario.title}]({scenario.id}.png)");
        row.AppendLine();
        row.AppendLine($"- Saat: `{timeOfDay.Clock}` · fırtına: `{weather.WorldStorm:F2}` · yağış: `{precipitation:F2}`");
        row.AppendLine($"- Sıcaklık: `{scenario.temperatureC:F1} °C` · rüzgâr: `{wind.Velocity.magnitude:F1} m/s` · kar: `{scenario.snowDepth:F2} m`");
        row.AppendLine($"- Deniz zamanı: `{scenario.seaTime:F1}` · swash fazı: `{scenario.swashPhase:F2}` · ıslak kot: `{wetLevel:F3}` · karsız kot: `{snowReach:F3}`");
        row.AppendLine($"- Kamera FOV: `{viewCamera.fieldOfView:F1}` · vizör hazır: `{photoMode.EditorPreviewReady}` · konsol: **{issueText}**");
        row.AppendLine();

        if (issues.Count > 0)
        {
            foreach (string issue in issues) row.AppendLine($"  - {issue}");
            row.AppendLine();
        }

        AppendReport(row.ToString());
        SessionState.SetInt(ScenarioIndexKey, SessionState.GetInt(ScenarioIndexKey, 0) + 1);

        if (CurrentScenario() == null)
            Finish(true, "Tamamlandı");
        else
            ApplyCurrentScenario();
    }

    static EnvironmentValidationScenario CurrentScenario()
    {
        string[] ids = SelectedIds();
        int index = SessionState.GetInt(ScenarioIndexKey, 0);
        return index >= 0 && index < ids.Length
            ? EnvironmentValidationCatalog.Find(ids[index])
            : null;
    }

    static string[] SelectedIds() => SessionState.GetString(ScenarioIdsKey, string.Empty)
        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

    static void Listen()
    {
        if (listening) return;
        Application.logMessageReceived += OnLog;
        listening = true;
    }

    static void StopListening()
    {
        if (!listening) return;
        Application.logMessageReceived -= OnLog;
        listening = false;
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception) return;
        if (issues.Count >= 20) return;
        issues.Add($"{type}: {message.Replace('\n', ' ')}");
    }

    static void Fail(string reason)
    {
        AppendReport($"\n**KOŞU BAŞARISIZ:** {reason}\n");
        Finish(false, "Başarısız: " + reason);
    }

    static void Finish(bool success, string status)
    {
        if (!IsRunning) return;

        StopListening();
        string report = Path.Combine(AbsoluteProjectPath(
            SessionState.GetString(OutputKey, string.Empty)), "report.md");
        AppendReport($"\n---\n\nSonuç: **{(success ? "TAMAMLANDI" : "TAMAMLANAMADI")}**\n");

        SessionState.SetString(LastReportKey, report);
        SessionState.SetBool(RunningKey, false);
        SetStatus(status);

        if (EditorApplication.isPlaying)
        {
            RenderTexture.active = null;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }

        Debug.Log($"Environment validation {status}. Report: {report}");
    }

    static void SetStatus(string value) => SessionState.SetString(StatusKey, value);

    static string AbsoluteProjectPath(string relative) => Path.GetFullPath(
        Path.Combine(Application.dataPath, "..", relative));

    static void WriteReportHeader(IReadOnlyList<string> ids, string output)
    {
        var text = new StringBuilder();
        text.AppendLine("# Çevre Doğrulama Raporu");
        text.AppendLine();
        text.AppendLine($"- Başlangıç: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
        text.AppendLine($"- Unity: `{Application.unityVersion}`");
        text.AppendLine($"- Senaryo sayısı: `{ids.Count}`");
        text.AppendLine("- Tarih: `2026 / gün 247`");
        text.AppendLine();
        File.WriteAllText(Path.Combine(output, "report.md"), text.ToString(),
            new UTF8Encoding(false));
    }

    static void AppendReport(string text)
    {
        string output = SessionState.GetString(OutputKey, string.Empty);
        if (string.IsNullOrEmpty(output)) return;
        string report = Path.Combine(AbsoluteProjectPath(output), "report.md");
        File.AppendAllText(report, text, new UTF8Encoding(false));
    }
}
