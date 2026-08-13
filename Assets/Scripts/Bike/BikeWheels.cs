using System;
using UnityEngine;

/// TEKERLEKLERİ DÖNDÜRÜR. Kontrolcüden ayrı, çünkü fizik ile görsel ayrı kalmalı:
/// bisiklet modelsiz de çalışıyor, model değişince fizik değişmiyor.
///
/// Dönüş hızı YOL HIZINDAN türüyor, sabit bir çarpandan değil: ω = v / r. Tekerlek
/// yarıçapı ayar asset'inde, yani 26 inç tekerlekli bir bisiklet aynı hızda daha hızlı
/// döner — kendiliğinden doğru.
///
/// CO-OP'TA GÖRÜNÜR. Uzaktaki oyuncunun tekerleği de dönüyor; dönüş hızı yalnız o
/// oyuncunun konumundan türediği için ağ üzerinden ayrıca gönderilecek bir şey yok.
public class BikeWheels : MonoBehaviour
{
    [SerializeField] BikeController bike;
    [SerializeField] BikeSettings settings;

    [Tooltip("Ön tekerlek. Kendi ekseninde dönüyor, direksiyonla ayrıca çevrilmiyor — " +
             "gidon açısı bisikletin kendi dönüşünde zaten var.")]
    [SerializeField] Transform frontWheel;
    [SerializeField] Transform rearWheel;

    [Tooltip("Tekerleğin döndüğü yerel eksen. Meshy'den gelen modelde eksen X ya da Z " +
             "olabiliyor; ayarlanabilir olması modeli yeniden ihraç etmekten ucuz.")]
    [SerializeField] Vector3 spinAxis = Vector3.right;

    float angle;

    public void Bind(BikeController bikeRef, BikeSettings settingsRef,
        Transform front, Transform rear, Vector3 axis)
    {
        bike = bikeRef;
        settings = settingsRef;
        frontWheel = front;
        rearWheel = rear;
        spinAxis = axis;
    }

    void OnEnable()
    {
        if (bike == null || settings == null)
            throw new InvalidOperationException($"{nameof(BikeWheels)}: bağımlılıklar atanmadı.");
    }

    void LateUpdate()
    {
        float radius = Mathf.Max(0.05f, settings.wheelRadius);

        // Açı BİRİKTİRİLİYOR, her karede sıfırdan kurulmuyor: `Rotate` çağrısı kayan
        // nokta hatası biriktiriyor ve uzun sürüşte tekerlek ekseninden kayıyor.
        angle += bike.Speed / radius * Mathf.Rad2Deg * Time.deltaTime;
        angle %= 360f;

        Quaternion spin = Quaternion.AngleAxis(angle, spinAxis.normalized);

        if (frontWheel != null) frontWheel.localRotation = spin;
        if (rearWheel != null) rearWheel.localRotation = spin;
    }
}
