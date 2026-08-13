using System;
using UnityEngine;

/// GİDON VE ÇATALI ÇEVİRİR. Kontrolcü bütün bisikleti döndürüyor; bu bileşen ONUN
/// ÜSTÜNE görsel bir sapma ekliyor — gidon dönüyor, gövde arkasından geliyor.
///
/// Neden ayrı: fizik ile görsel ayrı kalmalı. Kontrolcü modelsiz de çalışıyor, model
/// değişince fizik değişmiyor. Ayrıca çatal takımı ayrı bir nesne olmayan bir modelde
/// bu bileşen hiç eklenmez ve gerisi aynen çalışır.
///
/// AÇI FİZİKTEN TÜREMİYOR, GÖRSEL. Gerçek bir bisiklette hızlıyken gidon neredeyse hiç
/// dönmez — viraj yatarak alınır ve gidon birkaç derece kıpırdar. Dönüş açısını hıza
/// bağlamak bunu kendiliğinden veriyor: dururken tam açı, hızlıyken kıl payı.
public class BikeSteeringVisual : MonoBehaviour
{
    [SerializeField] BikeController bike;

    [Tooltip("Direksiyon ekseninde dönen parça: çatal, gidon ve kafa birlikte. " +
             "Modelde ayrı nesne değilse bu bileşen kullanılmaz.")]
    [SerializeField] Transform steeringAssembly;

    [Tooltip("Dururken gidonun çevrilebildiği en büyük açı (derece). Gerçek bir " +
             "bisiklette gidon 60-70 dereceye kadar döner ama sürüşte o açıya hiç " +
             "çıkılmaz.")]
    [Range(5f, 70f)] [SerializeField] float maxAngle = 35f;

    [Tooltip("Bu hızın üstünde gidon neredeyse hiç dönmüyor (m/s). Altı metre saniye " +
             "yaklaşık 22 km/h: o tempoda viraj yatarak alınır, gidon kıl payı kıpırdar.")]
    [Range(1f, 15f)] [SerializeField] float fullLeanSpeed = 6f;

    [Tooltip("Dönüşün yumuşaması (saniye). Sıfırda gidon zıplıyor.")]
    [Range(0.02f, 0.6f)] [SerializeField] float smoothing = 0.12f;

    /// Modelin kendi sıfır duruşu: gidonun düz olduğu andaki yerel dönüşü. Sıfırdan
    /// kurulsaydı model her açılışta kendi eksenine sıçrardı — üreteçten gelen mesh'in
    /// sıfır dönüşü düz gidon anlamına gelmiyor.
    Quaternion rest;
    float angle;

    public void Bind(BikeController bikeRef, Transform assembly)
    {
        bike = bikeRef;
        steeringAssembly = assembly;
        if (assembly != null) rest = assembly.localRotation;
    }

    void OnEnable()
    {
        if (bike == null || steeringAssembly == null)
            throw new InvalidOperationException($"{nameof(BikeSteeringVisual)}: bağımlılıklar atanmadı.");

        rest = steeringAssembly.localRotation;
    }

    void LateUpdate()
    {
        // Yatma açısı zaten direksiyon girdisinin yumuşatılmış hâli; ikinci bir girdi
        // okumak yerine ondan türetiliyor — iki kaynak olsaydı gidon ile gövde farklı
        // anlarda dönerdi.
        float steer = bike.LeanAngle / Mathf.Max(1f, MaxLeanOf(bike));

        // Hız arttıkça gidon kısılıyor: viraj yatarak alınmaya başlıyor.
        float speedFade = 1f - Mathf.Clamp01(bike.Speed / Mathf.Max(0.1f, fullLeanSpeed));
        float target = steer * maxAngle * Mathf.Lerp(0.15f, 1f, speedFade);

        angle = Mathf.Lerp(angle, target,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothing)));

        steeringAssembly.localRotation = rest * Quaternion.Euler(0f, angle, 0f);
    }

    /// Kontrolcünün ayarındaki en büyük yatma açısı. Doğrudan okunamıyor çünkü ayar
    /// kontrolcünün özel alanı; oran için gereken tek şey bu sayı.
    static float MaxLeanOf(BikeController bike) => bike.MaxLean;
}
