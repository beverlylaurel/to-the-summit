// ROL: kar hacminin ALTINDAN yukarı bakan ortografik yakalamayı kurar ve
// deformer çizimlerini komut tamponuna kaydeder.
// Çağıran: SnowManager (Dispatch içinden).

using UnityEngine;
using UnityEngine.Rendering;

/// KAMERA BİLEŞENİ DEĞİL — ÖLÇÜLMÜŞ SEBEPLE.
///
/// Spec §9.1 `Camera` + `SetReplacementShader` istiyor. URP bunu
/// DESTEKLEMİYOR: `SetReplacementShader` / `RenderWithShader` yerleşik boru
/// hattına ait, URP runtime'ında tek referansı yok (ölçüldü). URP'de aynı işi
/// yapmanın kamera yolu, kendi `ScriptableRendererData`'sını URP asset'inin
/// renderer listesine eklemeyi gerektirir — bu da §1.1'in yasakladığı URP
/// asset değişikliğidir.
///
/// ASSUMPTION: kamera yerine açık ortografik matris + override materyal ile
/// `DrawRenderer`. Teknik BİREBİR aynı (alttan yukarı bakan ortografik frustum,
/// derinlik testiyle en alçak yüzeyin kazanması); değişen yalnızca çizimi kimin
/// tetiklediği. Böylece mevcut hiçbir proje ayarına dokunulmuyor ve her şey
/// spec §15.2'nin istediği TEK CommandBuffer içinde kalıyor.
public sealed class SnowCaptureCamera
{
    /// Kamera yukarı baksın: ileri yön +Y (spec §9.1).
    static readonly Quaternion LookUp = Quaternion.Euler(-90f, 0f, 0f);

    /// Görüntü uzayı -Z ileri; dünya→kamera dönüşümü bu çevirmeyi taşır.
    static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

    const float NearClip = 0.05f;

    /// Bölgede iş var mı. Yoksa yakalama, blur, KDeform ve KRim atlanır
    /// (spec §15.2 — "Yakalama kamerasını gereksiz çalıştırma").
    public static bool HasWork(Vector2 areaCenter, float areaSize, float observerY)
    {
        Bounds box = CaptureBounds(areaCenter, areaSize, observerY);

        for (int i = 0; i < SnowDeformerRegistry.Count; i++)
        {
            SnowDeformer d = SnowDeformerRegistry.Get(i);
            if (d == null || d.Renderer == null || !d.Renderer.enabled) continue;
            if (box.Intersects(d.Renderer.bounds)) return true;
        }

        return false;
    }

    /// Yakalama hacmi: XZ'de kar bölgesi, Y'de gözlemcinin altı ve üstü.
    static Bounds CaptureBounds(Vector2 areaCenter, float areaSize, float observerY)
    {
        float height = SnowConstants.CaptureBelow + SnowConstants.CaptureAbove;
        var center = new Vector3(
            areaCenter.x,
            observerY - SnowConstants.CaptureBelow + height * 0.5f,
            areaCenter.y);

        return new Bounds(center, new Vector3(areaSize, height, areaSize));
    }

    public void Record(CommandBuffer cmd, RenderTexture color, RenderTexture depth,
                       Material captureMaterial, Vector2 areaCenter, float areaSize,
                       float observerY, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        float half = areaSize * 0.5f;
        float far = SnowConstants.CaptureBelow + SnowConstants.CaptureAbove;

        var position = new Vector3(areaCenter.x,
                                   observerY - SnowConstants.CaptureBelow,
                                   areaCenter.y);

        Matrix4x4 view = FlipZ * Matrix4x4.TRS(position, LookUp, Vector3.one).inverse;
        Matrix4x4 proj = Matrix4x4.Ortho(-half, half, -half, half, NearClip, far);

        cmd.SetRenderTarget(color, depth);

        // Arka plan -9999: "burada deformer yok". A = 0 zaten kapıyı kapatıyor
        // ama R'nin çok alçak olması blur kenarında tam batma üretiyor —
        // spec §9.1'in verdiği değer aynen kullanıldı.
        cmd.ClearRenderTarget(true, true, new Color(-9999f, 0f, 0f, 0f), 1f);

        cmd.SetViewProjectionMatrices(view, proj);

        // Yükseklik gözlemciye göre kodlanıyor; yarım hassasiyet mutlak dünya
        // Y'sini taşıyamıyor (bkz. Hidden_SnowCaptureDepth).
        cmd.SetGlobalFloat(SnowShaderIDs.SnowCaptureOriginY, observerY);

        Bounds box = CaptureBounds(areaCenter, areaSize, observerY);

        for (int i = 0; i < SnowDeformerRegistry.Count; i++)
        {
            SnowDeformer d = SnowDeformerRegistry.Get(i);
            if (d == null || d.Renderer == null || !d.Renderer.enabled) continue;
            if (!box.Intersects(d.Renderer.bounds)) continue;

            // Hız property block'tan da geliyor ama globali açıkça yazmak
            // çizim yolundan bağımsız olarak doğru değeri garantiliyor.
            cmd.SetGlobalVector(SnowShaderIDs.DeformerVelocity, d.VelocityXZ);
            cmd.DrawRenderer(d.Renderer, captureMaterial, 0, 0);
        }

        // MATRİSLER GERİ ALINIYOR. URP kamera matrislerini kare başına bir kez
        // yazıyor; burada bırakılırsa opak geçiş kar bölgesinin ortografik
        // matrisiyle çizilir ve ekran boş kalır.
        cmd.SetViewProjectionMatrices(restoreView, restoreProj);
    }
}
