// ROL: birikme, erime, yağmur etkisi, gökyüzü örtüsü, rüzgâr dağıtımı ve
// yağış histerezisini ÖLÇER. Play gerekmiyor.
// Çağıran: menü — To The Summit/Kar/Birikme Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// KORUNAN NİCELİK SWE. Görünür derinlik ondan türüyor, o yüzden iddialar
/// SWE üzerinden kuruluyor: kâğıtta hesaplanabilen tek büyüklük o.
/// Yükseklik ayrıca yazılıyor ama ölçüt değil — yoğunluk oturmasıyla
/// birlikte değiştiği için tek başına bir şey kanıtlamaz.
public static class SnowAccumulationTest
{
    const int Res = 256;
    const float AreaSize = 16f;
    const float ObserverY = 4900.5f;
    const float GroundY = ObserverY - 1f;

    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";

    static readonly Vector2 Center = new(-7494f, -4327.5f);

    [MenuItem("To The Summit/Kar/Birikme Sınaması", false, 54)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — birikme sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = HysteresisTest(r);
        ok &= IntensityTest(r);
        ok &= GpuTests(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // -------------------------------------------------------------- histerezis

    /// SICAKLIK EŞİĞİNDE TİTREME OLMAMALI. Tek eşik olsaydı 0.5 °C civarında
    /// salınan bir sıcaklık karı saniyede birkaç kez başlatıp durdururdu.
    static bool HysteresisTest(StringBuilder r)
    {
        r.AppendLine("## Yağış histerezisi (spec §3.4)");

        var env = new FakeEnvironment { PrecipKind = PrecipitationKind.Rain, PrecipIntensity01 = 1f };
        var controller = new SnowfallController();
        controller.Reset();

        // Soğuktan sıcağa: kar 2.0 °C'yi GEÇENE kadar durmamalı.
        env.TemperatureC = -5f; controller.Tick(env);
        bool onCold = SnowRuntimeState.IsSnowing;

        env.TemperatureC = 1.0f; controller.Tick(env);       // bandın içi
        bool stillOnInBand = SnowRuntimeState.IsSnowing;

        env.TemperatureC = 2.5f; controller.Tick(env);
        bool offWarm = !SnowRuntimeState.IsSnowing;

        env.TemperatureC = 1.0f; controller.Tick(env);       // bandın içine geri
        bool stillOffInBand = !SnowRuntimeState.IsSnowing;

        env.TemperatureC = 0.2f; controller.Tick(env);
        bool onAgain = SnowRuntimeState.IsSnowing;

        bool hysteresis = onCold && stillOnInBand && offWarm && stillOffInBand && onAgain;

        r.AppendLine("  [" + M(hysteresis) + "] Bant içinde durum KORUNUYOR   " +
                     "-5→kar, 1.0→kar (sürüyor), 2.5→yok, 1.0→yok (sürüyor), 0.2→kar");

        // Yağış yoksa sıcaklık ne olursa olsun kar yok.
        env.PrecipKind = PrecipitationKind.None;
        env.TemperatureC = -20f;
        controller.Tick(env);

        bool noPrecipNoSnow = !SnowRuntimeState.IsSnowing;
        r.AppendLine("  [" + M(noPrecipNoSnow) + "] Yağış yoksa kar da yok       " +
                     "-20 °C ve PrecipKind.None → IsSnowing " + SnowRuntimeState.IsSnowing);

        // Sulu kar bandı: tek tanecik türü, biçim enterpole ediliyor.
        env.PrecipKind = PrecipitationKind.Rain;
        env.TemperatureC = -1f; controller.Tick(env);
        float wetCold = controller.Wetness;

        env.TemperatureC = 1.25f; controller.Tick(env);
        float wetMid = controller.Wetness;

        env.TemperatureC = 5f; controller.Tick(env);
        float wetWarm = controller.Wetness;

        bool wetRamp = wetCold < 0.01f && Mathf.Abs(wetMid - 0.5f) < 0.02f && wetWarm > 0.99f;
        r.AppendLine("  [" + M(wetRamp) + "] Sulu kar rampası             -1 °C → " +
                     wetCold.ToString("0.00") + ",  1.25 °C → " + wetMid.ToString("0.00") +
                     ",  5 °C → " + wetWarm.ToString("0.00"));

        controller.Reset();

        return hysteresis && noPrecipNoSnow && wetRamp;
    }

    // ----------------------------------------------------------------- şiddet

    /// SPEC §17.2 TABLOSU BİREBİR. VFX yoğunluğu ile SWE hızı aynı `i01`
    /// değerinden türüyor; ayrılırsa "yağıyor ama birikmiyor" olur.
    static bool IntensityTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Şiddet eşlemesi (spec §17.2)");

        var env = new FakeEnvironment
        {
            PrecipKind = PrecipitationKind.Rain,
            TemperatureC = -5f,
        };

        var controller = new SnowfallController();
        controller.Reset();

        // SPEC'İN KODU BAĞLAYICI, TABLOSU DEĞİL. §17.2'nin kod bloğu
        // `Lerp(0, 16000, i01)` diyor; aynı bölümün "referans" tablosu ise
        // 0.06 → 1200 veriyor ve bu doğrusal değil (960 olmalı). SWE sütunu
        // doğrusalla uyuşuyor, tane sütunu uyuşmuyor — tablo yuvarlanmış.
        // Kod uygulandı, sapma raporda yazılı.
        (float i01, float swe, float flake)[] table =
        {
            (0.06f, 8.33e-8f, 16000f * 0.06f),
            (0.24f, 3.33e-7f, 16000f * 0.24f),
            (0.60f, 8.33e-7f, 16000f * 0.60f),
            (1.00f, 1.39e-6f, 16000f * 1.00f),
        };

        bool all = true;

        foreach ((float i01, float swe, float flake) row in table)
        {
            env.PrecipIntensity01 = row.i01;
            controller.Tick(env);

            // Tablo yuvarlanmış; %1 tolerans.
            bool sweOk = Mathf.Abs(controller.SnowfallSweRate - row.swe) < row.swe * 0.01f;
            bool flakeOk = Mathf.Abs(controller.FlakeRate - row.flake) < row.flake * 0.01f;

            all &= sweOk && flakeOk;

            r.AppendLine("  [" + M(sweOk && flakeOk) + "] i01 " + row.i01.ToString("0.00") +
                         "  SWE " + controller.SnowfallSweRate.ToString("0.00e+0") +
                         " m/s (tablo " + row.swe.ToString("0.00e+0") + "),  tane " +
                         controller.FlakeRate.ToString("0") + "/s (kod " +
                         row.flake.ToString("0") + ")");
        }

        r.AppendLine("  [i] Spec §17.2'nin 'referans' tablosu tane sütununda koddan sapıyor " +
                     "(0.06 → 1200, doğrusal karşılığı 960). Kod bloğu uygulandı.");

        controller.Reset();
        return all;
    }

    // -------------------------------------------------------------------- GPU

    static bool GpuTests(StringBuilder r)
    {
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " yüklenemedi."); return false; }

        var rig = new Rig(sim, Res, AreaSize, Center, GroundY, ObserverY);
        bool all = true;

        try
        {
            r.AppendLine();
            r.AppendLine("## Birikme (spec §11)");

            // --- 1. SWE tam olarak hız × süre kadar artıyor ---
            rig.ResetSnow(0f, 0.10f);
            rig.ClearSky();

            const float Rate = 8.33e-7f;      // i01 = 0.60
            const float Hour = 3600f;

            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float swe = rig.Snow(Center).r;
            float wantSwe = Rate * Hour;
            bool sweOk = Mathf.Abs(swe - wantSwe) < wantSwe * 0.02f;
            all &= sweOk;

            float rhoN = rig.Snow(Center).g;
            float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rhoN);
            float height = swe * SnowConstants.RhoWater / rho;

            r.AppendLine("  [" + M(sweOk) + "] SWE artışı        " + (swe * 1000f).ToString("0.000") +
                         " mm/saat  (beklenen " + (wantSwe * 1000f).ToString("0.000") + ")");
            r.AppendLine("  [i] Karşılığı        yoğunluk " + rho.ToString("0") + " kg/m³ → " +
                         (height * 100f).ToString("0.00") + " cm/saat  (spec sağlaması " +
                         "3 mm/sa SWE + ρ 107 için ~2.8 cm/sa)");

            // --- 2. Taze karın yoğunluğu 55 kg/m³ ---
            // BİR DAKİKA, BİR SANİYE DEĞİL. Yoğunluk karışımı
            // `max(sweNext, 1e-6)` ile korunuyor; bir saniyede biriken SWE
            // (8.3e-7 m) o korumanın altında kalıyor ve yoğunluk bozuluyor.
            // Ölçülen: 1 sn'de 50 kg/m³, 60 sn'de 55.
            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(60f, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float freshRho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rig.Snow(Center).g);
            bool freshOk = Mathf.Abs(freshRho - 55f) < 2f;
            all &= freshOk;
            r.AppendLine("  [" + M(freshOk) + "] Taze kar yoğunluğu " + freshRho.ToString("0.0") +
                         " kg/m³  (kuru, rüzgârsız: lerp(55,145,wet=0) = 55)");

            // --- 3. Oturma yoğunluğu yükseltiyor ---
            rig.ResetSnow(0.02f, SnowDensityN(60f));
            rig.Accumulate(Hour * 6f, 0f, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float settled = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rig.Snow(Center).g);
            bool settleOk = settled > 60f && settled < 190f;
            all &= settleOk;
            r.AppendLine("  [" + M(settleOk) + "] Oturma            6 saatte 60 → " +
                         settled.ToString("0.0") + " kg/m³  (hedef 190, tau 6 saat)");

            r.AppendLine();
            r.AppendLine("## Erime (spec §11, §3.5)");

            // --- 4. Negatif sıcaklıkta erime YOK ---
            // TABAN ÖLÇÜLEREK ALINIYOR. 0.02 m yarım hassasiyette tam
            // temsil edilmiyor (en yakın half 0.019989); nominal değerle
            // karşılaştırmak dokunun kendi yuvarlamasını "erime" sayardı.
            rig.ResetSnow(0.02f, 0.30f);
            float stored = rig.Snow(Center).r;

            rig.Accumulate(Hour, 0f, temperature: -5f, rain: 0f, wind: Vector2.zero);
            bool noMelt = Mathf.Abs(rig.Snow(Center).r - stored) < 1e-6f;
            all &= noMelt;
            r.AppendLine("  [" + M(noMelt) + "] -5 °C             SWE " +
                         (rig.Snow(Center).r * 1000f).ToString("0.0000") +
                         " mm  (dokuya yazılan " + (stored * 1000f).ToString("0.0000") +
                         " — değişmemeli, derece-gün max(0,T) kullanıyor)");

            // --- 5. +5 °C'de derece-gün ---
            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(Hour, 0f, temperature: 5f, rain: 0f, wind: Vector2.zero);

            float melted = stored - rig.Snow(Center).r;
            float wantMelt = SnowConstants.MeltDdf * 5f * Hour;
            bool meltOk = Mathf.Abs(melted - wantMelt) < wantMelt * 0.03f;
            all &= meltOk;
            r.AppendLine("  [" + M(meltOk) + "] +5 °C             " + (melted * 1000f).ToString("0.0000") +
                         " mm erimiş  (beklenen " + (wantMelt * 1000f).ToString("0.0000") +
                         " = DDF × 5 °C × 1 saat)");

            // --- 6. Yağmur erimeyi hızlandırıyor ---
            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(Hour, 0f, temperature: 5f, rain: 1f, wind: Vector2.zero);

            float meltedRain = stored - rig.Snow(Center).r;
            float wantRain = wantMelt * (1f + SnowConstants.RainMeltBoost);
            bool rainOk = Mathf.Abs(meltedRain - wantRain) < wantRain * 0.03f;
            all &= rainOk;
            r.AppendLine("  [" + M(rainOk) + "] +5 °C + yağmur    " + (meltedRain * 1000f).ToString("0.0000") +
                         " mm  (beklenen " + (wantRain * 1000f).ToString("0.0000") + " = ×" +
                         (1f + SnowConstants.RainMeltBoost).ToString("0.0") + ")");

            // --- 7. Yağmur karı ıslatıyor ---
            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(1800f, 0f, temperature: 0f, rain: 1f, wind: Vector2.zero);
            float wet = rig.Snow(Center).b;
            bool wetOk = wet > 0.5f;
            all &= wetOk;
            r.AppendLine("  [" + M(wetOk) + "] Islanma           yarım saatte wet " +
                         wet.ToString("0.000") + "  (tau 1800 s → 0.632 beklenir)");

            r.AppendLine();
            r.AppendLine("## Gökyüzü örtüsü (spec §12)");

            // --- 8. Çatının altına kar yağmıyor ---
            rig.ResetSnow(0f, 0.10f);
            rig.SetSkyHalfCovered(GroundY + 3f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float openSwe = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float roofSwe = rig.Snow(Center - new Vector2(4f, 0f)).r;

            bool roofOk = roofSwe < openSwe * 0.05f && openSwe > wantSwe * 0.9f;
            all &= roofOk;
            r.AppendLine("  [" + M(roofOk) + "] Çatı altı         açıkta " +
                         (openSwe * 1000f).ToString("0.000") + " mm,  altta " +
                         (roofSwe * 1000f).ToString("0.000") + " mm");

            r.AppendLine();
            r.AppendLine("## Rüzgâr dağıtımı (spec §11)");

            // --- 9. Rüzgâr yönü birikme dağılımını çeviriyor ---
            // SABİT EĞİM İŞE YARAMAZ. Rüzgâr çarpanı yerel eğimden çıkıyor;
            // yamaç her yerde aynı eğimdeyse çarpan da her yerde aynı olur ve
            // iki nokta arasında fark oluşmaz (ölçüldü: oran 0.999). Sırt
            // kuruluyor — bir yanı rüzgâra bakıyor, öbürü sırtını dönüyor.
            rig.ClearSky();
            rig.SetGroundRidge(GroundY, 2.4f);

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: new Vector2(8f, 0f));
            float plusWindward = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float plusLeeward = rig.Snow(Center - new Vector2(4f, 0f)).r;

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: new Vector2(-8f, 0f));
            float minusWindward = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float minusLeeward = rig.Snow(Center - new Vector2(4f, 0f)).r;

            float plusRatio = plusWindward / Mathf.Max(plusLeeward, 1e-9f);
            float minusRatio = minusWindward / Mathf.Max(minusLeeward, 1e-9f);

            // Yön çevrilince oran TERSİNE dönmeli.
            bool windOk = Mathf.Abs(plusRatio - 1f) > 0.02f &&
                          (plusRatio - 1f) * (minusRatio - 1f) < 0f;
            all &= windOk;
            r.AppendLine("  [" + M(windOk) + "] Yön çevrilince    +X rüzgârda doğu/batı oranı " +
                         plusRatio.ToString("0.000") + ",  −X rüzgârda " +
                         minusRatio.ToString("0.000") + "  (1'in iki yanına düşmeli)");

            // --- 10. Rüzgârsızken dağılım düz ---
            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);
            float flatRatio = rig.Snow(Center + new Vector2(4f, 0f)).r /
                              Mathf.Max(rig.Snow(Center - new Vector2(4f, 0f)).r, 1e-9f);

            bool flatOk = Mathf.Abs(flatRatio - 1f) < 0.01f;
            all &= flatOk;
            r.AppendLine("  [" + M(flatOk) + "] Rüzgârsız         oran " +
                         flatRatio.ToString("0.0000") + "  (sırt var ama rüzgâr yok → 1.0000)");
        }
        finally
        {
            rig.Dispose();
        }

        return all;
    }

    static float SnowDensityN(float rho) =>
        Mathf.Clamp01((rho - SnowConstants.RhoMin) / (SnowConstants.RhoMax - SnowConstants.RhoMin));

    static string M(bool ok) => ok ? "+" : "-";

    // ------------------------------------------------------------------ sahte

    /// Köprünün yerine geçen sahte çevre. Gerçek `SnowEnvironmentBridge`
    /// sahneye bağlı; sınama onu değil, ONDAN OKUYAN mantığı ölçüyor.
    sealed class FakeEnvironment : ISnowEnvironmentSource
    {
        public Vector3 WindDirection { get; set; } = Vector3.right;
        public float WindSpeed { get; set; }
        public Light Sun => null;
        public float SunElevation01 { get; set; }
        public float TemperatureC { get; set; }
        public PrecipitationKind PrecipKind { get; set; }
        public float PrecipIntensity01 { get; set; }
        public float FogDensity01 { get; set; }
    }

    // ---------------------------------------------------------------- düzenek

    sealed class Rig
    {
        readonly ComputeShader sim;
        readonly int res;
        readonly float areaSize;
        readonly Vector2 center;
        readonly float groundY;

        readonly int kClear, kAccumulate;
        readonly int groups;

        RenderTexture snow, snowTemp, skyVis;
        Texture2D ground;
        readonly Texture2D readOne;

        public Rig(ComputeShader sim, int res, float areaSize, Vector2 center,
                   float groundY, float observerY)
        {
            this.sim = sim;
            this.res = res;
            this.areaSize = areaSize;
            this.center = center;
            this.groundY = groundY;

            kClear = sim.FindKernel("KClear");
            kAccumulate = sim.FindKernel("KAccumulate");
            groups = Mathf.CeilToInt(res / 8f);

            snow = Rt(res, RenderTextureFormat.ARGBHalf);
            snowTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            skyVis = Rt(res, RenderTextureFormat.RFloat);

            readOne = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            SetGroundFlat(groundY);

            Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, res);

            // Gökyüzü haritası kar bölgesiyle aynı alanı kapsıyor: sınama
            // örtüyü teksel teksel yerleştirebilsin diye.
            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, res);
        }

        static RenderTexture Rt(int res, RenderTextureFormat format)
        {
            var rt = new RenderTexture(res, res, 0, format)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        // --- zemin ---

        void SetGroundFlat(float y) => WriteGround((_, __) => 0.5f, y - 1f, 2f);

        /// Ortada tepe yapan sırt: −X yamacı +X'e, +X yamacı −X'e bakıyor.
        /// Rüzgâr çevrilince hangi yamacın maruz kaldığı değişiyor.
        public void SetGroundRidge(float baseY, float rise)
        {
            WriteGround((u, _) => 1f - Mathf.Abs(2f * u - 1f), baseY, rise);
        }

        void WriteGround(System.Func<float, float, float> value, float baseY, float range)
        {
            if (ground != null) Object.DestroyImmediate(ground);

            const int GroundRes = 64;

            ground = new Texture2D(GroundRes, GroundRes, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[GroundRes * GroundRes];

            for (int y = 0; y < GroundRes; y++)
            for (int x = 0; x < GroundRes; x++)
            {
                float u = (x + 0.5f) / GroundRes;
                float v = (y + 0.5f) / GroundRes;
                px[y * GroundRes + x] = new Color(value(u, v), 0f, 0f, 0f);
            }

            ground.SetPixels(px);
            ground.Apply(false, false);

            Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ,
                new Vector4(center.x - areaSize * 0.5f, center.y - areaSize * 0.5f, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ,
                new Vector4(areaSize, areaSize, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ,
                new Vector4(areaSize / GroundRes, areaSize / GroundRes, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, baseY);
            Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, range);
        }

        // --- gökyüzü ---

        public void ClearSky() => FillSky(_ => -9999f);

        /// Bölgenin −X yarısına verilen kotta bir çatı koyar.
        public void SetSkyHalfCovered(float roofY) =>
            FillSky(x => x < res / 2 ? roofY : -9999f);

        void FillSky(System.Func<int, float> valueAtX)
        {
            var tex = new Texture2D(res, res, TextureFormat.RFloat, false, true);
            var px = new Color[res * res];

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                px[y * res + x] = new Color(valueAtX(x), 0f, 0f, 0f);

            tex.SetPixels(px);
            tex.Apply(false, false);
            Graphics.Blit(tex, skyVis);
            Object.DestroyImmediate(tex);
        }

        // --- durum ---

        public void ResetSnow(float swe, float rhoN)
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetVector(SnowShaderIDs.ClearValue, new Vector4(swe, rhoN, 0f, 0f));
            sim.SetTexture(kClear, SnowShaderIDs.Dst, snow);
            sim.Dispatch(kClear, groups, groups, 1);
        }

        /// Tek adımda `seconds` kadar ilerletir. Döşeme döndürmesi kapalı
        /// (tiles = 1) — sınamanın ölçtüğü şey döndürme değil fizik.
        public void Accumulate(float seconds, float sweRate, float temperature,
                               float rain, Vector2 wind)
        {
            Shader.SetGlobalFloat(SnowShaderIDs.TemperatureC, temperature);
            Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rain);
            Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(wind.x, 0f, wind.y, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, wind.magnitude);

            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.SnowfallSWERate, sweRate);
            sim.SetFloat(SnowShaderIDs.DeltaTimeEff, seconds);
            sim.SetInt(SnowShaderIDs.TileIndex, 0);
            sim.SetInt(SnowShaderIDs.TileCount, 1);

            sim.SetTexture(kAccumulate, SnowShaderIDs.GroundHeightTex, ground);
            sim.SetTexture(kAccumulate, SnowShaderIDs.SnowSkyVisTex, skyVis);
            sim.SetTexture(kAccumulate, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kAccumulate, SnowShaderIDs.SnowOut, snowTemp);
            sim.Dispatch(kAccumulate, groups, groups, 1);

            (snow, snowTemp) = (snowTemp, snow);
        }

        public Color Snow(Vector2 worldXZ)
        {
            Vector2 uv = (worldXZ - center) / areaSize + new Vector2(0.5f, 0.5f);
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * res), 0, res - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * res), 0, res - 1);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = snow;
            readOne.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readOne.Apply(false);
            RenderTexture.active = prev;

            return readOne.GetPixel(0, 0);
        }

        public void Dispose()
        {
            Rel(ref snow);
            Rel(ref snowTemp);
            Rel(ref skyVis);

            if (ground != null) Object.DestroyImmediate(ground);
            Object.DestroyImmediate(readOne);
        }

        static void Rel(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
