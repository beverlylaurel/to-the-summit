// ROL: bir yağış şiddeti seviyesinin bütün sayıları. §10.3 tablosunun asset karşılığı.
// Çağıran: SnowWeather (geçiş yaparken), Faz 8'de SnowfallController (VFX).

using UnityEngine;

[CreateAssetMenu(menuName = "To The Summit/Kar Yağış Preseti", fileName = "SnowWeatherPreset")]
public class SnowWeatherPreset : ScriptableObject
{
    [Header("Parçacık")]
    [Tooltip("VFX doğum hızı, tane/saniye.")]
    [SerializeField] float flakeRate;

    [Tooltip("VFX kapasitesi. Runtime'da değişmez; en yüksek preset için ayarlanır.")]
    [SerializeField] int capacity;

    [Header("Yağış")]
    [Tooltip("Su eşdeğeri yağış, mm/saat. TEK KAYNAK — birikme hızı buradan türer.")]
    [SerializeField] float sweMillimetersPerHour;

    [Header("Rüzgâr")]
    [SerializeField] float windSpeedMin;
    [SerializeField] float windSpeedMax;

    [Header("Atmosfer")]
    [Tooltip("Sis yoğunluğu çarpanı (Faz 8).")]
    [SerializeField] float fogMultiplier = 1f;

    [Tooltip("Yer savrulması açık mı (Faz 8).")]
    [SerializeField] bool spindrift;

    public float FlakeRate => flakeRate;
    public int Capacity => capacity;
    public float SweMillimetersPerHour => sweMillimetersPerHour;
    public float WindSpeedMin => windSpeedMin;
    public float WindSpeedMax => windSpeedMax;
    public float FogMultiplier => fogMultiplier;
    public bool Spindrift => spindrift;

    /// Simülasyonun kullandığı birikme hızı, m SWE/saniye.
    ///
    /// TÜRETİLİYOR, ayrı alan DEĞİL. §15: "Yağış hızı ile birikme uyuşmuyor — VFX rate
    /// ile _SnowfallSWERate ayrı kaynaklardan." İkisi de bu asset'ten çıkıyor.
    public float SnowfallSWERate => sweMillimetersPerHour / 1000f / 3600f;

    /// §10.3 tablosunun satırlarını asset'e yazar. Editör kurulumu kullanır.
    public void Configure(float rate, int cap, float mmPerHour,
                          float windMin, float windMax, float fog, bool drift)
    {
        flakeRate = rate;
        capacity = cap;
        sweMillimetersPerHour = mmPerHour;
        windSpeedMin = windMin;
        windSpeedMax = windMax;
        fogMultiplier = fog;
        spindrift = drift;
    }
}
