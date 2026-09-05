using System.Collections.Generic;
using UnityEngine;

/// Shared Sapli Kart renderer for every held item.
public sealed class HeldItemHud
{
    const float TransitionSeconds = 0.22f;
    const float OffsetPixels = -4f;

    static readonly Color Ink = RainGlassUi.Ink;
    static readonly Color MutedInk = RainGlassUi.MutedInk;
    static readonly Color Rule = RainGlassUi.Border;

    readonly Font regularFont;
    readonly Font mediumFont;
    readonly Font semiboldFont;
    readonly ThinTripleIconSet icons;

    GUIStyle labelStyle;
    GUIStyle keyStyle;
    GUIStyle nameStyle;
    GUIStyle metaStyle;
    GUIStyle noticeStyle;

    bool transitionInitialized;
    bool targetVisible;
    float transitionStartedAt;
    float transitionFrom;
    float amount;

    public bool IsVisible
    {
        get
        {
            UpdateTransition();
            return targetVisible || amount > 0.001f;
        }
    }

    public HeldItemHud(Font regular, Font medium, Font semibold, ThinTripleIconSet iconSet)
    {
        regularFont = regular;
        mediumFont = medium;
        semiboldFont = semibold;
        icons = iconSet;
    }

    public void SetVisible(bool visible)
    {
        if (!transitionInitialized)
        {
            transitionInitialized = true;
            targetVisible = visible;
            transitionFrom = 0f;
            amount = 0f;
            transitionStartedAt = Time.unscaledTime;
            return;
        }

        UpdateTransition();
        if (targetVisible == visible) return;
        targetVisible = visible;
        transitionFrom = amount;
        transitionStartedAt = Time.unscaledTime;
    }

    public void Draw(EquippableItem item, IReadOnlyList<HeldItemAction> actions)
    {
        EnsureStyles();
        UpdateTransition();
        if (amount <= 0.001f || item == null) return;

        Color previousColor = GUI.color;
        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b,
            previousColor.a * amount);
        float safe = 30f + Mathf.Lerp(OffsetPixels, 0f, amount);
        Rect panel = PixelRect(safe, Screen.height - 138f, 232f, 52f);
        RainGlassUi.DrawSurface(panel, 0.54f);
        ThinTripleIconRenderer.Draw(icons, item.DisplayIcon,
            PixelRect(panel.x + 10f, panel.y + 10f, 32f, 32f), Ink);
        GUI.Label(new Rect(panel.x + 52f, panel.y + 7f, panel.width - 62f, 22f),
            item.DisplayName, nameStyle);
        GUI.Label(new Rect(panel.x + 52f, panel.y + 27f, panel.width - 62f, 18f),
            item.StatusText, metaStyle);

        float actionY = Screen.height - 68f;
        RainGlassUi.DrawStem(new Vector2(panel.x + 23f, panel.yMax),
            new Vector2(panel.x + 23f, actionY));
        const float actionWidth = 120f;
        for (int i = 0; i < actions.Count; i++)
        {
            Rect rect = PixelRect(safe + i * (actionWidth + 7f), actionY, actionWidth, 34f);
            DrawAction(rect, actions[i]);
        }
        GUI.color = previousColor;
    }

    public void DrawNotice(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        EnsureStyles();
        float width = Mathf.Min(Screen.width - 32f,
            noticeStyle.CalcSize(new GUIContent(text)).x + 40f);
        Rect panel = PixelRect((Screen.width - width) * 0.5f, 18f, width, 36f);
        RainGlassUi.DrawSurface(panel, 0.68f);
        GUI.Label(panel, text, noticeStyle);
    }

    void DrawAction(Rect rect, HeldItemAction action)
    {
        RainGlassUi.DrawSurface(rect, 0.54f);
        if (action.Input.IsKey)
        {
            Rect keyRect = PixelRect(rect.x + 6f, rect.y + 6f, 22f, 22f);
            RainGlassUi.Fill(keyRect, RainGlassUi.KeyFill);
            RainGlassUi.DrawOutline(keyRect, Rule, 1f);
            GUI.Label(keyRect, action.Input.KeyLabel, keyStyle);
            GUI.Label(new Rect(rect.x + 36f, rect.y, rect.width - 42f, rect.height),
                action.Label, labelStyle);
            return;
        }

        ThinTripleIconRenderer.Draw(icons, action.Input.PointerIcon,
            PixelRect(rect.x + 7f, rect.y + 7f, 20f, 20f), Ink);
        GUI.Label(new Rect(rect.x + 35f, rect.y, rect.width - 42f, rect.height),
            action.Label, labelStyle);
    }

    void EnsureStyles()
    {
        labelStyle ??= Style(regularFont, 13, TextAnchor.MiddleLeft, MutedInk);
        keyStyle ??= Style(mediumFont, 13, TextAnchor.MiddleCenter, Ink);
        nameStyle ??= Style(semiboldFont, 14, TextAnchor.MiddleLeft, Ink);
        metaStyle ??= Style(regularFont, 12, TextAnchor.MiddleLeft, MutedInk);
        noticeStyle ??= Style(regularFont, 15, TextAnchor.MiddleCenter, Ink);
    }

    void UpdateTransition()
    {
        if (!transitionInitialized) return;
        float t = Mathf.Clamp01((Time.unscaledTime - transitionStartedAt) / TransitionSeconds);
        float eased = t * t * (3f - 2f * t);
        amount = Mathf.Lerp(transitionFrom, targetVisible ? 1f : 0f, eased);
    }

    static GUIStyle Style(Font font, int size, TextAnchor alignment, Color color) =>
        new(GUI.skin.label)
        {
            font = font,
            fontSize = size,
            fontStyle = FontStyle.Normal,
            alignment = alignment,
            normal = { textColor = color },
            clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 0, 0)
        };

    static Rect PixelRect(float x, float y, float width, float height) =>
        new(Mathf.Round(x), Mathf.Round(y), Mathf.Round(width), Mathf.Round(height));
}
