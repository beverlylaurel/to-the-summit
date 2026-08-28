using UnityEditor;
using UnityEngine;

/// Displays surface maps channel by channel. Material consumes these maps;
/// writing consumer before verifying producer makes bugs twice as hard to locate.
public class SurfaceMapWindow : EditorWindow
{
    enum Channel { Accumulation, Concavity, Exposure }

    static readonly (Channel channel, string label, string expected)[] Channels =
    {
        (Channel.Accumulation, "Accumulation",
            "Stream and gully network should be visible: lines branching downward and merging thicker. " +
            "Salt-and-pepper noise indicates broken flow calculations. Scree/gravel is driven from here."),
        (Channel.Concavity, "Concavity",
            "Valley floors and hollows should be light, ridges dark. Lichen moisture reads from here."),
        (Channel.Exposure, "Sky Exposure",
            "Ridges and plains should be near white, valley depths and gullies dark. " +
            "Both lichen and surface ambient occlusion feed from here."),
    };

    Texture2D maps;
    Texture2D preview;
    Channel channel = Channel.Accumulation;

    // Height reserved above preview for toolbar, info box, and margins
    const float ChromeHeight = 110f;

    [MenuItem("To The Summit/Terrain/Surface Maps", false, 22)]
    static void Open()
    {
        var window = GetWindow<SurfaceMapWindow>("Surface Maps");
        window.minSize = new Vector2(420f, 560f);

        // Map is 1024^2; downsampling into a small preview introduces aliasing resembling noise.
        // Window opens at maximum fitting screen size, keeping preview close to 1:1.
        var screen = Screen.currentResolution;
        float side = Mathf.Min(screen.height - ChromeHeight - 80f, SurfaceMapBaker.MapResolution);

        window.position = new Rect(
            (screen.width - side) * 0.5f,
            Mathf.Max(0f, (screen.height - side - ChromeHeight) * 0.5f),
            side,
            side + ChromeHeight);
    }

    void OnEnable() => maps = SurfaceMapBaker.Load();

    void OnDisable()
    {
        if (preview != null) DestroyImmediate(preview);
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake Maps")) Bake();

            using (new EditorGUI.DisabledScope(maps == null))
                if (GUILayout.Button("Reload")) Reload();
        }

        if (maps == null)
        {
            EditorGUILayout.HelpBox(
                "No surface maps found. Press 'Bake Maps' with a terrain in the scene.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space(6f);

        var next = (Channel)GUILayout.Toolbar((int)channel, System.Array.ConvertAll(Channels, c => c.label));
        if (next != channel)
        {
            channel = next;
            preview = null;
        }

        EnsurePreview();

        var info = System.Array.Find(Channels, c => c.channel == channel);
        EditorGUILayout.HelpBox(info.expected, MessageType.None);

        float available = Mathf.Min(position.width - 12f, position.height - ChromeHeight);
        var rect = GUILayoutUtility.GetRect(available, available, GUILayout.ExpandWidth(false));
        rect.x = (position.width - available) * 0.5f;

        // Point filtering: displays actual texels. As long as display is close to 1:1,
        // this is correct; bilinear smoothing conceals the noise we are diagnosing.
        EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);

        EditorGUILayout.LabelField(
            $"{maps.width}² map in {available:F0} px area " +
            (available >= maps.width ? "(1:1 or enlarged)" : $"({available / maps.width * 100f:F0}% reduced)"),
            EditorStyles.miniLabel);
    }

    void Bake()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("No terrain in scene.");
            return;
        }

        // Accumulation weight bakes based on prevailing wind direction from settings asset.
        var wind = AssetDatabase.LoadAssetAtPath<WindSettings>("Assets/Settings/WindSettings.asset");
        if (wind == null)
        {
            Debug.LogWarning("WindSettings missing; scene setup must run first.");
            return;
        }

        EditorUtility.DisplayProgressBar("Surface Maps", "Calculating flow accumulation...", 0.5f);
        try { maps = SurfaceMapBaker.Bake(terrain, wind.prevailingDegrees); }
        finally { EditorUtility.ClearProgressBar(); }

        preview = null;
    }

    void Reload()
    {
        maps = SurfaceMapBaker.Load();
        preview = null;
    }

    /// Expands selected channel into grayscale. Isolating a single channel visually from color texture is difficult;
    /// we are inspecting the pattern distribution itself, not color.
    void EnsurePreview()
    {
        if (preview != null) return;

        int size = maps.width;
        var source = maps.GetPixels32();
        var gray = new Color32[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            byte value = channel switch
            {
                Channel.Accumulation => source[i].r,
                Channel.Concavity => source[i].g,
                _ => source[i].b,
            };

            gray[i] = new Color32(value, value, value, 255);
        }

        preview = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };

        preview.SetPixels32(gray);
        preview.Apply(false);
    }
}
