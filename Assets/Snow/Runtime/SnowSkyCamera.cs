// ROL: kar yağışını engelleyen geometriyi tepeden çizip RT_SkyVis'i üretir.
// Çağıran: SnowManager (Dispatch içinden, yalnız kirliyken).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// HER KARE DEĞİL (spec §12.1). Harita yalnız bölge merkezi 4 m'den fazla
/// kaydığında veya `MarkDirty()` çağrıldığında yenileniyor. Statik geometrinin
/// silueti kare kare değişmiyor; her kare çizmek bedava bir maliyet olurdu.
///
/// Yakalama kamerasıyla aynı sebeple KAMERA BİLEŞENİ DEĞİL: URP replacement
/// shader desteklemiyor (ölçüldü) ve kamera yolu URP asset değişikliği
/// gerektiriyor (spec §1.1 yasaklıyor).
public sealed class SnowSkyCamera
{
    /// Aşağı bakıyor: ileri yön −Y (spec §12.1).
    static readonly Quaternion LookDown = Quaternion.Euler(90f, 0f, 0f);

    static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

    /// Kamera bölgenin bu kadar üstünde duruyor; altındaki her şeyi görsün.
    const float Height = 300f;

    const float NearClip = 0.05f;
    const float FarClip = 1200f;

    readonly List<Renderer> occluders = new(128);

    Vector2 bakedCenter;
    bool dirty = true;

    /// Haritanın merkezinin dünya XZ'si — örnekleme bunu okuyor.
    public Vector2 Center => bakedCenter;

    public int OccluderCount => occluders.Count;

    public void MarkDirty() => dirty = true;

    /// Sahnedeki engelleri bir kez tarar. Engeller statik; her karede aramak
    /// hem allocation hem boşa iş olurdu (spec §0.8).
    public void Rescan(int layer)
    {
        occluders.Clear();

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            if (r.gameObject.layer == layer) occluders.Add(r);

        dirty = true;
    }

    /// Yenileme gerekiyor mu. Bölge merkezi eşikten fazla kaydıysa evet.
    public bool NeedsRefresh(Vector2 areaCenter) =>
        dirty || (areaCenter - bakedCenter).sqrMagnitude >
                 SnowConstants.SkyMoveThreshold * SnowConstants.SkyMoveThreshold;

    public void Record(CommandBuffer cmd, RenderTexture target, RenderTexture depth,
                       Material skyMaterial, Vector2 areaCenter, float observerY,
                       Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        bakedCenter = areaCenter;
        dirty = false;

        float half = SnowConstants.SkyAreaSize * 0.5f;

        var position = new Vector3(areaCenter.x, observerY + Height, areaCenter.y);

        Matrix4x4 view = FlipZ * Matrix4x4.TRS(position, LookDown, Vector3.one).inverse;
        Matrix4x4 proj = Matrix4x4.Ortho(-half, half, -half, half, NearClip, FarClip);

        cmd.SetRenderTarget(target, depth);

        // Arka plan −9999: "burada örtü yok". Örnekleme `occlY − posWS.y`
        // farkına bakıyor; çok alçak bir değer farkı negatif yapıyor ve
        // görünürlük 1 kalıyor.
        cmd.ClearRenderTarget(true, true, new Color(-9999f, 0f, 0f, 0f), 1f);

        cmd.SetViewProjectionMatrices(view, proj);

        var box = new Bounds(new Vector3(areaCenter.x, observerY, areaCenter.y),
                             new Vector3(SnowConstants.SkyAreaSize, FarClip * 2f,
                                         SnowConstants.SkyAreaSize));

        for (int i = 0; i < occluders.Count; i++)
        {
            Renderer r = occluders[i];
            if (r == null || !r.enabled) continue;
            if (!box.Intersects(r.bounds)) continue;

            cmd.DrawRenderer(r, skyMaterial, 0, 0);
        }

        cmd.SetViewProjectionMatrices(restoreView, restoreProj);
    }
}
