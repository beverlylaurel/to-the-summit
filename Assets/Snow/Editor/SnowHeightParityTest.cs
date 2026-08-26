// ROL: kar yüzeyi yükseklik fonksiyonunun GPU ve CPU sürümlerinin aynı
// sonucu verdiğini doğrular.
// Çağıran: menü (To The Summit/Kar/Yükseklik Eşliğini Sına).

using System.Text;
using UnityEditor;
using UnityEngine;

/// İKİ DİLDE YAZILMIŞ TEK FORMÜLÜN SINAMASI.
///
/// `SnowSurfaceHeight` `SnowRelief.hlsl`'in ikizi. İkisi ayrışırsa karakter
/// gördüğü yüzeyin üstünde ya da altında yürümeye başlar ve belirti YAVAŞ
/// büyür — bir sabit değiştiğinde tek tarafta unutulur. Bu test o sapmayı
/// değiştirildiği anda yakalıyor.
///
/// `SnowConstantsTest` sabitlerin eşitliğini sınıyor; bu test FORMÜLÜN
/// eşitliğini. İkisi ayrı: sabitler aynı olup çeviri yanlış olabilir.
public static class SnowHeightParityTest
{
    const int OrnekSayisi = 512;

    /// Tolerans 1 mm. Kayan nokta farkı bundan çok küçük (GPU ve CPU aynı
    /// IEEE-754 tek duyarlığı kullanıyor); 1 mm'yi aşan sapma çeviri hatası
    /// demektir, yuvarlama değil.
    const float ToleransMetre = 0.001f;

    [MenuItem("To The Summit/Kar/Yükseklik Eşliğini Sına", false, 61)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nEŞLİK BOZUK."));

    public static string Run(out bool ok)
    {
        ok = true;
        var rapor = new StringBuilder();

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Snow/Shaders/SnowHeightProbe.compute");

        if (compute == null)
        {
            ok = false;
            return "SnowHeightProbe.compute bulunamadı.";
        }

        // TEŞHİS ANAHTARLARI KAPATILIYOR. Açık kalan bir anahtar GPU tarafında
        // bir katmanı siler, CPU tarafı onu bilmez ve test sahte kırmızı verir.
        var anahtarlar = new[] { "_SnowDbgNoFbm", "_SnowDbgNoRipple",
                                 "_SnowDbgNoSastrugi", "_SnowDbgNoDrift" };
        var eskiAnahtar = new float[anahtarlar.Length];
        for (int i = 0; i < anahtarlar.Length; i++)
        {
            eskiAnahtar[i] = Shader.GetGlobalFloat(anahtarlar[i]);
            Shader.SetGlobalFloat(anahtarlar[i], 0f);
        }

        // RÜZGÂR YÖNÜ İKİ TARAFA DA AYNI VERİLİYOR. GPU `_SastrugiWindDir`
        // globalini okuyor; CPU'ya parametre olarak aynısı geçiyor.
        Vector4 wd = Shader.GetGlobalVector("_SastrugiWindDir");
        Vector2 windDir = new(wd.x, wd.y);
        if (windDir.sqrMagnitude < 1e-6f)
        {
            windDir = Vector2.right;
            Shader.SetGlobalVector("_SastrugiWindDir", new Vector4(1f, 0f, 0f, 0f));
        }

        // TOHUM SABİT: test her koşuda aynı noktaları sınıyor. Rastgele
        // tohumla bir koşuda geçip ötekinde kalan bir sapma teşhis edilemez.
        var rnd = new System.Random(20260827);

        var konum = new Vector2[OrnekSayisi];
        var derinlik = new float[OrnekSayisi];
        var maruziyet = new float[OrnekSayisi];

        for (int i = 0; i < OrnekSayisi; i++)
        {
            konum[i] = new Vector2((float)(rnd.NextDouble() * 2000.0 - 1000.0),
                                   (float)(rnd.NextDouble() * 2000.0 - 1000.0));
            derinlik[i] = (float)(rnd.NextDouble() * 0.8);
            maruziyet[i] = (float)rnd.NextDouble();
        }

        var bufKonum = new ComputeBuffer(OrnekSayisi, sizeof(float) * 2);
        var bufDerinlik = new ComputeBuffer(OrnekSayisi, sizeof(float));
        var bufMaruz = new ComputeBuffer(OrnekSayisi, sizeof(float));
        var bufSonuc = new ComputeBuffer(OrnekSayisi, sizeof(float));

        bufKonum.SetData(konum);
        bufDerinlik.SetData(derinlik);
        bufMaruz.SetData(maruziyet);

        int k = compute.FindKernel("KHeightProbe");
        compute.SetBuffer(k, "_ProbePositions", bufKonum);
        compute.SetBuffer(k, "_ProbeDepths", bufDerinlik);
        compute.SetBuffer(k, "_ProbeExposure", bufMaruz);
        compute.SetBuffer(k, "_ProbeResult", bufSonuc);
        compute.SetInt("_ProbeCount", OrnekSayisi);
        compute.Dispatch(k, (OrnekSayisi + 63) / 64, 1, 1);

        var gpu = new float[OrnekSayisi];
        bufSonuc.GetData(gpu);

        bufKonum.Release();
        bufDerinlik.Release();
        bufMaruz.Release();
        bufSonuc.Release();

        for (int i = 0; i < anahtarlar.Length; i++)
            Shader.SetGlobalFloat(anahtarlar[i], eskiAnahtar[i]);

        float enBuyukSapma = 0f;
        int bozuk = 0;

        for (int i = 0; i < OrnekSayisi; i++)
        {
            float cpu = SnowSurfaceHeight.Rolyef(konum[i], derinlik[i],
                                                 windDir, maruziyet[i]);
            float sapma = Mathf.Abs(cpu - gpu[i]);

            if (sapma > enBuyukSapma) enBuyukSapma = sapma;

            if (sapma > ToleransMetre)
            {
                bozuk++;
                if (bozuk <= 5)
                    rapor.AppendLine($"AYRIK {konum[i]} derinlik={derinlik[i]:F3} " +
                                     $"maruziyet={maruziyet[i]:F3} " +
                                     $"GPU={gpu[i]:F5} CPU={cpu:F5} " +
                                     $"sapma={sapma * 1000f:F2} mm");
            }
        }

        ok = bozuk == 0;

        rapor.Insert(0, ok
            ? $"Yükseklik eşliği TAMAM — {OrnekSayisi} örnek, en büyük sapma " +
              $"{enBuyukSapma * 1000f:F4} mm.\n"
            : $"Yükseklik eşliği BOZUK — {bozuk}/{OrnekSayisi} örnek toleransı " +
              $"({ToleransMetre * 1000f:F1} mm) aştı, en büyük sapma " +
              $"{enBuyukSapma * 1000f:F2} mm.\n" +
              "Sapma bir katmanın genliği kadarsa (0.30 drift, 0.20 sastrugi, " +
              "0.015 fBm, 0.006 ripple) o katman C#'ta eksik veya yanlış.\n");

        return rapor.ToString();
    }
}
