// ROL: nesne ve karakter üstü karı ÖLÇER — eğim eşiği, gökyüzü örtüsü,
// oyuk çarpanı, kaplama bağı ve karakterde birikme/temizlenme.
// Çağıran: menü — To The Summit/Kar/Kaplama Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// EN ÇOK FARK EDİLEN HATA: ÇATININ ALTINA KAR (spec §17.1, §22).
/// Maskede gökyüzü çarpanı yoksa nesnenin alt yüzeyi de karlanır. Burada
/// çatının altı ile üstü ayrı ayrı ölçülüyor.
public static class SnowCoverTest
{
    const string KernelPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string ShaderPath = "Assets/Snow/Shaders/SnowCoverObject.shader";

    [MenuItem("To The Summit/Kar/Kaplama Sınaması", false, 56)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — kaplama sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = MaskTest(r);
        ok &= AccumulatorTest(r);
        ok &= ShaderTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ------------------------------------------------------------------ maske

    static bool MaskTest(StringBuilder r)
    {
        r.AppendLine("## SnowCoverMask (spec §16)");

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);
        if (cs == null) { r.AppendLine("  [-] " + KernelPath + " yüklenemedi."); return false; }

        var probe = new MaskProbe(cs);
        bool all = true;

        try
        {
            // Gökyüzü tamamen açık, kaplama tam.
            probe.SetSky(clear: true);
            probe.SetCoverage(1f);

            float up = probe.Sample(Vector3.up, ao: 1f);
            float tilted = probe.Sample(new Vector3(0f, 1f, 1f), ao: 1f);       // 45°
            float wall = probe.Sample(Vector3.right, ao: 1f);                    // dikey
            float down = probe.Sample(Vector3.down, ao: 1f);                     // alt yüzey

            // MASKE İKİLİYE YAKIN. `edgeSharpness` (×4) geçişi dar bir banda
            // sıkıştırıyor; 45° de yatay da doyuma gidiyor. Ölçülen şey
            // sıralama değil, uçların doğru olması.
            bool slope = up > 0.99f && wall < 0.01f && down < 0.01f;
            all &= slope;

            r.AppendLine("  [" + M(slope) + "] Eğim              yatay " + up.ToString("0.000") +
                         ",  45° " + tilted.ToString("0.000") + ",  dikey " + wall.ToString("0.000") +
                         ",  alt yüzey " + down.ToString("0.000"));

            // EFEKTİF EŞİK ÖLÇÜLÜYOR, VARSAYILMIYOR. `_SnowSlopeThreshold`
            // (0.25) eğim maskesinin eşiği; maskenin kendisi ondan sonra
            // gürültüyü ÇIKARIYOR, yani karın gerçekten tuttuğu eğim daha
            // dik. İkisini karıştırmak "eşik çalışmıyor" yanılgısı üretir.
            float cutIn = probe.FindCutInSlope();

            bool threshold = cutIn > 0.25f && cutIn < 0.95f;
            all &= threshold;

            r.AppendLine("  [" + M(threshold) + "] Efektif eşik      dot(N,up) = " +
                         cutIn.ToString("0.000") + "  (" +
                         (Mathf.Acos(Mathf.Clamp01(cutIn)) * Mathf.Rad2Deg).ToString("0") +
                         "° yatıklığa kadar kar tutuyor; ham eğim eşiği 0.25)");

            // Kaplama arttıkça daha dik yüzeyler de karlanmalı.
            probe.SetCoverage(0.6f);
            float cutInLow = probe.FindCutInSlope();
            probe.SetCoverage(1f);

            bool coverageShifts = cutInLow > cutIn;
            all &= coverageShifts;

            r.AppendLine("  [" + M(coverageShifts) + "] Kaplama eşiği kaydırıyor  0.60 → " +
                         cutInLow.ToString("0.000") + ",  1.00 → " + cutIn.ToString("0.000") +
                         "  (az kar = yalnız düz yüzeyler)");

            // ÇATI: gökyüzü kapalı → kar yok.
            probe.SetSky(clear: false);
            float underRoof = probe.Sample(Vector3.up, ao: 1f);

            bool roof = underRoof < 0.01f;
            all &= roof;

            r.AppendLine("  [" + M(roof) + "] Çatı altı         " + underRoof.ToString("0.000") +
                         "  (gökyüzü kapalı, yatay yüzey — kar OLMAMALI)");

            // Oyuk: AO düşükse kar girmiyor.
            probe.SetSky(clear: true);
            float cavity = probe.Sample(Vector3.up, ao: 0.2f);

            bool cavityOk = cavity < 0.01f;
            all &= cavityOk;

            r.AppendLine("  [" + M(cavityOk) + "] Oyuk (AO 0.2)     " + cavity.ToString("0.000") +
                         "  (girinti — kar dolmamalı)");

            // Kaplama sıfırsa hiçbir yerde kar yok.
            probe.SetCoverage(0f);
            float noCoverage = probe.Sample(Vector3.up, ao: 1f);

            bool coverageOk = noCoverage < 0.001f;
            all &= coverageOk;

            r.AppendLine("  [" + M(coverageOk) + "] Kaplama 0         " + noCoverage.ToString("0.000") +
                         "  (zeminde kar yoksa nesnede de yok)");

            // Kaplama kademeli artıyor mu.
            probe.SetCoverage(0.35f);
            float partial = probe.Sample(Vector3.up, ao: 1f);

            bool graded = partial > 0.001f && partial < up;
            all &= graded;

            r.AppendLine("  [" + M(graded) + "] Kaplama 0.35      " + partial.ToString("0.000") +
                         "  (0 ile " + up.ToString("0.000") + " arasında)");
        }
        finally
        {
            probe.Dispose();
        }

        return all;
    }

    // ------------------------------------------------------------- karakter

    static bool AccumulatorTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Karakter üstü kar (spec §16.1)");

        var go = new GameObject("SnowCoverTest_Character") { hideFlags = HideFlags.HideAndDontSave };
        var accumulator = go.AddComponent<SnowCharacterAccumulator>();

        var env = new FakeEnvironment { PrecipKind = PrecipitationKind.Rain, TemperatureC = -5f };
        var snowfall = new SnowfallController();

        bool all = true;

        try
        {
            accumulator.SetEnvironment(env);
            accumulator.SetSkyVisibility(1f);

            // Kar yağıyor, karakter duruyor → birikiyor.
            env.PrecipIntensity01 = 1f;
            snowfall.Tick(env);

            for (int i = 0; i < 200; i++) accumulator.Step(0.1f, 0f);
            float snowed = accumulator.Accumulation;

            bool accumulates = snowed > 0.9f;
            all &= accumulates;
            r.AppendLine("  [" + M(accumulates) + "] Yağışta birikiyor   20 sn sonra " +
                         snowed.ToString("0.000"));

            // Koşuyor → siliniyor.
            for (int i = 0; i < 100; i++) accumulator.Step(0.1f, 5f);
            float ran = accumulator.Accumulation;

            bool shakesOff = ran < snowed;
            all &= shakesOff;
            r.AppendLine("  [" + M(shakesOff) + "] Koşunca siliniyor   " + snowed.ToString("0.000") +
                         " → " + ran.ToString("0.000") + "  (10 sn, 5 m/s)");

            // Kapalı alan → hızla azalıyor.
            for (int i = 0; i < 200; i++) accumulator.Step(0.1f, 0f);
            float refilled = accumulator.Accumulation;

            accumulator.SetSkyVisibility(0f);
            for (int i = 0; i < 100; i++) accumulator.Step(0.1f, 0f);
            float sheltered = accumulator.Accumulation;

            bool shelters = sheltered < refilled;
            all &= shelters;
            r.AppendLine("  [" + M(shelters) + "] Kapalı alanda azalıyor " + refilled.ToString("0.000") +
                         " → " + sheltered.ToString("0.000"));

            // Yağmur → hızla sıfırlanıyor.
            accumulator.SetSkyVisibility(1f);
            for (int i = 0; i < 200; i++) accumulator.Step(0.1f, 0f);
            float beforeRain = accumulator.Accumulation;

            env.TemperatureC = 10f;              // kar durur, yağmur başlar
            snowfall.Tick(env);

            for (int i = 0; i < 50; i++) accumulator.Step(0.1f, 0f);
            float afterRain = accumulator.Accumulation;

            bool rainClears = afterRain < 0.01f && beforeRain > 0.5f;
            all &= rainClears;
            r.AppendLine("  [" + M(rainClears) + "] Yağmurda temizleniyor " + beforeRain.ToString("0.000") +
                         " → " + afterRain.ToString("0.000") + "  (5 sn)");
        }
        finally
        {
            Object.DestroyImmediate(go);
            snowfall.Reset();
        }

        return all;
    }

    // ------------------------------------------------------------------ shader

    static bool ShaderTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Shader");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        if (shader == null)
        {
            r.AppendLine("  [-] " + ShaderPath + " yüklenemedi.");
            return false;
        }

        bool hasError = ShaderUtil.ShaderHasError(shader);

        r.AppendLine("  [" + M(!hasError) + "] Derleme           " +
                     (hasError ? "HATA VAR" : "hatasız"));

        foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            r.AppendLine("      [" + m.severity + "] " + m.file + "(" + m.line + "): " + m.message);

        r.AppendLine("  [i] MEVCUT NESNE SHADER'LARI DEĞİŞTİRİLMEDİ (spec §16). " +
                     "Hangi nesnelerin bu shader'a geçeceği kullanıcının kararı; " +
                     "toplu materyal değişimi yapılmadı.");
        r.AppendLine("  [i] Karakter shader'ına `_SnowAccum` / `_SnowLineY` EKLENMEDİ " +
                     "(spec §1.4 onay istiyor). `SnowCharacterAccumulator` bu " +
                     "property'leri yazıyor; shader tanımıyorsa yazma sessizce yok sayılıyor.");

        return !hasError;
    }

    static string M(bool ok) => ok ? "+" : "-";

    // ------------------------------------------------------------------ sahte

    sealed class FakeEnvironment : ISnowEnvironmentSource
    {
        public Vector3 WindDirection { get; set; } = Vector3.right;
        public float WindSpeed { get; set; }
        public Light Sun => null;
        public float SunElevation01 { get; set; }
        public float TemperatureC { get; set; }
        public float FreezingLevelY { get; set; } = 1400f;
        public PrecipitationKind PrecipKind { get; set; }
        public float PrecipIntensity01 { get; set; }
        public float FogDensity01 { get; set; }
    }

    // ---------------------------------------------------------------- düzenek

    sealed class MaskProbe
    {
        readonly ComputeShader cs;
        readonly int kernel;

        RenderTexture output;
        RenderTexture sky;
        Texture2D breakup;
        readonly Texture2D readOne;

        static readonly Vector3 Position = new(0f, 100f, 0f);

        public MaskProbe(ComputeShader cs)
        {
            this.cs = cs;
            kernel = cs.FindKernel("KTestCoverMask");

            output = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat)
            {
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave,
            };
            output.Create();

            sky = new RenderTexture(4, 4, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            sky.Create();

            // GÜRÜLTÜ SABİT 0.5. Maske `raw - noise` yapıyor; rastgele bir
            // doku eşik ölçümünü teksele bağımlı kılardı.
            breakup = new Texture2D(2, 2, TextureFormat.R8, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var grey = new Color(0.5f, 0f, 0f, 0f);
            breakup.SetPixels(new[] { grey, grey, grey, grey });
            breakup.Apply(false, false);

            readOne = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, Vector3.up);
            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, SnowConstants.SkyAreaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, 4f);

            // Gürültü şiddeti 0 → `lerp(0.5, noise, 0)` = 0.5, sabit eşik.
            cs.SetFloat("_SnowSlopeThreshold", 0.25f);
            cs.SetFloat("_SnowSlopeSharpness", 1.6f);
            cs.SetFloat("_SnowBreakupScale", 1.8f);
            cs.SetFloat("_SnowBreakupStrength", 0f);
            cs.SetFloat("_SnowEdgeSharpness", 4f);
        }

        /// Açıkken örtü çok aşağıda (fark negatif → görünürlük 1),
        /// kapalıyken noktanın 3 m üstünde.
        public void SetSky(bool clear)
        {
            float value = clear ? -9999f : Position.y + 3f;

            var tex = new Texture2D(4, 4, TextureFormat.RFloat, false, true);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = new Color(value, 0f, 0f, 0f);
            tex.SetPixels(px);
            tex.Apply(false, false);

            Graphics.Blit(tex, sky);
            Object.DestroyImmediate(tex);
        }

        public void SetCoverage(float value) =>
            Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage, value);

        public float Sample(Vector3 normal, float ao)
        {
            cs.SetVector("_TestNormal", normal.normalized);
            cs.SetFloat("_TestAo", ao);
            cs.SetVector("_TestPosition", Position);

            cs.SetTexture(kernel, "_SnowBreakup", breakup);
            cs.SetTexture(kernel, SnowShaderIDs.SnowSkyVisTex, sky);
            cs.SetTexture(kernel, "_TestOut", output);
            cs.Dispatch(kernel, 1, 1, 1);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = output;
            readOne.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            readOne.Apply(false);
            RenderTexture.active = prev;

            return readOne.GetPixel(0, 0).r;
        }

        /// Verilen `dot(N, up)` değerine karşılık gelen normal.
        public float SampleAtSlope(float slope)
        {
            float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - slope * slope));
            return Sample(new Vector3(horizontal, slope, 0f), 1f);
        }

        /// Maskenin 0.5'i geçtiği eğim, ikiye bölerek. Formülü tekrar
        /// yazmadan ölçüyor; katsayılar değişirse sayı kendiliğinden kayar.
        public float FindCutInSlope()
        {
            float lo = 0f, hi = 1f;

            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (SampleAtSlope(mid) < 0.5f) lo = mid; else hi = mid;
            }

            return (lo + hi) * 0.5f;
        }

        public void Dispose()
        {
            Rel(ref output);
            Rel(ref sky);
            Object.DestroyImmediate(breakup);
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
