// ROL: kar püskürtmesini ve süspansiyon perdelerini ÖLÇER — V̇ formülü,
// eşikler, üstel yükseklik profili, zemin takibi.
// Çağıran: menü — To The Summit/Kar/Püskürtme Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// İKİ KATMAN KARIŞTIRILMIYOR (spec §18.7).
///
/// Saltasyon 1–5 cm, yüzeye yapışık ve yoğun; süspansiyon onun üstü,
/// pratikte ≤ 5 m, seyrek. Spec §22'nin belirtisi "spindrift 1 m yüksekliğe
/// çıkıyor" — ikisini aynı sisteme koymanın sonucu.
public static class SnowSprayTest
{
    const string ComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";
    const string CurtainShaderPath = "Assets/Snow/Shaders/SnowCurtain.shader";

    const int Capacity = 4096;
    const int Stride = 12 * sizeof(float);

    const float GroundY = 100f;

    [MenuItem("To The Summit/Kar/Püskürtme Sınaması", false, 61)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — püskürtme ve perde sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = SprayTest(r);
        ok &= CurtainGateTest(r);
        ok &= CurtainSimTest(r);
        ok &= ShaderTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ------------------------------------------------------------ püskürtme

    static bool SprayTest(StringBuilder r)
    {
        r.AppendLine("## Püskürtme miktarı (spec §18.6)");
        r.AppendLine("  [i] [KAYNAK: Sumner, O'Brien & Hodgins, CGF 1999]");

        const float Width = 0.11f;
        const float PerM3 = 40000f;

        // SPEC'İN KENDİ SAĞLAMASI: bot 0.11 m, batma 0.20 m, hız 4 m/s
        // → V̇ = 0.088 m³/s → gevşeklik 0.8'de saniyede ~2800 parçacık.
        var reference = new SnowSample
        {
            SinkDepth = 0.20f,
            Density01 = 0.20f,     // gevşeklik 0.80
            Valid = true,
        };

        float rate = SnowSprayController.RateFor(reference, 4f, Width, PerM3);

        bool sanity = Mathf.Abs(rate - 2816f) < 40f;

        r.AppendLine("  [" + (sanity ? "+" : "-") + "] Spec sağlaması    " +
                     rate.ToString("0") + " parçacık/s  (spec ~2800, V̇ = 0.11 × 0.20 × 4 = " +
                     (Width * 0.20f * 4f).ToString("0.000") + " m³/s)");

        bool all = sanity;

        // EŞİKLER. Üçü de spec §18.6'dan; biri eksikse "yürürken de
        // püskürtme çıkıyor" olur (spec §22).
        (string name, float sink, float density, float speed, bool wanted)[] cases =
        {
            ("yürüyüş (1.5 m/s)",   0.20f, 0.20f, 1.5f, false),
            ("koşu (4 m/s)",        0.20f, 0.20f, 4.0f, true),
            ("sığ kar (4 cm)",      0.04f, 0.20f, 4.0f, false),
            ("sıkışmış patika",     0.20f, 0.60f, 4.0f, false),
            ("veri yok",            0.20f, 0.20f, 4.0f, false),
        };

        for (int i = 0; i < cases.Length; i++)
        {
            (string name, float sink, float density, float speed, bool wanted) c = cases[i];

            var sample = new SnowSample
            {
                SinkDepth = c.sink,
                Density01 = c.density,
                Valid = i != cases.Length - 1,
            };

            float got = SnowSprayController.RateFor(sample, c.speed, Width, PerM3);
            bool ok = (got > 0f) == c.wanted;
            all &= ok;

            r.AppendLine("  [" + (ok ? "+" : "-") + "] " + c.name.PadRight(20) +
                         got.ToString("0").PadLeft(6) + " parçacık/s  (beklenen " +
                         (c.wanted ? "> 0" : "0") + ")");
        }

        // HIZA VE DERİNLİĞE GÖRÜNÜR ŞEKİLDE BAĞLI (spec §21 Faz 13).
        float slow = SnowSprayController.RateFor(reference, 3f, Width, PerM3);
        float fast = SnowSprayController.RateFor(reference, 6f, Width, PerM3);

        var deeper = reference;
        deeper.SinkDepth = 0.40f;
        float deep = SnowSprayController.RateFor(deeper, 4f, Width, PerM3);

        bool scales = Mathf.Abs(fast / Mathf.Max(slow, 1f) - 2f) < 0.01f &&
                      Mathf.Abs(deep / Mathf.Max(rate, 1f) - 2f) < 0.01f;

        all &= scales;

        r.AppendLine("  [" + (scales ? "+" : "-") + "] Hız ve derinlikle  3→6 m/s: " +
                     slow.ToString("0") + " → " + fast.ToString("0") +
                     ",  20→40 cm: " + rate.ToString("0") + " → " + deep.ToString("0") +
                     "  (ikisi de doğrusal)");

        return all;
    }

    // -------------------------------------------------------------- perde eşiği

    static bool CurtainGateTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Perde tetiği (spec §18.7 — §18.1 ile AYNI eşik)");

        float calm = SnowCurtainController.DriftActiveFor(5f, 0.9f);
        float onset = SnowCurtainController.DriftActiveFor(7f, 0.9f);
        float windy = SnowCurtainController.DriftActiveFor(12f, 0.9f);

        // SİNTERLENME EŞİĞİ YÜKSELTİYOR, YASAKLAMIYOR. Sıkışmış karda eşik
        // 10.7 m/s; 12 m/s onu aşıyor ve az da olsa savrulma başlıyor.
        // "Sıkışmışta hiç savrulmaz" beklemek modeli yanlış okumaktır.
        float packedWindy = SnowCurtainController.DriftActiveFor(12f, 0.05f);
        float packedModerate = SnowCurtainController.DriftActiveFor(9f, 0.05f);

        bool gated = calm <= 0f && onset > 0f && windy > onset &&
                     packedModerate <= 0f && packedWindy > 0f && packedWindy < windy * 0.5f;

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Eşik              5 m/s → " +
                     calm.ToString("0.00") + ",  7 m/s → " + onset.ToString("0.00") +
                     ",  12 m/s → " + windy.ToString("0.00"));

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Sinterlenme       sıkışmış karda 9 m/s → " +
                     packedModerate.ToString("0.00") + ",  12 m/s → " +
                     packedWindy.ToString("0.00") + "  (aynı rüzgârda gevşek kar " +
                     windy.ToString("0.00") + ")");

        r.AppendLine("  [i] Gevşek karda eşik 5 m/s, sıkışmışta 11 m/s. Ayrı bir perde " +
                     "eşiği TANIMLANMADI — §18.1'inkiyle aynı.");

        return gated;
    }

    // ------------------------------------------------------------ perde simülasyonu

    static bool CurtainSimTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Perde simülasyonu (spec §18.7)");

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        if (cs == null) { r.AppendLine("  [-] " + ComputePath + " yüklenemedi."); return false; }

        int kernel = cs.FindKernel("KCurtainUpdate");

        var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, Stride);
        Texture2D ground = FlatGround(GroundY);
        Texture2D sky = FlatSky(-9999f);

        bool all = true;

        try
        {
            buffer.SetData(new float[Capacity * 12]);

            Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(10f, 0f, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, 10f);
            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, 96f);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, 4f);

            cs.SetInt(SnowShaderIDs.FlakeCapacity, Capacity);
            cs.SetInt(SnowShaderIDs.FlakeAliveCount, Capacity);
            cs.SetFloat(SnowShaderIDs.SnowDeltaTime, 0.016f);
            cs.SetFloat(SnowShaderIDs.FlakeSeed, 3.7f);

            cs.SetVector(SnowShaderIDs.DriftOrigin, Vector3.zero);
            cs.SetFloat(SnowShaderIDs.CurtainDriftActive, 1f);
            cs.SetFloat(SnowShaderIDs.CurtainScaleH, SnowConstants.SuspScaleH);
            cs.SetFloat(SnowShaderIDs.CurtainAlphaBase, SnowConstants.SuspAlphaBase);
            cs.SetFloat(SnowShaderIDs.CurtainSpawnDistance, 35f);
            cs.SetFloat(SnowShaderIDs.CurtainSpawnWidth, 40f);

            cs.SetTexture(kernel, SnowShaderIDs.GroundHeightTex, ground);
            cs.SetTexture(kernel, SnowShaderIDs.SnowSkyVisTex, sky);
            cs.SetBuffer(kernel, SnowShaderIDs.Flakes, buffer);

            // İlk adım: hepsi doğuyor.
            cs.Dispatch(kernel, Mathf.CeilToInt(Capacity / 64f), 1, 1);

            var raw = new float[Capacity * 12];
            buffer.GetData(raw);

            float minHeight = float.MaxValue, maxHeight = 0f, meanHeight = 0f;
            int aboveCap = 0;

            for (int i = 0; i < Capacity; i++)
            {
                float h = raw[i * 12 + 1] - GroundY;

                minHeight = Mathf.Min(minHeight, h);
                maxHeight = Mathf.Max(maxHeight, h);
                meanHeight += h;

                if (h > SnowConstants.SuspMaxHeight + 1e-3f) aboveCap++;
            }

            meanHeight /= Capacity;

            // ÜSTEL DAĞILIM: ortalama = ölçek yüksekliği (1.1 m). Düz
            // dağılımda ortalama 2.5 m olurdu.
            bool exponential = Mathf.Abs(meanHeight - SnowConstants.SuspScaleH) <
                               SnowConstants.SuspScaleH * 0.25f;

            all &= exponential;

            r.AppendLine("  [" + M(exponential) + "] Üstel yükseklik   ortalama " +
                         meanHeight.ToString("0.000") + " m  (ölçek yüksekliği " +
                         SnowConstants.SuspScaleH.ToString("0.0") +
                         "; düz dağılımda 2.5 olurdu)");

            // PBSM ÜST SINIRI: 5 m. Üstü artık savrulan kar değil, yağıştır.
            bool capped = aboveCap == 0 && maxHeight <= SnowConstants.SuspMaxHeight + 1e-3f;
            all &= capped;

            r.AppendLine("  [" + M(capped) + "] 5 m tavanı        en yüksek " +
                         maxHeight.ToString("0.000") + " m,  aşan " + aboveCap +
                         "  (PBSM üst sınırı)");

            // ALPHA YÜKSELDİKÇE SOLUYOR. Sabit alpha "yükseklikten bağımsız
            // opak" belirtisini verirdi (spec §22).
            cs.Dispatch(kernel, Mathf.CeilToInt(Capacity / 64f), 1, 1);
            buffer.GetData(raw);

            float lowAlpha = 0f, highAlpha = 0f;
            int lowCount = 0, highCount = 0;

            for (int i = 0; i < Capacity; i++)
            {
                float h = raw[i * 12 + 1] - GroundY;
                float a = raw[i * 12 + 11];

                if (h < 0.6f) { lowAlpha += a; lowCount++; }
                else if (h > 2.5f) { highAlpha += a; highCount++; }
            }

            lowAlpha /= Mathf.Max(1, lowCount);
            highAlpha /= Mathf.Max(1, highCount);

            bool fades = lowCount > 0 && highCount > 0 && highAlpha < lowAlpha * 0.5f;
            all &= fades;

            r.AppendLine("  [" + M(fades) + "] Yükseldikçe soluyor  0–0.6 m → " +
                         lowAlpha.ToString("0.0000") + ",  2.5 m üstü → " +
                         highAlpha.ToString("0.0000"));

            // ZEMİNİ TAKİP EDİYOR: arazinin içine girmiyor.
            int belowGround = 0;
            for (int i = 0; i < Capacity; i++)
                if (raw[i * 12 + 1] < GroundY + 0.14f) belowGround++;

            bool follows = belowGround == 0;
            all &= follows;

            r.AppendLine("  [" + M(follows) + "] Zemini takip ediyor  " + belowGround +
                         " perde arazinin içinde  (0 olmalı)");
        }
        finally
        {
            buffer.Dispose();
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(sky);
        }

        return all;
    }

    // ------------------------------------------------------------------ shader

    static bool ShaderTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Shader");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(CurtainShaderPath);
        if (shader == null) { r.AppendLine("  [-] " + CurtainShaderPath + " yüklenemedi."); return false; }

        bool hasError = ShaderUtil.ShaderHasError(shader);

        r.AppendLine("  [" + M(!hasError) + "] Derleme           " +
                     (hasError ? "HATA VAR" : "hatasız"));

        foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            r.AppendLine("      [" + m.severity + "] " + m.file + "(" + m.line + "): " + m.message);

        string source = System.IO.File.ReadAllText(CurtainShaderPath);

        (string needle, string symptom)[] checks =
        {
            ("_NearFade", "İçinden geçerken ekran beyaza boğuluyor"),
            ("_FogDensity01", "Perdeler sisle çakışıyor"),
            ("SampleSceneDepth", "Yumuşak parçacık yok"),
            ("_ScrollSpeed", "Perde akmıyor"),
        };

        bool all = !hasError;

        foreach ((string needle, string symptom) c in checks)
        {
            bool found = source.Contains(c.needle);
            all &= found;
            r.AppendLine("  [" + M(found) + "] " + c.needle.PadRight(20) +
                         (found ? "" : "EKSİK → " + c.symptom));
        }

        return all;
    }

    // ----------------------------------------------------------------- yardım

    static string M(bool ok) => ok ? "+" : "-";

    static Texture2D FlatGround(float y)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var half = new Color(0.5f, 0f, 0f, 0f);
        tex.SetPixels(new[] { half, half, half, half });
        tex.Apply(false, false);

        Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ, new Vector4(-500f, -500f, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ, new Vector4(1000f, 1000f, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ, new Vector4(500f, 500f, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, y - 1f);
        Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, 2f);

        return tex;
    }

    static Texture2D FlatSky(float value)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = new Color(value, 0f, 0f, 0f);

        tex.SetPixels(px);
        tex.Apply(false, false);

        return tex;
    }
}
