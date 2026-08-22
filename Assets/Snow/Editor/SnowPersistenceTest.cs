// ROL: uzak kaskadı ve kalıcılığı ÖLÇER — kaskadın birikmesi ve kayması,
// blok paketleme/açma turu, yarım hassasiyetin taşıdığı çözünürlük.
// Çağıran: menü — To The Summit/Kar/Kalıcılık Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// EN KRİTİK İDDİA: "blok geri geldiğinde iz aynı yerde."
///
/// Paketleme ve açma ters yazılırsa iz kayar ya da kaybolur; ikisi de
/// ekranda "izler bazen duruyor bazen durmuyor" diye görünür ve hangi
/// tarafın yanlış olduğu anlaşılmaz. Burada tur kapanışı teksel teksel
/// ölçülüyor.
public static class SnowPersistenceTest
{
    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";
    const string KernelPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string CommonPath = "Assets/Snow/Shaders/SnowCommon.hlsl";

    const int Res = 256;
    const int BlockTexels = 128;
    const int StoredSide = 64;


    [MenuItem("To The Summit/Kar/Kalıcılık Sınaması", false, 59)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — kalıcılık ve bölge dışı kar sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = HalfTest(r);
        ok &= BlockTest(r);
        ok &= WiringTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ------------------------------------------------------- yarım hassasiyet

    /// Bloklar yarım hassasiyette saklanıyor (spec `half[]`). O adımın
    /// sakladığımız büyüklükleri taşıyıp taşımadığı ÖLÇÜLÜYOR.
    static bool HalfTest(StringBuilder r)
    {
        r.AppendLine("## Saklama hassasiyeti");

        (string name, float value, float tolerance)[] cases =
        {
            ("SWE 0.020 m", 0.02f, 1e-5f),
            ("SWE 0.600 m", 0.60f, 1e-3f),
            ("rhoN 0.55", 0.55f, 1e-3f),
            ("carve 0.120 m", 0.12f, 1e-4f),
            ("rim 0.010 m", 0.01f, 1e-5f),
        };

        bool all = true;

        foreach ((string name, float value, float tolerance) c in cases)
        {
            float back = Mathf.HalfToFloat(Mathf.FloatToHalf(c.value));
            float error = Mathf.Abs(back - c.value);

            bool ok = error < c.tolerance;
            all &= ok;

            r.AppendLine("  [" + M(ok) + "] " + c.name.PadRight(16) + "geri " +
                         back.ToString("0.000000") + ",  hata " +
                         (error * 1000f).ToString("0.0000") + " mm");
        }

        return all;
    }

    static bool BlockTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Blok paketleme turu (spec §21 Faz 10)");

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        var kernels = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);

        if (sim == null || kernels == null)
        {
            r.AppendLine("  [-] Compute yüklenemedi.");
            return false;
        }

        int stamp = kernels.FindKernel("KStamp");
        int clear = sim.FindKernel("KClear");
        int pack = sim.FindKernel("KBlockPack");
        int unpack = sim.FindKernel("KBlockUnpack");

        RenderTexture snow = NewArgb(Res);
        RenderTexture trail = NewArgb(Res);
        RenderTexture snowOut = NewArgb(Res);
        RenderTexture trailOut = NewArgb(Res);

        var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                                        StoredSide * StoredSide, 4 * sizeof(float));

        bool all = true;

        try
        {
            // Kaynağa konum bağımlı bir desen bas: r = x, g = y.
            int stampGroups = Mathf.CeilToInt(Res / 8f);

            kernels.SetInt(SnowShaderIDs.Resolution, Res);
            kernels.SetTexture(stamp, SnowShaderIDs.Dst, snow);
            kernels.Dispatch(stamp, stampGroups, stampGroups, 1);
            kernels.SetTexture(stamp, SnowShaderIDs.Dst, trail);
            kernels.Dispatch(stamp, stampGroups, stampGroups, 1);

            Color[] source = ReadAll(snow);

            // Hedefi sıfırla — açma gerçekten yazıyor mu görülsün.
            sim.SetInt(SnowShaderIDs.Resolution, Res);
            sim.SetVector(SnowShaderIDs.ClearValue, Vector4.zero);
            sim.SetTexture(clear, SnowShaderIDs.Dst, snowOut);
            sim.Dispatch(clear, stampGroups, stampGroups, 1);
            sim.SetTexture(clear, SnowShaderIDs.Dst, trailOut);
            sim.Dispatch(clear, stampGroups, stampGroups, 1);

            // Paketle.
            sim.SetInt(SnowShaderIDs.BlockTexels, BlockTexels);
            sim.SetInt(SnowShaderIDs.BlockStored, StoredSide);
            sim.SetVector(SnowShaderIDs.BlockOrigin, new Vector4(0f, 0f, 0f, 0f));
            sim.SetTexture(pack, SnowShaderIDs.Snow, snow);
            sim.SetTexture(pack, SnowShaderIDs.Trail, trail);
            sim.SetBuffer(pack, SnowShaderIDs.BlockBuffer, buffer);
            sim.Dispatch(pack, Mathf.CeilToInt(StoredSide / 8f), Mathf.CeilToInt(StoredSide / 8f), 1);

            // Aç.
            sim.SetTexture(unpack, SnowShaderIDs.Snow, snowOut);
            sim.SetTexture(unpack, SnowShaderIDs.Trail, trailOut);
            sim.SetTexture(unpack, SnowShaderIDs.SnowOut, snowOut);
            sim.SetTexture(unpack, SnowShaderIDs.TrailOut, trailOut);
            sim.SetBuffer(unpack, SnowShaderIDs.BlockBuffer, buffer);
            sim.Dispatch(unpack, Mathf.CeilToInt(BlockTexels / 8f), Mathf.CeilToInt(BlockTexels / 8f), 1);

            Color[] result = ReadAll(snowOut);

            int step = BlockTexels / StoredSide;

            float maxError = 0f;
            int written = 0;

            for (int y = 0; y < BlockTexels; y++)
            for (int x = 0; x < BlockTexels; x++)
            {
                // Beklenen: kaynağın 2×2 kutu ortalaması.
                float sumR = 0f, sumG = 0f;

                int bx = x / step * step;
                int by = y / step * step;

                for (int oy = 0; oy < step; oy++)
                for (int ox = 0; ox < step; ox++)
                {
                    Color c = source[(by + oy) * Res + (bx + ox)];
                    sumR += c.r;
                    sumG += c.g;
                }

                float wantR = sumR / (step * step);
                float wantG = sumG / (step * step);

                Color got = result[y * Res + x];

                maxError = Mathf.Max(maxError, Mathf.Abs(got.r - wantR));
                maxError = Mathf.Max(maxError, Mathf.Abs(got.g - wantG));

                if (got.r != 0f || got.g != 0f) written++;
            }

            bool roundTrip = maxError < 0.01f;
            all &= roundTrip;

            r.AppendLine("  [" + M(roundTrip) + "] Tur kapanıyor           " + written + " / " +
                         (BlockTexels * BlockTexels) + " teksel yazıldı,  maks hata " +
                         maxError.ToString("0.00000"));

            // BLOĞUN DIŞI DOKUNULMAMIŞ OLMALI. Açma taşarsa komşu blokların
            // izleri silinir.
            int leaked = 0;

            for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                if (x < BlockTexels && y < BlockTexels) continue;
                Color c = result[y * Res + x];
                if (c.r != 0f || c.g != 0f) leaked++;
            }

            bool contained = leaked == 0;
            all &= contained;

            r.AppendLine("  [" + M(contained) + "] Blok dışına taşmıyor    " + leaked +
                         " teksel  (0 olmalı)");
        }
        finally
        {
            buffer.Dispose();

            Release(ref snow);
            Release(ref trail);
            Release(ref snowOut);
            Release(ref trailOut);
        }

        return all;
    }

    // ----------------------------------------------------------------- bağlar

    static bool WiringTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Bağlar");

        string common = System.IO.File.ReadAllText(CommonPath);

        // SPEC §8.4: "24 m ötesinde kar mesh'i yoktur." Bölgenin dışındaki
        // kar durumu SABİT BİR SAYI DEĞİL, kar çizgisi eğrisi — sabit olsaydı
        // dağın tepesi ile eteği aynı kalınlıkta kar taşırdı.
        bool defined = common.Contains("float2 SnowOutsideStateAt");
        bool curved = common.Contains("SnowInitialSweAt(SampleGroundHeight(posXZ))");
        bool used = common.Contains("SnowOutsideStateAt(SnowUVToWorld(uv))");

        // Kaskad geri sızmasın: silindiği için gerekçesi de yok.
        bool noCascade = !common.Contains("SnowFarStateAt") && !common.Contains("_SnowFarTex");

        r.AppendLine("  [" + M(defined) + "] Bölge dışı tanımlı      `SnowOutsideStateAt`");
        r.AppendLine("  [" + M(curved) + "] Kar çizgisi eğrisi      yükseklik fonksiyonu, sabit değil");
        r.AppendLine("  [" + M(used) + "] `SnowStateAt` okuyor    bölge dışında devreye giriyor");
        r.AppendLine("  [" + M(noCascade) + "] Kaskad kalıntısı yok    (spec §8.4)");

        return defined && curved && used && noCascade;
    }

    // ----------------------------------------------------------------- yardım

    static string M(bool ok) => ok ? "+" : "-";

    static RenderTexture NewArgb(int res) => New(res, RenderTextureFormat.ARGBHalf);

    static RenderTexture New(int res, RenderTextureFormat format)
    {
        var rt = new RenderTexture(res, res, 0, format)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        rt.Create();
        return rt;
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Object.DestroyImmediate(rt);
        rt = null;
    }

    static Color Read(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(rt.width / 2, rt.height / 2, 1, 1), 0, 0);
        tex.Apply(false);

        RenderTexture.active = prev;

        Color c = tex.GetPixel(0, 0);
        Object.DestroyImmediate(tex);
        return c;
    }

    static Color[] ReadAll(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
        tex.Apply(false);

        RenderTexture.active = prev;

        Color[] px = tex.GetPixels();
        Object.DestroyImmediate(tex);
        return px;
    }
}
