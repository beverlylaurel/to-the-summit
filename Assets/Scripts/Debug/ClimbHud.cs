using System;
using System.Text;
using UnityEngine;

/// Sağ üstte tırmanış ve hava durumunu çizer. Kendisi hiçbir şeyi sürmez, yalnızca okur.
public class ClimbHud : MonoBehaviour
{
    [SerializeField] Transform observer;
    [SerializeField] Terrain terrain;
    [SerializeField] AltitudeWeatherDriver weatherDriver;
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [SerializeField] TemperatureField temperature;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] TerrainSurface surface;

    const float PanelWidth = 330f;
    const int FontSize = 12;
    const float PaddingX = 8f;
    const float PaddingY = 5f;
    const float Margin = 10f;

    // OnGUI saniyede 120 kez çağrılır (Layout + Repaint). Metni orada kurmak kare
    // başına string çöpü üretiyordu; PerformanceHud'daki gibi ayrık aralıkla hazırlanır.
    const float RefreshInterval = 0.1f;

    readonly StringBuilder builder = new();
    readonly GUIContent content = new();
    // GUIStyle SERİLEŞTİRİLMİYOR. Unity onu kaydetmeye çalıştığında içindeki font
    // referansı derleme sonrası geçersiz kalıyor ve her yeniden yüklemede
    // "Deleting invalid font reference" uyarısı basılıyor. Biçim zaten her
    // kullanımda kuruluyor, saklanacak bir şey yok.
    [System.NonSerialized] GUIStyle style;
    string readout = "";
    float nextRefresh;

    public void Bind(Transform observerRef, Terrain terrainRef, AltitudeWeatherDriver driverRef,
        WeatherState weatherRef, WindField windRef, TimeOfDay timeRef,
        AtmosphereController atmosphereRef, TerrainSurface surfaceRef,
        TemperatureField temperatureRef)
    {
        observer = observerRef;
        terrain = terrainRef;
        weatherDriver = driverRef;
        weather = weatherRef;
        wind = windRef;
        time = timeRef;
        atmosphere = atmosphereRef;
        surface = surfaceRef;
        temperature = temperatureRef;
    }

    void OnEnable()
    {
        if (observer == null || terrain == null || weatherDriver == null || weather == null
            || wind == null || time == null || atmosphere == null || surface == null
            || temperature == null)
            throw new InvalidOperationException($"{nameof(ClimbHud)}: bağımlılıklar atanmadı.");
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;

        nextRefresh = Time.unscaledTime + RefreshInterval;
        Format();
    }

    void Format()
    {
        float ground = terrain.transform.position.y;
        float altitude = observer.position.y - ground;
        float summit = weatherDriver.SummitAltitude;

        builder.Clear();

        // Bütün kotlar zemine göre. Yükseklik göreli, kuşak sınırları mutlak yazılınca
        // ikisi karşılaştırılamıyordu: "2114 m'deyim, fırtına 4709 m'de" iki farklı
        // sıfır noktasından ölçülmüş iki sayıydı.
        builder.AppendFormat("TIRMANIŞ\n");
        builder.AppendFormat("  Bulunduğun yükseklik   {0:F0} m\n", altitude);
        builder.AppendFormat("  Zirve                  {0:F0} m   (%{1:F0} tamamlandı)\n",
            summit - ground, Mathf.Clamp01(altitude / (summit - ground)) * 100f);
        builder.AppendFormat("  Havanın gördüğü kot    {0:F0} m\n\n",
            weatherDriver.ProgressAltitude - ground);

        // SICAKLIK. Ölçülen ve hissedilen ayrı yazılıyor: rüzgâr termometreyi
        // değiştirmez, insanı değiştirir. Donma seviyesi de aynı kaynaktan türüyor —
        // üç satır tek modelin üç yüzü.
        builder.AppendFormat("SICAKLIK\n");
        builder.AppendFormat("  Ölçülen                {0:F1} °C\n",
            temperature.At(observer.position.y));
        builder.AppendFormat("  Hissedilen             {0:F1} °C   (rüzgâr soğuğu)\n",
            temperature.FeltAt(observer.position.y));
        builder.AppendFormat("  Donma seviyesi         {0:F0} m\n\n",
            temperature.FreezingLevel - ground);

        builder.AppendFormat("HAVA KUŞAKLARI\n");
        builder.AppendFormat("  Yağmur biter           {0:F0} m\n", weatherDriver.RainCeiling - ground);
        builder.AppendFormat("  Saf kar başlar         {0:F0} m\n", weatherDriver.SnowFloor - ground);
        builder.AppendFormat("  Sürekli fırtına        {0:F0} m\n\n",
            weatherDriver.BlizzardAltitude - ground);

        builder.AppendFormat("YAĞIŞ\n");
        builder.AppendFormat("  Şiddet                 {0:F2}\n", weather.Precipitation);
        builder.AppendFormat("  Kar oranı              {0:F2}   (0 yağmur, 1 kar)\n",
            weather.Snowiness);
        builder.AppendFormat("  Yere düşen             yağmur {0:F2}   kar {1:F2}\n",
            weather.Precipitation * (1f - weather.Snowiness),
            weather.Precipitation * weather.Snowiness);
        builder.AppendFormat("  Zemindeki kar örtüsü   %{0:F0}   (bulunduğun kotta)\n",
            surface.SnowCoverAt(observer.position.y) * 100f);
        builder.AppendFormat("  Kar kalınlığı deposu   %{0:F0}\n",
            surface.SnowPackAt(observer.position.y) * 100f);
        builder.AppendFormat("  Açık pencere           {0:F2}   (1 = hava açıldı)\n\n",
            weatherDriver.ClearWindow);

        builder.AppendFormat("RÜZGÂR\n");
        builder.AppendFormat("  Sürekli şiddet         {0:F2}\n", wind.Strength);
        builder.AppendFormat("  Anlık esinti           {0:+0.00;-0.00}\n", wind.Gust);
        builder.AppendFormat("  Hız                    {0:F1} m/s\n\n", wind.Velocity.magnitude);

        builder.AppendFormat("GÖRÜŞ\n");
        builder.AppendFormat("  Görüş mesafesi         {0:F0} m\n", atmosphere.Visibility);
        // "Kaplama" değil "kapsama": bu, katmanın ne kadarının bulut olduğu — göğün ne
        // kadarının kapandığı değil. Zirvede %95 yazarken gökyüzü açık olabiliyor, çünkü
        // oyuncu katmanın üstüne çıkmış oluyor. O yüzden nerede durduğu da yazılıyor.
        float top = atmosphere.CloudTop;
        float bottom = atmosphere.CloudBottom;
        string place = observer.position.y > top ? "üstündesin"
                     : observer.position.y < bottom ? "altındasın"
                     : "içindesin";

        builder.AppendFormat("  Bulut kapsaması        %{0:F0}\n", atmosphere.Coverage * 100f);
        builder.AppendFormat("  Bulut katmanı          {0:F0} – {1:F0} m   ({2})\n\n",
            bottom - ground, top - ground, place);

        builder.AppendFormat("ZAMAN\n");
        builder.AppendFormat("  Saat                   {0}\n", time.Clock);
        builder.AppendFormat("  Gündüz oranı           {0:F2}   (0 gece, 1 tam gündüz)", time.DayFactor);

        readout = builder.ToString();
    }

    void OnGUI()
    {
        // Sabit genişlikli yazı: sayılar sütun hâlinde hizalanıyor, orantılı yazıda
        // her satır kayıp okunmaz hale geliyordu. Sarma kapalı, çünkü sarılan satır
        // hizayı bozuyor — panel zaten en uzun satıra göre ölçülü.
        style ??= new GUIStyle(GUI.skin.label)
        {
            font = Font.CreateDynamicFontFromOSFont("Consolas", FontSize),
            fontSize = FontSize,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };

        content.text = readout;
        style.normal.textColor = Color.white;

        float textWidth = PanelWidth - PaddingX * 2f;
        float height = style.CalcHeight(content, textWidth) + PaddingY * 2f;
        var rect = new Rect(Screen.width - PanelWidth - Margin, Margin, PanelWidth, height);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + PaddingX, rect.y + PaddingY, textWidth, height - PaddingY * 2f),
            content, style);
    }
}
