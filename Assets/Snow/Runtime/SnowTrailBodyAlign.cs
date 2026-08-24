// ROL: kar izi gövdesini hareket yönüne hizalar ve adım adım hafifçe oynatır.
// Çağıran: yok — gövdenin kendi bileşeni.

using UnityEngine;

/// İZ GÖVDESİ HAREKET YÖNÜNE BAKAR, OYUNCUYA DEĞİL.
///
/// Gövde oval (dar ve uzun). Oyuncunun rotasyonuna bağlı kalsaydı, oyuncu
/// çapraz yürürken gövde de çapraz duruyor ve iz yana çıkıyordu — kullanıcı
/// bunu iki kübik ayak proxy'si döneminde bildirdi. Hareket yönüne hizalanınca
/// oluk her zaman gidiş doğrultusunda uzuyor.
///
/// DURURKEN SON YÖN KORUNUYOR. Hız sıfıra inince hedef yön tanımsız olur;
/// serbest bırakılırsa gövde rastgele bir yöne sıçrar ve durduğu yerde izin
/// şekli değişir.
///
/// ADIM ADIM OYNAMA. Sabit bir gövde oluk boyunca aynı deseni basıyor ve
/// tekrar gözle yakalanıyor (kullanıcı bildirdi: "sürekli aynı deseni
/// üretiyor, çok yapay"). Gerçek yürüyüşte her adım biraz farklı yere, biraz
/// farklı derinliğe basar. Sapma adım sayacından türeyen bir hash'ten geliyor:
/// tekrar etmiyor ama kare kare de titremiyor — bir adım boyunca sabit.
///
/// Yanal sapma OLUĞU BÖLECEK KADAR BÜYÜK OLAMAZ. Gövde 22 cm geniş; ±3 cm
/// sapma izi kırmadan kenarını düzensizleştiriyor. Daha büyüğü iki ayrı
/// oluk üretmeye başlar ki tam olarak kaçınılan şey odur.
[DisallowMultipleComponent]
public class SnowTrailBodyAlign : MonoBehaviour
{
    [Tooltip("Hareket yönünü okuduğumuz gövde.")]
    [SerializeField] CharacterController body;

    [Tooltip("Altında hizalamanın kapandığı hız (m/s).")]
    [SerializeField] float minSpeed = 0.15f;

    [Tooltip("Yön değişiminin yumuşama hızı (derece/saniye).")]
    [SerializeField] float turnRate = 540f;

    [Tooltip("Adım başına yanal sapmanın genliği (m).")]
    [SerializeField] float lateralJitter = 0.03f;

    [Tooltip("Adım başına derinlik sapmasının genliği (m).")]
    [SerializeField] float depthJitter = 0.012f;

    [Tooltip("Adım fazını okuduğumuz ritim. Yoksa sapma uygulanmaz.")]
    [SerializeField] SnowStepRhythm rhythm;

    float yaw;
    Vector3 baseLocalPos;
    int lastStep = -1;
    Vector2 stepOffset;

    void OnEnable()
    {
        baseLocalPos = transform.localPosition;
        yaw = transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (body != null)
        {
            Vector3 v = body.velocity;
            v.y = 0f;

            if (v.sqrMagnitude > minSpeed * minSpeed)
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg,
                                             turnRate * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (rhythm == null) return;

        // Adım değiştiği KARE'de yeni sapma seçiliyor; adım boyunca sabit
        // kalıyor ki gövde kare kare titremesin.
        int step = rhythm.StepCount;
        if (step != lastStep)
        {
            lastStep = step;
            stepOffset = new Vector2(Hash01(step * 2 + 0), Hash01(step * 2 + 1)) * 2f - Vector2.one;
        }

        Vector3 p = baseLocalPos;
        p.x += stepOffset.x * lateralJitter;
        p.y += stepOffset.y * depthJitter;
        transform.localPosition = p;
    }

    /// Tam sayıdan 0..1. `frac(sin(...))` büyük indekste tekrar ediyor;
    /// bu karıştırıcı 32 bit boyunca dağınık kalıyor.
    static float Hash01(int n)
    {
        uint x = (uint)n * 747796405u + 2891336453u;
        x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
        x = (x >> 22) ^ x;
        return (x & 0xFFFFFFu) / 16777215f;
    }
}
