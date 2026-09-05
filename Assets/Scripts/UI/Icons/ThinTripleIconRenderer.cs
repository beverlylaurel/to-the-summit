using UnityEngine;

public static class ThinTripleIconRenderer
{
    public static void Draw(ThinTripleIconSet set, ThinTripleIconId id, Rect rect, Color color)
    {
        ThinTripleIconSet.Icon icon = set != null ? set.Get(id) : null;
        if (icon == null) return;
        Texture2D texture = rect.width < 24f ? icon.small : rect.width < 40f ? icon.medium : icon.large;
        if (texture == null) return;
        Color previous = GUI.color;
        GUI.color = RainGlassUi.Multiply(color, previous);
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.color = previous;
    }
}
