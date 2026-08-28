using System;
using System.IO;
using System.Text;

/// TOOL OUTPUT DIRECTED TO FILE, NOT CONSOLE. All editor tools — scene bootstrap,
/// terrain generation, texture baking, measurement, brushes — produce verbose tables.
/// Outputting to console would bury real errors and warnings among those tables.
///
/// Rule when writing new tools: informative output via `ToolLog.Write`, issues via
/// `Debug.LogWarning` or `Debug.LogError`. Every console line must require attention.
///
/// FILE OPENED IN SHARED MODE. While logs are read (editor, terminal, external tool),
/// file is locked; earlier versions called `File.Delete` for truncation, throwing `IOException`
/// and interrupting setup. Truncation is used instead of deletion, granting reader permissions.
public static class ToolLog
{
    /// CANNOT CONFLICT WITH UNITY LOG NAME. Path was `Logs/editor.log`; Unity writes its own
    /// log to `Logs/Editor.log` in the same directory, and Windows filesystem is CASE-INSENSITIVE —
    /// colliding into a single file. Tool output mixed into Unity log, 512 KB truncation
    /// cropped Unity log, and Unity continued writing at its previous file offset inserting zero-byte padding.
    const string LogPath = "Logs/tools.log";

    /// File truncated when exceeding this size. Unbounded growth impairs readability;
    /// half a megabyte retains recent hundreds of entries.
    const long MaxBytes = 512 * 1024;

    public static void Write(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}";

        // Stream opened manually for sharing and truncation control:
        // `File.AppendAllText` opens without read sharing, failing with sharing violation during active reading.
        // Truncating resets stream length and seeks to end.
        using var stream = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length > MaxBytes) stream.SetLength(0);
        stream.Seek(0, SeekOrigin.End);

        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(line);
    }
}
