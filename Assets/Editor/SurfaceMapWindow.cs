using UnityEditor;
using UnityEngine;

/// Yüzey haritalarını kanal kanal gösterir. Materyal bu haritalardan besleniyor;
/// üreteni doğrulamadan tüketeni yazmak, hatayı iki kat zor bulunur hale getiriyor.
public class SurfaceMapWindow : EditorWindow
{
    enum Channel { Accumulation, Concavity, Exposure }

    static readonly (Channel channel, string label, string expected)[] Channels =
    {
        (Channel.Accumulation, "Birikim",
            "Dere ve oluk ağı görünmeli: yukarıdan aşağı dallanan, birleşerek kalınlaşan " +
            "çizgiler. Tuz-biber görünüyorsa akış hesabı bozuk. Çakıl buradan sürülüyor."),
        (Channel.Concavity, "Konkavlık",
            "Vadi tabanları ve oyuklar açık, sırtlar koyu olmalı. Liken nemi buradan okuyor."),
        (Channel.Exposure, "Gökyüzü maruziyeti",
            "Sırtlar ve düzlükler beyaza yakın, vadi dipleri ve yarıklar koyu olmalı. " +
            "Hem liken hem yüzey gölgelenmesi buradan besleniyor."),
    };

    Texture2D maps;
    Texture2D preview;
    Channel channel = Channel.Accumulation;

    // Toolbar, açıklama kutusu ve kenar boşlukları için görüntünün üstünde kalan yer
    const float ChromeHeight = 110f;

    [MenuItem("To The Summit/Terrain/Surface Maps", false, 22)]
    static void Open()
    {
        var window = GetWindow<SurfaceMapWindow>("Yüzey Haritaları");
        window.minSize = new Vector2(420f, 560f);

        // Harita 1024²; daha küçük bir alana sıkıştırılınca önizlemenin kendi
        // aliasing'i beneği taklit ediyor ve neyin gerçek gürültü olduğu anlaşılmıyor.
        // Pencere ekrana sığan en büyük boyutta açılır, görüntü 1:1'e yakın kalır.
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
            if (GUILayout.Button("Haritaları hesapla")) Bake();

            using (new EditorGUI.DisabledScope(maps == null))
                if (GUILayout.Button("Yeniden yükle")) Reload();
        }

        if (maps == null)
        {
            EditorGUILayout.HelpBox(
                "Yüzey haritası yok. Sahnede terrain varken 'Haritaları hesapla'ya bas.",
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

        // Nokta filtreleme: gerçek pikselleri gösterir. Görüntü 1:1'e yakın olduğu
        // sürece doğru olan bu; yumuşatma tam da aradığımız gürültüyü gizler.
        EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);

        EditorGUILayout.LabelField(
            $"{maps.width}² harita, {available:F0} piksellik alanda " +
            (available >= maps.width ? "(1:1 veya büyütülmüş)" : $"(%{available / maps.width * 100f:F0} küçültülmüş)"),
            EditorStyles.miniLabel);
    }

    void Bake()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("Sahnede terrain yok.");
            return;
        }

        // Birikim ağırlığı hâkim rüzgâr yönüne göre pişiyor; yön ayar asset'inde.
        var wind = AssetDatabase.LoadAssetAtPath<WindSettings>("Assets/Settings/WindSettings.asset");
        if (wind == null)
        {
            Debug.LogWarning("WindSettings yok; önce sahne kurulumu çalışmalı.");
            return;
        }

        EditorUtility.DisplayProgressBar("Yüzey haritaları", "Akış birikimi hesaplanıyor...", 0.5f);
        try { maps = SurfaceMapBaker.Bake(terrain, wind.prevailingDegrees); }
        finally { EditorUtility.ClearProgressBar(); }

        preview = null;
    }

    void Reload()
    {
        maps = SurfaceMapBaker.Load();
        preview = null;
    }

    /// Seçili kanalı gri tonlamaya açar. Tek kanalı renkli dokudan gözle ayırmak zor;
    /// aradığımız şey desenin kendisi, rengi değil.
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
