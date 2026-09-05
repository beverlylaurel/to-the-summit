using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class EnvironmentValidationWindow : EditorWindow
{
    readonly Dictionary<string, bool> selected = new();
    Vector2 scroll;

    [MenuItem("To The Summit/Validation/Environment Scenarios", false, 10)]
    static void Open() => GetWindow<EnvironmentValidationWindow>("Environment Validation");

    void OnEnable()
    {
        foreach (EnvironmentValidationScenario scenario in EnvironmentValidationCatalog.All)
            if (!selected.ContainsKey(scenario.id)) selected[scenario.id] = true;
        EditorApplication.update += Repaint;
    }

    void OnDisable() => EditorApplication.update -= Repaint;

    void OnGUI()
    {
        EditorGUILayout.LabelField("Deterministik Çevre Senaryoları", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Her senaryo Play Mode'da saati, havayı, rüzgârı, deniz zamanını ve swash fazını " +
            "kilitler. Sahne kaydedilmez. PNG kareleri ve ölçümler Temp/Validation altında raporlanır.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Durum", EnvironmentValidationRunner.Status);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (EnvironmentValidationScenario scenario in EnvironmentValidationCatalog.All)
            selected[scenario.id] = EditorGUILayout.ToggleLeft(scenario.title, selected[scenario.id]);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EnvironmentValidationRunner.IsRunning
                                            || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seçilenleri Çalıştır")) RunSelected();
                if (GUILayout.Button("Tümünü Çalıştır")) EnvironmentValidationRunner.RunAll();
            }
        }

        if (EnvironmentValidationRunner.IsRunning
            && GUILayout.Button("Koşuyu İptal Et"))
            EnvironmentValidationRunner.Cancel();

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(
                   EnvironmentValidationRunner.LastReportPath)))
        {
            if (GUILayout.Button("Son Raporu Göster"))
                EnvironmentValidationRunner.RevealLastReport();
        }
    }

    void RunSelected()
    {
        var ids = new List<string>();
        foreach (EnvironmentValidationScenario scenario in EnvironmentValidationCatalog.All)
            if (selected.TryGetValue(scenario.id, out bool include) && include)
                ids.Add(scenario.id);
        EnvironmentValidationRunner.Start(ids);
    }
}
