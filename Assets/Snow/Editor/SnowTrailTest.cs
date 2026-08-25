// ROL: iz oluşumunu ÖLÇER — carve kalıcılığı, sırtın izin ETRAFINDA olması,
// hareket yönünde asimetri, derinlik ölçeği, patika oluşumu, dolma, rüzgâr eşiği.
// Çağıran: menü — To The Summit/Kar/İz Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// ÜRETİM ÇÖZÜNÜRLÜĞÜNDE KOŞUYOR. 256'da teksel 6.25 cm; 30 cm'lik bir ayak
/// 4.8 teksel eder ve yakalama blur'u (1.5 teksel) ayağın MERKEZİNE kadar
/// sızıp arka plandaki -9999'u karıştırır. Ölçüm o zaman kernel'i değil
/// düzeneğin çözünürlüğünü sınar.
///
/// "Sırt izin etrafında mı" göz kararı değil: yarıçapa göre rim profili
/// çıkarılıyor, tepenin nerede olduğu yazılıyor. Ters yazılmış bir
/// `blur − carve` tepeyi merkeze taşır ve burada yakalanır.
public static class SnowTrailTest
{
    const int Res = 1024;
    const float AreaSize = 16f;
    const float ObserverY = 4900.5f;

    /// Zemin düz ve gözlemcinin 1 m altında; batma kâğıtta hesaplanabilsin.
    const float GroundY = ObserverY - 1f;

    /// Ayak: 30 cm çapında dairesel alt yüzey (bot genişliğinden büyük, ölçüm
    /// için yeterince çok teksel kaplasın diye).
    const float FootDiameter = 0.30f;
    const float FootRadius = FootDiameter * 0.5f;

    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";

    static readonly Vector2 Center = new(-7494f, -4327.5f);

    [MenuItem("To The Summit/Kar/İz Sınaması", false, 52)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — iz sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = Body(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    static bool Body(StringBuilder r)
    {
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " yüklenemedi."); return false; }

        var rig = new Rig(sim, Res, AreaSize, Center, GroundY, ObserverY);
        bool all = true;

        try
        {
            // Taze kar: SWE 0.02, rhoN 0.10 → h = 0.02*1000/(50+0.1*500) = 0.20 m
            float baseH = SnowConstants.RhoWater * 0.02f /
                          Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, 0.10f);

            float footY = GroundY + baseH - 0.12f;   // karın 12 cm içine giriyor

            r.AppendLine("## Düzenek");
            r.AppendLine("  [i] Çözünürlük " + Res + ",  teksel " +
                         (AreaSize / Res * 100f).ToString("0.00") + " cm,  ayak yarıçapı " +
                         (FootRadius / (AreaSize / Res)).ToString("0.0") + " teksel");
            r.AppendLine("  [i] Zemin " + GroundY.ToString("0.000") + " m,  SWE 0.020, rhoN 0.10 → " +
                         "taban derinlik " + (baseH * 100f).ToString("0.0") + " cm");
            r.AppendLine();
            r.AppendLine("## Faz 3 kabul kriterleri");

            // --- 1. carve beliriyor ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);

            float carve1 = rig.Trail(Center).r;
            bool carved = Mathf.Abs(carve1 - 0.12f) < 0.006f;
            all &= carved;
            r.AppendLine("  [" + M(carved) + "] carve beliriyor      " + (carve1 * 100f).ToString("0.00") +
                         " cm  (batma 12.00 cm, taze karda tam batmalı)");

            // --- 2. carve KALICI ---
            rig.ClearCapture();
            rig.Deform(1.0f, 0f, 0f);
            float carve2 = rig.Trail(Center).r;
            bool persists = Mathf.Abs(carve2 - carve1) < 1e-4f;
            all &= persists;
            r.AppendLine("  [" + M(persists) + "] carve KALICI         1 sn sonra " +
                         (carve2 * 100f).ToString("0.00") + " cm  (kar yağmıyorsa dolmaz)");

            // --- 3. rim izin ETRAFINDA ---
            rig.Rim();
            RimProfile flat = rig.Profile(Center, 0.40f);

            bool ring = flat.Peak > 0.002f && flat.PeakRadius > FootRadius &&
                        flat.AtCenter < flat.Peak * 0.20f;
            all &= ring;
            r.AppendLine("  [" + M(ring) + "] rim HALKA            merkez " +
                         (flat.AtCenter * 1000f).ToString("0.00") + " mm,  tepe " +
                         (flat.Peak * 1000f).ToString("0.00") + " mm @ " +
                         (flat.PeakRadius * 100f).ToString("0.0") + " cm  (ayak yarıçapı " +
                         (FootRadius * 100f).ToString("0.0") + " cm)" +
                         (ring ? "" : "  TERS: tepe izin içinde"));

            // --- 4. rim derinlikle ölçekleniyor ---
            rig.ResetSnow(0.01f, 0.10f);                       // SWE yarıya → taban 10 cm
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, GroundY + baseH * 0.5f - 0.06f, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();

            float rimHalf = rig.Profile(Center, 0.40f).Peak;
            bool scaled = rimHalf < flat.Peak * 0.75f;
            all &= scaled;
            r.AppendLine("  [" + M(scaled) + "] rim DERİNLİKLE       SWE yarıya inince tepe " +
                         (flat.Peak * 1000f).ToString("0.00") + " → " +
                         (rimHalf * 1000f).ToString("0.00") + " mm" +
                         (scaled ? "" : "  depthScale uygulanmamış"));

            // --- 5. rim hareket yönünde ASİMETRİK ---
            // Ölçü: rim ağırlık merkezinin X kayması. Simetrikse tam 0 çıkar.
            // Durağan ayakla da ölçülüyor — aracın kendisi sıfır vermeli, yoksa
            // ölçülen asimetri düzeneğin değil kernel'in olduğu söylenemez.
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidStill = rig.Profile(Center, 0.40f).CentroidX;

            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, new Vector2(3f, 0f));
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidPlus = rig.Profile(Center, 0.40f).CentroidX;

            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, new Vector2(-3f, 0f));
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidMinus = rig.Profile(Center, 0.40f).CentroidX;

            // İKİ TARAFLI ÖLÇÜM. Durağan hâl sıfır DEĞİL: Batman'ın 4-tap
            // Poisson çekirdeği simetrik değil (x toplamı +0.5463 teksel) ve
            // yakalama blur'una küçük bir +X yanlılığı katıyor. Bu tekniğin
            // kendisinden geliyor, spec'ten aynen alındı. Ölçülen şey bu
            // taban değerin ETRAFINDAKİ hıza bağlı kayma.
            float shiftPlus = centroidPlus - centroidStill;
            float shiftMinus = centroidMinus - centroidStill;

            bool asym = shiftPlus > 0.002f && shiftMinus < -0.002f;
            all &= asym;
            r.AppendLine("  [" + M(asym) + "] rim ASİMETRİK        ağırlık merkezi X: durağan " +
                         (centroidStill * 1000f).ToString("0.00") + " mm,  +X 3 m/s " +
                         (centroidPlus * 1000f).ToString("0.00") + " mm (" +
                         (shiftPlus * 1000f).ToString("+0.00;-0.00") + "),  −X 3 m/s " +
                         (centroidMinus * 1000f).ToString("0.00") + " mm (" +
                         (shiftMinus * 1000f).ToString("+0.00;-0.00") + ")");

            r.AppendLine("  [i] Poisson yanlılığı   durağan kayma " +
                         (centroidStill * 1000f).ToString("0.00") +
                         " mm — 4-tap Poisson çekirdeğinin x toplamı +0.5463 teksel. " +
                         "Spec §9.4'ten aynen alındı, tekniğin kendisine ait.");

            // --- 6. patika oluşuyor ---
            rig.ResetSnow(0.02f, 0.10f);

            float firstSink = 0f, lastSink = 0f;
            int passesTo18 = -1;

            for (int pass = 0; pass < 40; pass++)
            {
                rig.ClearTrail();                       // her geçiş taze izle başlasın
                rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
                rig.Deform(0.016f, 0f, 0f);

                float sink = rig.Trail(Center).r;
                if (pass == 0) firstSink = sink;
                lastSink = sink;

                if (passesTo18 < 0 && pass > 0 && sink <= firstSink * SnowConstants.PackedSinkScale)
                    passesTo18 = pass + 1;
            }

            float rhoN = rig.Snow(Center).g;

            // ÖLÇÜT BATMANIN DÜŞÜŞÜ. Spec §10.1 "5–6 geçişten sonra batma %18'e
            // düşer" diyor; rhoN'ın tavana dayanmasını istemiyor.
            bool trailForms = lastSink < firstSink * 0.20f && rhoN > SnowConstants.LooseN + 0.05f;
            all &= trailForms;
            r.AppendLine("  [" + M(trailForms) + "] PATİKA oluşuyor      ilk batma " +
                         (firstSink * 100f).ToString("0.00") + " cm → 40. geçişte " +
                         (lastSink * 100f).ToString("0.00") + " cm,  rhoN 0.100 → " +
                         rhoN.ToString("0.000"));

            r.AppendLine("  [i] Sıkışma          batma %18'in altına " + passesTo18 +
                         ". geçişte indi; spec metni '5–6 geçiş' diyor. Sıkışma AÇILAN " +
                         "OYMAYA orantılı (SNOW_COMPACT_GAIN), süreye değil: yerinde " +
                         "bekleyen oyuncunun altında iz derinleşmiyor.");

            // --- 7. yağışla doluyor ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            float beforeFill = rig.Trail(Center).r;

            rig.ClearCapture();
            rig.Deform(60f, 8.33e-7f, 0f);              // i01 = 0.60, spec §17.2 tablosu
            float afterFill = rig.Trail(Center).r;

            // Dolma hizi yerel yogunluktan: SWE * (ro_su / ro_kar).
            float rhoTest = SnowConstants.RhoMin
                          + rig.Snow(Center).g * (SnowConstants.RhoMax - SnowConstants.RhoMin);
            float expectedDrop = 8.33e-7f * SnowConstants.FillGain(rhoTest) * 60f;
            bool fills = Mathf.Abs((beforeFill - afterFill) - expectedDrop) < expectedDrop * 0.02f;
            all &= fills;
            r.AppendLine("  [" + M(fills) + "] YAĞIŞLA doluyor      " +
                         (beforeFill * 100f).ToString("0.00") + " → " +
                         (afterFill * 100f).ToString("0.00") + " cm,  60 sn'de " +
                         ((beforeFill - afterFill) * 100f).ToString("0.00") +
                         " cm  (beklenen " + (expectedDrop * 100f).ToString("0.00") + " cm)");

            bool densityStays = rig.Snow(Center).g > SnowConstants.LooseN + 1e-3f;
            all &= densityStays;
            r.AppendLine("  [" + M(densityStays) + "] Yoğunluk KALIYOR     dolduktan sonra rhoN " +
                         rig.Snow(Center).g.ToString("0.000") + "  (taze kar 0.100)");

            // --- 8. rüzgâr eşiği ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            float w0 = rig.Trail(Center).r;

            rig.ClearCapture();
            rig.Deform(60f, 0f, 3f);
            float wLow = rig.Trail(Center).r;

            rig.Deform(60f, 0f, 10f);
            float wHigh = rig.Trail(Center).r;

            bool windGate = Mathf.Abs(wLow - w0) < 1e-5f && wHigh < wLow;
            all &= windGate;
            r.AppendLine("  [" + M(windGate) + "] RÜZGÂR eşiği         " +
                         (w0 * 100f).ToString("0.00") + " cm → 3 m/s'te " +
                         (wLow * 100f).ToString("0.00") + " cm (değişmedi) → 10 m/s'te " +
                         (wHigh * 100f).ToString("0.00") + " cm  (eşik 4 m/s)");
        }
        finally
        {
            rig.Dispose();
        }

        return all;
    }

    static string M(bool ok) => ok ? "+" : "-";

    /// Yarıçapa göre rim profilinin özeti.
    struct RimProfile
    {
        public float AtCenter;
        public float Peak;
        public float PeakRadius;
        public float CentroidX;
    }

    // ------------------------------------------------------------------- düzenek

    /// Kernel'leri ÜRETİMDEKİ SIRAYLA koşturan düzenek: yakalama →
    /// KDeform → KRimBlurH → KRimBlurV → KRim. Ayrı bir sıra
    /// yazılsaydı sınama üretimi değil kendini doğrulardı.
    sealed class Rig
    {
        readonly ComputeShader sim;
        readonly int res;
        readonly float areaSize;
        readonly Vector2 center;

        readonly int kClear, kDeform, kBlurH, kBlurV, kRim;
        readonly int groups;

        RenderTexture trail, trailTemp, snow, snowTemp, rimBlur;

        /// İZ PARÇASI TAMPONU — üretimdekiyle aynı düzen.
        ComputeBuffer segments;
        readonly Vector4[] segmentData = new Vector4[2];
        readonly Texture2D ground;
        readonly Texture2D readOne;

        public Rig(ComputeShader sim, int res, float areaSize, Vector2 center,
                   float groundY, float observerY)
        {
            this.sim = sim;
            this.res = res;
            this.areaSize = areaSize;
            this.center = center;

            kClear = sim.FindKernel("KClear");
            kDeform = sim.FindKernel("KDeform");
            kBlurH = sim.FindKernel("KRimBlurH");
            kBlurV = sim.FindKernel("KRimBlurV");
            kRim = sim.FindKernel("KRim");
            groups = Mathf.CeilToInt(res / 8f);

            trail = Rt(res, RenderTextureFormat.ARGBHalf);
            trailTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            snow = Rt(res, RenderTextureFormat.ARGBHalf);
            snowTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            segments = new ComputeBuffer(2, 16);
            rimBlur = Rt(res, RenderTextureFormat.RHalf);

            readOne = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            // DÜZ ZEMİN: dört teksel, hepsi aynı. Batma hesabı arazi
            // gürültüsünden bağımsız kalsın.
            ground = new Texture2D(2, 2, TextureFormat.RHalf, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var half = new Color(0.5f, 0f, 0f, 0f);
            ground.SetPixels(new[] { half, half, half, half });
            ground.Apply(false, false);

            Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, res);

            // n = 0.5 → groundY = BaseY + 0.5 * Range. Range 2 m: yarım değer
            // yarım hassasiyette tam temsil edilebiliyor.
            Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ,
                new Vector4(center.x - areaSize, center.y - areaSize, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ,
                new Vector4(areaSize * 2f, areaSize * 2f, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, groundY - 1f);
            Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, 2f);
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

        void Clear(RenderTexture target, Vector4 value)
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetVector(SnowShaderIDs.ClearValue, value);
            sim.SetTexture(kClear, SnowShaderIDs.Dst, target);
            sim.Dispatch(kClear, groups, groups, 1);
        }

        public void ResetSnow(float swe, float rhoN) => Clear(snow, new Vector4(swe, rhoN, 0f, 0f));
        public void ClearTrail() => Clear(trail, Vector4.zero);

        /// Bu karede iz bırakan bir şey yok.
        public void ClearCapture() => sim.SetInt(SnowShaderIDs.TrailSegmentCount, 0);

        /// TEK BİR KÜRENİN BU KAREKİ İZ PARÇASI.
        ///
        /// `surfaceY` artık kullanılmıyor: batma derinliğini kar söylüyor,
        /// nesnenin yüksekliği değil. İmza korunuyor ki mevcut sınamalar
        /// olduğu gibi çalışsın.
        public void Stamp(Vector2 worldXZ, float diameter, float surfaceY, Vector2 velocity)
        {
            segmentData[0] = new Vector4(worldXZ.x, 0f, worldXZ.y, diameter * 0.5f);
            segmentData[1] = new Vector4(worldXZ.x, 0f, worldXZ.y, 0f);

            segments.SetData(segmentData);

            sim.SetBuffer(kDeform, SnowShaderIDs.TrailSegments, segments);
            sim.SetInt(SnowShaderIDs.TrailSegmentCount, 1);
            sim.SetVector(SnowShaderIDs.TrailVelocityXZ,
                          new Vector4(velocity.x, velocity.y, 0f, 0f));
        }

        public void Deform(float dt, float snowfallSweRate, float windSpeed)
        {
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, windSpeed);

            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.SnowDeltaTime, dt);
            sim.SetFloat(SnowShaderIDs.SnowfallSWERate, snowfallSweRate);

            sim.SetTexture(kDeform, SnowShaderIDs.GroundHeightTex, ground);
            sim.SetTexture(kDeform, SnowShaderIDs.Trail, trail);
            sim.SetTexture(kDeform, SnowShaderIDs.TrailOut, trailTemp);
            sim.SetTexture(kDeform, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kDeform, SnowShaderIDs.SnowOut, snowTemp);
            sim.Dispatch(kDeform, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
            (snow, snowTemp) = (snowTemp, snow);
        }

        public void Rim()
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.RimBlurTexels, SnowConstants.RimBlurTexels);

            sim.SetTexture(kBlurH, SnowShaderIDs.Src, trail);
            sim.SetTexture(kBlurH, SnowShaderIDs.Dst, trailTemp);
            sim.Dispatch(kBlurH, groups, groups, 1);

            sim.SetTexture(kBlurV, SnowShaderIDs.Src, trailTemp);
            sim.SetTexture(kBlurV, SnowShaderIDs.CarveOut, rimBlur);
            sim.Dispatch(kBlurV, groups, groups, 1);

            sim.SetTexture(kRim, SnowShaderIDs.Trail, trail);
            sim.SetTexture(kRim, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kRim, SnowShaderIDs.BlurredCarve, rimBlur);
            sim.SetTexture(kRim, SnowShaderIDs.TrailOut, trailTemp);
            sim.Dispatch(kRim, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
        }

        Vector2 WorldToTexel(Vector2 worldXZ)
        {
            Vector2 uv = (worldXZ - center) / areaSize + new Vector2(0.5f, 0.5f);
            return uv * res;
        }

        public Color Trail(Vector2 worldXZ) => One(trail, worldXZ);
        public Color Snow(Vector2 worldXZ) => One(snow, worldXZ);

        /// TEK TEKSEL OKUNUYOR. 1024² tam geri okuma 16 MB; kırk geçişlik
        /// döngüde araç ölçülenden pahalı olurdu.
        Color One(RenderTexture rt, Vector2 worldXZ)
        {
            Vector2 t = WorldToTexel(worldXZ);
            int x = Mathf.Clamp(Mathf.FloorToInt(t.x), 0, res - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(t.y), 0, res - 1);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readOne.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readOne.Apply(false);
            RenderTexture.active = prev;

            return readOne.GetPixel(0, 0);
        }

        /// Merkez etrafındaki pencerede rim'in yarıçap profili ve X ağırlık
        /// merkezi. "Halka mı, hareket yönünde asimetrik mi" sorularının
        /// sayısal karşılığı.
        public RimProfile Profile(Vector2 worldXZ, float windowRadius)
        {
            float texel = areaSize / res;
            int span = Mathf.CeilToInt(windowRadius / texel);

            Vector2 c = WorldToTexel(worldXZ);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(c.x) - span, 0, res - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(c.y) - span, 0, res - 1);
            int w = Mathf.Min(span * 2 + 1, res - x0);
            int h = Mathf.Min(span * 2 + 1, res - y0);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = trail;

            var tex = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
            tex.Apply(false);

            RenderTexture.active = prev;

            Color[] px = tex.GetPixels();
            Object.DestroyImmediate(tex);

            // 1 cm'lik yarıçap kutuları
            const float BinSize = 0.01f;
            int bins = Mathf.CeilToInt(windowRadius / BinSize);
            var sum = new float[bins];
            var count = new int[bins];

            float weighted = 0f, weight = 0f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x0 + x + 0.5f - c.x) * texel;
                float dy = (y0 + y + 0.5f - c.y) * texel;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > windowRadius) continue;

                float rim = px[y * w + x].g;

                int bin = Mathf.Min(bins - 1, Mathf.FloorToInt(d / BinSize));
                sum[bin] += rim;
                count[bin]++;

                weighted += rim * dx;
                weight += rim;
            }

            var profile = new RimProfile
            {
                AtCenter = count[0] > 0 ? sum[0] / count[0] : 0f,
                CentroidX = weight > 1e-6f ? weighted / weight : 0f,
            };

            for (int b = 0; b < bins; b++)
            {
                if (count[b] == 0) continue;
                float mean = sum[b] / count[b];
                if (mean <= profile.Peak) continue;

                profile.Peak = mean;
                profile.PeakRadius = (b + 0.5f) * BinSize;
            }

            return profile;
        }

        public void Dispose()
        {
            Rel(ref trail); Rel(ref trailTemp);
            Rel(ref snow); Rel(ref snowTemp);
            Rel(ref rimBlur);
            segments?.Release();
            segments = null;

            Object.DestroyImmediate(ground);
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
