// ROL: karakteri kar yüzeyinin üstünde tutar. Arazi collider'ı kayayı
// temsil ediyor; kar onun üstünde geometri olarak yükseliyor.
// Çağıran: yok — kendi başına çalışıyor, bağımlılıkları Inspector'dan.

using System;
using UnityEngine;

/// KARAKTER ÇİZİLEN YÜZEYDE DURUYOR, KAYANIN ÜSTÜNDE DEĞİL.
///
/// `CharacterController` arazi collider'ının üstünde duruyor ve o collider
/// heightmap'ten geliyor — 7.32 m çözünürlükte, karsız. Kar yüzeyi
/// tessellation ile 15-30 cm yükselince karakter o kadar gömülü kalıyor.
///
/// BU HATA BİR KEZ YAPILDI. `MountainSurface.shader` yorumu: "ayak 205.539,
/// kaya 205.489, çizilen yüzey 205.98 — karakter yarım metre gömülü
/// başlıyordu ve göz kar yüzeyinin altında kalıyordu." O tur kar
/// yüksekliğinin geometriden tamamen çıkarılmasıyla bitti.
///
/// Okunan fonksiyon shader'ın kullandığının ikizi (`SnowSurfaceHeight`) ve
/// eşliği `SnowHeightParityTest` ile sınanıyor: 512 örnek, tolerans 1 mm.
///
/// YALNIZ YERDEYKEN. Havadayken (zıplama, düşüş) yüzey düzeltmesi
/// uygulanmıyor; uygulansaydı karakter havada yukarı çekilirdi.
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class SnowGroundOffset : MonoBehaviour
{
    [Tooltip("Kar yöneticisi. Kar derinliği ve rüzgâr maruziyeti buradan " +
             "okunuyor; boş bırakılırsa karakter kayanın üstünde kalır.")]
    [SerializeField] SnowManager snowManager;

    [Tooltip("Yüzey düzeltmesinin yumuşama zaman sabiti (s). Sıfır = anında.")]
    [SerializeField, Min(0f)] float smoothing = 0.06f;

    CharacterController controller;

    /// O an uygulanmış olan düzeltme. Karakter yürürken yüzey yüksekliği
    /// değişiyor; fark ANINDA uygulanırsa kamera zıplıyor.
    float uygulanan;

    void Awake() => controller = GetComponent<CharacterController>();

    void OnEnable() => uygulanan = 0f;

    /// LATEUPDATE, UPDATE DEĞİL. `FirstPersonController` hareketi `Update`'te
    /// yapıyor; düzeltme ondan sonra gelmeli, yoksa bir kare geriden gider.
    void LateUpdate()
    {
        if (snowManager == null) return;

        // Havadayken yüzey düzeltmesi yok: karakter zıplarken yukarı
        // çekilmemeli. Düzeltme yavaşça sıfıra dönüyor ki iniş yumuşasın.
        float hedef = controller.isGrounded ? YuzeyYuksekligi() : 0f;

        float k = smoothing > 0f
            ? 1f - Mathf.Exp(-Time.deltaTime / smoothing)
            : 1f;

        float yeni = Mathf.Lerp(uygulanan, hedef, k);
        float fark = yeni - uygulanan;

        if (Mathf.Abs(fark) > 1e-5f)
        {
            // Kontrolcü kapalıyken taşınıyor: `Move` çarpışma çözerdi ve
            // karakter kendi kapsülüyle zemine takılıp titrerdi.
            controller.enabled = false;
            transform.position += new Vector3(0f, fark, 0f);
            controller.enabled = true;
        }

        uygulanan = yeni;
    }

    float YuzeyYuksekligi()
    {
        float derinlik = snowManager.WorldSnowDepth;
        if (derinlik <= 0f) return 0f;

        Vector3 p = transform.position;

        return SnowSurfaceHeight.RolyefDunya(p, derinlik,
                                             snowManager.WindShadowAt(p),
                                             snowManager.SastrugiWindDir);
    }

    void OnValidate()
    {
        if (snowManager == null && Application.isPlaying)
            throw new InvalidOperationException(
                $"{nameof(SnowGroundOffset)}: {nameof(snowManager)} atanmadı.");
    }
}
