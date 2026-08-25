// ROL: kamerayı kar sütunu kadar yükseltir; oyuncu karın üstünden bakar.
// Çağıran: yok — kameranın kendi bileşeni.

using UnityEngine;

/// GÖZ KARIN ÜSTÜNDE, KAYANIN ÜSTÜNDE DEĞİL.
///
/// Arazi shader'ı köşeleri kar sütunu kadar yükseltiyor
/// (`SnowWorldCoverHeight`, ölçüldü: 0.49 m). Fizikte böyle bir yükseltme yok:
/// `CharacterController` arazi collider'ının, yani KAYANIN üstünde duruyor.
///
/// Belirti: göz kar yüzeyinin yarım metre ALTINDA kalıyor. Sıyırtma bakışta
/// kamera kar yüzeyinin altına düşüyor, mesh tepede asılı görünüyor ve iz
/// "havada" okunuyor — kullanıcı bunu üst üste bildirdi. Ölçüldü: gövde
/// y=206.18, zemin 205.99, kar yüzeyi 206.48; göz yüzeyin 30 cm altında.
///
/// COLLIDER'A DOKUNULMUYOR. `CharacterController.center` veya `transform`
/// yükseltilirse kapsül zeminden kopuyor ve karakter her karede geri düşüyor;
/// yükseltme ile yer çekimi birbirini yiyor. Görünen konum tek başına
/// düzeltilebilir çünkü kar katmanı zaten yalnız GÖRSEL: iz gövdesi de kar
/// yüzeyine ayrıca oturtuluyor (`SnowTrailBodyAlign`).
///
/// BATMA PAYI BIRAKILIYOR. İnsan karın üstünde yüzmez; ayak gömülür.
[DisallowMultipleComponent]
public class SnowEyeHeight : MonoBehaviour
{
    [Tooltip("Yükseltilecek kamera. Yerel Y'sine ekleniyor.")]
    [SerializeField] Transform eye;

    [Tooltip("Kar yüksekliğini okuduğumuz örnekleyici.")]
    [SerializeField] SnowSampler sampler;

    [Tooltip("Ayağın kara gömülme payı (m).")]
    [SerializeField] float sinkDepth = 0.18f;

    [Tooltip("Yükseklik yumuşama süresi (s).")]
    [SerializeField] float smoothTime = 0.12f;

    float baseLocalY;
    float lift;
    float liftVel;

    void OnEnable()
    {
        if (eye == null || sampler == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowEyeHeight)}: bağımlılık atanmadı.");

        // TABAN BİR KEZ OKUNUYOR VE KENDİ ÇIKTISINDAN ARINDIRILIYOR: bileşen her
        // kare yerel Y yazıyor, `OnEnable` bir sonraki açılışta kendi çıktısını
        // taban sanarsa ofset birikir.
        baseLocalY = eye.localPosition.y - lift;
        lift = 0f;
        liftVel = 0f;
    }

    void LateUpdate()
    {
        float hedef = 0f;

        // İZ-ÖNCESİ SÜTUN OKUNUYOR. `Depth` tek başına oyulmuş yüzeydir; kamera
        // kendi izini okursa her kare biraz daha çöker (geri besleme).
        if (sampler.TrySampleSnow(transform.position, out SnowSample s) && s.Valid)
            hedef = Mathf.Max(0f, s.Depth + s.SinkDepth - sinkDepth);

        lift = Mathf.SmoothDamp(lift, hedef, ref liftVel, smoothTime, Mathf.Infinity, Time.deltaTime);

        Vector3 p = eye.localPosition;
        p.y = baseLocalY + lift;
        eye.localPosition = p;
    }
}
