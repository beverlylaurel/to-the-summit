// ROL: kar sisteminin teşhis penceresi. Sabit eşlik testi, bölge/snap ölçümü ve
// durum dokusunun kanal görselleştirmesi. Bir de sahneyi kuran düğme.
// Çağıran: menü — To The Summit > Kar > Kar Teşhisi.

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class SnowDebugWindow : EditorWindow
{
    static readonly string[] ChannelNames =
    {
        "R — swe (kar su eşdeğeri, m)",
        "G — rhoN (normalize yoğunluk)",
        "B — wet (ıslaklık)",
        "A — disturb (tazelik)",
        "h — türetilmiş derinlik (m)",
    };

    /// Her modun görüntüleme tavanı. rhoN/wet/disturb zaten 0..1.
    static readonly float[] ChannelRanges = { 0.60f, 1f, 1f, 1f, 1.20f };

    /// Hangi doku gösteriliyor.
    enum PreviewSource
    {
        Durum,
        Engel,
    }

    static readonly string[] SourceNames = { "Durum dokusu (RT_State)", "Engel haritası (RT_Occlusion)" };

    PreviewSource source;
    int channel;
    float gridSize = 1f;
    Vector2 scroll;

    Material debugMaterial;
    RenderTexture preview;

    /// Merkez tekseli CPU'ya okuyan prob. Onizleme siyah gorunurken 0.02 ile 0.00'i
    /// gozle ayirmak imkansiz; ayiran sey sayinin kendisi.
    RenderTexture probe;
    Texture2D probeCPU;

    /// İkİ NOKTA ÖLÇÜLÜYOR: bölgenin merkezi ve ondan 8 m uzaktaki bir nokta.
    /// Test küpü merkezin üstünde durduğu için ikisi birden "çatı altına kar birikmez"
    /// ve "açık alanda ~3 cm/saat birikir" kriterlerini aynı anda sınıyor.
    Color centerValue;
    Color openValue;
    bool centerValid;

    // Kütle testi (§5 kabul kriteri).
    ComputeBuffer sumBuffer;
    readonly uint[] sumZero = new uint[1];
    readonly uint[] sumRead = new uint[1];
    ComputeShader sumCompute;
    int sumKernel = -1;
    double massTotal;
    double massBaseline;
    bool massRunning;

    // Birikme hızı ölçümü.
    double rateStartTime;
    float rateStartCenterH;
    float rateStartOpenH;
    float rateStartCenterSWE;
    float rateStartOpenSWE;
    bool rateRunning;

    /// Rapor dosyası. Pencerede kaydirarak okumak yerine her şey buraya yazılıyor.
    const string LogPath = "Logs/snow.log";

    bool writeLog = true;
    double lastLogTime;

    string parityReport;
    bool parityOk;

    [MenuItem("To The Summit/Kar/Kar Teşhisi", false, 50)]
    static void Open() => GetWindow<SnowDebugWindow>("Kar Teşhisi").minSize = new Vector2(420f, 560f);

    void OnDisable()
    {
        if (debugMaterial != null) DestroyImmediate(debugMaterial);
        if (preview != null) { preview.Release(); DestroyImmediate(preview); }
        if (probe != null) { probe.Release(); DestroyImmediate(probe); }
        sumBuffer?.Release();
        sumBuffer = null;
        sumKernel = -1;
        if (probeCPU != null) DestroyImmediate(probeCPU);

        debugMaterial = null;
        preview = null;
        probe = null;
        probeCPU = null;
        centerValid = false;
    }

    /// BLIT BURADA, OnGUI'DE DEGIL. Play modunda OnGUI icinde Graphics.Blit cagirmak
    /// aktif render hedefini editor GUI gecisinin altindan cekiyor ve pencerenin tamami
    /// siyah kaliyor — hicbir istisna basmadan. Bu metot GUI cizim yolunun disinda.
    void OnInspectorUpdate()
    {
        UpdatePreview();
        WriteLogFile();
        Repaint();
    }

    /// BÜTÜN ÖLÇÜMLERİ DOSYAYA yazıyor, iki saniyede bir.
    ///
    /// Pencere uzun ve kaydırmalı; ekran görüntüsüyle okumak hem kullanıcıya iş
    /// çıkarıyor hem yarısını göstermiyor. Dosya tek parça ve doğrudan okunabiliyor.
    void WriteLogFile()
    {
        if (!writeLog) return;
        if (EditorApplication.timeSinceStartup - lastLogTime < 2.0) return;

        lastLogTime = EditorApplication.timeSinceStartup;

        try
        {
            System.IO.File.WriteAllText(LogPath, BuildReport());
        }
        catch (System.IO.IOException)
        {
            // Dosya o an başkası tarafından açıksa bir sonraki turda yazılır.
            // Yutulmuyor: rapor bir ölçüm aracı, yazılamaması sistemi etkilemiyor.
        }
    }

    string BuildReport()
    {
        var report = new System.Text.StringBuilder(4096);

        report.AppendLine("# Kar sistemi raporu  " + System.DateTime.Now.ToString("HH:mm:ss"));
        report.AppendLine("Play: " + EditorApplication.isPlaying);

        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady)
        {
            report.AppendLine("SnowManager ETKİN DEĞİL.");
            return report.ToString();
        }

        SnowQualityData quality = manager.Settings.QualityData;

        report.AppendLine();
        report.AppendLine("## Bölge");
        report.AppendLine("kalite         " + manager.Settings.Quality);
        report.AppendLine("çözünürlük     " + quality.Resolution);
        report.AppendLine("teksel         " + (manager.TexelSize * 100f).ToString("0.###") + " cm");
        report.AppendLine("snap adımı      " + manager.SnapTexels + " teksel");
        report.AppendLine("merkez teksel  " + manager.CenterTexel.x + " , " + manager.CenterTexel.y);
        report.AppendLine("snap kalan     " + Mathf.Abs(manager.CenterTexel.x % manager.SnapTexels)
                          + " , " + Mathf.Abs(manager.CenterTexel.y % manager.SnapTexels));
        report.AppendLine("son kaydırma   " + manager.LastScrollTexels.x + " , " + manager.LastScrollTexels.y);

        SnowOcclusionCapture capture = manager.Occlusion;
        report.AppendLine();
        report.AppendLine("## Engel haritası");
        report.AppendLine("katman         " + LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName));
        report.AppendLine("yenileme       " + capture.CaptureCount + "  (kare " + Time.frameCount + ")");
        report.AppendLine("kayma          "
            + Vector2.Distance(manager.AreaCenter, capture.LastCaptureCenter).ToString("0.00") + " m");

        SnowWeather weather = manager.Weather;
        report.AppendLine();
        report.AppendLine("## Hava");
        report.AppendLine("preset         " + weather.ActivePresetName);
        report.AppendLine("yağış          "
            + (weather.SnowfallSWERate * 1000f * 3600f).ToString("0.###") + " mm SWE/saat");
        report.AppendLine("rüzgâr         " + weather.WindSpeed.ToString("0.0") + " m/s");
        report.AppendLine("sıcaklık        " + weather.TemperatureC.ToString("0.0") + " C");
        report.AppendLine("kaplama        " + weather.Coverage.ToString("0.00"));
        report.AppendLine("zaman hızı      " + SnowManager.SimulationSpeed.ToString("0"));

        if (centerValid)
        {
            report.AppendLine();
            report.AppendLine("## Prob");
            report.AppendLine("merkez  swe " + centerValue.r.ToString("0.00000")
                              + "  rhoN " + centerValue.g.ToString("0.000")
                              + "  wet " + centerValue.b.ToString("0.00")
                              + "  disturb " + centerValue.a.ToString("0.00")
                              + "  h " + (HeightOf(centerValue) * 100f).ToString("0.00") + " cm");
            report.AppendLine("8m yan  swe " + openValue.r.ToString("0.00000")
                              + "  rhoN " + openValue.g.ToString("0.000")
                              + "  wet " + openValue.b.ToString("0.00")
                              + "  disturb " + openValue.a.ToString("0.00")
                              + "  h " + (HeightOf(openValue) * 100f).ToString("0.00") + " cm");

            double elapsed = EditorApplication.timeSinceStartup - rateStartTime;
            if (elapsed > 1.0)
            {
                float hours = (float)(elapsed * SnowManager.SimulationSpeed / 3600.0);
                report.AppendLine("geçen          " + elapsed.ToString("0") + " s gerçek");
                report.AppendLine("merkez hızı    "
                    + ((centerValue.r - rateStartCenterSWE) * 1000f / hours).ToString("0.00") + " mm/saat");
                report.AppendLine("açık hızı      "
                    + ((openValue.r - rateStartOpenSWE) * 1000f / hours).ToString("0.00") + " mm/saat");
            }

            report.AppendLine("Σ swe          " + massTotal.ToString("0.000")
                              + "   başlangıç " + massBaseline.ToString("0.000")
                              + "   sapma "
                              + (massBaseline > 1e-6 ? (massTotal - massBaseline) / massBaseline * 100.0 : 0.0)
                                    .ToString("0.000") + " %");
        }

        var clipmap = manager.GetComponent<SnowClipmap>();
        if (clipmap != null)
        {
            report.AppendLine();
            report.AppendLine("## Clipmap");
            report.AppendLine("halka          " + clipmap.RingCount);
            report.AppendLine("üçgen          " + clipmap.TriangleCount);
        }

        SnowDeformerRegistry registry = manager.Deformers;
        if (registry != null)
        {
            report.AppendLine();
            report.AppendLine("## Deformasyon");
            report.AppendLine("etkin deformer " + registry.ActiveCount + " / " + registry.Capacity);
            report.AppendLine("kapasite kayna\u011f\u0131 " + SnowDeformerRegistry.LastCapacityReading);
            report.AppendLine("en büyük temas  "
                + (registry.MaxContactExtent * 100f).ToString("0.0") + " cm");
            report.AppendLine("damga atlası   "
                + (manager.Settings.StampAtlas != null ? manager.Settings.StampAtlas.name : "YOK"));
        }

        var snowfall = manager.GetComponent<SnowfallController>();
        if (snowfall != null)
        {
            report.AppendLine();
            report.AppendLine("## Yağış parçacıkları");
            report.AppendLine("etkin tane     " + snowfall.ActiveFlakes);
            report.AppendLine("etkin savrulma " + snowfall.ActiveSpindrift);
            report.AppendLine("gevşek oran     " + snowfall.LooseSnowFraction.ToString("0.000"));
        }

        var persistence = manager.GetComponent<SnowPersistence>();
        if (persistence != null)
        {
            report.AppendLine();
            report.AppendLine("## Kalıcılık");
            report.AppendLine("saklanan blok  " + persistence.BlockCount);
            report.AppendLine("atılan blok    " + persistence.EvictedBlocks);
        }

        var movement = Object.FindAnyObjectByType<SnowMovementModifier>();
        if (movement != null)
        {
            report.AppendLine();
            report.AppendLine("## Oyun tarafı");
            report.AppendLine("örnek var mı    " + movement.HasSample);
            report.AppendLine("derinlik       " + (movement.Depth * 100f).ToString("0.0") + " cm");
            report.AppendLine("yoğunluk       " + movement.Density01.ToString("0.00"));
            report.AppendLine("hız çarpanı     " + movement.SpeedMultiplier.ToString("0.000"));
        }

        var profiler = manager.GetComponent<SnowProfiler>();
        if (profiler != null)
        {
            report.AppendLine();
            report.AppendLine("## Profil");

            float total = 0f;
            for (int i = 0; i < SnowProfiler.PassNames.Length; i++)
            {
                float gpu = profiler.GpuMilliseconds(i);
                float cpu = profiler.CpuMilliseconds(i);
                float shown = gpu > 0f ? gpu : cpu;
                total += shown;

                report.AppendLine(SnowProfiler.PassNames[i].PadRight(18)
                    + shown.ToString("0.000") + " ms " + (gpu > 0f ? "GPU" : "CPU"));
            }

            report.AppendLine("TOPLAM            " + total.ToString("0.000") + " ms  / hedef "
                + SnowProfiler.BudgetMilliseconds(manager.Settings.Quality).ToString("0.00"));
        }

        return report.ToString();
    }

    void UpdatePreview()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady) return;

        if (!EnsureMaterial()) return;
        EnsurePreview();

        SnowOcclusionCapture capture = manager.Occlusion;
        bool showOcclusion = source == PreviewSource.Engel
                             && capture != null && capture.OcclusionTexture != null;

        RenderTexture shown = showOcclusion ? capture.OcclusionTexture : manager.StateTexture;
        Vector2 worldCenter = showOcclusion ? capture.LastCaptureCenter : manager.AreaCenter;
        float worldSize = showOcclusion
            ? SnowConstants.OcclusionArea
            : manager.Settings.QualityData.AreaSize;

        debugMaterial.SetFloat(SnowShaderIDs.DebugMode, showOcclusion ? 5f : channel);
        debugMaterial.SetFloat(SnowShaderIDs.DebugRange, ChannelRanges[channel]);
        debugMaterial.SetFloat(SnowShaderIDs.DebugGridSize, gridSize);
        debugMaterial.SetVector(SnowShaderIDs.DebugWorldCenter,
            new Vector4(worldCenter.x, worldCenter.y, 0f, 0f));
        debugMaterial.SetFloat(SnowShaderIDs.DebugWorldSize, worldSize);

        Graphics.Blit(shown, preview, debugMaterial, 0);

        ReadCenterTexel(manager);
    }

    /// Bolgenin tam ortasindaki tekseli CPU'ya okur. Tek teksel, saniyede 10 kez —
    /// olcum araci, oyun yolunda degil.
    void ReadCenterTexel(SnowManager manager)
    {
        // PROB KAYNAKLA AYNI BİÇİMDE olmak zorunda: Graphics.CopyTexture bayt bayt
        // kopyalıyor, biçim uyuşmazsa kopya reddediliyor.
        RenderTextureFormat sourceFormat = manager.StateTexture.format;

        if (probe != null && probe.format != sourceFormat)
        {
            probe.Release();
            DestroyImmediate(probe);
            probe = null;
        }

        if (probe == null)
        {
            probe = new RenderTexture(1, 1, 0, sourceFormat)
            {
                name = "RT_SnowProbe",
                hideFlags = HideFlags.HideAndDontSave,
            };
            probe.Create();
        }

        if (probeCPU == null)
            probeCPU = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

        int resolution = manager.Settings.QualityData.Resolution;
        int center = resolution / 2;

        // 8 m yanda: test küpü 6x6 m olduğu için bu nokta kesinlikle açıkta.
        int offset = Mathf.Clamp(Mathf.RoundToInt(8f / manager.TexelSize), 1, center - 2);

        centerValue = ReadTexel(manager.StateTexture, center, center);
        openValue = ReadTexel(manager.StateTexture, center + offset, center);
        centerValid = true;

        if (!rateRunning) ResetRate();

        MeasureMass(manager);
    }

    /// Dokunun tamamındaki swe toplamını okur.
    ///
    /// GetData BLOKLUYOR ama bu bir ölçüm aracı ve saniyede on kez koşuyor; oyun
    /// yolunda değil. Karşılığında kütle testi tek karede kesin sonuç veriyor.
    void MeasureMass(SnowManager manager)
    {
        if (sumCompute == null)
        {
            sumCompute = LoadCompute();
            sumKernel = -1;
        }

        if (sumKernel < 0) sumKernel = sumCompute.FindKernel("KSumSWE");
        if (sumBuffer == null)
            sumBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);

        int resolution = manager.Settings.QualityData.Resolution;
        int groups = Mathf.CeilToInt(resolution / (float)SnowConstants.GroupSize);

        sumZero[0] = 0;
        sumBuffer.SetData(sumZero);

        sumCompute.SetInt(SnowShaderIDs.Resolution, resolution);
        sumCompute.SetTexture(sumKernel, SnowShaderIDs.State, manager.StateTexture);
        sumCompute.SetBuffer(sumKernel, SnowShaderIDs.SumOut, sumBuffer);
        sumCompute.Dispatch(sumKernel, groups, groups, 1);

        sumBuffer.GetData(sumRead);
        massTotal = sumRead[0] / 1000.0;

        if (!massRunning)
        {
            massBaseline = massTotal;
            massRunning = true;
        }
    }

    Color ReadTexel(RenderTexture source, int x, int y)
    {
        Graphics.CopyTexture(source, 0, 0, x, y, 1, 1, probe, 0, 0, 0, 0);

        RenderTexture previousTarget = RenderTexture.active;
        RenderTexture.active = probe;
        probeCPU.ReadPixels(new Rect(0f, 0f, 1f, 1f), 0, 0, false);
        probeCPU.Apply(false);
        RenderTexture.active = previousTarget;

        return probeCPU.GetPixel(0, 0);
    }

    static float HeightOf(Color state)
    {
        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, state.g);
        return state.r * SnowConstants.RhoWater / Mathf.Max(rho, 1f);
    }

    /// Birikme hızını UZUN pencerede ölçer.
    ///
    /// Kısa pencere çalışmaz: swe half hassasiyetinde ve 0.02 civarında adımı 1.5e-5 m.
    /// 3 mm/saat yağışta bir adım 18 saniye sürüyor, yani anlık fark saf yuvarlama
    /// gürültüsü olurdu. Başlangıç noktası sabitlenip üzerinden geçen süre okunuyor.
    void ResetRate()
    {
        rateStartTime = EditorApplication.timeSinceStartup;
        rateStartCenterH = HeightOf(centerValue);
        rateStartOpenH = HeightOf(openValue);
        rateStartCenterSWE = centerValue.r;
        rateStartOpenSWE = openValue.r;
        rateRunning = true;
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawParity();
        EditorGUILayout.Space();
        DrawRegion();
        EditorGUILayout.Space();
        DrawOcclusion();
        EditorGUILayout.Space();
        DrawWeather();
        EditorGUILayout.Space();
        DrawClipmap();
        EditorGUILayout.Space();
        DrawDeformation();
        EditorGUILayout.Space();
        DrawSnowfall();
        EditorGUILayout.Space();
        DrawGameSide();
        EditorGUILayout.Space();
        DrawProfiler();
        EditorGUILayout.Space();
        DrawPreview();
        EditorGUILayout.Space();
        DrawSetup();

        EditorGUILayout.EndScrollView();
    }

    // ---------------------------------------------------------------- sabit eşliği

    /// Faz 0 kabul kriteri: sabitler C# ve HLSL tarafında birebir aynı.
    /// İkisi ayrı dosyada durduğu için biri değişip diğeri unutulabilir; test bunu yakalar.
    void DrawParity()
    {
        EditorGUILayout.LabelField("Sabit eşliği (C# ↔ HLSL)", EditorStyles.boldLabel);

        if (GUILayout.Button("Sabitleri karşılaştır")) RunParityTest();

        if (string.IsNullOrEmpty(parityReport)) return;

        EditorGUILayout.HelpBox(parityReport, parityOk ? MessageType.Info : MessageType.Error);
    }

    void RunParityTest()
    {
        string path = FindHlslConstantsPath();
        if (path == null)
        {
            parityOk = false;
            parityReport = "SnowConstants.hlsl bulunamadı.";
            return;
        }

        string source = File.ReadAllText(path);
        var report = new System.Text.StringBuilder();
        int mismatches = 0;

        foreach (SnowConstants.SharedConstant shared in SnowConstants.SharedWithHlsl)
        {
            var match = Regex.Match(source, @"#define\s+" + shared.Define + @"\s+([0-9.eE+-]+)");

            if (!match.Success)
            {
                mismatches++;
                report.AppendLine(shared.Define + ": HLSL tarafında yok");
                continue;
            }

            float hlslValue = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            // Tolerans yok: ikisi de kaynak koddaki yazılı sayı, birebir eşleşmeli.
            if (hlslValue != shared.Value)
            {
                mismatches++;
                report.AppendLine(shared.Define + ": C# " + shared.Value.ToString(CultureInfo.InvariantCulture)
                                  + "  ≠  HLSL " + hlslValue.ToString(CultureInfo.InvariantCulture));
            }
        }

        parityOk = mismatches == 0;
        parityReport = parityOk
            ? SnowConstants.SharedWithHlsl.Length + " sabitin hepsi eşleşiyor."
            : mismatches + " sabit uyuşmuyor:" + System.Environment.NewLine + report;
    }

    static string FindHlslConstantsPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("SnowConstants"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("SnowConstants.hlsl")) return path;
        }

        return null;
    }

    // ------------------------------------------------------------------ bölge/snap

    void DrawRegion()
    {
        EditorGUILayout.LabelField("Bölge ve snap", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady)
        {
            EditorGUILayout.HelpBox("SnowManager etkin değil. Play'e bas.", MessageType.Info);
            return;
        }

        SnowQualityData quality = manager.Settings.QualityData;

        EditorGUILayout.LabelField("Kalite", manager.Settings.Quality.ToString());
        EditorGUILayout.LabelField("Çözünürlük", quality.Resolution + " × " + quality.Resolution);
        EditorGUILayout.LabelField("Alan", quality.AreaSize.ToString("0.##") + " m");
        EditorGUILayout.LabelField("Teksel", (manager.TexelSize * 100f).ToString("0.###") + " cm");

        float snapWorld = manager.SnapTexels * manager.TexelSize;
        EditorGUILayout.LabelField("Snap adımı",
            manager.SnapTexels + " teksel = " + (snapWorld * 100f).ToString("0.##") + " cm");

        Vector2 center = manager.AreaCenter;
        EditorGUILayout.LabelField("Merkez",
            center.x.ToString("0.0000") + " , " + center.y.ToString("0.0000"));

        Vector2Int centerTexel = manager.CenterTexel;
        EditorGUILayout.LabelField("Merkez teksel", centerTexel.x + " , " + centerTexel.y);

        // TEK CEVAPLI TEST: merkez teksel snap adımının tam katı olmak zorunda.
        // Kalan sıfırdan farklıysa snap bozuk ve izler teksel altı kayar.
        int remainderX = Mathf.Abs(centerTexel.x % manager.SnapTexels);
        int remainderY = Mathf.Abs(centerTexel.y % manager.SnapTexels);
        bool aligned = remainderX == 0 && remainderY == 0;

        EditorGUILayout.HelpBox(
            aligned
                ? "Snap doğru: merkez tam teksel ızgarasında (kalan 0, 0)."
                : "SNAP BOZUK: kalan " + remainderX + ", " + remainderY,
            aligned ? MessageType.Info : MessageType.Error);

        Vector2Int last = manager.LastScrollTexels;
        EditorGUILayout.LabelField("Son kaydırma", last.x + " , " + last.y + " teksel");
    }

    // -------------------------------------------------------------------- engel

    /// §4.2'nin "her frame değil" kuralının ölçümü. Profiler'a bakmaya gerek yok:
    /// yenileme sayısı kare sayısından çok küçük olmalı.
    void DrawOcclusion()
    {
        EditorGUILayout.LabelField("Gökyüzü görünürlüğü", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null || manager.Occlusion == null) return;

        SnowOcclusionCapture capture = manager.Occlusion;

        int layer = LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName);
        EditorGUILayout.LabelField("Engel katmanı",
            layer < 0 ? "YOK — Sahneyi kur" : SnowOcclusionCapture.OccluderLayerName + " (" + layer + ")");

        if (!capture.HasCaptured)
        {
            EditorGUILayout.HelpBox("Henüz yakalama yapılmadı.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Çözünürlük",
            manager.Settings.QualityData.OcclusionResolution.ToString());
        EditorGUILayout.LabelField("Alan", SnowConstants.OcclusionArea.ToString("0.##") + " m");
        EditorGUILayout.LabelField("Yenileme sayısı", capture.CaptureCount.ToString());
        EditorGUILayout.LabelField("Kare sayısı", Time.frameCount.ToString());

        Vector2 center = manager.AreaCenter;
        float drift = Vector2.Distance(center, capture.LastCaptureCenter);
        EditorGUILayout.LabelField("Son yenilemeden beri kayma",
            drift.ToString("0.00") + " m  (eşik " + SnowConstants.OcclusionMoveThreshold.ToString("0.#") + " m)");

        bool withinThreshold = drift <= SnowConstants.OcclusionMoveThreshold + 0.01f;
        EditorGUILayout.HelpBox(
            withinThreshold
                ? "Eşik tutuyor: harita yalnız 4 m'den fazla kayınca yenileniyor."
                : "Eşik AŞILDI: kayma " + drift.ToString("0.00") + " m ama yenileme olmadı.",
            withinThreshold ? MessageType.Info : MessageType.Error);
    }

    // --------------------------------------------------------------------- hava

    static readonly string[] PresetNames = { "Clear", "Light", "Moderate", "Heavy", "Blizzard" };

    void DrawWeather()
    {
        EditorGUILayout.LabelField("Hava ve birikme", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null || manager.Weather == null)
        {
            EditorGUILayout.HelpBox("SnowWeather yok. Sahneyi kur çalıştır.", MessageType.Info);
            return;
        }

        SnowWeather weather = manager.Weather;

        int presetCount = Mathf.Min(PresetNames.Length, weather.PresetCount);
        if (presetCount > 0)
        {
            var names = new string[presetCount];
            System.Array.Copy(PresetNames, names, presetCount);

            int next = EditorGUILayout.Popup("Yağış preseti", weather.ActivePreset, names);
            if (next != weather.ActivePreset)
            {
                weather.SetPreset(next);
                ResetRate();
            }
        }

        float speed = EditorGUILayout.Slider("Kar zamanı hızı (x)", SnowManager.SimulationSpeed, 1f, 2000f);
        if (!Mathf.Approximately(speed, SnowManager.SimulationSpeed))
        {
            SnowManager.SimulationSpeed = speed;
            ResetRate();
        }

        EditorGUILayout.LabelField("  1 gerçek saniye =",
            (speed / 60f).ToString("0.0") + " kar dakikası");

        float temperature = EditorGUILayout.Slider("Sıcaklık (C)", weather.TemperatureC, -20f, 10f);
        if (!Mathf.Approximately(temperature, weather.TemperatureC))
        {
            weather.SetTemperature(temperature);
            ResetRate();
        }

        EditorGUILayout.LabelField("Yağış hızı",
            (weather.SnowfallSWERate * 1000f * 3600f).ToString("0.###") + " mm SWE/saat");
        EditorGUILayout.LabelField("Rüzgâr", weather.WindSpeed.ToString("0.0") + " m/s");
        EditorGUILayout.LabelField("Taze kar ıslaklığı", weather.SnowWetness.ToString("0.00"));
        EditorGUILayout.LabelField("Kaplama", weather.Coverage.ToString("0.00"));

        if (!centerValid) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Birikme ölçümü", EditorStyles.miniBoldLabel);

        double elapsed = EditorApplication.timeSinceStartup - rateStartTime;

        float centerH = HeightOf(centerValue);
        float openH = HeightOf(openValue);

        EditorGUILayout.LabelField("  geçen süre", elapsed.ToString("0") + " s");
        EditorGUILayout.LabelField("  merkez h  (çatı altı)",
            (centerH * 100f).ToString("0.000") + " cm   wet " + centerValue.b.ToString("0.00"));
        EditorGUILayout.LabelField("  8 m yanda h  (açık)",
            (openH * 100f).ToString("0.000") + " cm   wet " + openValue.b.ToString("0.00"));

        if (elapsed > 1.0)
        {
            // Kar saati cinsinden: geçen gerçek süre x hız.
            float hours = (float)(elapsed * SnowManager.SimulationSpeed / 3600.0);
            EditorGUILayout.LabelField("  merkez hızı",
                ((centerH - rateStartCenterH) * 100f / hours).ToString("0.00") + " cm/saat");
            EditorGUILayout.LabelField("  açık alan hızı",
                ((openH - rateStartOpenH) * 100f / hours).ToString("0.00") + " cm/saat");

            // KARİŞIMSIZ ÖLÇÜM. h hem yağıştan hem oturmadan etkileniyor; ikisi tek
            // sayıda karışıyor. swe yalnız yağış ve erimeyle değişiyor — presetin
            // mm/saat değerini doğrudan sınayan satır bu.
            EditorGUILayout.LabelField("  merkez swe hızı",
                ((centerValue.r - rateStartCenterSWE) * 1000f / hours).ToString("0.00") + " mm/saat");
            EditorGUILayout.LabelField("  açık alan swe hızı",
                ((openValue.r - rateStartOpenSWE) * 1000f / hours).ToString("0.00") + " mm/saat");
        }

        EditorGUILayout.LabelField("Hız x600'de 1 gerçek saniye = 10 kar dakikası;",
                                   EditorStyles.miniLabel);
        EditorGUILayout.LabelField("ölçüm 10 saniyede oturur.", EditorStyles.miniLabel);

        if (GUILayout.Button("Ölçümü sıfırla")) ResetRate();
    }

    // ------------------------------------------------------------------ clipmap

    void DrawClipmap()
    {
        EditorGUILayout.LabelField("Kar yüzeyi (clipmap)", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Play'e bas.", MessageType.Info);
            return;
        }

        var clipmap = manager.GetComponent<SnowClipmap>();
        if (clipmap == null)
        {
            EditorGUILayout.HelpBox("SnowClipmap yok. Sahneyi kur çalıştır.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Halka sayısı", clipmap.RingCount.ToString());
        EditorGUILayout.LabelField("Üçgen sayısı", clipmap.TriangleCount.ToString("N0"));
        EditorGUILayout.LabelField("Çizim çağrısı", clipmap.RingCount + " (halka başına bir)");

        // §7.1: 240x240 ızgara, iç delik 80x80, dört halka -> 422 400 üçgen.
        bool matchesSpec = clipmap.RingCount != 4 || clipmap.TriangleCount == 422400;

        EditorGUILayout.HelpBox(
            matchesSpec
                ? "Üçgen sayısı şartnameyle uyuşuyor."
                : "ŞARTNAMEDEN SAPMA: 4 halkada 422 400 üçgen beklenir.",
            matchesSpec ? MessageType.Info : MessageType.Error);
    }

    // -------------------------------------------------------------- deformasyon

    void DrawDeformation()
    {
        EditorGUILayout.LabelField("Deformasyon", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null || manager.Deformers == null)
        {
            EditorGUILayout.HelpBox("Play'e bas.", MessageType.Info);
            return;
        }

        SnowDeformerRegistry registry = manager.Deformers;

        EditorGUILayout.LabelField("Etkin deformer",
            registry.ActiveCount + " / " + registry.Capacity);
        EditorGUILayout.LabelField("En büyük temas",
            (registry.MaxContactExtent * 100f).ToString("0.0") + " cm köşegen");
        EditorGUILayout.LabelField("Damga atlası",
            manager.Settings.StampAtlas != null ? manager.Settings.StampAtlas.name : "YOK");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kütle testi (§5 kabul kriteri)", EditorStyles.miniBoldLabel);

        EditorGUILayout.LabelField("  Σ swe", massTotal.ToString("0.000") + " m·teksel");
        EditorGUILayout.LabelField("  başlangıç", massBaseline.ToString("0.000"));

        double drift = massBaseline > 1e-6 ? (massTotal - massBaseline) / massBaseline * 100.0 : 0.0;
        EditorGUILayout.LabelField("  sapma", drift.ToString("0.000") + " %  (tolerans 0.5 %)");

        bool within = System.Math.Abs(drift) <= 0.5;
        EditorGUILayout.HelpBox(
            within
                ? "Kütle korunuyor. NOT: bu test yalnız yağış Clear ve sıcaklık eksi iken geçerli."
                : "KÜTLE SAPMASI " + drift.ToString("0.00") + " %",
            within ? MessageType.Info : MessageType.Error);

        if (GUILayout.Button("Kütle testini sıfırla")) massRunning = false;
    }

    // ------------------------------------------------------------------- yağış

    void DrawSnowfall()
    {
        EditorGUILayout.LabelField("Kar yağışı", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null) { EditorGUILayout.HelpBox("Play'e bas.", MessageType.Info); return; }

        var snowfall = manager.GetComponent<SnowfallController>();
        if (snowfall == null)
        {
            EditorGUILayout.HelpBox("SnowfallController yok. Sahneyi kur çalıştır.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Etkin tane", snowfall.ActiveFlakes.ToString("N0"));
        EditorGUILayout.LabelField("Etkin savrulma", snowfall.ActiveSpindrift.ToString("N0"));
        EditorGUILayout.LabelField("Gevşek kar oranı", snowfall.LooseSnowFraction.ToString("0.000"));

        var cascade = manager.FarCascade;
        var persistence = manager.GetComponent<SnowPersistence>();

        if (cascade != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Uzak kaskad", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("  alan", SnowFarCascade.AreaSize + " m / " + SnowFarCascade.Resolution);
            EditorGUILayout.LabelField("  teksel", (cascade.TexelSize * 100f).ToString("0.0") + " cm");
        }

        if (persistence != null)
        {
            EditorGUILayout.LabelField("  saklanan blok", persistence.BlockCount + " / 512");
            EditorGUILayout.LabelField("  atılan blok", persistence.EvictedBlocks.ToString());
        }
    }

    // -------------------------------------------------------------- oyun tarafı

    void DrawGameSide()
    {
        EditorGUILayout.LabelField("Oyun tarafı", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null) { EditorGUILayout.HelpBox("Play'e bas.", MessageType.Info); return; }

        var movement = Object.FindAnyObjectByType<SnowMovementModifier>();
        if (movement == null)
        {
            EditorGUILayout.HelpBox("SnowMovementModifier yok. Sahneyi kur çalıştır.", MessageType.Info);
            return;
        }

        if (!movement.HasSample)
        {
            EditorGUILayout.HelpBox("Henüz örnek gelmedi (geri okuma iki kare gecikmeli).",
                                    MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Ayak altı derinlik", (movement.Depth * 100f).ToString("0.0") + " cm");
        EditorGUILayout.LabelField("Yoğunluk", movement.Density01.ToString("0.00"));
        EditorGUILayout.LabelField("Islaklık", movement.Wetness.ToString("0.00"));
        EditorGUILayout.LabelField("Hız çarpanı", movement.SpeedMultiplier.ToString("0.000"));

        var audio = Object.FindAnyObjectByType<SnowFootstepAudio>();
        if (audio != null)
            EditorGUILayout.LabelField("Son ayak sesi",
                audio.LastSurface + (audio.LastWasWet ? " (ıslak)" : " (kuru)"));
    }

    // ------------------------------------------------------------------- profil

    void DrawProfiler()
    {
        EditorGUILayout.LabelField("Profil (§11.2)", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;
        if (manager == null) { EditorGUILayout.HelpBox("Play'e bas.", MessageType.Info); return; }

        var profiler = manager.GetComponent<SnowProfiler>();
        if (profiler == null)
        {
            EditorGUILayout.HelpBox("SnowProfiler yok. Sahneyi kur çalıştır.", MessageType.Info);
            return;
        }

        float total = 0f;

        for (int i = 0; i < SnowProfiler.PassNames.Length; i++)
        {
            float gpu = profiler.GpuMilliseconds(i);
            float cpu = profiler.CpuMilliseconds(i);
            float shown = gpu > 0f ? gpu : cpu;

            total += shown;

            EditorGUILayout.LabelField("  " + SnowProfiler.PassNames[i],
                shown.ToString("0.000") + " ms" + (gpu > 0f ? " (GPU)" : " (CPU)"));
        }

        float budget = SnowProfiler.BudgetMilliseconds(manager.Settings.Quality);

        EditorGUILayout.LabelField("  TOPLAM", total.ToString("0.000") + " ms  / hedef "
                                   + budget.ToString("0.00") + " ms");

        EditorGUILayout.HelpBox(
            total <= budget
                ? "Bütçe içinde. NOT: hedef, garanti değil."
                : "BÜTÇE AŞILDI: " + total.ToString("0.00") + " ms",
            total <= budget ? MessageType.Info : MessageType.Warning);

        if (!profiler.HasGpuTiming)
            EditorGUILayout.LabelField("GPU zamanlaması kapalı; CPU süresi gösteriliyor.",
                                       EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------------- önizleme

    void DrawPreview()
    {
        EditorGUILayout.LabelField("Durum dokusu", EditorStyles.boldLabel);

        source = (PreviewSource)EditorGUILayout.Popup("Doku", (int)source, SourceNames);

        using (new EditorGUI.DisabledScope(source == PreviewSource.Engel))
            channel = EditorGUILayout.Popup("Kanal", channel, ChannelNames);

        gridSize = EditorGUILayout.Slider("Dünya ızgarası (m)", gridSize, 0f, 4f);

        EditorGUILayout.LabelField("Izgara dünya koordinatından çiziliyor: yürürken",
                                   EditorStyles.miniLabel);
        EditorGUILayout.LabelField("kaymamalı, tam teksel adımlarla sıçramalı.",
                                   EditorStyles.miniLabel);

        DrawCenterReadout();

        if (preview == null) return;

        float side = Mathf.Min(position.width - 24f, 384f);
        Rect rect = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, preview);
    }

    /// TEK CEVAPLI TEST: merkez tekselin degeri ayardaki varsayilanla ayni mi.
    /// Ayniysa KClear kostu; sifirsa dokuya hic yazilmadi.
    void DrawCenterReadout()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady || !centerValid) return;

        if (source != PreviewSource.Durum) return;

        SnowSettings settings = manager.Settings;

        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, centerValue.g);
        float height = centerValue.r * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        EditorGUILayout.LabelField("Merkez teksel — okunan / beklenen", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  swe",
            centerValue.r.ToString("0.00000") + "  /  " + settings.DefaultSWE.ToString("0.00000"));
        EditorGUILayout.LabelField("  rhoN",
            centerValue.g.ToString("0.00000") + "  /  " + settings.DefaultRhoN.ToString("0.00000"));
        EditorGUILayout.LabelField("  wet",
            centerValue.b.ToString("0.00000") + "  /  " + settings.DefaultWet.ToString("0.00000"));
        EditorGUILayout.LabelField("  disturb", centerValue.a.ToString("0.00000") + "  /  0.00000");
        EditorGUILayout.LabelField("  h (türetilmiş)", height.ToString("0.0000") + " m");

        // Doku artık float; sapma yalnız birikmeden gelir.
        bool matches = Mathf.Abs(centerValue.r - settings.DefaultSWE) < 1e-3f
                    && Mathf.Abs(centerValue.g - settings.DefaultRhoN) < 1e-3f;

        EditorGUILayout.HelpBox(
            matches
                ? "KClear çalıştı: doku varsayılan kar durumuyla dolu."
                : "DOKU DOLU DEĞİL: okunan değer varsayılana uymuyor.",
            matches ? MessageType.Info : MessageType.Error);
    }

    bool EnsureMaterial()
    {
        if (debugMaterial != null) return true;

        Shader shader = Shader.Find("Hidden/Snow/Debug");
        if (shader == null)
            throw new System.InvalidOperationException("Hidden/Snow/Debug shader'ı bulunamadı.");

        debugMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return true;
    }

    void EnsurePreview()
    {
        if (preview != null) return;

        preview = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
        {
            name = "RT_SnowDebugPreview",
            hideFlags = HideFlags.HideAndDontSave,
        };

        preview.Create();
    }

    // --------------------------------------------------------------------- kurulum

    void DrawSetup()
    {
        EditorGUILayout.LabelField("Kurulum", EditorStyles.boldLabel);

        writeLog = GUILayout.Toggle(writeLog, "Raporu Logs/snow.log'a yaz (2 s'de bir)");

        if (GUILayout.Button("Sahneyi kur")) SetupScene();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Teşhis: engel testi", EditorStyles.miniBoldLabel);

        bool exists = GameObject.Find(TestOccluderName) != null;

        using (new EditorGUI.DisabledScope(exists))
            if (GUILayout.Button("Oyuncunun üstüne test küpü koy")) SpawnTestOccluder();

        using (new EditorGUI.DisabledScope(!exists))
            if (GUILayout.Button("Test küpünü sil")) RemoveTestOccluder();
    }

    const string TestOccluderName = "Kar Engel Testi";

    /// Oyuncunun başının üstüne SnowOccluder katmanında bir küp koyar. Engel
    /// haritasının çalıştığını gösteren tek cevaplı test: altında koyu bir kare çıkar.
    static void SpawnTestOccluder()
    {
        int layer = LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName);
        if (layer < 0)
            throw new System.InvalidOperationException(
                SnowOcclusionCapture.OccluderLayerName + " katmanı yok. Önce Sahneyi kur.");

        var player = Object.FindAnyObjectByType<FirstPersonController>();
        if (player == null)
            throw new System.InvalidOperationException("Sahnede FirstPersonController yok.");

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = TestOccluderName;
        cube.layer = layer;

        // 3 m yukarıda: oyuncunun başının üstünde ama saçak gecisinin (0.05–0.40 m)
        // çok üstünde, yani harita tam koyu çıkmalı.
        cube.transform.position = player.transform.position + Vector3.up * 3f;
        cube.transform.localScale = new Vector3(6f, 0.5f, 6f);

        // Çarpışma yok: oyuncu küpün altında serbest yürüsün.
        Object.DestroyImmediate(cube.GetComponent<Collider>());

        Undo.RegisterCreatedObjectUndo(cube, "Kar engel testi");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cube.scene);

        if (SnowManager.Active != null) SnowManager.Active.MarkOcclusionDirty();
    }

    static void RemoveTestOccluder()
    {
        var cube = GameObject.Find(TestOccluderName);
        if (cube == null) return;

        Scene scene = cube.scene;
        Undo.DestroyObjectImmediate(cube);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        if (SnowManager.Active != null) SnowManager.Active.MarkOcclusionDirty();
    }

    /// Ayar asset'ini, sahne bileşenini ve renderer özelliğini kurar. Kullanıcının
    /// Inspector'da elle bağlaması gerekmesin diye.
    static void SetupScene()
    {
        // KATMAN İLK. SnowOcclusionCapture katman yoksa OnEnable'da fırlatıyor.
        EnsureOccluderLayer();

        SnowSettings settings = LoadOrCreateSettings();
        ComputeShader compute = LoadCompute();

        // Engel renderer'ı ÖZELLİKLER EKLENMEDEN ÖNCE yaratılıyor ki AddFeatureToRenderers
        // ona da SnowRendererFeature'ı taksın.
        UniversalRendererData occlusionRenderer = LoadOrCreateOcclusionRenderer();
        int occlusionRendererIndex = RegisterOcclusionRenderer(occlusionRenderer);

        var manager = Object.FindAnyObjectByType<SnowManager>();
        if (manager == null)
        {
            var go = new GameObject("Kar Sistemi");
            manager = go.AddComponent<SnowManager>();
            Undo.RegisterCreatedObjectUndo(go, "Kar sistemi kur");
        }

        var capture = manager.GetComponent<SnowOcclusionCapture>();
        if (capture == null) capture = manager.gameObject.AddComponent<SnowOcclusionCapture>();

        var captureSerialized = new SerializedObject(capture);
        captureSerialized.FindProperty("settings").objectReferenceValue = settings;
        captureSerialized.FindProperty("occlusionShader").objectReferenceValue =
            Shader.Find("Hidden/Snow/OcclusionDepth");
        captureSerialized.ApplyModifiedProperties();

        var ground = manager.GetComponent<SnowGroundHeight>();
        if (ground == null) ground = manager.gameObject.AddComponent<SnowGroundHeight>();

        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            throw new System.InvalidOperationException("Sahnede Terrain yok; zemin yüksekliği pişirilemez.");

        var groundSerialized = new SerializedObject(ground);
        groundSerialized.FindProperty("settings").objectReferenceValue = settings;
        groundSerialized.FindProperty("terrain").objectReferenceValue = terrain;
        groundSerialized.ApplyModifiedProperties();

        var weather = manager.GetComponent<SnowWeather>();
        if (weather == null) weather = manager.gameObject.AddComponent<SnowWeather>();

        SnowWeatherPreset[] presets = LoadOrCreateWeatherPresets();

        var weatherSerialized = new SerializedObject(weather);
        SerializedProperty presetList = weatherSerialized.FindProperty("presets");
        presetList.arraySize = presets.Length;
        for (int i = 0; i < presets.Length; i++)
            presetList.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
        weatherSerialized.ApplyModifiedProperties();

        var player = Object.FindAnyObjectByType<FirstPersonController>();
        if (player == null)
            throw new System.InvalidOperationException("Sahnede FirstPersonController yok; takip hedefi bağlanamadı.");

        // HAVA ZINCIRI BAGLANIYOR. F1'deki tek sürgü artık kar sistemini de sürüyor;
        // iki ayrı panelden hava ayarlamak bitti.
        var weatherChainSerialized = new SerializedObject(weather);
        weatherChainSerialized.FindProperty("weatherState").objectReferenceValue =
            Object.FindAnyObjectByType<WeatherState>();
        weatherChainSerialized.FindProperty("temperature").objectReferenceValue =
            Object.FindAnyObjectByType<TemperatureField>();
        weatherChainSerialized.FindProperty("altitudeSource").objectReferenceValue = player.transform;
        weatherChainSerialized.ApplyModifiedProperties();


        var coverage = manager.GetComponent<SnowCoverageDriver>();
        if (coverage == null) coverage = manager.gameObject.AddComponent<SnowCoverageDriver>();

        var coverageSerialized = new SerializedObject(coverage);
        coverageSerialized.FindProperty("weather").objectReferenceValue = weather;
        coverageSerialized.FindProperty("occlusion").objectReferenceValue = capture;
        coverageSerialized.ApplyModifiedProperties();

        var registry = manager.GetComponent<SnowDeformerRegistry>();
        if (registry == null) registry = manager.gameObject.AddComponent<SnowDeformerRegistry>();

        var registrySerialized = new SerializedObject(registry);
        registrySerialized.FindProperty("settings").objectReferenceValue = settings;
        registrySerialized.ApplyModifiedProperties();

        var footsteps = player.GetComponent<SnowFootstepDriver>();
        if (footsteps == null) footsteps = player.gameObject.AddComponent<SnowFootstepDriver>();

        SerializedObject footstepsSerialized = new SerializedObject(footsteps);
        footstepsSerialized.FindProperty("registry").objectReferenceValue = registry;
        footstepsSerialized.FindProperty("followTarget").objectReferenceValue = player.transform;
        footstepsSerialized.ApplyModifiedProperties();

        var clipmap = manager.GetComponent<SnowClipmap>();
        if (clipmap == null) clipmap = manager.gameObject.AddComponent<SnowClipmap>();

        var clipmapSerialized = new SerializedObject(clipmap);
        clipmapSerialized.FindProperty("settings").objectReferenceValue = settings;
        clipmapSerialized.FindProperty("material").objectReferenceValue = LoadOrCreateSnowMaterial();
        clipmapSerialized.FindProperty("followTarget").objectReferenceValue = player.transform;
        clipmapSerialized.FindProperty("groundHeight").objectReferenceValue = ground;
        clipmapSerialized.ApplyModifiedProperties();

        // --- Faz 10: uzak kaskad ve kalıcılık ---
        var cascadeComponent = manager.GetComponent<SnowFarCascade>();
        if (cascadeComponent == null) cascadeComponent = manager.gameObject.AddComponent<SnowFarCascade>();

        var persistence = manager.GetComponent<SnowPersistence>();
        if (persistence == null) persistence = manager.gameObject.AddComponent<SnowPersistence>();

        var cascadeSerialized = new SerializedObject(cascadeComponent);
        cascadeSerialized.FindProperty("manager").objectReferenceValue = manager;
        cascadeSerialized.FindProperty("simCompute").objectReferenceValue = compute;
        // KALICILIK VARSAYILAN KAPALI. Gezinen yakalama ile geri yükleme henüz
        // doğrulanmadı ve depo taşınca (LRU) kaskada eski blok yazıp uzak alanda
        // siyah lekeler bırakıyor. Kaydı DECISIONS.md'de.
        cascadeSerialized.FindProperty("persistence").objectReferenceValue = null;
        cascadeSerialized.ApplyModifiedProperties();

        var persistenceSerialized = new SerializedObject(persistence);
        persistenceSerialized.FindProperty("cascade").objectReferenceValue = cascadeComponent;
        persistenceSerialized.ApplyModifiedProperties();

        if (manager.GetComponent<SnowProfiler>() == null)
            manager.gameObject.AddComponent<SnowProfiler>();

        // --- Faz 9: oyun tarafı ---
        var sampler = manager.GetComponent<SnowSampler>();
        if (sampler == null) sampler = manager.gameObject.AddComponent<SnowSampler>();

        var samplerSerialized = new SerializedObject(sampler);
        samplerSerialized.FindProperty("manager").objectReferenceValue = manager;
        samplerSerialized.FindProperty("followTarget").objectReferenceValue = player.transform;
        samplerSerialized.ApplyModifiedProperties();

        var movement = player.GetComponent<SnowMovementModifier>();
        if (movement == null) movement = player.gameObject.AddComponent<SnowMovementModifier>();

        var movementSerialized = new SerializedObject(movement);
        movementSerialized.FindProperty("sampler").objectReferenceValue = sampler;
        movementSerialized.FindProperty("player").objectReferenceValue = player;
        movementSerialized.ApplyModifiedProperties();

        var footstepAudio = player.GetComponent<SnowFootstepAudio>();
        if (footstepAudio == null) footstepAudio = player.gameObject.AddComponent<SnowFootstepAudio>();

        var audioSource = player.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = player.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        var footstepAudioSerialized = new SerializedObject(footstepAudio);
        footstepAudioSerialized.FindProperty("sampler").objectReferenceValue = sampler;
        footstepAudioSerialized.FindProperty("source").objectReferenceValue = audioSource;
        footstepAudioSerialized.ApplyModifiedProperties();

        footstepsSerialized = new SerializedObject(footsteps);
        footstepsSerialized.FindProperty("footstepAudio").objectReferenceValue = footstepAudio;
        footstepsSerialized.FindProperty("sampler").objectReferenceValue = sampler;
        footstepsSerialized.ApplyModifiedProperties();

        // --- Faz 8: yağış ---
        var snowfall = manager.GetComponent<SnowfallController>();
        if (snowfall == null) snowfall = manager.gameObject.AddComponent<SnowfallController>();

        var snowfallSerialized = new SerializedObject(snowfall);
        snowfallSerialized.FindProperty("manager").objectReferenceValue = manager;
        snowfallSerialized.FindProperty("weather").objectReferenceValue = weather;
        snowfallSerialized.FindProperty("flakeCompute").objectReferenceValue = LoadFlakeCompute();
        snowfallSerialized.FindProperty("flakeShader").objectReferenceValue =
            Shader.Find("Hidden/Snow/Flakes");
        snowfallSerialized.FindProperty("followCamera").objectReferenceValue =
            Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        snowfallSerialized.ApplyModifiedProperties();

        var atmosphere = manager.GetComponent<SnowAtmosphereDriver>();
        if (atmosphere == null) atmosphere = manager.gameObject.AddComponent<SnowAtmosphereDriver>();

        var atmosphereSerialized = new SerializedObject(atmosphere);
        atmosphereSerialized.FindProperty("weather").objectReferenceValue = weather;
        atmosphereSerialized.ApplyModifiedProperties();

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("settings").objectReferenceValue = settings;
        serialized.FindProperty("simCompute").objectReferenceValue = compute;
        serialized.FindProperty("followTarget").objectReferenceValue = player.transform;
        serialized.FindProperty("occlusion").objectReferenceValue = capture;
        serialized.FindProperty("groundHeight").objectReferenceValue = ground;
        serialized.FindProperty("weather").objectReferenceValue = weather;
        serialized.FindProperty("deformers").objectReferenceValue = registry;
        serialized.FindProperty("farCascade").objectReferenceValue = cascadeComponent;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        var settingsSerialized = new SerializedObject(settings);
        settingsSerialized.FindProperty("occlusionRendererIndex").intValue = occlusionRendererIndex;
        settingsSerialized.FindProperty("stampAtlas").objectReferenceValue = SnowStampGenerator.Generate();
        settingsSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);

        int added = AddFeatureToRenderers();

        AssetDatabase.SaveAssets();
        Debug.Log("Kar sistemi kuruldu. Renderer özelliği eklenen renderer sayısı: " + added
                  + ". Engel renderer'sı sıra numarası: " + occlusionRendererIndex + ".");
    }

    /// SnowOccluder katmanını ilk boş kullanıcı yuvasına açar. 0–7 arası Unity'nin
    /// kendi yuvaları, kullanıcı katmanları 8'den başlıyor.
    static void EnsureOccluderLayer()
    {
        if (LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName) >= 0) return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            throw new System.InvalidOperationException("TagManager.asset okunamadı.");

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = SnowOcclusionCapture.OccluderLayerName;
            tagManager.ApplyModifiedProperties();
            Debug.Log(SnowOcclusionCapture.OccluderLayerName + " katmanı açıldı: yuva " + i);
            return;
        }

        throw new System.InvalidOperationException("Boş katman yuvası kalmamış.");
    }

    const string OcclusionRendererPath = "Assets/Settings/SnowOcclusionRenderer.asset";

    /// Engel kamerasının kendi renderer'ı. Ana renderer'da fiziksel gökyüzü, bulut ve
    /// sis geçişleri var; onlar tek kanallı RHalf hedefte render graph'i çökertiyor.
    static UniversalRendererData LoadOrCreateOcclusionRenderer()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(OcclusionRendererPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.name = "SnowOcclusionRenderer";
            AssetDatabase.CreateAsset(data, OcclusionRendererPath);

            // Kaynakları ana renderer'dan kopyala: taze bir örnekte null kalıyorlar.
            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (source != null)
            {
                var from = new SerializedObject(source);
                var to = new SerializedObject(data);

                to.FindProperty("postProcessData").objectReferenceValue =
                    from.FindProperty("postProcessData").objectReferenceValue;
                to.FindProperty("xrSystemData").objectReferenceValue =
                    from.FindProperty("xrSystemData").objectReferenceValue;
                to.ApplyModifiedProperties();
            }
        }

        int layer = LayerMask.NameToLayer(SnowOcclusionCapture.OccluderLayerName);

        var serialized = new SerializedObject(data);
        serialized.FindProperty("m_OpaqueLayerMask").intValue = layer >= 0 ? 1 << layer : 0;
        serialized.FindProperty("m_TransparentLayerMask").intValue = 0;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);

        return data;
    }

    /// Renderer'ı her URP asset'inin listesine ekler ve sıra numarasını döndürür.
    /// Numara asset'ler arasında AYNI olmak zorunda: kamera tek bir numara tutuyor.
    static int RegisterOcclusionRenderer(UniversalRendererData data)
    {
        int index = -1;

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(path);
            if (asset == null) continue;

            var serialized = new SerializedObject(asset);
            SerializedProperty list = serialized.FindProperty("m_RendererDataList");
            if (list == null) continue;

            int found = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { found = i; break; }
            }

            if (found < 0)
            {
                list.arraySize++;
                found = list.arraySize - 1;
                list.GetArrayElementAtIndex(found).objectReferenceValue = data;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }

            if (index < 0) index = found;
            else if (index != found)
                throw new System.InvalidOperationException(
                    "Engel renderer'sı URP asset'lerinde farklı sıralarda: " + index + " ve " + found + ".");
        }

        if (index < 0)
            throw new System.InvalidOperationException("Assets altında URP asset'i bulunamadı.");

        return index;
    }

    const string PresetFolder = "Assets/Settings/Snow";
    const string SnowMaterialPath = "Assets/Settings/Snow/SnowLit.mat";

    static Material LoadOrCreateSnowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SnowMaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find("To The Summit/Snow Lit");
        if (shader == null)
            throw new System.InvalidOperationException("'To The Summit/Snow Lit' shader'\u0131 bulunamad\u0131.");

        if (!AssetDatabase.IsValidFolder(PresetFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Snow");

        material = new Material(shader) { name = "SnowLit" };
        AssetDatabase.CreateAsset(material, SnowMaterialPath);

        return material;
    }

    /// §10.3 tablosunun beş satırı. Sayilar tablodan birebir.
    /// flakeRate, capacity, mm SWE/saat, rüzgâr min–max, sis çarpanı, spindrift.
    static SnowWeatherPreset[] LoadOrCreateWeatherPresets()
    {
        if (!AssetDatabase.IsValidFolder(PresetFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Snow");

        var presets = new SnowWeatherPreset[5];

        presets[0] = MakePreset("Clear", 0f, 0, 0f, 0f, 2f, 1.00f, false);
        presets[1] = MakePreset("Light", 1200f, 4000, 0.3f, 0f, 3f, 1.15f, false);
        presets[2] = MakePreset("Moderate", 4000f, 12000, 1.2f, 2f, 6f, 1.50f, false);
        presets[3] = MakePreset("Heavy", 9000f, 24000, 3.0f, 4f, 10f, 2.40f, true);
        presets[4] = MakePreset("Blizzard", 16000f, 40000, 5.0f, 10f, 20f, 4.00f, true);

        return presets;
    }

    static SnowWeatherPreset MakePreset(string presetName, float rate, int capacity, float mmPerHour,
                                        float windMin, float windMax, float fog, bool spindrift)
    {
        string path = PresetFolder + "/SnowWeather_" + presetName + ".asset";

        var preset = AssetDatabase.LoadAssetAtPath<SnowWeatherPreset>(path);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<SnowWeatherPreset>();
            AssetDatabase.CreateAsset(preset, path);
        }

        preset.Configure(rate, capacity, mmPerHour, windMin, windMax, fog, spindrift);
        EditorUtility.SetDirty(preset);

        return preset;
    }

    static SnowSettings LoadOrCreateSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SnowSettings"))
            return AssetDatabase.LoadAssetAtPath<SnowSettings>(AssetDatabase.GUIDToAssetPath(guid));

        var settings = ScriptableObject.CreateInstance<SnowSettings>();
        AssetDatabase.CreateAsset(settings, "Assets/Settings/SnowSettings.asset");
        return settings;
    }

    static ComputeShader LoadFlakeCompute()
    {
        foreach (string guid in AssetDatabase.FindAssets("SnowFlakes t:ComputeShader"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("SnowFlakes.compute")) return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }

        throw new System.InvalidOperationException("SnowFlakes.compute bulunamadı.");
    }

    static ComputeShader LoadCompute()
    {
        foreach (string guid in AssetDatabase.FindAssets("SnowSim t:ComputeShader"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("SnowSim.compute")) return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }

        throw new System.InvalidOperationException("SnowSim.compute bulunamadı.");
    }

    /// Özelliği her UniversalRendererData'ya ekler. Zaten varsa dokunmaz.
    static int AddFeatureToRenderers()
    {
        int added = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // PAKET ICINDEKILER ATLANIR. URP'nin kendi sablon renderer'i da bu aramaya
            // dusuyor; ona yazmak "immutable package altered" uyarisi uretiyor ve
            // degisiklik paket guncellemesinde habersiz kayboluyor.
            if (!path.StartsWith("Assets/")) continue;

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            bool exists = false;
            foreach (ScriptableRendererFeature existing in data.rendererFeatures)
            {
                if (existing is SnowRendererFeature) { exists = true; break; }
            }

            if (exists) continue;

            var feature = ScriptableObject.CreateInstance<SnowRendererFeature>();
            feature.name = "SnowRendererFeature";

            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();

            // Özellik listesi ve GUID haritası BİRLİKTE yazılmalı. Yalnız liste
            // yazılırsa renderer yeniden yüklendiğinde özellik düşer.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);

            added++;
        }

        return added;
    }
}
