using System;
using UnityEngine;

/// Arazinin rüzgâra ne kadar açık olduğunu ölçer ve `WindField`'e iter.
///
/// Rüzgâr araziyi bilmez — bilmemeli de, kaynak olarak kalmalı. Ama arazi rüzgârı bilir:
/// sırt tepeyi aşan havayı sıkıştırıp hızlandırır, oyuk keser. Dağda hissedilen en büyük
/// farklardan biri budur ve rüzgâr global olduğu sürece hiç oluşmuyordu.
///
/// ÖLÇÜM KENDİ HESABI DEĞİL, PİŞMİŞ HARİTA. Burada rüzgâr ekseninde iki yükseklik
/// örneği alınıp kabartma hesaplanıyordu; aynı soruyu ("bu nokta rüzgârdan ne kadar
/// korunaklı") başka bir sistem de cevaplarsa ikisi ayrışır — yüzeyin
/// rüzgâraltı yığını saydığı yerde oyuncu tam rüzgâr hissedebiliyordu. Aynı büyüklük
/// için iki kaynak olmaz.
///
/// Pişmiş harita aynı fiziği daha iyi kuruyor: eğim VE eğrilik, 103 metrelik Gauss
/// çekirdeği, hâkim rüzgâr ekseni (bkz. `SurfaceMapBaker.BakeWindWeight`). Buradaki
/// iki nokta örneği tek bir kayanın üstünde "sırt" sanabiliyordu.
[RequireComponent(typeof(WindField))]
public class TerrainWindShelter : MonoBehaviour
{
    [Tooltip("Maruziyetin ölçüleceği yer. Oyuncunun kendisi.")]
    [SerializeField] Transform observer;
    [Tooltip("Birikim ağırlığı haritasını tutan bileşen.")]
    [SerializeField] TerrainSurface surface;
    [SerializeField] WindField wind;

    /// Maruziyetin varış süresi (saniye). Anlık okunursa oyuncu bir adım atınca rüzgâr
    /// zıplıyor; hava kütlesi o kadar çabuk yön değiştirmez.
    const float Smoothing = 1.5f;

    float exposure = 0.6f;

    void OnEnable()
    {
        if (observer == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(observer)} atanmadı.");
        if (surface == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(surface)} atanmadı.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(wind)} atanmadı.");
    }

    public void Bind(Transform target, TerrainSurface terrainSurface, WindField field)
    {
        observer = target;
        surface = terrainSurface;
        wind = field;
    }

    void Update()
    {
        // Harita BİRİKİM ağırlığı taşıyor (0.67-2.0); rüzgâr hızı çarpanı onun tersi
        // (0.5-1.5). Maruziyet sözleşmesi 0-1, ortası 0.5 — çarpandan yarım çıkarınca
        // birebir oturuyor.
        float windSpeedFactor = 1f / surface.WindWeightAt(observer.position);
        float target = Mathf.Clamp01(windSpeedFactor - 0.5f);

        exposure = Mathf.Lerp(exposure, target,
            1f - Mathf.Exp(-Time.deltaTime / Smoothing));

        wind.Exposure = exposure;
    }
}
