// ROL: kar sisteminin SANATSAL ve kurulum ayarları (§14: sanatsal olan burada,
// fiziksel olan SnowConstants'ta). Asset olarak dışarıdan verilir.
// Çağıran: SnowManager ve sonraki fazlarda SnowWeather / SnowClipmap / SnowOcclusionCapture.

using UnityEngine;

/// Zemin yüksekliğinin nereden geleceği (§3).
public enum SnowGroundSource
{
    /// Unity Terrain'in heightmap'i bake edilir. Varsayılan (§14).
    UnityTerrain,

    /// Mesh tabanlı dünya: Ground layer'ı tepeden ortografik render edilir.
    OrthographicCapture,
}

[CreateAssetMenu(menuName = "To The Summit/Kar Sistemi", fileName = "SnowSettings")]
public class SnowSettings : ScriptableObject
{
    [Header("Kalite")]
    [Tooltip("Çözünürlük, halka sayısı, deformer sayısı ve VFX kapasitesini birlikte " +
             "belirler. Sayılar SnowQualityPreset içinde.")]
    [SerializeField] SnowQualityPreset quality = SnowQualityPreset.High;

    [Header("Zemin")]
    [SerializeField] SnowGroundSource groundSource = SnowGroundSource.UnityTerrain;

    [Header("Gökyüzü görünürlüğü kamerası")]
    [Tooltip("Ortografik kameranın sahnenin en yüksek noktasının ne kadar üstünde " +
             "duracağı, metre.")]
    [SerializeField] float occlusionCameraHeight = 400f;

    [Tooltip("Kameranın görüş derinliği, metre. En alçak noktayı kapsamalı.")]
    [SerializeField] float occlusionCameraDepth = 800f;

    // ASSUMPTION: engel kamerasının KENDİ renderer'ı olmak zorunda. Projenin ana
    // renderer'ında fiziksel gökyüzü, bulut ve sis geçişleri var; bunlar tek kanallı
    // RHalf hedefte null'a düşüp render graph'i çökertiyor, çökmese bile boşuna koşuyor.
    // §4.1 bunu öngörmüyor çünkü replacement shader'ı varsayıyor; URP'de karşılığı bu.
    [Tooltip("Engel kamerasının kullanacağı renderer'ın URP asset'indeki sıra numarası. " +
             "Kar Teşhisi > Sahneyi kur bunu yazıyor.")]
    [SerializeField] int occlusionRendererIndex = -1;

    [Header("Birikme")]
    [Tooltip("Rüzgâr yeniden dağıtımının şiddeti. Rüzgâra bakan yamaçta kar birikmesi " +
             "azalır, arkasında artar. 0 = kapalı.")]
    [SerializeField, Range(0f, 1f)] float driftBias = 0.45f;

    [Header("Deformasyon")]
    [Tooltip("Damga atlası (§5.2). SnowStampGenerator üretiyor.")]
    [SerializeField] Texture2DArray stampAtlas;

    [Tooltip("Kenar mevduatının hız yönünde kaydırılması, saniye. İtiş anında kar öne savrulur.")]
    [SerializeField, Range(0f, 0.2f)] float rimVelocityBias = 0.045f;

    [Tooltip("Duruş açısı gevşemesinin hızı, 1/saniye.")]
    [SerializeField, Range(0f, 20f)] float relaxRate = 3f;

    [Tooltip("Bu rüzgâr hızının üstünde gevşeme dokunun TAMAMINDA koşuyor, m/s.")]
    [SerializeField] float forceRelaxWindSpeed = 6f;

    [Header("Dünyanın temel kar durumu")]
    // ASSUMPTION: §2.4 bu üç değerin SnowWeather'dan geleceğini söylüyor ama SnowWeather
    // Faz 3'te yazılıyor. Faz 1–2 boyunca kaynak burası; SnowWeather gelince onun sürdüğü
    // değer bunların üstüne yazacak ve bu alanlar başlangıç durumu olarak kalacak.
    [Tooltip("Bölgeye yeni giren şeridin kar su eşdeğeri, metre. Yeni açılan şerit boş " +
             "değil, oyunun genel kar seviyesiyle dolu gelir.")]
    [SerializeField, Range(0f, 0.60f)] float defaultSWE = 0.02f;

    [Tooltip("Yeni şeridin normalize yoğunluğu. rho = lerp(50, 550, bu).")]
    [SerializeField, Range(0f, 1f)] float defaultRhoN = 0.12f;

    [Tooltip("Yeni şeridin ıslaklığı.")]
    [SerializeField, Range(0f, 1f)] float defaultWet = 0f;

    public SnowQualityPreset Quality => quality;
    public SnowQualityData QualityData => SnowQuality.Get(quality);
    public SnowGroundSource GroundSource => groundSource;
    public float OcclusionCameraHeight => occlusionCameraHeight;
    public float OcclusionCameraDepth => occlusionCameraDepth;
    public int OcclusionRendererIndex => occlusionRendererIndex;
    public Texture2DArray StampAtlas => stampAtlas;
    public float RimVelocityBias => rimVelocityBias;
    public float RelaxRate => relaxRate;
    public float ForceRelaxWindSpeed => forceRelaxWindSpeed;
    public float DriftBias => driftBias;
    public float DefaultSWE => defaultSWE;
    public float DefaultRhoN => defaultRhoN;
    public float DefaultWet => defaultWet;
}
