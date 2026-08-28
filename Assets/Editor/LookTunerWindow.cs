using UnityEditor;
using UnityEngine;

/// Window for tuning color grading with live preview across weather and time-of-day combinations.
public class LookTunerWindow : EditorWindow
{
    LookController controller;
    SerializedObject serializedLook;
    Vector2 scroll;

    bool preview = true;
    float storm = 0.8f;
    float day = 0.6f;

    [MenuItem("To The Summit/Look Settings", false, 100)]
    static void Open() => GetWindow<LookTunerWindow>("Look").minSize = new Vector2(340f, 420f);

    void OnEnable() => Acquire();

    void OnDisable()
    {
        // Revert scene to actual weather and time of day when window closes
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
            EditorGUILayout.HelpBox("No look controller in scene. Focus Unity and wait for compilation.",
                MessageType.Warning);
            if (GUILayout.Button("Search Again")) Acquire();
            return;
        }

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        preview = EditorGUILayout.Toggle("Use Preview", preview);
        storm = EditorGUILayout.Slider("Storm", storm, 0f, 1f);
        day = EditorGUILayout.Slider("Day", day, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
            controller.SetPreview(preview, storm, day);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Day")) SetPreview(0f, 1f);
            if (GUILayout.Button("Clear Night")) SetPreview(0f, 0f);
            if (GUILayout.Button("Storm Day")) SetPreview(1f, 1f);
            if (GUILayout.Button("Storm Night")) SetPreview(1f, 0f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Four Corners — intermediate values blended", EditorStyles.boldLabel);

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
