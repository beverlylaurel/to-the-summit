using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Disk-backed photo index. Only the currently viewed JPEG is decoded, so a full card does
/// not turn into hundreds of resident 10 MP textures.
public sealed class VintagePhotoLibrary
{
    readonly List<string> files = new();

    public string DirectoryPath { get; }
    public int Count => files.Count;
    public IReadOnlyList<string> Files => files;

    public VintagePhotoLibrary()
    {
        DirectoryPath = Path.Combine(Application.persistentDataPath, "Photos");
        Directory.CreateDirectory(DirectoryPath);
        Refresh();
    }

    public void Refresh()
    {
        files.Clear();
        files.AddRange(Directory.GetFiles(DirectoryPath, "*.jpg", SearchOption.TopDirectoryOnly));
        files.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public string NewJpegPath()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        return Path.Combine(DirectoryPath, $"TTS_{stamp}.jpg");
    }

    public void Register(string path)
    {
        if (!files.Contains(path)) files.Add(path);
        files.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public Texture2D Load(int index)
    {
        if (index < 0 || index >= files.Count || !File.Exists(files[index])) return null;

        byte[] bytes = File.ReadAllBytes(files[index]);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = Path.GetFileNameWithoutExtension(files[index]),
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        if (ImageConversion.LoadImage(texture, bytes, false)) return texture;
        UnityEngine.Object.Destroy(texture);
        return null;
    }
}
