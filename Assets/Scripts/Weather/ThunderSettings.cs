using UnityEngine;

/// Gök gürültüsünün sıklığı, sesi ve çakmanın ne kadar uzakta olduğu.
///
/// Gecikme burada bir ayar değil: mesafeden çıkıyor. İkisini ayrı ayrı vermek, aynı
/// fiziksel büyüklüğe iki sistemin ayrı karar vermesi olurdu — bir buçuk saniyelik
/// gecikme beş yüz metre demektir, ışık başka bir yerde çakamaz.
///
/// Bileşenin üstünde `[SerializeField]` olarak durdukları sürece değerin üç kopyası
/// oluyordu: koddaki varsayılan, sahnedeki serileştirilmiş kopya ve gerçekte çalışan.
/// Sahne kazanıyor, üstelik Unity sahneyi kendi belleğinden istediği an diske yeniden
/// yazıyor — koddan yapılan bir düzeltme sessizce geri alınıyordu. Tek dosyada yaşayınca
/// ayrışacak ikinci kopya kalmıyor.
///
/// Kliplerin kendisi burada değil: onlar ayar değil içerik, sahne kurulumu bağlıyor.
[CreateAssetMenu(menuName = "To The Summit/Thunder", fileName = "ThunderSettings")]
public class ThunderSettings : ScriptableObject
{
    [Header("Sıklık")]
    [Tooltip("En şiddetli yağışta iki gürültü arası en kısa süre (saniye).")]
    public float minInterval = 15f;
    [Tooltip("Yağış zayıfken iki gürültü arası en uzun süre (saniye).")]
    public float maxInterval = 110f;
    [Tooltip("Bu şiddetin altında gök gürültüsü hiç çalmaz.")]
    [Range(0f, 1f)] public float minPrecipitation = 0.2f;
    [Tooltip("Karlılık bu değeri aşınca susar. Tipide şimşek nadirdir ama yok değildir.")]
    [Range(0f, 1f)] public float snowCutoff = 0.65f;
    [Tooltip("Karlı havada bile korunan en düşük ses seviyesi.")]
    [Range(0f, 1f)] public float minVolume = 0.5f;

    [Header("Yakınlık")]
    [Tooltip("Yakın çakma olasılığının tavanı. Eğri karekök olduğu için eşiğin hemen " +
             "üstünde bile bu değerin üçte birine ulaşır.")]
    [Range(0f, 1f)] public float closeChanceAtPeak = 0.85f;
    [Tooltip("Yakın çakmanın başladığı yağış şiddeti. Altında yalnızca uzak, sakin " +
             "gürültüler çalar — dağ eteğindeki dingin açılışı bozmasın.")]
    [Range(0f, 1f)] public float closeThreshold = 0.45f;

    [Header("Varyasyon")]
    [Range(0f, 0.5f)] public float volumeVariation = 0.25f;
    [Range(0f, 0.5f)] public float pitchVariation = 0.15f;
    [Range(0f, 1f)] public float panVariation = 0.6f;
    [Tooltip("Uzak gürültünün kesim frekans aralığı (Hz). Hava yüksek frekansları yutar.")]
    public Vector2 distantCutoff = new(400f, 1200f);
    [Tooltip("Yakın gürültünün kesim frekans aralığı (Hz).")]
    public Vector2 closeCutoff = new(3000f, 8000f);

    [Header("Mesafe")]
    [Tooltip("Yakın çakmanın uzaklık aralığı (metre).")]
    public Vector2 closeDistance = new(200f, 1500f);
    [Tooltip("Uzak çakmanın uzaklık aralığı (metre). Ses saniyede 340 m gittiği için " +
             "8 km yirmi dört saniye demek — gerçekten böyle.")]
    public Vector2 distantDistance = new(2500f, 8000f);
}
