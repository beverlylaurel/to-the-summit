using UnityEngine;

public static class RainGlassUi
{
    public static readonly Color Ink = new(0.886f, 0.91f, 0.886f, 0.96f);
    public static readonly Color MutedInk = new(0.635f, 0.678f, 0.655f, 0.90f);
    public static readonly Color Surface = new(0.051f, 0.071f, 0.067f, 0.54f);
    public static readonly Color Border = new(0.765f, 0.812f, 0.776f, 0.52f);
    public static readonly Color BorderSoft = new(0.765f, 0.812f, 0.776f, 0.18f);
    public static readonly Color Accent = new(0.773f, 0.816f, 0.776f, 0.82f);
    public static readonly Color KeyFill = new(0.055f, 0.075f, 0.069f, 0.74f);

    public static void DrawSurface(Rect rect, float alpha = 0.54f, bool innerBorder = true)
    {
        rect = PixelRect(rect);
        Fill(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height),
            new Color(0f, 0f, 0f, 0.20f));
        Fill(rect, WithAlpha(Surface, alpha));
        DrawOutline(rect, Border, 1f);

        if (innerBorder && rect.width > 8f && rect.height > 8f)
            DrawOutline(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f),
                BorderSoft, 1f);

        Fill(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), 1f),
            new Color(Ink.r, Ink.g, Ink.b, 0.16f));
    }

    public static void DrawFrame(Rect rect)
    {
        rect = PixelRect(rect);
        DrawOutline(rect, Border, 1f);
        DrawOutline(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f),
            BorderSoft, 1f);
    }

    public static void DrawStem(Vector2 from, Vector2 to) =>
        Fill(PixelRect(from.x, from.y, 1f, Mathf.Max(1f, to.y - from.y)), Border);

    public static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = Multiply(color, previous);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    public static void DrawOutline(Rect rect, Color color, float thickness)
    {
        Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    public static Color WithAlpha(Color color, float alpha) =>
        new(color.r, color.g, color.b, alpha);

    public static Color Multiply(Color color, Color tint) =>
        new(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a * tint.a);

    public static Rect PixelRect(Rect rect) => PixelRect(rect.x, rect.y, rect.width, rect.height);

    public static Rect PixelRect(float x, float y, float width, float height) =>
        new(Mathf.Round(x), Mathf.Round(y), Mathf.Round(width), Mathf.Round(height));
}
