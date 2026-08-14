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
    /// ADI UNITY'NİNKİYLE ÇAKIŞAMAZ. Yol `Logs/editor.log` idi; Unity kendi logunu aynı
    /// klasöre `Logs/Editor.log` diye yazıyor ve Windows dosya adlarında büyük-küçük harf
    /// ayrımı YOK — ikisi tek dosyaydı. Sonuçları: araç çıktısı Unity'nin logunun içine
    /// karışıyordu, aşağıdaki 512 KB kesme Unity'nin logunu kırpıyordu ve Unity kendi
    /// eski konumundan yazmaya devam ettiği için araya sıfır bayt dolgusu giriyordu
    /// (bir kez 23 MB'lık NUL dosyası buradan çıktı, sebebi o zaman anlaşılmamıştı).
    const string LogPath = "Logs/tools.log";

    /// Dosya bu boyutu aşınca baştan yazılıyor. Sınırsız büyüseydi okunamaz olurdu;
    /// yarım megabayt son birkaç yüz kaydı tutuyor.
    const long MaxBytes = 512 * 1024;

    public static void Write(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}";

        // AKIŞ ELLE AÇILIYOR, iki sebeple. Birincisi paylaşım: `File.AppendAllText`
        // dosyayı okumaya kapalı açıyor ve log okunurken yazma "sharing violation" ile
        // düşüp kurulumu kesiyordu. İkincisi kesme: bir çağrıda `Create`, diğerinde
        // `Append` kullanılınca Windows aradaki boşluğu sıfır baytla dolduruyor ve dosya
        // yirmi üç megabayta şişti — burada uzunluk sıfırlanıp sona konumlanılıyor.
        using var stream = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length > MaxBytes) stream.SetLength(0);
        stream.Seek(0, SeekOrigin.End);

        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(line);
    }
}
