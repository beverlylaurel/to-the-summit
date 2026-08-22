// ROL: bu nesnenin karda iz bırakmasını sağlar. Yakalama pass'i onu otomatik
// görür; nesnenin kendisi kar sistemi hakkında hiçbir şey bilmez.
// Çağıran: SnowDeformerRegistry (kayıt), SnowCaptureCamera (çizim).

using UnityEngine;

/// GERÇEK ŞEKİL YAKALANIYOR, TAHMİN EDİLMİYOR (spec §9).
///
/// Kapsül tanımı, damga dokusu, basınç formülü yok. Nesnenin alt yüzeyi
/// aşağıdan bakan bir kamerayla ölçülüyor; ayak, diz, düşen gövde, tekerlek —
/// hepsi tek pass'te, ek kod olmadan.
/// ASSUMPTION: `ExecuteAlways`. Deformer var olduğu her an kayıtlı olmalı;
/// böylece yakalama Play'e girmeden editörde de ölçülebiliyor. Bedeli: editör
/// karesi başına bir `SetPropertyBlock`.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class SnowDeformer : MonoBehaviour
{
    Renderer rend;
    MaterialPropertyBlock block;
    Vector3 prevPos;
    Vector4 velocityXZ;

    public Renderer Renderer => rend;

    /// x,y = dünya XZ hızı (m/s). Kenar sırtının hareket yönünde asimetrik
    /// olmasını sağlar (spec §10.2).
    public Vector4 VelocityXZ => velocityXZ;

    void OnEnable()
    {
        // GetComponent BURADA serbest, LateUpdate'te değil (spec §0.8).
        rend = GetComponent<Renderer>();
        block ??= new MaterialPropertyBlock();
        prevPos = transform.position;
        velocityXZ = Vector4.zero;

        SnowDeformerRegistry.Register(this);
    }

    void OnDisable() => SnowDeformerRegistry.Unregister(this);

    void LateUpdate()
    {
        Vector3 p = transform.position;
        Vector3 v = (p - prevPos) / Mathf.Max(Time.deltaTime, 1e-4f);
        prevPos = p;

        velocityXZ.x = v.x;
        velocityXZ.y = v.z;

        // Property block hem çizim yolunun kullandığı değeri taşır hem de
        // nesne başka bir yoldan çizilirse doğru kalır.
        block.SetVector(SnowShaderIDs.DeformerVelocity, velocityXZ);
        rend.SetPropertyBlock(block);
    }
}
