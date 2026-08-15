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

    public void Bind(TimeOfDay timeRef) => time = timeRef;

    void OnEnable()
    {
        if (time == null)
            throw new System.InvalidOperationException($"{nameof(SkyAmbientBaker)}: bağımlılık atanmadı.");

        bakedSunDirection = Vector3.zero;
        nextBakeTime = -1f;
    }

    void LateUpdate()
    {
        if (Time.time < nextBakeTime) return;

        // Açı eşiği: güneş 0.25° kayınca gökyüzü ölçülebilir biçimde değişmiş oluyor.
        // Yön karşılaştırması nokta çarpımdan, çünkü küçük açıda kosinüs 1'e çok yakın.
        float moved = Vector3.Angle(bakedSunDirection, time.SunDirection);
        if (bakedSunDirection != Vector3.zero && moved < movementDegrees) return;

        bakedSunDirection = time.SunDirection;
        nextBakeTime = Time.time + minimumInterval;

        DynamicGI.UpdateEnvironment();
    }
}
