// ROL: kar sisteminin geçiş başına maliyetini ölçer (§11.2). Ölçmeden optimize etme.
// Çağıran: SnowManager (örnek adlarını kuyruğa yazıyor), SnowDebugWindow (tabloyu okur).

using UnityEngine;
using UnityEngine.Profiling;

[DisallowMultipleComponent]
public class SnowProfiler : MonoBehaviour
{
    /// Ölçülen geçişler. Ad CommandBuffer örnek adıyla BİREBİR aynı olmak zorunda.
    public static readonly string[] PassNames =
    {
        "Snow.Scroll",
        "Snow.Deformation",
        "Snow.Relax",
        "Snow.Accumulate",
        "Snow.Cascade",
    };

    Recorder[] recorders;

    /// GPU zamanlaması için Unity'nin GPU profillemesi açık olmalı; kapalıysa
    /// `gpuElapsedNanoseconds` sıfır döner ve tablo CPU sütununu gösterir.
    public bool HasGpuTiming { get; private set; }

    void OnEnable()
    {
        recorders = new Recorder[PassNames.Length];

        for (int i = 0; i < PassNames.Length; i++)
        {
            recorders[i] = Recorder.Get(PassNames[i]);
            recorders[i].enabled = true;

            // GPU zamanı ayrı bir bayrakla açılıyor; kapalıyken hiç toplanmıyor.
            recorders[i].CollectFromAllThreads();
        }
    }

    void OnDisable()
    {
        if (recorders == null) return;

        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i] != null) recorders[i].enabled = false;

        recorders = null;
    }

    void LateUpdate()
    {
        if (recorders == null || recorders.Length == 0) return;

        HasGpuTiming = false;

        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i].gpuElapsedNanoseconds > 0) HasGpuTiming = true;

        WriteReport();
    }

    // --------------------------------------------------------------------- rapor

    /// Rapor dosyası.
    ///
    /// ÇALIŞMA ZAMANINDA yazılıyor, editör penceresinde değil: pencere kapalıyken
    /// rapor donuyordu ve elde bayat veri kalıyordu — ölçüldü.
    const string ReportPath = "Logs/snow.log";

    /// Kaç saniyede bir yazılıyor.
    const float ReportInterval = 2f;

    float nextReportTime;
    System.Text.StringBuilder report;

    void WriteReport()
    {
        if (Time.unscaledTime < nextReportTime) return;
        nextReportTime = Time.unscaledTime + ReportInterval;

        report ??= new System.Text.StringBuilder(4096);
        report.Clear();

        SnowManager manager = SnowManager.Active;

        report.Append("# Kar sistemi raporu  ").AppendLine(System.DateTime.Now.ToString("HH:mm:ss"));
        report.Append("kare ").AppendLine(Time.frameCount.ToString());

        if (manager == null || !manager.IsReady)
        {
            report.AppendLine("SnowManager ETKİN DEĞİL.");
            Flush();
            return;
        }

        SnowQualityData quality = manager.Settings.QualityData;

        report.AppendLine();
        report.AppendLine("## Bölge");
        Line("kalite", manager.Settings.Quality.ToString());
        Line("çözünürlük", quality.Resolution.ToString());
        Line("teksel cm", (manager.TexelSize * 100f).ToString("0.###"));
        Line("snap teksel", manager.SnapTexels.ToString());
        Line("merkez teksel", manager.CenterTexel.x + " , " + manager.CenterTexel.y);
        Line("snap kalan", Mathf.Abs(manager.CenterTexel.x % manager.SnapTexels)
                           + " , " + Mathf.Abs(manager.CenterTexel.y % manager.SnapTexels));
        Line("son kaydırma", manager.LastScrollTexels.x + " , " + manager.LastScrollTexels.y);

        SnowWeather weather = manager.Weather;
        report.AppendLine();
        report.AppendLine("## Hava");
        Line("dış zincir", weather.DrivenExternally.ToString());
        Line("preset", weather.ActivePresetName);
        Line("yağış mm/saat", (weather.SnowfallSWERate * 1000f * 3600f).ToString("0.###"));
        Line("kaplama", weather.Coverage.ToString("0.000"));
        Line("rüzgâr m/s", weather.WindSpeed.ToString("0.0"));
        Line("rüzgâr yönü", weather.WindWS.normalized.ToString("0.00"));
        Line("sıcaklık C", weather.TemperatureC.ToString("0.0"));
        Line("taban kar swe", weather.BaseSWE.ToString("0.00000"));
        Line("ıslaklık", weather.SnowWetness.ToString("0.00"));

        SnowOcclusionCapture occlusion = manager.Occlusion;
        report.AppendLine();
        report.AppendLine("## Engel");
        Line("katman", LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName).ToString());
        Line("yenileme", occlusion.CaptureCount.ToString());
        Line("kayma m", Vector2.Distance(manager.AreaCenter, occlusion.LastCaptureCenter).ToString("0.00"));

        var clipmap = manager.GetComponent<SnowClipmap>();
        if (clipmap != null)
        {
            report.AppendLine();
            report.AppendLine("## Clipmap");
            Line("halka", clipmap.RingCount.ToString());
            Line("üçgen", clipmap.TriangleCount.ToString());
        }

        SnowDeformerRegistry registry = manager.Deformers;
        if (registry != null)
        {
            report.AppendLine();
            report.AppendLine("## Deformasyon");
            Line("etkin / kapasite", registry.ActiveCount + " / " + registry.Capacity);
            Line("kapasite kaynağı", SnowDeformerRegistry.LastCapacityReading);
            Line("en büyük temas cm", (registry.MaxContactExtent * 100f).ToString("0.0"));
            Line("damga atlası",
                 manager.Settings.StampAtlas != null ? manager.Settings.StampAtlas.name : "YOK");
        }

        var footsteps = Object.FindAnyObjectByType<SnowFootstepDriver>();
        if (footsteps != null)
        {
            Line("ayak sürücüsü", "var, zeminde: " + footsteps.Grounded);
        }
        else
        {
            Line("ayak sürücüsü", "YOK");
        }

        var snowfall = manager.GetComponent<SnowfallController>();
        if (snowfall != null)
        {
            report.AppendLine();
            report.AppendLine("## Yağış parçacıkları");
            Line("etkin tane", snowfall.ActiveFlakes.ToString());
            Line("kapasite", snowfall.FlakeCapacity.ToString());
            Line("etkin savrulma", snowfall.ActiveSpindrift.ToString());
            Line("gevşek oran", snowfall.LooseSnowFraction.ToString("0.000"));
        }

        var movement = Object.FindAnyObjectByType<SnowMovementModifier>();
        if (movement != null)
        {
            report.AppendLine();
            report.AppendLine("## Ayak altı");
            Line("örnek var", movement.HasSample.ToString());
            Line("derinlik cm", (movement.Depth * 100f).ToString("0.0"));
            Line("yoğunluk", movement.Density01.ToString("0.00"));
            Line("hız çarpanı", movement.SpeedMultiplier.ToString("0.000"));
        }

        report.AppendLine();
        report.AppendLine("## Profil");

        float total = 0f;
        for (int i = 0; i < PassNames.Length; i++)
        {
            float gpu = GpuMilliseconds(i);
            float cpu = CpuMilliseconds(i);
            float shown = gpu > 0f ? gpu : cpu;
            total += shown;

            Line(PassNames[i], shown.ToString("0.000") + " ms " + (gpu > 0f ? "GPU" : "CPU"));
        }

        Line("TOPLAM", total.ToString("0.000") + " ms / hedef "
             + BudgetMilliseconds(manager.Settings.Quality).ToString("0.00"));

        Flush();
    }

    void Line(string label, string value) =>
        report.Append("  ").Append(label.PadRight(20)).AppendLine(value);

    void Flush()
    {
        try
        {
            System.IO.File.WriteAllText(ReportPath, report.ToString());
        }
        catch (System.IO.IOException)
        {
            // Dosya o an başkasında açıksa bir sonraki turda yazılır. Rapor bir ölçüm
            // aracı; yazılamaması sistemi etkilemiyor.
        }
    }

    /// Geçişin CPU süresi, milisaniye.
    public float CpuMilliseconds(int index)
    {
        if (recorders == null || index < 0 || index >= recorders.Length) return 0f;
        return recorders[index].elapsedNanoseconds * 1e-6f;
    }

    /// Geçişin GPU süresi, milisaniye. GPU profillemesi kapalıysa 0.
    public float GpuMilliseconds(int index)
    {
        if (recorders == null || index < 0 || index >= recorders.Length) return 0f;
        return recorders[index].gpuElapsedNanoseconds * 1e-6f;
    }

    /// §11.2'deki hedef bütçe, milisaniye. Ölçüm bunun üstüne çıkarsa tablo kırmızıya
    /// dönüyor — hedef, garanti değil.
    public static float BudgetMilliseconds(SnowQualityPreset quality)
    {
        switch (quality)
        {
            case SnowQualityPreset.Low: return 0.75f;
            case SnowQualityPreset.Medium: return 1.59f;
            default: return 2.60f;
        }
    }
}
