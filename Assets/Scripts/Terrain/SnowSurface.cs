using System;
using UnityEngine;

/// KARIN ÇARPIŞMA YÜZEYİ. `SnowDisplacement.hlsl` içindeki `SnowMacroDepth` ile aynı
/// hesabı CPU'da yapar: kar geometrik olarak yükseldiğine göre üstünde yürünen zemin
/// de yükselmeli, yoksa oyuncu birikintinin içinden geçer.
///
/// MESAFE SÖNÜMÜ BURADA YOK — bilerek. Görsel yer değiştirme kameraya uzaklıkla
/// sönüyor (bölünme uzakta kapanıyor, bkz. `SnowDisplacement.hlsl`); çarpışma
/// kamerayı bilmez ve bilmemeli. Oyuncu birinci şahısta kameranın konumunda durduğu
/// için ayağının altındaki sönüm zaten 1; uzaktaki yüzeyde ikisi ayrışıyor ama orada
/// kimse yürümüyor. Ortak oyunda bu bir borç — bkz. COOP.md.
///
/// Kar örtüsü ÇARPANI da yok: örtü maskesi gölgelendirmenin işi. Geometri yalnız
/// depodan, eğimden ve birikinti alanından türüyor — ikisi ayrı kanal.
public class SnowSurface : MonoBehaviour
{
    [Tooltip("Kar deposunu ve yüzey haritasını tutan bileşen.")]
    [SerializeField] TerrainSurface surface;
    [Tooltip("Hâkim rüzgâr. Birikinti ekseni buradan geliyor.")]
    [SerializeField] WindField wind;
    [Tooltip("Kar ayarları. Shader ile AYNI asset'i okumak zorunda.")]
    [SerializeField] TerrainMaterialSettings settings;
    [Tooltip("Kot sıfırı: arazinin taban kotu.")]
    [SerializeField] Terrain terrain;

    public void Bind(TerrainSurface surfaceRef, WindField windRef,
        TerrainMaterialSettings settingsRef, Terrain terrainRef)
    {
        surface = surfaceRef;
        wind = windRef;
        settings = settingsRef;
        terrain = terrainRef;
    }

    void OnEnable()
    {
        if (surface == null || wind == null || settings == null || terrain == null)
            throw new InvalidOperationException($"{nameof(SnowSurface)}: bağımlılıklar atanmadı.");
    }

    /// Verilen dünya konumunda karın zeminden yüksekliği (metre). Eşiğin altındaki
    /// ince örtü sıfır döner: 20 cm'lik örtü arazi ızgarasında (4.28 m) zaten
    /// çözülemiyor ve geometriye geçmiyor, çarpışmaya da geçmemeli.
    public float DepthAt(Vector3 worldPos)
    {
        float altitude = worldPos.y - terrain.transform.position.y;

        // Kalınlık DEPOSU, örtü değil: örtü yüzeyin beyazlığı, depo altındaki kalınlık.
        float supply = surface.SnowPackAt(altitude);
        if (supply < 0.001f) return 0f;

        // Dik yamaçta kalın kar durmaz. Duruş açısı 70-75 derece ama KALIN birikinti
        // çok daha erken kayar — 40 derecede pratik olarak sıfır.
        float slopeFit = Mathf.Clamp01((surface.SlopeAt(worldPos) - 0.72f) / 0.28f);
        slopeFit *= slopeFit;

        float drift = SnowDriftField.Shape(new Vector2(worldPos.x, worldPos.z), WindAxis());

        // Arazi ağırlığı: rüzgâraltı ve içbükey yüzey biriktirir, rüzgârüstü ve
        // dışbükey kazınır. Pişmiş dokudan geliyor — shader ile aynı kaynak.
        float depth = supply * slopeFit * Mathf.Lerp(0.35f, 1.4f, drift)
                    * surface.DriftWeightAt(worldPos) * settings.snowDisplaceMax;

        // Eşik yumuşak: sert kesme, birikintinin kenarında basamak bırakır.
        float start = settings.snowDisplaceStart;
        return depth * Smoothstep(start, start * 2f, depth);
    }

    /// Rüzgârın yatay birim vektörü. `TerrainSurface`'in materyale bastığı vektörün
    /// AYNISI — HÂKİM yön, anlık hız değil. İki taraf farklı eksen kullanırsa
    /// çarpışma yüzeyi görsel yüzeyden kayar.
    Vector2 WindAxis()
    {
        Vector3 direction = wind.PrevailingDirection;

        // Sıfır vektörü normalleme: shader'daki 0.0001 kaydırması burada da var.
        return new Vector2(direction.x + 0.0001f, direction.z).normalized;
    }

    static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
