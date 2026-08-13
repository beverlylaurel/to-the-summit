using UnityEngine;

/// BİRİKİNTİ ALANININ CPU İKİZİ. `Assets/Shaders/SnowDrift.hlsl` ile bit birebir aynı
/// sayıyı üretmek zorunda: görsel yüzey GPU'da, çarpışma yüzeyi burada hesaplanıyor.
/// İki taraf ayrışırsa belirti "kar var ama içinden geçiyorum" olur ve ayrıştığı yer
/// gözle bulunamaz.
///
/// Karma TAM SAYI aritmetiğiyle — sin tabanlı karma platformdan platforma, hatta
/// derleyiciden derleyiciye kayıyor. Tam sayı karması her yerde aynı.
///
/// ÇİFT YAZIM BİLİNÇLİ. Tek kaynaktan üretmek (compute shader'dan geri okuma ya da
/// C#'tan HLSL üretmek) her kare senkron bekleme ya da derleme adımı getiriyor;
/// fonksiyon otuz satır ve iki dosya yan yana duruyor. Biri değişirse ÖTEKİ DE
/// DEĞİŞİR — ayrışma probu (F1) bunu ölçüyor.
public static class SnowDriftField
{
    /// Wang karması. `SnowDriftHash` ile aynı.
    static float Hash(uint x, uint y)
    {
        uint h = x * 73856093u ^ y * 19349663u;
        h = (h ^ 61u) ^ (h >> 16);
        h *= 9u;
        h = h ^ (h >> 4);
        h *= 0x27d4eb2du;
        h = h ^ (h >> 15);
        return (h & 0x00ffffffu) / 16777216f;
    }

    /// Değer gürültüsü. `SnowDriftNoise` ile aynı.
    static float Noise(float px, float py)
    {
        float cx = Mathf.Floor(px);
        float cy = Mathf.Floor(py);

        float fx = px - cx;
        float fy = py - cy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        // Kaydırma negatif koordinatlar için: işaretsiz karmaya negatif tam sayı
        // giremez ve iki taraf aynı kaydırmayı kullanmak zorunda.
        uint ix = (uint)((int)cx + 4096);
        uint iy = (uint)((int)cy + 4096);

        float a = Hash(ix, iy);
        float b = Hash(ix + 1u, iy);
        float c = Hash(ix, iy + 1u);
        float d = Hash(ix + 1u, iy + 1u);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// Birikinti şekli, 0-1. `SnowDriftShape` ile aynı. Konkavlık GİRDİ DEĞİL —
    /// gerekçe HLSL tarafında yazılı.
    public static float Shape(Vector2 worldXZ, Vector2 windAxis)
    {
        float alignedX = worldXZ.x * windAxis.x + worldXZ.y * windAxis.y;
        float alignedY = worldXZ.x * -windAxis.y + worldXZ.y * windAxis.x;

        float warpX = Noise(alignedX / 62f, alignedY / 62f) - 0.5f;
        float warpY = Noise(alignedX / 62f + 37.7f, alignedY / 62f + 37.7f) - 0.5f;

        alignedX += warpX * 24f;
        alignedY += warpY * 24f;

        float shape = Noise(alignedX / 45f, alignedY / 16f);

        float secondX = (0.857f * alignedX - 0.515f * alignedY) / 19f;
        float secondY = (0.515f * alignedX + 0.857f * alignedY) / 11f;
        shape = shape * 0.68f + Noise(secondX, secondY) * 0.32f;

        return Mathf.Clamp01(shape);
    }
}
