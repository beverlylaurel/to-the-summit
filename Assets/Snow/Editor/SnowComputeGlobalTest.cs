// ROL: `Shader.SetGlobalFloat` ile yazilan bir degerin compute shader'a ulasip
// ulasmadigini olcer.
// Cagiran: SnowTestRunner.

using System.Text;
using UnityEngine;
using UnityEditor;

/// COMPUTE GLOBAL'LERI GORUYOR MU.
///
/// Kar sisteminin bircok parametresi yalniz `Shader.SetGlobalFloat` ile
/// yaziliyor ve compute kernel'leri onlari okuyor. Ulasmiyorsa kernel sessizce
/// SIFIR okur: durum dokusu bos kalir, ekranda "kar cizgisi calismiyor" gibi
/// gorunur. Hatirlamak yerine olculuyor.
public static class SnowComputeGlobalTest
{
    const string Path = "Assets/Snow/Editor/SnowTestKernels.compute";

    public static string Run(out bool pass)
    {
        var r = new StringBuilder();
        pass = false;

        r.AppendLine("# Kar — compute global'leri goruyor mu");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(Path);

        if (cs == null) { r.AppendLine("  [-] Test compute bulunamadi."); return r.ToString(); }

        const float A = 12.345f;
        const float B = -6.789f;

        Shader.SetGlobalFloat("_ProbeGlobalA", A);
        Shader.SetGlobalFloat("_ProbeGlobalB", B);

        var rt = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat)
        { enableRandomWrite = true, hideFlags = HideFlags.HideAndDontSave };
        rt.Create();

        int k = cs.FindKernel("KGlobalProbe");
        cs.SetTexture(k, "_Dst", rt);
        cs.Dispatch(k, 1, 1, 1);

        var tex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        Color c = tex.GetPixel(0, 0);

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);

        bool okA = Mathf.Abs(c.r - A) < 0.001f;
        bool okB = Mathf.Abs(c.g - B) < 0.001f;
        pass = okA && okB;

        r.AppendLine("  yazilan                  A = " + A + "   B = " + B);
        r.AppendLine("  compute'ta okunan        A = " + c.r + "   B = " + c.g);
        r.AppendLine();
        r.AppendLine("  [" + (pass ? "+" : "-") + "] Global compute'a ulasiyor mu");

        if (!pass)
        {
            r.AppendLine();
            r.AppendLine("      ULASMIYOR. `Shader.SetGlobalFloat` ile yazilan her parametre");
            r.AppendLine("      compute kernel'inde SIFIR okunuyor. Compute'a ihtiyac duyulan");
            r.AppendLine("      her deger `cmd.SetComputeFloatParam` ile ayrica gecirilmeli.");
        }

        r.AppendLine();
        r.AppendLine("SONUC: " + (pass ? "TAMAM" : "BASARISIZ"));
        return r.ToString();
    }
}
