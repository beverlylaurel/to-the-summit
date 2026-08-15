using UnityEngine;

/// Ortam ışığını GERÇEK gökyüzünden pişirir.
///
/// Paketin kendi probe'u C#'taki analitik `RenderSky`'dan geliyordu ve o yol çoklu
/// saçılım taşımıyor — alacakaranlık tam olarak çoklu saçılımdan gelir, güneş ufkun
/// altındayken tek saçılım sıfırdır. Ölçülmüştü: 18:36'da çizilen gökyüzü kızıl,
/// probe `0.00000`, sahne zifiri karanlık. Analitik yol devre dışı bırakıldı.
///
/// Skybox materyali PBSky'ın kendisi, yani LUT'un ürettiği alacakaranlığı çiziyor;
/// `DynamicGI.UpdateEnvironment()` onu küresel harmoniğe çeviriyor.
///
/// PİŞİRME KISILIYOR. Küresel harmonik ve yansıma küpü yeniden üretiliyor; her karede
/// çağrılınca kare süresi ikiye katlanıyor. Gökyüzü bir saniyede gözle görülür kadar
/// değişmiyor.
public class SkyAmbientBaker : MonoBehaviour
{
    [Tooltip("Güneş yönünün kaynağı. Pişirme yalnız gökyüzü kaydığında yenileniyor.")]
    [SerializeField] TimeOfDay time;

    [Tooltip("İki pişirme arasındaki en kısa süre (saniye).")]
    [SerializeField, Range(0.1f, 5f)] float minimumInterval = 0.5f;

    [Tooltip("Güneş yönü bu kadar kayınca yeniden pişiriliyor (derece).")]
    [SerializeField, Range(0.05f, 5f)] float movementDegrees = 0.25f;

    Vector3 bakedSunDirection = Vector3.zero;
    float nextBakeTime = -1f;

    /// PİŞİRME BİR KARE GERİDEN OKUYOR. `DynamicGI.UpdateEnvironment()` gökyüzü
    /// materyalini o anki hâliyle okuyor, ama materyalin parametrelerini render geçişi
    /// yazıyor — yani `LateUpdate` anında materyalde ÖNCEKİ karenin durumu duruyor.
    ///
    /// Sürekli akan zamanda görünmez: her kare yeni pişirme geliyor, bir kare gecikme
    /// fark edilmiyor. Ama saat SIÇRADIĞINDA tek pişirme yapılıp güneş de durursa
    /// (F1'de "Saati durdur" işaretliyken) probe eski gökyüzünde DONUYOR.
    ///
    /// Ölçüldü: öğlenden geceye atlandığında saat 00:00'da `ortam tepe` hâlâ
    /// 0.0793 0.1064 0.1355 — öğlen değeri. `LookController` pozlamayı bu probe'dan
    /// okuduğu için gece sahnesi GÜNDÜZ pozlamasıyla çiziliyor ve her şey siyah çıkıyor;
    /// bulutlar en çok bundan etkileniyordu.
    int followUpBakes;

    public void Bind(TimeOfDay timeRef) => time = timeRef;

    void OnEnable()
    {
        if (time == null)
            throw new System.InvalidOperationException($"{nameof(SkyAmbientBaker)}: bağımlılık atanmadı.");

        bakedSunDirection = Vector3.zero;
        nextBakeTime = -1f;
        followUpBakes = 0;
    }

    void LateUpdate()
    {
        // Açı eşiği: güneş 0.25° kayınca gökyüzü ölçülebilir biçimde değişmiş oluyor.
        float moved = Vector3.Angle(bakedSunDirection, time.SunDirection);
        bool skyMoved = bakedSunDirection == Vector3.zero || moved >= movementDegrees;

        if (skyMoved && Time.time >= nextBakeTime)
        {
            bakedSunDirection = time.SunDirection;
            nextBakeTime = Time.time + minimumInterval;

            // Bu pişirme eski materyal durumunu okuyor; takip pişirmesi yenisini alacak.
            followUpBakes = 1;

            DynamicGI.UpdateEnvironment();
            return;
        }

        // TAKİP PİŞİRMESİ ARALIĞA TABİ DEĞİL: amacı bir kareyi kapatmak, kısılırsa
        // gecikme aynen kalır. Kare başına en fazla bir tane, yani maliyeti bir pişirme.
        if (followUpBakes > 0)
        {
            followUpBakes--;
            DynamicGI.UpdateEnvironment();
        }
    }
}
