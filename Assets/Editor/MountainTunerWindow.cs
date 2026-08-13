using UnityEditor;
using UnityEngine;

/// Dağ parametrelerini canlı önizlemeyle ayarlama penceresi.
/// Önizleme düşük çözünürlükte üretilir; tam çözünürlük ayrı düğmeyle.
public class MountainTunerWindow : EditorWindow
{
    static readonly int[] PreviewResolutions = { 513, 1025, 2049 };
    static readonly string[] PreviewLabels = { "513 (anlık)", "1025 (~2 sn)", "2049 (~15 sn)" };

    static readonly string[] BandNames = { "Etek", "Alt yamaç", "Üst yamaç", "Zirve" };
    static readonly float[,] BandTargets =
    {
        { 75f, 20f,  5f,  0f },
        { 55f, 30f, 14f,  1f },
        { 35f, 35f, 27f,  3f },
        { 15f, 30f, 45f, 10f }
    };

    const float PreviewDelay = 0.25f;

    MountainGenerator generator;
    SerializedObject serializedSettings;
    Vector2 scroll;

    int previewIndex;
    bool autoPreview = true;
    double regenerateAt = -1;
    double lastPreviewSeconds;

    [MenuItem("To The Summit/Arazi/Dağ Ayarları", false, 21)]
    static void Open() => GetWindow<MountainTunerWindow>("Dağ Ayarı").minSize = new Vector2(360f, 480f);

    void OnEnable()
    {
        EditorApplication.update += OnUpdate;
        Acquire();
    }

    void OnDisable() => EditorApplication.update -= OnUpdate;

    void Acquire()
    {
        generator = Object.FindAnyObjectByType<MountainGenerator>();
        serializedSettings = generator != null && generator.Settings != null
            ? new SerializedObject(generator.Settings)
            : null;
    }

    void OnUpdate()
    {
        if (regenerateAt < 0 || EditorApplication.timeSinceStartup < regenerateAt) return;

        regenerateAt = -1;
        Regenerate(PreviewResolutions[previewIndex]);
    }

    void OnGUI()
    {
        if (generator == null || serializedSettings == null)
        {
            EditorGUILayout.HelpBox("Sahnede dağ bulunamadı. Unity'ye odaklanıp derlemeyi bekle.", MessageType.Warning);
            if (GUILayout.Button("Tekrar ara")) Acquire();
            return;
        }

        DrawToolbar();
        EditorGUILayout.Space();
        DrawHistogram();
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        serializedSettings.Update();

        EditorGUI.BeginChangeCheck();
        DrawSettingsProperties();

        if (EditorGUI.EndChangeCheck())
        {
            serializedSettings.ApplyModifiedProperties();
            if (autoPreview) regenerateAt = EditorApplication.timeSinceStartup + PreviewDelay;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            previewIndex = EditorGUILayout.Popup(previewIndex, PreviewLabels);
            autoPreview = GUILayout.Toggle(autoPreview, "Canlı", EditorStyles.miniButton, GUILayout.Width(50f));

            if (GUILayout.Button("Üret", EditorStyles.miniButton, GUILayout.Width(50f)))
                Regenerate(PreviewResolutions[previewIndex]);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rastgele seed"))
                RandomizeSeed();

            if (GUILayout.Button("Rastgele tümü"))
                RandomizeAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Eğriyi düzelt"))
                SmoothProfile();

            if (GUILayout.Button("Eğriyi sıfırla"))
                ResetProfile();

            if (GUILayout.Button($"Tam çözünürlükte üret ({generator.Settings.heightmapResolution})"))
                Regenerate(generator.Settings.heightmapResolution);
        }

        if (lastPreviewSeconds > 0)
            EditorGUILayout.LabelField($"Son üretim: {lastPreviewSeconds:F1} sn", EditorStyles.miniLabel);
    }

    void DrawHistogram()
    {
        EditorGUILayout.LabelField("Eğim dağılımı — üstte ölçüm, altta hedef", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Kuşak", GUILayout.Width(70f));
            EditorGUILayout.LabelField("Yürü", GUILayout.Width(50f));
            EditorGUILayout.LabelField("Zorlu", GUILayout.Width(50f));
            EditorGUILayout.LabelField("Tırman", GUILayout.Width(50f));
            EditorGUILayout.LabelField("Duvar", GUILayout.Width(50f));
            EditorGUILayout.LabelField("Ort", GUILayout.Width(45f));
        }

        for (int i = 0; i < MountainGenerator.AltitudeBandCount; i++)
        {
            var band = generator.bands[i];

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(BandNames[i], GUILayout.Width(70f));
                DrawValue(band.walkable, BandTargets[i, 0]);
                DrawValue(band.strenuous, BandTargets[i, 1]);
                DrawValue(band.climbable, BandTargets[i, 2]);
                DrawValue(band.wall, BandTargets[i, 3]);
                EditorGUILayout.LabelField($"{band.meanDegrees:F0}°", GUILayout.Width(45f));
            }
        }

        EditorGUILayout.LabelField($"Ortalama eğim {generator.meanSlopeDegrees:F1}°   " +
                                   $"örnek aralığı {generator.Settings.terrainSize / (PreviewResolutions[previewIndex] - 1f):F1} m   " +
                                   $"oktav {generator.EffectiveOctaves}",
            EditorStyles.miniLabel);
    }

    /// Hedeften sapma büyüdükçe renk kızarır
    static void DrawValue(float value, float target)
    {
        float error = Mathf.Abs(value - target) / Mathf.Max(8f, target);
        var color = Color.Lerp(new Color(0.55f, 0.85f, 0.55f), new Color(0.95f, 0.5f, 0.45f), Mathf.Clamp01(error));

        var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
        EditorGUILayout.LabelField($"{value:F0} / {target:F0}", style, GUILayout.Width(50f));
    }

    void DrawSettingsProperties()
    {
        var property = serializedSettings.GetIterator();
        property.NextVisible(true);

        while (property.NextVisible(false))
            EditorGUILayout.PropertyField(property, true);
    }

    /// Aynı formu koruyup farklı bir dağ üretir
    void RandomizeSeed()
    {
        Undo.RecordObject(generator.Settings, "Rastgele seed");
        generator.Settings.seed = Random.Range(1, 100000);
        Commit();
    }

    /// Formun tamamını makul aralıklarda rastgeleler; boyut ve yükseklik korunur
    void RandomizeAll()
    {
        Undo.RecordObject(generator.Settings, "Rastgele tümü");
        generator.Settings.Randomize(new System.Random(Random.Range(1, int.MaxValue)));
        Commit();
    }

    /// Eğriyi varsayılan dağ silüetine döndürür
    void ResetProfile()
    {
        Undo.RecordObject(generator.Settings, "Eğriyi sıfırla");
        generator.Settings.heightProfile = MountainSettings.DefaultProfile();
        Commit();
    }

    /// Teğetleri Auto moduna alır. Sabit teğetli bir noktayı sürüklemek eğriyi
    /// iki nokta arasında şişirir; Auto modda teğet konuma göre yeniden hesaplanır.
    void SmoothProfile()
    {
        var curve = generator.Settings.heightProfile;
        Undo.RecordObject(generator.Settings, "Eğriyi düzelt");

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyBroken(curve, i, false);
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }

        Commit();
    }

    void Commit()
    {
        EditorUtility.SetDirty(generator.Settings);
        serializedSettings.Update();
        Regenerate(PreviewResolutions[previewIndex]);
    }

    void Regenerate(int resolution)
    {
        double start = EditorApplication.timeSinceStartup;

        generator.Generate(resolution);
        EditorUtility.SetDirty(generator);
        EditorUtility.SetDirty(generator.GetComponent<Terrain>().terrainData);

        lastPreviewSeconds = EditorApplication.timeSinceStartup - start;
        Repaint();
        SceneView.RepaintAll();
    }
}
