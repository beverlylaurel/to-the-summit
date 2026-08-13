using System;
using System.IO;
using System.Text;

/// ARAÇ ÇIKTISI DOSYAYA GİDER, KONSOLA DEĞİL. Editördeki bütün araçlar — sahne kurulumu,
/// arazi üretimi, doku pişirme, ölçüm, fırça — sonuçlarını uzun tablolar hâlinde basıyor.
/// Konsola gitseydi gerçek hata ve uyarılar o tabloların arasında kaybolurdu.
///
/// Yeni araç yazarken kural: bilgi `ToolLog.Write`, sorun `Debug.LogWarning` ya da
/// `Debug.LogError`. Konsolda görünen her satır bakılması gereken bir şey olmalı.
///
/// DOSYA PAYLAŞIMLI AÇILIYOR. Log okunurken (editör, terminal, başka bir araç) dosya
/// kilitli oluyor; ilk sürüm döndürme için `File.Delete` çağırıyordu ve kilitli dosyada
/// `IOException` fırlatıp kurulumu yarıda kesti. Silmek yerine kesiliyor ve okuyucuya
/// izin veriliyor.
public static class ToolLog
{
    const string LogPath = "Logs/editor.log";

    /// Dosya bu boyutu aşınca baştan yazılıyor. Sınırsız büyüseydi okunamaz olurdu;
    /// yarım megabayt son birkaç yüz kaydı tutuyor.
    const long MaxBytes = 512 * 1024;

    public static void Write(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        bool full = File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes;
        FileMode mode = full ? FileMode.Create : FileMode.Append;

        using var stream = new FileStream(LogPath, mode, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        writer.Write($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
    }
}
