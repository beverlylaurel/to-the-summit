using UnityEditor;
using UnityEngine;

/// Enforces import settings for audio assets under Assets/Audio.
/// Ambiances stream from disk (not decompressed into memory), while thunder clips stay in memory for instant playback.
public class AudioImportSettings : AssetPostprocessor
{
    const string AudioRoot = "Assets/Audio/";
    const string ThunderFolder = "Assets/Audio/Thunder/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioRoot)) return;

        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;

        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        settings.loadType = assetPath.StartsWith(ThunderFolder)
            ? AudioClipLoadType.CompressedInMemory
            : AudioClipLoadType.Streaming;

        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = true;
    }
}
