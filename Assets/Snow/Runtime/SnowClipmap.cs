// ROL: kar yüzeyinin geometrisi — iç içe halkaları yaratır, oyuncuyla birlikte
// kendi ızgaralarına snap'ler, kar yokken tamamen kapatır.
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;

/// TESSELLATION YOK, GEOMETRİ CLIPMAP VAR (spec §13).
///
/// URP'de donanım tessellation'ı hull/domain shader ve URP Lit'in elle
/// yeniden kurulmasını gerektiriyor. Aynı sonuç iç içe halkalarla alınıyor:
/// yoğun geometri yalnız oyuncunun çevresinde, uzakta kabalaşarak.
///
/// KAR YOKKEN MALİYET SIFIR (spec §15.2). Kaplama eşiğin altındaysa ve kar
/// yağmıyorsa bütün renderer'lar kapanıyor — yaz aylarında oyun kar sistemi
/// yokmuş gibi davranmalı.
[DisallowMultipleComponent]
public class SnowClipmap : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    [Tooltip("Halkaların merkezinde duracak hedef — genelde oyuncu.")]
    [SerializeField] Transform followTarget;

    [Tooltip("ToTheSummit/SnowLit materyali.")]
    [SerializeField] Material snowMaterial;

    SnowMeshBuilder.Ring[] rings;
    Transform[] ringTransforms;
    MeshRenderer[] ringRenderers;
    Mesh[] ringMeshes;

    bool visible;

    public int RingCount => rings?.Length ?? 0;

    /// Teşhis: kaç halka gerçekten çiziliyor.
    public int ActiveRingCount => visible ? RingCount : 0;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowClipmap)}: {nameof(settings)} atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowClipmap)}: takip hedefi atanmadı.");
        if (snowMaterial == null)
            throw new System.InvalidOperationException($"{nameof(SnowClipmap)}: {nameof(snowMaterial)} atanmadı.");

        Build();
        SetVisible(false);
    }

    void OnDisable() => Teardown();

    void Build()
    {
        rings = SnowMeshBuilder.Describe(settings.QualityData);

        ringTransforms = new Transform[rings.Length];
        ringRenderers = new MeshRenderer[rings.Length];
        ringMeshes = new Mesh[rings.Length];

        for (int i = 0; i < rings.Length; i++)
        {
            ringMeshes[i] = SnowMeshBuilder.Build(rings[i]);

            var go = new GameObject("SnowRing" + i)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            go.transform.SetParent(transform, false);

            go.AddComponent<MeshFilter>().sharedMesh = ringMeshes[i];

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = snowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // Halkalar birbirine göre sabit sırada çizilsin: içten dışa.
            // Kaplama bandında derinlik testini iç halka kazanıyor.
            renderer.rendererPriority = i;

            ringTransforms[i] = go.transform;
            ringRenderers[i] = renderer;
        }
    }

    void Teardown()
    {
        if (ringTransforms != null)
            foreach (Transform t in ringTransforms)
                if (t != null) DestroyImmediate(t.gameObject);

        if (ringMeshes != null)
            foreach (Mesh m in ringMeshes)
                if (m != null) DestroyImmediate(m);

        rings = null;
        ringTransforms = null;
        ringRenderers = null;
        ringMeshes = null;
        visible = false;
    }

    void LateUpdate()
    {
        if (rings == null) return;

        // Kar yoksa hiç iş yok (spec §15.2).
        bool wanted = SnowRuntimeState.GroundCoverage01 >= 0.01f || SnowRuntimeState.IsSnowing;
        if (wanted != visible) SetVisible(wanted);
        if (!wanted) return;

        Vector3 p = followTarget.position;

        for (int i = 0; i < rings.Length; i++)
        {
            float step = rings[i].SnapStep;

            // HER HALKA KENDİ QUAD BOYUTUNUN İKİ KATINA (spec §13.1).
            // Snap'lenmezse köşeler yüzeyi kayarak örnekler ve yürürken
            // yüzey dalgalanır.
            ringTransforms[i].position = new Vector3(
                Mathf.Floor(p.x / step) * step,
                0f,
                Mathf.Floor(p.z / step) * step);
        }
    }

    void SetVisible(bool on)
    {
        visible = on;

        if (ringRenderers == null) return;

        foreach (MeshRenderer r in ringRenderers)
            if (r != null) r.enabled = on;
    }
}
