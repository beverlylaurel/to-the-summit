using System;
using UnityEngine;

/// AYRIŞMA PROBU. Karın görsel yüzeyi GPU'da (`SnowDisplacement.hlsl`), çarpışma
/// yüzeyi CPU'da (`SnowSurface`) hesaplanıyor. İki kopya kaçınılmaz olarak ayrışır ve
/// ayrıştığı yer gözle bulunamaz: belirti "kar var ama içinden geçiyorum" ya da
/// "karın on santim üstünde yürüyorum" olur, ikisi de fark edilmeden kalır.
///
/// Prob GPU'dan geri okuma YAPMIYOR — okuma her kare senkron bekleme demek. Bunun
/// yerine CPU yüzeyini oyuncunun çevresine ızgara hâlinde işaretlerle çiziyor:
/// işaretler karın görünen yüzeyine oturuyorsa iki taraf aynı, havada duruyorsa CPU
/// fazla, karın içinde kayboluyorsa eksik hesaplıyor.
///
/// Ölçüm bittiğinde silinir; kalıcı bir sistem değil.
public class SnowCollisionProbe : MonoBehaviour
{
    [SerializeField] SnowSurface snow;
    [SerializeField] Transform player;

    /// Izgara kenarındaki işaret sayısı. 7 → 49 işaret, 3 m aralıkla 18 m'lik alan:
    /// birikinti gövdesi 16-45 m, yani bir yığının hem tepesi hem eteği görünüyor.
    const int Side = 7;
    const float Spacing = 3f;
    const float MarkerSize = 0.16f;

    Transform[] markers;
    Material material;

    /// Oyuncunun kendi çarpışma hacmi ışını kesiyor: filtrelenmezse merkez örneğinde
    /// "zemin" kapsülün tepesi çıkıyor ve prob kendi taşıyıcısını ölçüyor.
    Collider playerBody;
    readonly RaycastHit[] hits = new RaycastHit[8];

    /// Panelin okuduğu son ölçüm: ayağın altındaki üç kot.
    public float GroundHeight { get; private set; }
    public float SnowDepth { get; private set; }
    public float FeetHeight { get; private set; }

    public void Bind(SnowSurface snowRef, Transform playerRef)
    {
        snow = snowRef;
        player = playerRef;
    }

    void OnEnable()
    {
        if (snow == null || player == null)
            throw new InvalidOperationException($"{nameof(SnowCollisionProbe)}: bağımlılıklar atanmadı.");

        playerBody = player.GetComponentInParent<CharacterController>();
        Build();
    }

    void OnDisable()
    {
        if (markers != null)
            foreach (Transform marker in markers)
                if (marker != null) DestroyOwned(marker.gameObject);

        markers = null;

        DestroyOwned(material);
        material = null;
    }

    /// Çalışma anında `Destroy`, editörde `DestroyImmediate`. Sahne boşaltılırken
    /// `DestroyImmediate` çağırmak Unity'de tanımsız davranış.
    static void DestroyOwned(UnityEngine.Object owned)
    {
        if (owned == null) return;

        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    void Build()
    {
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            hideFlags = HideFlags.DontSave
        };
        material.SetColor("_BaseColor", new Color(1f, 0.25f, 0.05f));

        markers = new Transform[Side * Side];
        for (int i = 0; i < markers.Length; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"SnowProbe{i}";
            marker.hideFlags = HideFlags.DontSave;
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * MarkerSize;

            // Çarpışma YOK: prob ölçtüğü yüzeyin üstüne kendi zeminini kurmamalı.
            DestroyOwned(marker.GetComponent<Collider>());
            marker.GetComponent<MeshRenderer>().sharedMaterial = material;

            markers[i] = marker.transform;
        }
    }

    void LateUpdate()
    {
        Vector3 centre = player.position;
        const float Half = (Side - 1) * 0.5f;

        for (int z = 0; z < Side; z++)
        for (int x = 0; x < Side; x++)
        {
            var sample = new Vector3(centre.x + (x - Half) * Spacing, centre.y,
                                     centre.z + (z - Half) * Spacing);

            float ground = GroundAt(sample);
            float depth = snow.DepthAt(new Vector3(sample.x, ground, sample.z));

            // İşaretin MERKEZİ yüzeye konuyor: doğru hesapta küre tam yarısına kadar
            // gömülü görünür. Yarıçap kadar kaldırıp yüzeye teğet yapmak denendi ve
            // geri alındı — teğetlik gözle okunmuyor, "havada mı değiyor mu" ayırt
            // edilemiyor. Batma ORANI ise okunuyor: yarıdan az gömülü = CPU fazla,
            // fazla gömülü = CPU eksik.
            markers[z * Side + x].position = new Vector3(sample.x, ground + depth, sample.z);
        }

        GroundHeight = GroundAt(centre);
        SnowDepth = snow.DepthAt(new Vector3(centre.x, GroundHeight, centre.z));
        FeetHeight = centre.y;
    }

    /// Çıplak arazinin kotu. Prob işaretlerinin çarpışması yok, oyuncunun kapsülü
    /// ayıklanıyor — geriye yalnız arazi kalıyor.
    float GroundAt(Vector3 position)
    {
        var origin = new Vector3(position.x, position.y + 200f, position.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 400f,
            ~0, QueryTriggerInteraction.Ignore);

        float ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == playerBody) continue;
            ground = Mathf.Max(ground, hits[i].point.y);
        }

        return float.IsNegativeInfinity(ground) ? position.y : ground;
    }
}
