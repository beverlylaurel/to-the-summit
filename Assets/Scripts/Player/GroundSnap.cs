using System;
using UnityEngine;

/// Oyuncuyu zemine oturtur ve orada tutar.
///
/// Sahnedeki kayıtlı konum, arazi her yeniden üretildiğinde geçersiz kalıyor: havada
/// veya zemin altında. Bu yüzden konum sahneye güvenilerek değil, her başlangıçta
/// ölçülerek belirlenir. Zeminin altına düşülürse tekrar oturtulur.
[RequireComponent(typeof(CharacterController))]
public class GroundSnap : MonoBehaviour
{
    [SerializeField] Terrain terrain;
    [Tooltip("Işının başlayacağı yükseklik (metre).")]
    [SerializeField] float probeHeight = 500f;
    [SerializeField] float clearance = 0.1f;
    [Tooltip("Zeminin bu kadar altına düşülürse yeniden oturtulur.")]
    [SerializeField] float rescueDepth = 30f;

    CharacterController controller;

    public void Bind(Terrain target) => terrain = target;

    void Awake() => controller = GetComponent<CharacterController>();

    void Start()
    {
        if (terrain == null)
            throw new InvalidOperationException($"{nameof(GroundSnap)}: {nameof(terrain)} atanmadı.");

        Snap();
    }

    void Update()
    {
        // Arazi altına kaçtıysa kurtar. Düşüş fark edilmeden sonsuza gitmesin.
        float floor = terrain.transform.position.y - rescueDepth;
        if (transform.position.y < floor) Snap();
    }

    void Snap()
    {
        Vector3 position = ClampInsideTerrain(transform.position);

        // Kendi kapsülü ışını engellemesin: kontrolcü kapalıyken ölçülür.
        // Açıkken ışın önce oyuncunun kendi çarpışmasına çarpıyor ve zemin hiç görülmüyordu.
        controller.enabled = false;

        var origin = new Vector3(position.x, position.y + probeHeight, position.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, probeHeight * 4f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + clearance;
        }
        else
        {
            // Işın boşa gittiyse yükseklik haritasına düş
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + clearance;
        }

        transform.position = position;
        controller.enabled = true;
    }

    /// Arazi dışında kalan konum altında zemin bulamaz; sınırların içine çekilir
    Vector3 ClampInsideTerrain(Vector3 position)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float edge = 20f;

        position.x = Mathf.Clamp(position.x, origin.x + edge, origin.x + size.x - edge);
        position.z = Mathf.Clamp(position.z, origin.z + edge, origin.z + size.z - edge);
        return position;
    }
}
