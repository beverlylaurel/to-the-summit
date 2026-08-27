using System;
using System.Text;
using UnityEngine;

/// Draws the metrics and watchdog warnings in the top left. It measures nothing itself.
public class PerformanceHud : MonoBehaviour
{
    [SerializeField] PerformanceSampler sampler;
    [SerializeField] PerformanceWatchdog watchdog;

    static readonly Color WarningColor = new(1f, 0.75f, 0.2f);
    static readonly Color CriticalColor = new(1f, 0.35f, 0.3f);

    const float PanelWidth = 300f;
    const int FontSize = 12;
    const float PaddingX = 8f;
    const float PaddingY = 5f;

    readonly StringBuilder builder = new();
    readonly GUIContent content = new();
    string metrics = "";
    // THE GUIStyle IS NOT SERIALIZED. When Unity tries to save it the font reference inside
    // becomes invalid after a compile and every reload prints a "Deleting invalid font
    // reference" warning. The style is built at every use anyway, so there is nothing to
    // store.
    [System.NonSerialized] GUIStyle style;

    public void Bind(PerformanceSampler source, PerformanceWatchdog monitor)
    {
        sampler = source;
        watchdog = monitor;
    }

    void OnEnable()
    {
        if (sampler == null)
            throw new InvalidOperationException($"{nameof(PerformanceHud)}: {nameof(sampler)} is not assigned.");

        sampler.Sampled += Format;
    }

    void OnDisable()
    {
        if (sampler != null) sampler.Sampled -= Format;
    }

    void Format(PerformanceSnapshot s)
    {
        builder.Clear();
        builder.AppendFormat("{0:F0} FPS   {1:F1} ms\n", s.InstantFps, s.InstantMs);
        builder.AppendFormat("ort {0:F0}   1% low {1:F0}\n", s.AverageFps, s.OnePercentLowFps);
        builder.AppendFormat("Bellek {0} MB   GC {1} MB", s.TotalMemoryMb, s.ManagedMemoryMb);

        if (s.DrawCalls > 0)
            builder.AppendFormat("\nDraw {0}   SetPass {1}   Tri {2}k", s.DrawCalls, s.SetPassCalls, s.Triangles / 1000);

        metrics = builder.ToString();
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = FontSize,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };

        float y = DrawPanel(10f, metrics, Color.white);

        if (watchdog == null) return;

        foreach (var alert in watchdog.Alerts)
        {
            var color = alert.Severity == AlertSeverity.Critical ? CriticalColor : WarningColor;
            y = DrawPanel(y + 4f, alert.Message, color);
        }
    }

    /// Draws the panel tall enough for its text and returns the y for the next panel
    float DrawPanel(float y, string text, Color textColor)
    {
        content.text = text;
        style.normal.textColor = textColor;

        float textWidth = PanelWidth - PaddingX * 2f;
        float height = style.CalcHeight(content, textWidth) + PaddingY * 2f;
        var rect = new Rect(10f, y, PanelWidth, height);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + PaddingX, rect.y + PaddingY, textWidth, height - PaddingY * 2f), content, style);

        return y + height;
    }
}
