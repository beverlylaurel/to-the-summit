using System;
using System.Text;
using UnityEngine;

/// Draws the climb and weather state at the top right. It drives nothing itself, it only reads.
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
    [SerializeField] CloudLayerProbe cloudLayer;

    const float PanelWidth = 330f;
    const int FontSize = 12;
    const float PaddingX = 8f;
    const float PaddingY = 5f;
    const float Margin = 10f;

    // OnGUI is called 120 times a second (Layout + Repaint). Building the text there produced
    // string garbage every frame; as in PerformanceHud it is prepared at a discrete interval.
    const float RefreshInterval = 0.1f;

    readonly StringBuilder builder = new();
    readonly GUIContent content = new();
    // GUIStyle IS NOT SERIALIZED. When Unity tries to save it the font reference inside
    // stays invalid after a recompile and a "Deleting invalid font reference" warning is
    // printed on every reload. The style is built at every use anyway, there is nothing
    // to store.
    [System.NonSerialized] GUIStyle style;
    string readout = "";
    float nextRefresh;

    public void Bind(Transform observerRef, Terrain terrainRef, AltitudeWeatherDriver driverRef,
        WeatherState weatherRef, WindField windRef, TimeOfDay timeRef,
        AtmosphereController atmosphereRef, TerrainSurface surfaceRef,
        TemperatureField temperatureRef, CloudLayerProbe cloudLayerRef)
    {
        cloudLayer = cloudLayerRef;
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
            || cloudLayer == null
            || temperature == null)
            throw new InvalidOperationException($"{nameof(ClimbHud)}: dependencies are not assigned.");
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

        // All elevations are relative to the ground. With the altitude relative and the band
        // boundaries written as absolute, the two could not be compared: "I am at 2114 m, the
        // storm is at 4709 m" were two numbers measured from two different zero points.
        builder.AppendFormat("TIRMANIŞ\n");
        builder.AppendFormat("  Bulunduğun irtifa      {0:F0} m\n", altitude);
        builder.AppendFormat("  Zirve                  {0:F0} m   (%{1:F0} tamamlandı)\n",
            summit - ground, Mathf.Clamp01(altitude / (summit - ground)) * 100f);
        builder.AppendFormat("  Havanın gördüğü irtifa {0:F0} m\n\n",
            weatherDriver.ProgressAltitude - ground);

        // TEMPERATURE. Measured and felt are printed separately: the wind does not change the
        // thermometer, it changes the person. The freezing level derives from the same source —
        // three lines, three faces of one model.
        builder.AppendFormat("SICAKLIK\n");
        builder.AppendFormat("  Ölçülen                {0:F1} °C\n",
            temperature.At(observer.position.y));
        builder.AppendFormat("  Hissedilen             {0:F1} °C   (rüzgâr soğutması)\n",
            temperature.FeltAt(observer.position.y));
        builder.AppendFormat("  Donma seviyesi         {0:F0} m\n\n",
            temperature.FreezingLevel - ground);

        builder.AppendFormat("HAVA KUŞAKLARI\n");
        builder.AppendFormat("  Sürekli fırtına        {0:F0} m\n\n",
            weatherDriver.BlizzardAltitude - ground);

        builder.AppendFormat("YAĞIŞ\n");
        builder.AppendFormat("  Şiddet                 {0:F2}\n", weather.Precipitation);
        builder.AppendFormat("  Açık pencere           {0:F2}   (1 = hava açtı)\n\n",
            weatherDriver.ClearWindow);

        builder.AppendFormat("RÜZGÂR\n");
        builder.AppendFormat("  Sürekli şiddet         {0:F2}\n", wind.Strength);
        builder.AppendFormat("  Anlık hamle            {0:+0.00;-0.00}\n", wind.Gust);
        builder.AppendFormat("  Hız                    {0:F1} m/s\n\n", wind.Velocity.magnitude);

        builder.AppendFormat("GÖRÜŞ\n");
        builder.AppendFormat("  Görüş mesafesi         {0:F0} m\n", atmosphere.Visibility);
        // "Coverage", not "cover": this is how much of the LAYER is cloud — not how much of the
        // sky is closed. It can read 95% at the summit while the sky is clear, because the player
        // has climbed above the layer. That is why where they stand is printed too.
        // The elevations come from the cloud system's OWN data: the same Volume settings, the same
        // weather map. The atmosphere's `CloudTop`/`CloudBottom` belonged to the deleted system.
        float top = cloudLayer.TopAt(observer.position);
        float bottom = cloudLayer.Bottom;

        // BOTH NUMBERS ARE PRINTED, BECAUSE THEY ARE DIFFERENT THINGS.
        //
        // The local value is the coverage the weather map gives at your XZ; the global
        // value is how much cloud there is across the sky in general. At first only the
        // local one was printed and it was labelled "Cloud coverage" -- it led to a wrong
        // diagnosis TWICE:
        //   1. The HUD showed 0% while there was cloud on screen, and it was taken for
        //      "there is a line where there is no cloud".
        //   2. The HUD showed 0% while there were clear dark patches on the ground
        //      (the user sent two frames). The patches were cloud SHADOW:
        //      the global coverage was 19.5% and the cloud in the sun's direction was
        //      casting a shadow.
        //
        // The shadow comes not from the cloud above you but from the cloud IN THE SUN'S
        // DIRECTION. The local coverage never answers that question.
        builder.AppendFormat("  Bulut — tepende        %{0:F0}\n",
            cloudLayer.CoverageAt(observer.position) * 100f);
        builder.AppendFormat("  Bulut — gökyüzünde     %{0:F0}   (gölge bundan geliyor)\n",
            atmosphere.Coverage * 100f);

        if (float.IsPositiveInfinity(top))
            builder.AppendFormat("  Bulut katmanı          bu sütunda bulut yok\n\n");
        else
        {
            string place = observer.position.y > top ? "üstündesin"
                         : observer.position.y < bottom ? "altındasın"
                         : "içindesin";
            builder.AppendFormat("  Bulut katmanı          {0:F0} – {1:F0} m   ({2})\n\n",
                bottom - ground, top - ground, place);
        }

        builder.AppendFormat("ZAMAN\n");
        builder.AppendFormat("  Saat                   {0}\n", time.Clock);
        builder.AppendFormat("  Gündüz katsayısı       {0:F2}   (0 gece, 1 tam gündüz)", time.DayFactor);

        readout = builder.ToString();
    }

    void OnGUI()
    {
        // A fixed-width font: the numbers line up in columns, in a proportional font every line
        // was offset and became unreadable. Wrapping is off, because a wrapped line breaks the
        // alignment — the panel is already sized to the longest line.
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
