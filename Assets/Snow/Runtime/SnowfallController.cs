// ROL: kar yağışı parçacıklarını sürer (§10.1, §10.3). İki sistem: kar taneleri ve
// yer savrulması. Doğum kutusu kamerayı takip ediyor, sayı presetten geliyor.
// Çağıran: kimse — kendi LateUpdate'inde simüle edip çiziyor.

using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SnowfallController : MonoBehaviour
{
    /// Doğum hacmi (§10.1). Kutunun tamamına doğuluyor, yalnız üstüne değil.
    static readonly Vector3 SpawnBoxSize = new Vector3(40f, 26f, 40f);

    /// Kutunun kameraya göre kaydırması: 11 m yukarı, rüzgâr yönünde 3 m.
    const float SpawnBoxHeight = 11f;
    const float SpawnBoxWindLead = 3f;

    /// Tane verisi: 3+1+3+1+1+1+1+1 float = 48 bayt.
    const int FlakeStride = 48;

    /// Yer savrulması yalnız bu rüzgârın üstünde ve yüzeyde gevşek kar varken.
    const float SpindriftWindFloor = 7f;
    const float SpindriftWindRange = 10f;
    const float SpindriftLooseFloor = 0.15f;

    /// Gevşek kar oranı kaç karede bir okunuyor. Bloklamayan okuma.
    const int LooseReadInterval = 30;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowManager manager;
    [SerializeField] SnowWeather weather;
    [SerializeField] ComputeShader flakeCompute;
    [SerializeField] Shader flakeShader;

    [Tooltip("Doğum kutusunun merkezinde duracak kamera.")]
    [SerializeField] Camera followCamera;

    [Header("Görünüm")]
    [Tooltip("Uzaktaki tanenin asgari ekran boyutu, piksel (§10.1).")]
    [SerializeField] float minPixelSize = 1.3f;

    [SerializeField] float flutterFrequency = 5.5f;
    [SerializeField] float flutterAmplitude = 0.35f;

    [SerializeField] Color flakeTint = new Color(0.95f, 0.96f, 1f);
    [SerializeField] float flakeEmissive = 1f;
    [SerializeField] float softFadeDistance = 0.4f;

    // İKİ AYRI MATERYAL. DrawProcedural çizim anına erteleniyor; tek materyal
    // kullanılsaydı ikinci SetBuffer birincinin çizimini de değiştirir ve savrulma
    // tamponu iki kez çizilirdi.
    Material flakeMaterial;
    Material spindriftMaterial;

    ComputeBuffer flakes;
    ComputeBuffer spindrift;
    ComputeBuffer looseBuffer;

    readonly uint[] looseZero = new uint[1];

    int initKernel = -1;
    int simulateKernel = -1;
    int looseKernel = -1;

    /// Ortam dokularını isteyen kernel'ler. Dizi BİR KEZ ayrılıyor — her karede
    /// yeni dizi yaratmak §0.5'in yasakladığı şey.
    readonly int[] environmentKernels = new int[2];

    int flakeCapacity;
    int spindriftCapacity;

    float looseFraction;
    bool looseRequestPending;

    /// Rüzgârın taşıdığı birikmiş yol. Türbülans alanı bununla kaydırılıyor.
    Vector3 advect;

    /// Gök ortamı. Prosedürel çizimde küresel harmonik sabitleri yazılmıyor, bu
    /// yüzden CPU'da hesaplanıp materyale veriliyor.
    readonly Vector3[] ambientDirections = { Vector3.up };
    readonly Color[] ambientResults = new Color[1];
    bool needsInit;
    bool alive;

    public float LooseSnowFraction => looseFraction;
    public int FlakeCapacity => flakeCapacity;
    public int ActiveFlakes { get; private set; }
    public int ActiveSpindrift { get; private set; }

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException("SnowfallController: SnowManager atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (weather == null)
            throw new System.InvalidOperationException("SnowfallController: SnowWeather atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (flakeCompute == null)
            throw new System.InvalidOperationException("SnowfallController: SnowFlakes.compute atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (flakeShader == null)
            throw new System.InvalidOperationException("SnowfallController: Hidden/Snow/Flakes atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (followCamera == null)
            throw new System.InvalidOperationException("SnowfallController: kamera atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        flakeMaterial = CoreUtils.CreateEngineMaterial(flakeShader);
        spindriftMaterial = CoreUtils.CreateEngineMaterial(flakeShader);

        float scale = manager.Settings.QualityData.VfxCapacityScale;

        // KAPASİTE EN YÜKSEK PRESETE GÖRE. Çalışma zamanında değişmiyor; şiddet
        // etkin tane sayısıyla kontrol ediliyor (§10.1).
        flakeCapacity = Mathf.Max(64, Mathf.RoundToInt(MaxCapacity() * scale));
        spindriftCapacity = Mathf.Max(64, Mathf.RoundToInt(flakeCapacity * 0.5f));

        flakes = new ComputeBuffer(flakeCapacity, FlakeStride, ComputeBufferType.Structured);
        spindrift = new ComputeBuffer(spindriftCapacity, FlakeStride, ComputeBufferType.Structured);
        looseBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);

        ResolveKernels();

        alive = true;

        // KURULUM İLK LateUpdate'E ERTELENDİ. Doldurma zemin yükseklik dokusunu
        // istiyor; aynı nesnedeki bileşenlerin OnEnable sırası garanti değil ve o
        // doku henüz pişmemiş olabiliyor.
        needsInit = true;
    }

    void OnDisable()
    {
        alive = false;

        flakes?.Release();
        spindrift?.Release();
        looseBuffer?.Release();

        flakes = null;
        spindrift = null;
        looseBuffer = null;

        CoreUtils.Destroy(flakeMaterial);
        CoreUtils.Destroy(spindriftMaterial);
        flakeMaterial = null;
        spindriftMaterial = null;
    }

    int MaxCapacity()
    {
        int max = 0;

        for (int i = 0; i < weather.PresetCount; i++)
        {
            SnowWeatherPreset preset = weather.GetPreset(i);
            if (preset != null) max = Mathf.Max(max, preset.Capacity);
        }

        return max;
    }

    void ResolveKernels()
    {
        initKernel = flakeCompute.FindKernel("KFlakeInit");
        simulateKernel = flakeCompute.FindKernel("KFlakeSimulate");
        looseKernel = flakeCompute.FindKernel("KLooseFraction");

        environmentKernels[0] = initKernel;
        environmentKernels[1] = simulateKernel;
    }

    void InitBuffer(ComputeBuffer buffer, int capacity, bool isSpindrift)
    {
        BindCommon(capacity, isSpindrift);

        flakeCompute.SetBuffer(initKernel, SnowShaderIDs.Flakes, buffer);
        flakeCompute.Dispatch(initKernel, Mathf.CeilToInt(capacity / 64f), 1, 1);
    }

    void BindCommon(int capacity, bool isSpindrift)
    {
        Vector3 windWS = weather.WindWS;
        Vector3 windDir = windWS.sqrMagnitude > 1e-6f ? windWS.normalized : Vector3.forward;

        // KUTU MERKEZİ 1 m IZGARASINA SNAP'LENİYOR. Yapılmazsa kamera hareketinde
        // doğum deseni yürüyor ve taneler hep aynı yerde beliriyor gibi görünüyor.
        Vector3 raw = followCamera.transform.position
                    + Vector3.up * SpawnBoxHeight
                    + windDir * SpawnBoxWindLead;

        var center = new Vector3(Mathf.Floor(raw.x), Mathf.Floor(raw.y), Mathf.Floor(raw.z));

        flakeCompute.SetInt(SnowShaderIDs.FlakeCapacity, capacity);
        flakeCompute.SetInt(SnowShaderIDs.FlakeSpindrift, isSpindrift ? 1 : 0);
        flakeCompute.SetVector(SnowShaderIDs.FlakeBoxCenter, center);
        flakeCompute.SetVector(SnowShaderIDs.FlakeBoxSize, SpawnBoxSize);
        flakeCompute.SetVector(SnowShaderIDs.FlakeWind, windWS);
        flakeCompute.SetFloat(SnowShaderIDs.FlakeWindSpeed, weather.WindSpeed);
        flakeCompute.SetFloat(SnowShaderIDs.FlakeWetness, weather.FlakeWetness);
        flakeCompute.SetFloat(SnowShaderIDs.FlutterFreq, flutterFrequency);
        flakeCompute.SetFloat(SnowShaderIDs.FlutterAmp, flutterAmplitude);
        flakeCompute.SetFloat(SnowShaderIDs.FlakeTime, Time.time);
        flakeCompute.SetVector(SnowShaderIDs.FlakeAdvect, advect);

        BindEnvironment();
    }

    /// Örtü kesme ve zemin kesme için gereken dokular. Compute dokuları
    /// Shader.SetGlobalTexture ile gelmiyor, kernel başına bağlanmak zorunda.
    void BindEnvironment()
    {
        SnowGroundHeight ground = manager.GroundHeight;
        SnowOcclusionCapture occlusion = manager.Occlusion;

        if (ground.HeightTexture == null) return;

        for (int i = 0; i < environmentKernels.Length; i++)
        {
            flakeCompute.SetTexture(environmentKernels[i], SnowShaderIDs.GroundHeightTex, ground.HeightTexture);

            if (occlusion.OcclusionTexture != null)
                flakeCompute.SetTexture(environmentKernels[i], SnowShaderIDs.SnowOcclusionTex,
                                        occlusion.OcclusionTexture);
        }

        flakeCompute.SetVector(SnowShaderIDs.GroundOriginXZ,
            new Vector4(ground.OriginXZ.x, ground.OriginXZ.y, 0f, 0f));
        flakeCompute.SetVector(SnowShaderIDs.GroundSizeXZ,
            new Vector4(ground.SizeXZ.x, ground.SizeXZ.y, 0f, 0f));
        flakeCompute.SetFloat(SnowShaderIDs.GroundBaseY, ground.BaseY);
        flakeCompute.SetFloat(SnowShaderIDs.GroundHeightRange, ground.HeightRange);
        flakeCompute.SetVector(SnowShaderIDs.GroundHeightUV,
            new Vector4(ground.HeightUV.x, ground.HeightUV.y, 0f, 0f));

        flakeCompute.SetVector(SnowShaderIDs.OcclCenterXZ,
            new Vector4(occlusion.LastCaptureCenter.x, occlusion.LastCaptureCenter.y, 0f, 0f));
        flakeCompute.SetFloat(SnowShaderIDs.OcclAreaSize, SnowConstants.OcclusionArea);
        flakeCompute.SetFloat(SnowShaderIDs.OcclResolution,
            manager.Settings.QualityData.OcclusionResolution);
    }

    void LateUpdate()
    {
        if (flakes == null || !manager.IsReady) return;
        if (manager.GroundHeight.HeightTexture == null) return;

        if (needsInit)
        {
            needsInit = false;

            InitBuffer(flakes, flakeCapacity, false);
            InitBuffer(spindrift, spindriftCapacity, true);
        }

        // Advekte ofset burada birikiyor; rüzgâr değişse bile alan sürekli kalıyor.
        advect += weather.WindWS * Time.deltaTime;

        UpdateLooseFraction();

        SnowWeatherPreset preset = weather.GetPreset(weather.ActivePreset);

        // ETKİN TANE SAYISI YAĞIŞ HIZIYLA ORANTILI.
        //
        // Önceki formül (doğum hızı x ömür) Moderate'ten itibaren tavana çarpıyordu:
        // Moderate, Heavy ve Blizzard'ın üçü de 40 000 tane çiziyordu, yani şiddetin
        // görüntüye hiç etkisi yoktu. Ölçüldü.
        //
        // Oran §10.3 tablosuyla örtüşüyor: Heavy 3/5 x 40 000 = 24 000, tablodaki
        // kapasitenin ta kendisi.
        ActiveFlakes = Mathf.Clamp(
            Mathf.RoundToInt(flakeCapacity * weather.Coverage), 0, flakeCapacity);

        Simulate(flakes, flakeCapacity, ActiveFlakes, false);

        // YER SAVRULMASI: fırtına hissini veren şey bu. Kar tanesi sayısını
        // artırarak taklit edilemez (§10.1).
        float windFactor = Mathf.Pow(Mathf.Clamp01((weather.WindSpeed - SpindriftWindFloor) / SpindriftWindRange), 2f);
        bool spindriftOn = preset != null && preset.Spindrift
                           && windFactor > 0f && looseFraction > SpindriftLooseFloor;

        // SAVRULMA ÇOK SAYIDA OLMAK ZORUNDA (§10.1): tek tek seçilebilen taneler perde
        // hissi vermiyor. Sayı kapasitenin oranı olarak veriliyor, mutlak bir doğum
        // hızı olarak değil.
        ActiveSpindrift = spindriftOn
            ? Mathf.Clamp(Mathf.RoundToInt(spindriftCapacity * windFactor * looseFraction),
                          0, spindriftCapacity)
            : 0;

        Simulate(spindrift, spindriftCapacity, ActiveSpindrift, true);

        Draw();
    }

    void Simulate(ComputeBuffer buffer, int capacity, int active, bool isSpindrift)
    {
        BindCommon(capacity, isSpindrift);

        flakeCompute.SetInt(SnowShaderIDs.FlakeActive, active);
        flakeCompute.SetFloat(SnowShaderIDs.FlakeDeltaTime, Time.deltaTime);
        flakeCompute.SetBuffer(simulateKernel, SnowShaderIDs.Flakes, buffer);

        flakeCompute.Dispatch(simulateKernel, Mathf.CeilToInt(capacity / 64f), 1, 1);
    }

    void Draw()
    {
        var bounds = new Bounds(followCamera.transform.position, SpawnBoxSize * 2f);

        if (ActiveFlakes > 0)
        {
            ApplyMaterial(flakeMaterial, flakes, Mathf.Clamp01(weather.WindSpeed / 12f));
            Graphics.DrawProcedural(flakeMaterial, bounds, MeshTopology.Triangles, ActiveFlakes * 6, 1,
                                    followCamera, null, ShadowCastingMode.Off, false, gameObject.layer);
        }

        if (ActiveSpindrift > 0)
        {
            // Savrulma her zaman uzatılmış çiziliyor: yerde sürünen bir perde.
            ApplyMaterial(spindriftMaterial, spindrift, 1f);
            Graphics.DrawProcedural(spindriftMaterial, bounds, MeshTopology.Triangles, ActiveSpindrift * 6, 1,
                                    followCamera, null, ShadowCastingMode.Off, false, gameObject.layer);
        }
    }

    void ApplyMaterial(Material target, ComputeBuffer buffer, float stretch)
    {
        float fov = followCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;

        target.SetFloat(SnowShaderIDs.MinPixelSize, minPixelSize);
        target.SetFloat(SnowShaderIDs.ScreenHeight, followCamera.pixelHeight);
        target.SetFloat(SnowShaderIDs.TanHalfFov, Mathf.Tan(fov));
        target.SetColor(SnowShaderIDs.FlakeTint, flakeTint);
        target.SetFloat(SnowShaderIDs.FlakeEmissive, flakeEmissive);
        target.SetFloat(SnowShaderIDs.SoftFadeDistance, softFadeDistance);

        RenderSettings.ambientProbe.Evaluate(ambientDirections, ambientResults);
        target.SetColor(SnowShaderIDs.FlakeAmbient, ambientResults[0]);
        target.SetFloat(SnowShaderIDs.WindStretch, stretch);
        target.SetBuffer(SnowShaderIDs.Flakes, buffer);
    }

    /// Yüzeydeki gevşek kar oranı. BLOKLAMAYAN okuma, 30 karede bir (§10.1).
    void UpdateLooseFraction()
    {
        if (looseRequestPending || Time.frameCount % LooseReadInterval != 0) return;

        int resolution = manager.Settings.QualityData.Resolution;
        int groups = Mathf.CeilToInt(resolution / (float)SnowConstants.GroupSize);

        looseZero[0] = 0;
        looseBuffer.SetData(looseZero);

        flakeCompute.SetInt(SnowShaderIDs.Resolution, resolution);
        flakeCompute.SetTexture(looseKernel, SnowShaderIDs.State, manager.StateTexture);
        flakeCompute.SetBuffer(looseKernel, SnowShaderIDs.LooseOut, looseBuffer);
        flakeCompute.Dispatch(looseKernel, groups, groups, 1);

        looseRequestPending = true;

        AsyncGPUReadback.Request(looseBuffer, request =>
        {
            looseRequestPending = false;

            // Yok edilmiş nesne kontrolü — bkz. SnowSampler.
            if (!alive || request.hasError) return;

            uint total = request.GetData<uint>()[0];
            float texels = resolution * (float)resolution;

            // Kernel her teksel için (1-rhoN)*64 ekliyor.
            looseFraction = Mathf.Clamp01(total / 64f / texels);
        });
    }
}
