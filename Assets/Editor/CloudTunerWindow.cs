using UnityEditor;
using UnityEngine;

/// Bulut ve atmosfer ayarlarını canlı düzenleme penceresi.
/// AtmosphereController ExecuteAlways olduğu için değişiklikler edit modunda da anında görünür.
public class CloudTunerWindow : EditorWindow
{
    AtmosphereController atmosphere;
    TimeOfDay time;
    SerializedObject serialized;
    Vector2 scroll;

    [MenuItem("To The Summit/Hava/Bulut Ayarları", false, 80)]
    static void Open() => GetWindow<CloudTunerWindow>("Bulut").minSize = new Vector2(380f, 500f);

    void OnEnable() => Acquire();

    void Acquire()
    {
        atmosphere = Object.FindAnyObjectByType<AtmosphereController>();
        time = Object.FindAnyObjectByType<TimeOfDay>();
        serialized = null;

        if (atmosphere == null) return;

        // Ayarlar bileşende değil asset'te. Yolu burada tekrar yazmak yerine bileşenin
        // tuttuğu referans okunuyor: sahnedeki denetleyici hangi asset'i kullanıyorsa
        // pencere de onu düzenler, ikisi ayrışamaz.
        var link = new SerializedObject(atmosphere).FindProperty("settings");
        if (link?.objectReferenceValue != null)
            serialized = new SerializedObject(link.objectReferenceValue);
    }

    void OnGUI()
    {
        if (atmosphere == null || serialized == null)
        {
            EditorGUILayout.HelpBox("Sahnede atmosfer denetleyicisi yok. Unity'ye odaklanıp derlemeyi bekle.",
                MessageType.Warning);
            if (GUILayout.Button("Tekrar ara")) Acquire();
            return;
        }

        DrawQuickControls();

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        serialized.Update();
        EditorGUI.BeginChangeCheck();

        DrawAllProperties();

        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndScrollView();
    }

    /// Işıklandırmayı farklı saatlerde denemek için: bulut ayarı saate çok bağlı
    void DrawQuickControls()
    {
        if (time == null) return;

        EditorGUILayout.LabelField("Saat", EditorStyles.boldLabel);

        float value = time.Normalized;
        float next = EditorGUILayout.Slider(value, 0f, 1f);
        if (!Mathf.Approximately(next, value)) time.SetNormalized(next);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Şafak")) time.SetNormalized(0.25f);
            if (GUILayout.Button("Öğle")) time.SetNormalized(0.5f);
            if (GUILayout.Button("Batım")) time.SetNormalized(0.75f);
            if (GUILayout.Button("Gece")) time.SetNormalized(0f);
        }

        EditorGUILayout.LabelField($"Kapsama %{atmosphere.Coverage * 100f:F0}   " +
                                   $"görüş {atmosphere.Visibility:F0} m", EditorStyles.miniLabel);
    }

    /// Asset yalnızca ayar tuttuğu için filtreye gerek kalmadı; bağımlılıklar bileşende.
    void DrawAllProperties()
    {
        var property = serialized.GetIterator();
        property.NextVisible(true);

        while (property.NextVisible(false))
            EditorGUILayout.PropertyField(property, true);
    }
}
