// ROL: deniz mesh'ini kurar, kameraya snap'ler, materyalini yonetir.
// Cagiran: yok — kendi basina calisiyor, bagimliliklari Inspector'dan.

using System;
using UnityEngine;

/// DENİZ MESH'İ KAMERAYI TAKİP EDİYOR, OYUNCUYU DEĞİL.
///
/// Kamera denizden uzaklaşsa bile ufuk doğru kalıyor (spec §10.3).
///
/// **SNAP ADIMI EN İNCE QUAD BOYUTU.** Tüm quad boyutları onun ikinin
/// kuvveti katı olduğu için tek bir snap adımı her halkanın vertex'lerini
/// kendi kafesinde tutuyor (spec §10.1 hizalama ispatı).
///
/// `SnapStep` FFT teksel boyutuyla ilişkili OLMAK ZORUNDA DEĞİL — FFT
/// dokusu dünya koordinatından örnekleniyor, mesh vertex konumundan değil.
/// Kar sistemindeki `SnapStep / texelSize` tam sayı kuralı burada geçerli
/// değil (spec §10.3 bunu açıkça söylüyor).
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SeaSurface : MonoBehaviour
{
    [SerializeField] SeaSettings settings;
    [SerializeField] Shader surfaceShader;

    [Tooltip("Mesh'in takip edeceği kamera. Boşsa Camera.main kullanılır.")]
    [SerializeField] Transform followCamera;

    MeshFilter filter;
    MeshRenderer meshRenderer;
    Material material;
    Mesh mesh;

    float builtQuad = -1f;
    int builtRings = -1;

    public SeaSettings Settings => settings;

    /// DENİZ KAMERANIN GÖRÜŞ ALANINDA MI.
    ///
    /// `MeshRenderer.isVisible` herhangi bir kameranın (Scene view dahil)
    /// gördüğünü söylüyor. Mesh yoksa "görünüyor" sayılıyor: yokluğunu
    /// görünmezlikle karıştırmak simülasyonu kalıcı olarak susturur ve
    /// belirti "deniz donuk" olur.
    public bool IsVisible => meshRenderer == null || meshRenderer.isVisible;

    public void Bind(SeaSettings source, Shader shader, Transform cam)
    {
        settings = source;
        surfaceShader = shader;
        followCamera = cam;
    }

    void OnEnable()
    {
        filter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void OnDisable()
    {
        Temizle();
    }

    void Temizle()
    {
        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

        if (material != null)
        {
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }

        builtQuad = -1f;
        builtRings = -1;
    }

    /// MATERYAL VE MESH `Update`'TE GARANTİ EDİLİYOR.
    ///
    /// `AssetDatabase.ImportAsset` runtime'da kurulan materyalleri düşürüyor;
    /// `TerrainSurface` bunu yaşadı ve arazi magenta çıktı. Aynı desen:
    /// her karede varlık kontrol ediliyor, yoksa yeniden kuruluyor.
    void Update()
    {
        if (settings == null) return;

        EnsureMesh();
        EnsureMaterial();
        Snap();
    }

    void EnsureMesh()
    {
        // MESH KALITE KADEMESINDEN. Ayarda ayrı bir alan tutulsaydı preset
        // ile mesh ayrışır ve "Low seçtim ama üçgen sayısı düşmedi" durumu
        // çıkardı (spec §15.3).
        SeaQuality.Levels seviye = SeaQuality.Of(settings.quality);

        if (mesh != null &&
            Mathf.Approximately(builtQuad, seviye.FinestQuad) &&
            builtRings == seviye.RingCount)
        {
            if (filter.sharedMesh == mesh) return;
            filter.sharedMesh = mesh;
            return;
        }

        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        mesh = SeaMeshBuilder.Build(seviye.FinestQuad, seviye.RingCount);
        mesh.hideFlags = HideFlags.DontSave;

        filter.sharedMesh = mesh;

        builtQuad = seviye.FinestQuad;
        builtRings = seviye.RingCount;
    }

    void EnsureMaterial()
    {
        if (material != null && meshRenderer.sharedMaterial == material) return;

        if (surfaceShader == null)
            throw new InvalidOperationException(
                $"{nameof(SeaSurface)}: {nameof(surfaceShader)} atanmadı.");

        if (material == null)
            material = new Material(surfaceShader) { hideFlags = HideFlags.DontSave };

        meshRenderer.sharedMaterial = material;

        // GÖLGE YOK. Deniz düz bir yüzey; kendi gölgesini düşürmesi hem
        // maliyet hem yanlış (dalga gölgesi shader'da, geometriden değil).
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
    }

    void Snap()
    {
        Transform cam = followCamera != null ? followCamera
                      : (Camera.main != null ? Camera.main.transform : null);

        if (cam == null) return;

        float adim = SeaQuality.Of(settings.quality).FinestQuad;

        Vector3 c = cam.position;
        float sx = Mathf.Floor(c.x / adim) * adim;
        float sz = Mathf.Floor(c.z / adim) * adim;

        transform.position = new Vector3(sx, settings.seaLevelY, sz);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
