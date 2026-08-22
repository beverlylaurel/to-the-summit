// ROL: kar sisteminin çalışması için gereken proje koşullarını KONTROL EDER ve
// raporlar. Hiçbirini otomatik düzeltmez (spec §1.1, §1.2).
// Çağıran: menü — To The Summit/Kar/Proje Kontrolü.

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// HİÇBİR ŞEY DÜZELTİLMİYOR. Spec §1.1: mevcut proje ayarları kar sisteminin
/// değil oyunun kararıdır. Uygun olmayan bir şey bulunca kod yazmayı bırakıp
/// kullanıcıya bildirmek, sessizce düzeltmekten iyidir — Color Space'i Linear'a
/// çevirmek bütün oyunun görüntüsünü bozar.
public static class SnowProjectCheck
{
    /// Kar sisteminin ihtiyaç duyduğu iki layer (spec §1.3).
    public const string DeformerLayer = "SnowDeformer";
    public const string OccluderLayer = "SnowOccluder";

    [MenuItem("To The Summit/Kar/Proje Kontrolü", false, 48)]
    static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var r = new StringBuilder(4096);
        r.AppendLine("# Kar sistemi — proje kontrolü");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        bool blocking = false;

        // --- Color Space ---
        if (PlayerSettings.colorSpace == ColorSpace.Linear)
            Line(r, true, "Color Space", "Linear");
        else
            Line(r, false, "Color Space", PlayerSettings.colorSpace +
                " — Kar shading'i Gamma'da doğru çalışmaz. Bu bir proje kararıdır, " +
                "kullanıcı vermeli. DEĞİŞTİRİLMEDİ.");

        // --- URP ---
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            Line(r, true, "Render pipeline", "URP (" + urp.name + ")");

            // --- Depth texture ---
            if (urp.supportsCameraDepthTexture)
                Line(r, true, "Depth Texture", "açık");
            else
                Line(r, false, "Depth Texture", "KAPALI — yumuşak parçacıklar için gerekir. " +
                    "URP asset'inde 'Depth Texture' açılmalı. DEĞİŞTİRİLMEDİ.");
        }
        else
        {
            Line(r, false, "Render pipeline", "URP DEĞİL — kar sistemi çalışmaz.");
            blocking = true;
        }

        // --- Compute shader ---
        if (SystemInfo.supportsComputeShaders)
            Line(r, true, "Compute shader", "destekleniyor");
        else
        {
            Line(r, false, "Compute shader", "DESTEKLENMİYOR — kar sistemi çalışmaz.");
            blocking = true;
        }

        // --- VFX Graph ---
        bool vfx = HasType("UnityEngine.VFX.VisualEffect, Unity.VisualEffectGraph.Runtime");
        if (vfx)
            Line(r, true, "VFX Graph", "kurulu");
        else
            Line(r, null, "VFX Graph", "KURULU DEĞİL — Faz 1–7 etkilenmez, " +
                "Faz 8 (kar yağışı) için gerekir. Kurulum kullanıcı kararıdır.");

        // --- Layer'lar ---
        int free = CountFreeUserLayers();
        bool deformer = LayerMask.NameToLayer(DeformerLayer) >= 0;
        bool occluder = LayerMask.NameToLayer(OccluderLayer) >= 0;

        if (deformer && occluder)
            Line(r, true, "Layer'lar", $"{DeformerLayer} ve {OccluderLayer} tanımlı");
        else if (free >= (deformer ? 0 : 1) + (occluder ? 0 : 1))
            Line(r, null, "Layer'lar",
                $"eksik ({(deformer ? "" : DeformerLayer + " ")}{(occluder ? "" : OccluderLayer)}) — " +
                $"{free} boş yuva var, kurulum açacak.");
        else
        {
            Line(r, false, "Layer'lar", $"boş yuva {free}, gereken 2 — " +
                "kar sistemi kendi başına layer boşaltmaz. Bir yuva açın.");
            blocking = true;
        }

        // --- Environment lighting ---
        if (RenderSettings.ambientMode == AmbientMode.Skybox)
            Line(r, true, "Environment Lighting", "Skybox");
        else
            Line(r, null, "Environment Lighting", RenderSettings.ambientMode +
                " — Düz renk ambient ile kar ölü görünür. Kar sahnelerinde Skybox önerilir. " +
                "DEĞİŞTİRİLMEDİ.");

        // --- Terrain ---
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        if (terrains.Length == 0)
            Line(r, null, "Terrain", "sahnede yok — `groundSource = MeshBake` gerekir.");
        else if (terrains.Length == 1)
            Line(r, true, "Terrain", "1 adet (" + terrains[0].name + ") — " +
                "`groundSource = UnityTerrain` çalışır.");
        else
        {
            Line(r, false, "Terrain", terrains.Length + " adet — ÇOKLU TERRAIN DESTEKLENMİYOR. " +
                "Kar sistemi devre dışı kalır.");
            blocking = true;
        }

        // --- Sabit eşliği ---
        string parity = SnowConstantsTest.Run(out bool parityOk);
        Line(r, parityOk, "Sabit eşliği", parity.Split('\n')[0]);
        if (!parityOk) r.AppendLine(parity);

        // --- Kullanıcının elle yapacakları ---
        r.AppendLine();
        r.AppendLine("## Elle yapılacaklar");
        r.AppendLine("- Ana kameranın Culling Mask'inden `" + DeformerLayer +
                     "` layer'ını elle kaldırın; yoksa proxy mesh'ler ekranda görünür.");
        r.AppendLine("- Yağmur sisteminiz `SnowRuntimeState.IsSnowing` true iken yağmuru " +
                     "kapatmalı, yoksa aynı anda hem yağmur hem kar yağar.");
        r.AppendLine("- Karakterleri `" + OccluderLayer + "` layer'ına KOYMAYIN; konursa " +
                     "oyuncunun ayağının altına kar yağmaz ve izler hiç dolmaz.");

        r.AppendLine();
        r.AppendLine(blocking
            ? "SONUÇ: BLOKE — yukarıdaki kırmızı maddeler çözülmeden devam edilmemeli."
            : "SONUÇ: devam edilebilir.");

        return r.ToString();
    }

    /// `ok == null` → uyarı (bloke etmiyor).
    static void Line(StringBuilder r, bool? ok, string label, string value)
    {
        string mark = ok == null ? "!" : (ok.Value ? "+" : "-");
        r.AppendLine($"  [{mark}] {label.PadRight(22)} {value}");
    }

    /// Kullanıcı layer'ları 8'den başlar; 31'e kadar 24 yuva var.
    static int CountFreeUserLayers()
    {
        int free = 0;
        for (int i = 8; i < 32; i++)
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) free++;
        return free;
    }

    static bool HasType(string qualifiedName) => System.Type.GetType(qualifiedName) != null;
}
