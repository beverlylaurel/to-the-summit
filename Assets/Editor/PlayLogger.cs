using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Play sırasındaki hata ve uyarıları proje içindeki tek bir dosyaya yazar.
/// Amaç: konsol içeriğini elle aktarmaya gerek kalmadan sorunu görebilmek.
[InitializeOnLoad]
public static class PlayLogger
{
    const string LogPath = "Logs/play.log";
    const int MaxUniqueEntries = 200;

    static readonly Dictionary<string, int> repeats = new();
    static readonly List<string> order = new();
    static bool listening;

    static PlayLogger()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) Begin();
        else if (change == PlayModeStateChange.ExitingPlayMode) End();
    }

    static void Begin()
    {
        repeats.Clear();
        order.Clear();

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.WriteAllText(LogPath, Header(), Encoding.UTF8);

        if (!listening)
        {
            Application.logMessageReceived += OnLog;
            listening = true;
        }
    }

    static void End()
    {
        if (listening)
        {
            Application.logMessageReceived -= OnLog;
            listening = false;
        }

        Append(Summary());
    }

    static string Header()
    {
        var scene = SceneManager.GetActiveScene();
        var builder = new StringBuilder();

        builder.AppendLine($"# Play {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Unity {Application.unityVersion}   Sahne: {scene.name}");
        builder.AppendLine();
        builder.AppendLine("## Sahne");

        foreach (var root in scene.GetRootGameObjects())
            builder.AppendLine($"- {root.name}{Children(root.transform)}");

        builder.AppendLine();
        builder.AppendLine("## Hata ve uyarılar");
        return builder.ToString();
    }

    static string Children(Transform parent)
    {
        if (parent.childCount == 0) return "";

        var names = new string[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
            names[i] = parent.GetChild(i).name;

        return $" ({string.Join(", ", names)})";
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Log) return;

        string key = $"{type}|{message}";

        if (repeats.TryGetValue(key, out int count))
        {
            repeats[key] = count + 1;
            return;
        }

        if (order.Count >= MaxUniqueEntries) return;

        repeats[key] = 1;
        order.Add(key);

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"[{type}] {message}");

        if (type != LogType.Warning && !string.IsNullOrEmpty(stackTrace))
            builder.Append(stackTrace);

        Append(builder.ToString());
    }

    static string Summary()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("## Özet");

        if (order.Count == 0)
        {
            builder.AppendLine("Hata veya uyarı yok.");
            return builder.ToString();
        }

        foreach (var key in order)
        {
            int count = repeats[key];
            if (count > 1)
                builder.AppendLine($"{count} kez: {key.Replace('|', ' ')}");
        }

        builder.AppendLine($"Toplam {order.Count} farklı kayıt.");
        return builder.ToString();
    }

    static void Append(string text) => File.AppendAllText(LogPath, text, Encoding.UTF8);
}
