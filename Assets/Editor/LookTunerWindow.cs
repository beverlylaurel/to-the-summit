using UnityEditor;
using UnityEngine;

/// Hava ve saat kombinasyonlarını canlı önizleyerek renk düzenlemesi ayarlama penceresi.
public class LookTunerWindow : EditorWindow
{
    LookController controller;
    SerializedObject serializedLook;
    Vector2 scroll;

    bool preview = true;
    float storm = 0.8f;
    float day = 0.6f;

    [MenuItem("To The Summit/Görünüm Ayarları", false, 100)]
    static void Open() => GetWindow<LookTunerWindow>("Görünüm").minSize = new Vector2(340f, 420f);

    void OnEnable() => Acquire();

    void OnDisable()
    {
        // Pencere kapanınca sahne gerçek hava ve saate dönsün
        if (controller != null) controller.SetPreview(false, storm, day);
    }

    void Acquire()
    {
        controller = Object.FindAnyObjectByType<LookController>();
        serializedLook = controller != null && controller.Look != null
            ? new SerializedObject(controller.Look)
            : null;
    }

    void OnGUI()
    {
        if (controller == null || serializedLook == null)
        {
            EditorGUILayout.HelpBox("Sahnede görünüm denetleyicisi yok. Unity'ye odaklanıp derlemeyi bekle.",
                MessageType.Warning);
            if (GUILayout.Button("Tekrar ara")) Acquire();
            return;
        }

        EditorGUILayout.LabelField("Önizleme", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        preview = EditorGUILayout.Toggle("Önizlemeyi kullan", preview);
        storm = EditorGUILayout.Slider("Fırtına", storm, 0f, 1f);
        day = EditorGUILayout.Slider("Gündüz", day, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
            controller.SetPreview(preview, storm, day);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Açık gündüz")) SetPreview(0f, 1f);
            if (GUILayout.Button("Açık gece")) SetPreview(0f, 0f);
            if (GUILayout.Button("Fırtına gündüz")) SetPreview(1f, 1f);
            if (GUILayout.Button("Fırtına gece")) SetPreview(1f, 0f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dört köşe — ara değerler harmanlanır", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        serializedLook.Update();

        EditorGUI.BeginChangeCheck();

        var property = serializedLook.GetIterator();
        property.NextVisible(true);
        while (property.NextVisible(false))
            EditorGUILayout.PropertyField(property, true);

        if (EditorGUI.EndChangeCheck())
        {
            serializedLook.ApplyModifiedProperties();
            controller.SetPreview(preview, storm, day);
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndScrollView();
    }

    void SetPreview(float stormValue, float dayValue)
    {
        preview = true;
        storm = stormValue;
        day = dayValue;
        controller.SetPreview(true, storm, day);
        SceneView.RepaintAll();
    }
}
