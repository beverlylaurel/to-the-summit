// ROL: kar yüzeyinin tek ızgara mesh'ini sahneye kurar, bölgeyle birlikte
// taşır, kar yokken tamamen kapatır.
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;
using UnityEngine.Rendering;

/// TEK MESH, TEK DRAW CALL (spec §8.2).
///
/// Tessellation yok (spec §20), çok seviyeli clipmap yok (spec §8.1). Yüzey
/// tek kare ızgara; yükseklik köşe shader'ında veriliyor.
///
/// MERKEZ AYRI SNAP EDİLMİYOR (spec §6.4). Kar mesh'i, yakalama kamerası ve
/// deformasyon RT'si AYNI `snapped` merkezi kullanmak zorunda; merkez
/// `SnowManager.AreaCenter`'dan geliyor. Kendi başına snap eden ikinci bir
/// kaynak, iki ızgarayı birbirine göre kaydırır.
///
/// KAR YOKKEN MALİYET SIFIR (spec §15.2). Kaplama eşiğin altındaysa ve kar
/// yağmıyorsa renderer kapanıyor — yazın oyun kar sistemi yokmuş gibi
/// davranmalı.
[DisallowMultipleComponent]
public class SnowSurface : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    [Tooltip("Bölge merkezini yayınlayan yönetici. Merkez ondan okunuyor; " +
             "mesh kendi başına snap etmiyor (spec §6.4).")]
    [SerializeField] SnowManager manager;

    [Tooltip("ToTheSummit/SnowLit materyali.")]
    [SerializeField] Material snowMaterial;

    SnowMeshBuilder.Grid grid;
    Transform meshTransform;
    MeshRenderer meshRenderer;
    Mesh mesh;

    bool visible;

    /// Mesh'in merkezden kenara uzaklığı, metre. Bölge yarıçapıyla aynı —
    /// mesh ile deformasyon bölgesi aynı kareyi kaplıyor (spec §6.1).
    public float Extent => settings != null ? settings.QualityData.AreaSize * 0.5f : 0f;

    /// Teşhis: mesh gerçekten çiziliyor mu.
    public bool IsVisible => visible;

    /// Teşhis: kaç üçgen çiziliyor.
    public int TriangleCount => visible ? grid.TriangleCount : 0;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowSurface)}: {nameof(settings)} atanmadı.");
        if (manager == null)
            throw new System.InvalidOperationException($"{nameof(SnowSurface)}: {nameof(manager)} atanmadı.");
        if (snowMaterial == null)
            throw new System.InvalidOperationException($"{nameof(SnowSurface)}: {nameof(snowMaterial)} atanmadı.");

        Build();
        SetVisible(false);
    }

    void OnDisable() => Teardown();

    void Build()
    {
        grid = SnowMeshBuilder.Describe(settings.QualityData);
        mesh = SnowMeshBuilder.Build(grid);

        var go = new GameObject("SnowSurfaceMesh")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = snowMaterial;

        // Spec §8.2 birebir.
        // KAR MESH'İ GÖLGE ATMIYOR, ALIYOR.
        //
        // Attığında KENDİ gölgesini alıyordu: gölge sapması karın ince
        // detaylı normaline göre uygulanıyor, o normal yüksek frekanslı ve
        // sapma yetmiyor — yüzeyin tamamına yayılan akne çıkıyor.
        //
        // Ölçüldü (öğle, tam örtü, sınırın iki yakası):
        //   gölge açık   → mesh dağdan %15 KARANLIK  (oran 0.847)
        //   gölge kapalı → oran 0.999
        //
        // Belirti: oyuncunun çevresinde parlaklığı farklı bir KARE.
        //
        // Kaybedilen şey yok: kar araziye oturuyor ve arazi kendi gölgesini
        // zaten atıyor. Karın KENDİ öz-gölgelemesi ayrı bir terimde —
        // `SnowHeightAO` yükseklik alanından hesaplıyor ve akne üretmiyor.
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;
        r.allowOcclusionWhenDynamic = false;

        meshTransform = go.transform;
        meshRenderer = r;
    }

    void Teardown()
    {
        if (meshTransform != null) DestroyImmediate(meshTransform.gameObject);
        if (mesh != null) DestroyImmediate(mesh);

        meshTransform = null;
        meshRenderer = null;
        mesh = null;
        visible = false;
    }

    void LateUpdate()
    {
        if (meshTransform == null) return;

        // Kar yoksa hiç iş yok (spec §15.2).
        bool wanted = SnowRuntimeState.GroundCoverage01 >= 0.01f || SnowRuntimeState.IsSnowing;
        if (wanted != visible) SetVisible(wanted);
        if (!wanted) return;

        // BÖLGENİN MERKEZİ, MESH'İN KENDİ SNAP'İ DEĞİL (spec §6.4).
        Vector2 center = manager.AreaCenter;
        meshTransform.position = new Vector3(center.x, 0f, center.y);
    }

    void SetVisible(bool on)
    {
        visible = on;
        if (meshRenderer != null) meshRenderer.enabled = on;
    }
}
