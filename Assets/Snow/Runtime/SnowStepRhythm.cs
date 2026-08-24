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
/// `SnowTrailBodyAlign` gövdeyi kar yüzeyine oturturken bu bileşen aynı
/// `localPosition.y`'yi eziyordu. İki yazar çakışınca gövde yüksekliği kare
/// kare salınıyor, oluk derinliği testere dişine dönüyordu (ölçüldü: beklenen
/// localY 0.27, gerçekleşen 0.402 → 0.556). Üstüne `baseLeftY` her `OnEnable`'da
/// öteki bileşenin çıktısını taban sanıp ofseti biriktiriyordu.
///
/// Gövdenin yüksekliğinin TEK sahibi `SnowTrailBodyAlign`; adım hissi oradaki
/// adım başına sapmadan geliyor.
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
    [Tooltip("Bir adımda alınan yol (m). İnsan yürüyüşünde ~0.75 m.")]
    [SerializeField] float strideLength = 0.78f;

    [Tooltip("Bu hızın altında yürüme sayılmıyor; ayaklar yerde kalıyor.")]
    [SerializeField] float minSpeed = 0.15f;

    /// Adım düştüğünde yayınlanıyor. 0 = sol, 1 = sağ.
    public event Action<int> Stepped;

    /// Teşhis: adım döngüsünün neresindeyiz (0..1).
    public float Phase01 { get; private set; }

    /// Teşhis: şu an hangi ayak yerde (0 = sol, 1 = sağ).
    public int PlantedFoot { get; private set; }

    /// Atılan toplam adım sayısı. İz gövdesi buradan adım adım sapma
    /// türetiyor (`SnowTrailBodyAlign`); `PlantedFoot` iki değer arasında
    /// gidip geldiği için sapma kaynağı olarak kullanılamıyor — desen
    /// tekrar ederdi.
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

            // HER YARIM ADIMDA BİR AYAK. `strideLength` iki ayağın birlikte
            // aldığı yol; tek ayak yarısında düşüyor.
            float half = Mathf.Max(0.05f, strideLength * 0.5f);

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
