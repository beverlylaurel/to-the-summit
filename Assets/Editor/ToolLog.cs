using System;
using System.IO;
using System.Text;

/// ARAÇ ÇIKTISI DOSYAYA GİDER, KONSOLA DEĞİL. Menüden çalışan araçlar (kurulum, ölçüm,
/// föy, fırça) sonuçlarını uzun tablolar hâlinde basıyor; konsola gitseydi gerçek hata
/// ve uyarılar o tabloların arasında kaybolurdu.
///
/// Konsolda yalnız `Debug.LogError` ve `Debug.LogWarning` kalıyor: onlar bakılması
/// gereken şeyler. Bilgi kaydı burada.
public static class ToolLog
{
    const string LogPath = "Logs/editor.log";

    /// Dosya bu boyutu aşınca baştan yazılıyor. Sınırsız büyüseydi okunamaz olurdu.
    const long MaxBytes = 512 * 1024;

    public static void Write(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes)
            File.Delete(LogPath);

        File.AppendAllText(LogPath,
            $"[{DateTime.Now:HH:mm:ss}] {message}\n\n", Encoding.UTF8);
    }
}
