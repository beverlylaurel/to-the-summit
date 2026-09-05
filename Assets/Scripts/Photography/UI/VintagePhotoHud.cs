using UnityEngine;

public sealed class VintagePhotoHud
{
    static readonly Color Ink = RainGlassUi.Ink;
    static readonly Color MutedInk = RainGlassUi.MutedInk;
    static readonly Color Rule = RainGlassUi.Border;

    readonly Font regularFont;
    readonly Font mediumFont;
    readonly Font semiboldFont;
    readonly ThinTripleIconSet icons;

    GUIStyle valueStyle;
    GUIStyle labelStyle;
    GUIStyle keyStyle;
    GUIStyle titleStyle;
    GUIStyle centeredStyle;
    GUIStyle heldNameStyle;
    GUIStyle heldMetaStyle;

    public VintagePhotoHud(Font regular, Font medium, Font semibold, ThinTripleIconSet iconSet)
    {
        regularFont = regular;
        mediumFont = medium;
        semiboldFont = semibold;
        icons = iconSet;
    }

    public static Rect GetViewfinderFrame(out Rect controls)
    {
        float safe = Mathf.Clamp(Mathf.Round(Screen.height * 0.02f), 12f, 22f);
        float controlHeight = Mathf.Clamp(Mathf.Round(Screen.height * 0.09f), 68f, 86f);
        float availableHeight = Mathf.Max(180f, Screen.height - safe * 3f - controlHeight);
        float width = Mathf.Min(Screen.width - safe * 2f, availableHeight * 1.5f);
        float height = width / 1.5f;
        float y = safe + Mathf.Max(0f, (availableHeight - height) * 0.5f);
        Rect frame = PixelRect((Screen.width - width) * 0.5f, y, width, height);
        float controlsY = frame.yMax + safe;
        float controlsWidth = Mathf.Clamp(frame.width * 0.62f, 640f, 820f);
        controls = PixelRect((Screen.width - controlsWidth) * 0.5f, controlsY, controlsWidth,
            Mathf.Max(56f, Screen.height - controlsY - safe));
        return frame;
    }

    public void DrawViewfinder(Texture preview, bool ready, bool capturing, float aperture,
        string shutter, int iso, string ev, float zoom, int remaining)
    {
        EnsureStyles();
        Rect frame = GetViewfinderFrame(out Rect controls);
        DrawOutsideMask(frame);
        GUI.DrawTexture(frame, ready && preview != null ? preview : Texture2D.blackTexture,
            ScaleMode.StretchToFill, false);
        RainGlassUi.DrawFrame(frame);

        DrawTopIndicators(frame, remaining, capturing);
        DrawFocus(frame);
        DrawCameraReadout(frame, aperture, shutter, iso, ev, zoom);
        DrawControls(controls);
    }

    public void DrawEquipped(int remaining)
    {
        EnsureStyles();
        const float safe = 30f;
        Rect panel = PixelRect(safe, Screen.height - 138f, 232f, 52f);
        RainGlassUi.DrawSurface(panel, 0.54f);
        ThinTripleIconRenderer.Draw(icons, ThinTripleIconId.Camera,
            PixelRect(panel.x + 10f, panel.y + 10f, 32f, 32f), Ink);
        GUI.Label(new Rect(panel.x + 52f, panel.y + 7f, panel.width - 62f, 22f),
            "VINTAGE DSLR", heldNameStyle);
        GUI.Label(new Rect(panel.x + 52f, panel.y + 27f, panel.width - 62f, 18f),
            $"HAZIR · {remaining} POZ", heldMetaStyle);

        float actionY = Screen.height - 68f;
        RainGlassUi.DrawStem(new Vector2(panel.x + 23f, panel.yMax),
            new Vector2(panel.x + 23f, actionY));
        Rect viewfinder = PixelRect(safe, actionY, 132f, 34f);
        Rect gallery = PixelRect(viewfinder.xMax + 7f, actionY, 103f, 34f);
        Rect stow = PixelRect(gallery.xMax + 7f, actionY, 108f, 34f);
        DrawHeldIconAction(viewfinder, ThinTripleIconId.MouseRight, "VİZÖR");
        DrawHeldKeyAction(gallery, "G", "GALERİ");
        DrawHeldKeyAction(stow, "4", "KALDIR");
    }

    public void DrawReview(Texture photo) => DrawPhoto(photo, "ÖN İZLEME", "2 SN");

    public void DrawGallery(Texture photo, string fileName, int index, int count)
    {
        DrawPhoto(photo, "FOTOĞRAFLAR", $"{index + 1}/{count}");
        if (!string.IsNullOrEmpty(fileName))
        {
            float width = Mathf.Min(Screen.width - 48f,
                labelStyle.CalcSize(new GUIContent(fileName)).x + 28f);
            Rect filePanel = PixelRect(24f, Screen.height - 80f, width, 30f);
            RainGlassUi.DrawSurface(filePanel, 0.62f);
            GUI.Label(new Rect(filePanel.x + 12f, filePanel.y,
                filePanel.width - 24f, filePanel.height), fileName, labelStyle);
        }

        const string controls = "A / D  GEZİN     G / SAĞ TIK  KAPAT";
        float controlsWidth = centeredStyle.CalcSize(new GUIContent(controls)).x + 34f;
        Rect controlsPanel = PixelRect((Screen.width - controlsWidth) * 0.5f,
            Screen.height - 48f, controlsWidth, 34f);
        RainGlassUi.DrawSurface(controlsPanel, 0.62f);
        GUI.Label(controlsPanel, controls, centeredStyle);
    }

    public void DrawNotice(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        EnsureStyles();
        float width = Mathf.Min(Screen.width - 32f,
            centeredStyle.CalcSize(new GUIContent(text)).x + 40f);
        Rect panel = PixelRect((Screen.width - width) * 0.5f, 18f, width, 36f);
        RainGlassUi.DrawSurface(panel, 0.68f);
        GUI.Label(panel, text, centeredStyle);
    }

    void DrawTopIndicators(Rect frame, int remaining, bool capturing)
    {
        Rect left = PixelRect(frame.x + 16f, frame.y + 14f, capturing ? 196f : 132f, 44f);
        RainGlassUi.DrawSurface(left, 0.58f);
        ThinTripleIconRenderer.Draw(icons, ThinTripleIconId.Camera,
            PixelRect(left.x + 6f, left.y + 6f, 32f, 32f), Ink);
        GUI.Label(new Rect(left.x + 48f, left.y, left.width - 52f, left.height),
            capturing ? "KAYDEDİLİYOR" : "FOTOĞRAF", labelStyle);

        Rect right = PixelRect(frame.xMax - 116f, frame.y + 14f, 100f, 44f);
        RainGlassUi.DrawSurface(right, 0.58f);
        ThinTripleIconRenderer.Draw(icons, ThinTripleIconId.Card,
            PixelRect(right.x + 6f, right.y + 6f, 32f, 32f), Ink);
        GUI.Label(new Rect(right.x + 48f, right.y, right.width - 52f, right.height),
            remaining.ToString(), valueStyle);
    }

    void DrawFocus(Rect frame)
    {
        float size = Mathf.Clamp(frame.height * 0.055f, 30f, 42f);
        ThinTripleIconRenderer.Draw(icons, ThinTripleIconId.Focus,
            PixelRect(frame.center.x - size * 0.5f, frame.center.y - size * 0.5f, size, size),
            RainGlassUi.WithAlpha(Ink, 0.72f));
    }

    void DrawCameraReadout(Rect frame, float aperture, string shutter, int iso, string ev, float zoom)
    {
        float barHeight = Mathf.Clamp(frame.height * 0.098f, 70f, 76f);
        Rect bar = PixelRect(frame.x + 12f, frame.yMax - barHeight - 12f,
            frame.width - 24f, barHeight);
        RainGlassUi.DrawSurface(bar, 0.58f);

        float cell = bar.width / 5f;
        for (int i = 1; i < 5; i++)
            RainGlassUi.Fill(new Rect(Mathf.Round(bar.x + cell * i), bar.y + 10f, 1f,
                bar.height - 20f), RainGlassUi.BorderSoft);
        DrawReadoutCell(new Rect(bar.x, bar.y, cell, bar.height), ThinTripleIconId.Aperture,
            "DİYAFRAM", $"f/{aperture:0.#}");
        DrawReadoutCell(new Rect(bar.x + cell, bar.y, cell, bar.height), ThinTripleIconId.Shutter,
            "ENSTANTANE", shutter);
        DrawBadgeReadoutCell(new Rect(bar.x + cell * 2f, bar.y, cell, bar.height),
            "ISO", "DUYARLILIK", iso.ToString());
        DrawBadgeReadoutCell(new Rect(bar.x + cell * 3f, bar.y, cell, bar.height),
            "EV", "POZ TELAFİSİ", ev);
        DrawReadoutCell(new Rect(bar.x + cell * 4f, bar.y, cell, bar.height), ThinTripleIconId.Zoom,
            "ODAK UZAKLIĞI", $"{zoom:0.0}×");
    }

    void DrawReadoutCell(Rect rect, ThinTripleIconId icon, string label, string value)
    {
        float groupWidth = Mathf.Min(rect.width - 10f, 154f);
        float x = rect.center.x - groupWidth * 0.5f;
        Rect iconRect = PixelRect(x, rect.center.y - 16f, 32f, 32f);
        ThinTripleIconRenderer.Draw(icons, icon, iconRect, Ink);
        float textX = iconRect.xMax + 10f;
        GUI.Label(new Rect(textX, rect.y + 10f, groupWidth - 42f, 20f), label, labelStyle);
        GUI.Label(new Rect(textX, rect.y + 31f, groupWidth - 42f, 27f), value, valueStyle);
    }

    void DrawBadgeReadoutCell(Rect rect, string badge, string label, string value)
    {
        float groupWidth = Mathf.Min(rect.width - 10f, 148f);
        float x = rect.center.x - groupWidth * 0.5f;
        Rect badgeRect = PixelRect(x, rect.center.y - 13f, 30f, 26f);
        RainGlassUi.Fill(badgeRect, RainGlassUi.KeyFill);
        RainGlassUi.DrawOutline(badgeRect, Rule, 1f);
        GUI.Label(badgeRect, badge, keyStyle);
        float textX = badgeRect.xMax + 9f;
        GUI.Label(new Rect(textX, rect.y + 10f, groupWidth - 39f, 20f), label, labelStyle);
        GUI.Label(new Rect(textX, rect.y + 31f, groupWidth - 39f, 27f), value, valueStyle);
    }

    void DrawControls(Rect controls)
    {
        RainGlassUi.DrawSurface(controls, 0.54f);
        float cell = controls.width / 5f;
        DrawIconControl(new Rect(controls.x, controls.y, cell, controls.height),
            ThinTripleIconId.MouseLeft, "ÇEK");
        DrawIconControl(new Rect(controls.x + cell, controls.y, cell, controls.height),
            ThinTripleIconId.MouseWheel, "ZOOM");
        DrawKeyControl(new Rect(controls.x + cell * 2f, controls.y, cell, controls.height),
            "Q / E", "POZLAMA");
        DrawKeyControl(new Rect(controls.x + cell * 3f, controls.y, cell, controls.height),
            "G", "GALERİ");
        DrawIconControl(new Rect(controls.x + cell * 4f, controls.y, cell, controls.height),
            ThinTripleIconId.MouseRight, "ÇIK");
    }

    void DrawIconControl(Rect rect, ThinTripleIconId icon, string action)
    {
        const float iconSize = 32f;
        float labelWidth = labelStyle.CalcSize(new GUIContent(action)).x;
        float totalWidth = iconSize + 9f + labelWidth;
        float x = Mathf.Round(rect.center.x - totalWidth * 0.5f);
        Rect iconRect = PixelRect(x, rect.center.y - iconSize * 0.5f, iconSize, iconSize);
        ThinTripleIconRenderer.Draw(icons, icon, iconRect, Ink);
        GUI.Label(new Rect(iconRect.xMax + 9f, rect.center.y - 15f, labelWidth + 2f, 30f),
            action, labelStyle);
    }

    void DrawKeyControl(Rect rect, string key, string action)
    {
        Vector2 keySize = keyStyle.CalcSize(new GUIContent(key));
        float totalWidth = keySize.x + 18f + labelStyle.CalcSize(new GUIContent(action)).x;
        float x = rect.center.x - totalWidth * 0.5f;
        Rect keyRect = PixelRect(x, rect.center.y - 15f, keySize.x + 14f, 30f);
        RainGlassUi.Fill(keyRect, RainGlassUi.KeyFill);
        RainGlassUi.DrawOutline(keyRect, Rule, 1f);
        GUI.Label(keyRect, key, keyStyle);
        GUI.Label(new Rect(keyRect.xMax + 8f, rect.center.y - 15f,
            rect.xMax - keyRect.xMax - 8f, 30f), action, labelStyle);
    }

    void DrawHeldIconAction(Rect rect, ThinTripleIconId icon, string action)
    {
        RainGlassUi.DrawSurface(rect, 0.54f);
        ThinTripleIconRenderer.Draw(icons, icon,
            PixelRect(rect.x + 7f, rect.y + 7f, 20f, 20f), Ink);
        GUI.Label(new Rect(rect.x + 35f, rect.y, rect.width - 42f, rect.height),
            action, labelStyle);
    }

    void DrawHeldKeyAction(Rect rect, string key, string action)
    {
        RainGlassUi.DrawSurface(rect, 0.54f);
        Rect keyRect = PixelRect(rect.x + 6f, rect.y + 6f, 22f, 22f);
        RainGlassUi.Fill(keyRect, RainGlassUi.KeyFill);
        RainGlassUi.DrawOutline(keyRect, Rule, 1f);
        GUI.Label(keyRect, key, keyStyle);
        GUI.Label(new Rect(rect.x + 36f, rect.y, rect.width - 42f, rect.height),
            action, labelStyle);
    }

    void DrawPhoto(Texture photo, string heading, string detail)
    {
        EnsureStyles();
        RainGlassUi.Fill(new Rect(0f, 0f, Screen.width, Screen.height),
            new Color(0.008f, 0.012f, 0.011f, 0.96f));
        if (photo != null) GUI.DrawTexture(FitRect(3f / 2f, 0.86f), photo, ScaleMode.ScaleToFit, false);
        Rect title = PixelRect((Screen.width - 220f) * 0.5f, 14f, 220f, 52f);
        RainGlassUi.DrawSurface(title, 0.62f);
        GUI.Label(new Rect(title.x, title.y + 3f, title.width, 27f), heading, titleStyle);
        GUI.Label(new Rect(title.x, title.y + 27f, title.width, 19f), detail, centeredStyle);
    }

    void EnsureStyles()
    {
        valueStyle ??= Style(mediumFont, 17, TextAnchor.MiddleLeft, Ink);
        labelStyle ??= Style(regularFont, 13, TextAnchor.MiddleLeft, MutedInk);
        keyStyle ??= Style(mediumFont, 13, TextAnchor.MiddleCenter, Ink);
        titleStyle ??= Style(semiboldFont, 22, TextAnchor.MiddleCenter, Ink);
        centeredStyle ??= Style(regularFont, 15, TextAnchor.MiddleCenter, Ink);
        heldNameStyle ??= Style(semiboldFont, 14, TextAnchor.MiddleLeft, Ink);
        heldMetaStyle ??= Style(regularFont, 12, TextAnchor.MiddleLeft, MutedInk);
    }

    static GUIStyle Style(Font font, int size, TextAnchor alignment, Color color) =>
        new(GUI.skin.label)
        {
            font = font,
            fontSize = size,
            fontStyle = FontStyle.Normal,
            alignment = alignment,
            normal = { textColor = color }
        };

    static Rect FitRect(float aspect, float heightShare)
    {
        float height = Screen.height * heightShare;
        float width = height * aspect;
        if (width > Screen.width * 0.96f)
        {
            width = Screen.width * 0.96f;
            height = width / aspect;
        }
        return PixelRect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    static void DrawOutsideMask(Rect frame)
    {
        Color mask = new(0.008f, 0.014f, 0.013f, 0.64f);
        RainGlassUi.Fill(new Rect(0f, 0f, Screen.width, frame.y), mask);
        RainGlassUi.Fill(new Rect(0f, frame.yMax, Screen.width, Screen.height - frame.yMax), mask);
        RainGlassUi.Fill(new Rect(0f, frame.y, frame.x, frame.height), mask);
        RainGlassUi.Fill(new Rect(frame.xMax, frame.y, Screen.width - frame.xMax, frame.height), mask);
    }

    static Rect PixelRect(float x, float y, float width, float height) =>
        RainGlassUi.PixelRect(x, y, width, height);
}
