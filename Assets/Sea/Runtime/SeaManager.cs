// ROL: deniz sisteminin yasam dongusu. Ortam degerlerini global olarak
// yayinlar, bathymetry'yi bake eder, SeaRuntimeState'i doldurur.
// Cagiran: yok — kendi basina calisiyor, bagimliliklari Inspector'dan.

using System;
using UnityEngine;

/// DENİZ SÜRMEZ, OKUR.
///
/// Bu sınıf içinde `RenderSettings`, `VolumeProfile` veya `Light.intensity`
/// yazan tek bir satır yok ve olmayacak (spec §3.3, Faz 1 kabul kriteri).
/// Yazdığı tek şey `Shader.SetGlobal*` ve `SeaRuntimeState`.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaManager : MonoBehaviour
{
    [SerializeField] SeaSettings settings;

    [Tooltip("Ortam kaynağı. Bulunamazsa sistem hata basıp devre dışı kalır.")]
    [SerializeField] SeaEnvironmentBridge environment;

    [Tooltip("Su derinliği bu araziden çıkarılıyor.")]
    [SerializeField] Terrain terrain;

    ISeaEnvironmentSource env;
    Texture2D bathymetry;

    float bakedSeaLevel = float.NaN;

    public SeaSettings Settings => settings;

    public void Bind(SeaSettings source, SeaEnvironmentBridge bridge, Terrain target)
    {
        settings = source;
        environment = bridge;
        terrain = target;
    }

    void OnEnable()
    {
        env = environment;

        if (env == null)
        {
            // KENDİ VARSAYILANINI UYDURMUYOR (spec §3.2).
            Debug.LogError($"{nameof(SeaManager)}: {nameof(environment)} atanmadı. " +
                           "Deniz sistemi devre dışı.");
            SeaRuntimeState.Active = false;
            enabled = false;
            return;
        }

        if (settings == null)
            throw new InvalidOperationException($"{nameof(SeaManager)}: {nameof(settings)} atanmadı.");

        if (terrain == null)
            throw new InvalidOperationException($"{nameof(SeaManager)}: {nameof(terrain)} atanmadı.");

        // ÇOKLU TERRAIN DESTEKLENMİYOR (spec §9, §17).
        var hepsi = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (hepsi.Length > 1)
        {
            Debug.LogError($"{nameof(SeaManager)}: sahnede {hepsi.Length} Terrain var. " +
                           "Deniz sistemi tek terrain destekliyor. Devre dışı.");
            SeaRuntimeState.Active = false;
            enabled = false;
            return;
        }

        RefreshBathymetry();
        SeaRuntimeState.Active = true;
    }

    void OnDisable()
    {
        SeaRuntimeState.Active = false;
        BathymetryBirak();
    }

    void BathymetryBirak()
    {
        if (bathymetry == null) return;

        if (Application.isPlaying) Destroy(bathymetry); else DestroyImmediate(bathymetry);
        bathymetry = null;
        bakedSeaLevel = float.NaN;
    }

    /// Arazi veya deniz seviyesi değişirse çağrılır (spec §9).
    public void RefreshBathymetry()
    {
        BathymetryBirak();

        bathymetry = SeaBathymetry.Bake(terrain, settings.seaLevelY);
        bakedSeaLevel = settings.seaLevelY;
    }

    void Update()
    {
        if (env == null || settings == null || terrain == null) return;

        // Deniz seviyesi Inspector'dan değişirse derinlik alanı bayat kalır.
        if (!Mathf.Approximately(bakedSeaLevel, settings.seaLevelY))
            RefreshBathymetry();

        OrtamiYayinla();
        BathymetryYayinla();
        AyarlariYayinla();
        DurumuGuncelle();
    }

    /// RÜZGÂR SPEKTRUMUN ANA GİRDİSİ (spec §3.4).
    ///
    /// Deniz kendi rüzgâr noise'unu veya gust simülasyonunu KURMUYOR;
    /// köprüden geleni yayınlıyor.
    void OrtamiYayinla()
    {
        Vector3 w = env.WindDirection * env.WindSpeed;
        Shader.SetGlobalVector(SeaShaderIDs.SeaWindWS, new Vector4(w.x, w.z, 0f, 0f));

        // DÖNGÜ KUANTİZE ZAMAN. `Time.time` doğrudan verilseydi uzun
        // oturumda float hassasiyeti kaybolurdu (spec §6.5).
        float t = Application.isPlaying ? Time.time : 0f;
        Shader.SetGlobalFloat(SeaShaderIDs.SeaTime, Mathf.Repeat(t, settings.loopPeriod));

        Shader.SetGlobalFloat(SeaShaderIDs.SunElevation01, env.SunElevation01);
        Shader.SetGlobalColor(SeaShaderIDs.SkyColor, env.SkyColor);
        Shader.SetGlobalColor(SeaShaderIDs.HorizonColor, env.HorizonColor);
        Shader.SetGlobalFloat(SeaShaderIDs.CloudCover01, env.CloudCover01);

        // Kar yağarken deniz yüzeyine köpük eklenmiyor — yalnız yağmur
        // (spec §13.5).
        float yagmur = env.PrecipKind == SeaPrecipitationKind.Rain
                     ? env.PrecipIntensity01 : 0f;
        Shader.SetGlobalFloat(SeaShaderIDs.PrecipIntensity01, yagmur);
    }

    void BathymetryYayinla()
    {
        if (bathymetry == null) return;

        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;

        Shader.SetGlobalTexture(SeaShaderIDs.BathyTex, bathymetry);
        Shader.SetGlobalVector(SeaShaderIDs.BathyOriginXZ, new Vector4(o.x, o.z, 0f, 0f));
        Shader.SetGlobalVector(SeaShaderIDs.BathySizeXZ, new Vector4(s.x, s.z, 0f, 0f));
        Shader.SetGlobalFloat(SeaShaderIDs.BathyResolution, bathymetry.width);
        Shader.SetGlobalFloat(SeaShaderIDs.DeepWaterDepth, settings.deepWaterDepth);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaLevelY, settings.seaLevelY);
    }

    void AyarlariYayinla()
    {
        Shader.SetGlobalVector(SeaShaderIDs.PatchSizes, settings.patchSizes);
        Shader.SetGlobalVector(SeaShaderIDs.TierWeights, settings.tierWeights);
        Shader.SetGlobalVector(SeaShaderIDs.ChoppinessPerTier, settings.choppinessPerTier);

        Shader.SetGlobalFloat(SeaShaderIDs.SpectrumDepth, settings.spectrumDepth);
        Shader.SetGlobalFloat(SeaShaderIDs.Fetch, settings.fetch);
        Shader.SetGlobalFloat(SeaShaderIDs.Swell, settings.swell);
        Shader.SetGlobalFloat(SeaShaderIDs.SmallWaveCutoff, settings.smallWaveCutoff);
        Shader.SetGlobalFloat(SeaShaderIDs.LoopPeriod, settings.loopPeriod);
        Shader.SetGlobalFloat(SeaShaderIDs.Choppiness, settings.choppiness);

        Shader.SetGlobalFloat(SeaShaderIDs.MaxShoalingGain, settings.maxShoalingGain);
        Shader.SetGlobalFloat(SeaShaderIDs.RunupMaxDepth, settings.runupMaxDepth);

        Shader.SetGlobalVector(SeaShaderIDs.ExtinctionRGB, settings.extinctionRgb);
        Shader.SetGlobalColor(SeaShaderIDs.UpwellingColor, settings.upwellingColor);
        Shader.SetGlobalFloat(SeaShaderIDs.RefractionStrength, settings.refractionStrength);
        Shader.SetGlobalFloat(SeaShaderIDs.RoughnessCalm, settings.roughnessCalm);
        Shader.SetGlobalFloat(SeaShaderIDs.RoughnessRough, settings.roughnessRough);

        Shader.SetGlobalFloat(SeaShaderIDs.ShoreFoamDepth, settings.shoreFoamDepth);
        Shader.SetGlobalColor(SeaShaderIDs.FoamColor, settings.foamColor);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamRoughness, settings.foamRoughness);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamTiling, settings.foamTiling);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamBreakupTiling, settings.foamBreakupTiling);
    }

    /// TEPE PERİYODU SPEKTRUMDAN TÜRÜYOR.
    ///
    /// JONSWAP tepe frekansı `ωp = 22 (g² / (U₁₀ F))^(1/3)` ve `Tp = 2π/ωp`.
    /// [KAYNAK: Horvath 2015 / JONSWAP]
    ///
    /// Belirgin dalga yüksekliği Hs, fetch-sınırlı büyümeden:
    /// `Hs ≈ 0.0016 (g F / U₁₀²)^(1/2) U₁₀² / g`.
    /// [KAYNAK: JONSWAP fetch-sınırlı büyüme bağıntısı]
    void DurumuGuncelle()
    {
        float u = Mathf.Max(env.WindSpeed, 0.1f);
        float g = SeaConstants.G;
        float f = settings.fetch;

        float omegaP = 22f * Mathf.Pow(g * g / (u * f), 1f / 3f);
        SeaRuntimeState.PeakPeriod = SeaConstants.TwoPi / Mathf.Max(omegaP, 1e-4f);

        float boyutsuzFetch = g * f / (u * u);
        SeaRuntimeState.SignificantWaveHeight =
            0.0016f * Mathf.Sqrt(boyutsuzFetch) * u * u / g;

        Shader.SetGlobalFloat(SeaShaderIDs.PeakPeriod, SeaRuntimeState.PeakPeriod);

        // Kabarma fazı: kırılan dalga kıyıya ilerleyip geri çekiliyor
        // (spec §8.5). Periyot spektrumun tepe periyoduna bağlı.
        float t = Application.isPlaying ? Time.time : 0f;
        float faz = t * (SeaConstants.TwoPi / Mathf.Max(SeaRuntimeState.PeakPeriod, 0.1f));
        float runup = Mathf.Sin(faz) * 0.5f + 0.5f;

        SeaRuntimeState.ShoreFoamIntensity01 = runup;
        Shader.SetGlobalFloat(SeaShaderIDs.ShoreFoamPhase, runup);
    }
}
