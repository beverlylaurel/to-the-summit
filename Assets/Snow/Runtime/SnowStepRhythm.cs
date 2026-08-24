// ROL: yürüyüşün ayak fazını üretir; ayak proxy'lerini basar ve adım anında
// olay yayınlar.
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
/// İKİ İŞ TEK FAZDAN. Ayak proxy'lerinin yerden kalkması ve adım olayı aynı
/// sayının iki görünümü; ayrı bileşenlere bölünseydi ikisinin fazı kayabilirdi.
///
/// KAR SİSTEMİ BUNU BİLMİYOR. Ayak izi, ses ve toz bulutu bu olaya ABONE
/// oluyor; buradan kimse çağrılmıyor.
[DisallowMultipleComponent]
public class SnowStepRhythm : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Hızın okunduğu gövde.")]
    [SerializeField] CharacterController body;

    [Header("Ayak proxy'leri")]
    [Tooltip("Sol ayak — karda iz bırakan proxy.")]
    [SerializeField] Transform leftFoot;

    [Tooltip("Sağ ayak — karda iz bırakan proxy.")]
    [SerializeField] Transform rightFoot;

    [Header("Yürüyüş")]
    [Tooltip("Bir adımda alınan yol (m). İnsan yürüyüşünde ~0.75 m.")]
    [SerializeField] float strideLength = 0.78f;

    [Tooltip("Havadaki ayağın yerden yüksekliği (m).")]
    [SerializeField] float footLift = 0.16f;

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
    float baseLeftY, baseRightY;

    void OnEnable()
    {
        // Kurulumun verdiği taban yükseklikler saklanıyor: kaldırma bunun
        // ÜSTÜNE ekleniyor, mutlak bir sayı dayatılmıyor.
        if (leftFoot != null) baseLeftY = leftFoot.localPosition.y;
        if (rightFoot != null) baseRightY = rightFoot.localPosition.y;
    }

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
            // DURUNCA İKİ AYAK DA YERE İNİYOR. Havada kalan ayak durgun
            // oyuncunun altında asılı bir iz bırakıyordu.
            travelled = 0f;
            Phase01 = 0f;
        }

        Plant();
    }

    /// Yerdeki ayak tabanda, havadaki ayak kalkık. Kalkış eğrisi yarım sinüs:
    /// ayak yerden ayrılırken ve inerken yavaş, ortada hızlı.
    void Plant()
    {
        float lift = Speed > minSpeed ? Mathf.Sin(Phase01 * Mathf.PI) * footLift : 0f;

        SetFootY(leftFoot, baseLeftY + (PlantedFoot == 0 ? 0f : lift));
        SetFootY(rightFoot, baseRightY + (PlantedFoot == 1 ? 0f : lift));
    }

    static void SetFootY(Transform t, float y)
    {
        if (t == null) return;

        Vector3 p = t.localPosition;
        p.y = y;
        t.localPosition = p;
    }
}
