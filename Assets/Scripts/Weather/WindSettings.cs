using UnityEngine;

/// Rüzgârın hız, esinti ve yön ayarları.
///
/// Bileşenin üstünde `[SerializeField]` olarak durdukları sürece değerin üç kopyası
/// oluyordu: koddaki varsayılan, sahnedeki serileştirilmiş kopya ve gerçekte çalışan.
/// Sahne kazanıyor, üstelik Unity sahneyi kendi belleğinden istediği an diske yeniden
/// yazıyor — koddan yapılan bir düzeltme sessizce geri alınıyordu.
///
/// Şiddet burada yok: onu `AltitudeWeatherDriver` sürüyor. Rüzgâr ne kadar eseceğine
/// kendi karar vermez, yalnızca nasıl eseceğine.
[CreateAssetMenu(menuName = "To The Summit/Wind", fileName = "WindSettings")]
public class WindSettings : ScriptableObject
{
    [Header("Taban rüzgâr")]
    [Tooltip("Severity 0 iken hız (m/s). Sıfır olmamalı.")]
    public float calmSpeed = 2f;
    [Tooltip("Severity 1 iken hız (m/s). Tam fırtına.")]
    public float stormSpeed = 14f;

    [Header("Arazi maruziyeti")]
    [Tooltip("Korunaklı oyukta sürekli hızın kaç katına indiği. Dağda hissedilen en " +
             "büyük fark budur: sırtta ayakta duramazsın, otuz metre aşağıda rüzgâr " +
             "kesilir.")]
    [Range(0.1f, 1f)] public float shelteredFactor = 0.35f;
    [Tooltip("Açık sırtta sürekli hızın kaç katına çıktığı. Rüzgâr tepeyi aşarken " +
             "sıkışıp hızlanır.")]
    [Range(1f, 2.5f)] public float exposedFactor = 1.45f;
    [Tooltip("Taban salınımının hızı. 0.011 ≈ 90 saniyelik periyot.")]
    public float baseFrequency = 0.011f;
    [Tooltip("Taban hızın kendi etrafında salınma oranı.")]
    [Range(0f, 1f)] public float baseVariation = 0.25f;

    [Header("Esinti")]
    [Tooltip("Esintinin taban hıza oranı.")]
    [Range(0f, 1f)] public float gustAmount = 0.4f;
    [Tooltip("Esinti sıklığı. 0.08 ≈ 12 saniyelik periyot.")]
    public float gustFrequency = 0.08f;
    [Tooltip("Saniye altı sarsıntının payı: ceketi dalgalandıran kısa çarpmalar. " +
             "Esintinin üstüne biner.")]
    [Range(0f, 1f)] public float flickerAmount = 0.12f;
    [Tooltip("Sarsıntı sıklığı. 0.5 ≈ 2 saniyelik periyot.")]
    public float flickerFrequency = 0.5f;

    [Header("Yön")]
    [Tooltip("Dağın HÂKİM rüzgâr yönü (derece, +X'ten saat yönünün tersine). Kar " +
             "deseni bu eksene oturuyor: biçim saatler içinde " +
             "oluşur, anlık esintiyle dönmezler.")]
    [Range(0f, 360f)] public float prevailingDegrees = 205f;

    [Tooltip("Anlık rüzgârın hâkim yönün etrafındaki salınımı (derece). Rüzgâr her " +
             "yönden gelmez; dağın bir hâkim rüzgârı vardır ve esinti onun etrafında " +
             "oynar. Serbest 720°'lik süpürme denendi ve geri alındı: birikinti alanı " +
             "`dot(worldXZ, windAxis)` okuduğu için dağın ortasında (|worldXZ| ≈ 7000 m) " +
             "0.14 radyanlık bir sapma deseni 980 metre kaydırıyordu — gövde 45 m.")]
    [Range(0f, 90f)] public float directionSpread = 35f;

    [Tooltip("Yön kaymasının hızı.")]
    public float directionDrift = 0.02f;
}
