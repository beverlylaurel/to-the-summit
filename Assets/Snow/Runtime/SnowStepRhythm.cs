// ROL: yürüyüşün ayak fazını üretir ve adım anında olay yayınlar.
// Çağıran: sahne (oyuncunun üstünde).

using System;
using UnityEngine;

/// ADIM MESAFEDEN ÇIKIYOR, ZAMANDAN DEĞİL.
///
/// Sabit bir zamanlayıcı hız değişince yanlış ritim verir: yavaş yürürken
/// ayaklar kayar, koşarken adım sıklığı yetişmez. Alınan yol biriktiriliyor;
/// her `strideLength` metrede bir adım düşüyor. Hız arttıkça adım kendiliğinden
/// sıklaşıyor.
///
/// GÖVDEYİ SÜRMÜYOR, YALNIZ FAZ ÜRETİYOR.
///
/// Eskiden ayak proxy'lerini yarım sinüsle kaldırıp indiriyordu. Tek gövdeye
/// geçilince bu anlamını yitirdi — "havadaki ayak" yok — ve zararlı hâle geldi:
/// gövdenin `localPosition.y`'sini başka bir bileşenle birlikte eziyordu. İki
/// yazar çakışınca gövde yüksekliği kare kare salınıyor, oluk derinliği testere
/// dişine dönüyordu (ölçüldü: beklenen localY 0.27, gerçekleşen 0.402 → 0.556).
///
/// Artık gövdenin yüksekliği ize HİÇ girmiyor: batma derinliğini kar söylüyor
/// (`KDeform`, taşıma gücü).
///
/// KAR SİSTEMİ BUNU BİLMİYOR. Ayak izi, ses ve toz bulutu bu olaya ABONE
/// oluyor; buradan kimse çağrılmıyor.
[DisallowMultipleComponent]
public class SnowStepRhythm : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Hızın okunduğu gövde.")]
    [SerializeField] CharacterController body;

    [Header("Yürüyüş")]
    [Tooltip("Durma sınırındaki adım frekansı (çevrim/saniye).")]
    [SerializeField, Min(0.1f)] float baseFrequency = 0.75f;

    [Tooltip("Hızın frekansa katkısı (çevrim/saniye başına m/s).")]
    [SerializeField, Min(0f)] float frequencyPerSpeed = 0.25f;

    [Tooltip("Adım uzunluğunun alt sınırı (m). Çok yavaş yürürken adımlar " +
             "sonsuz kısalmasın.")]
    [SerializeField, Min(0.05f)] float minStride = 0.55f;

    [Tooltip("Bu hızın altında yürüme sayılmıyor; ayaklar yerde kalıyor.")]
    [SerializeField] float minSpeed = 0.15f;

    /// Adım düştüğünde yayınlanıyor. 0 = sol, 1 = sağ.
    public event Action<int> Stepped;

    /// Teşhis: adım döngüsünün neresindeyiz (0..1).
    public float Phase01 { get; private set; }

    /// Teşhis: şu an hangi ayak yerde (0 = sol, 1 = sağ).
    public int PlantedFoot { get; private set; }

    /// Atılan toplam adım sayısı. Ses ve toz bulutu buna abone.
    public int StepCount { get; private set; }

    /// Teşhis: yatay hız (m/s).
    public float Speed { get; private set; }

    float travelled;

    void LateUpdate()
    {
        if (body == null) return;

        Vector3 v = body.velocity;
        Speed = new Vector2(v.x, v.z).magnitude;

        if (Speed > minSpeed)
        {
            travelled += Speed * Time.deltaTime;

            // ADIM UZUNLUĞU HIZDAN TÜRÜYOR, SABİT DEĞİL.
            //
            // Sabit 0.78 m yazılıydı ve hız ne olursa olsun yarım adım 39 cm
            // düşüyordu. İnsan yürüyüşünde sabit olan uzunluk değil FREKANS:
            // bacak bir sarkaç gibi salınıyor ve daha hızlı gitmek için önce
            // adım uzuyor, sonra sıklaşıyor.
            //
            // 2.2 m/s'de gerçek adım ~1.1 m; sabit 0.78 ile izler 39 cm arayla
            // düşüyor ama ayak izinin toplam boyu (bot 30 cm + iki uçta omuz
            // ve kuyruk) 62 cm — izler örtüşüyordu (kullanıcı bildirdi:
            // "adımlar birbirine çok yakın, aralarında boşluk yok").
            // FREKANS DA HIZLA ARTIYOR, YALNIZ UZUNLUK DEĞİL.
            //
            // Sabit frekansla 2.2 m/s'de adım 2.3 m çıkıyor — absürt. Gerçek
            // yürüyüşte hızlanma ikisine BİRDEN gidiyor: hem adım uzuyor hem
            // sıklaşıyor. 1.4 m/s'de çevrim 1.1 Hz ve adım 1.3 m; 2.2 m/s'de
            // 1.3 Hz ve 1.7 m.
            float frekans = Mathf.Max(0.1f, baseFrequency + frequencyPerSpeed * Speed);
            float stride = Mathf.Max(minStride, Speed / frekans);
            float half = Mathf.Max(0.05f, stride * 0.5f);

            while (travelled >= half)
            {
                travelled -= half;
                PlantedFoot = 1 - PlantedFoot;
                StepCount++;
                Stepped?.Invoke(PlantedFoot);
            }

            Phase01 = travelled / half;
        }
        else
        {
            // DURUNCA FAZ SIFIRLANIYOR; yeni yürüyüş adımın başından başlıyor.
            travelled = 0f;
            Phase01 = 0f;
        }
    }
}
