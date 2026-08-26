// ROL: kar yüzeyinin yüksekliğini CPU'da verir. `SnowRelief.hlsl` içindeki
// `SnowYuzeyRolyef`'in birebir ikizi.
// Çağıran: GroundSnap (karakteri yüzeye oturtur).

using UnityEngine;

/// GÖRSEL VE FİZİK AYNI YÜZEYİ GÖRMEK ZORUNDA.
///
/// Kar yüksekliği bir kez geometriye konmuş ve fizik tarafında karşılığı
/// olmadığı için geri alınmıştı: "ayak 205.539, kaya 205.489, çizilen yüzey
/// 205.98 — karakter yarım metre gömülü başlıyordu" (`MountainSurface.shader`
/// yorumu). Bu sınıf o boşluğu kapatıyor.
///
/// DUBLİKASYON BİLİNÇLİ VE SINANIYOR. Aynı formül iki dilde iki kez
/// yazılıyor; sapma `SnowHeightParityTest` ile yakalanıyor. Alternatifler
/// daha kötü: GPU'dan async geri okuma bir kare gecikmeli (karakter geçen
/// karenin yüzeyinde durur), senkron okuma boru hattını durdurup kare
/// süresini patlatır.
///
/// CO-OP: fonksiyon SAF. Girdisi yalnız dünya konumu, kar derinliği, rüzgâr
/// yönü ve maruziyet; kare sayacı, `Time` ve yerel rastgelelik YOK. Bu yüzden
/// her istemci aynı XZ'de aynı yüksekliği hesaplıyor ve ağ üzerinden yükseklik
/// paylaşmak gerekmiyor. Kural `COOP.md`'de yazılı ve bozulamaz.
///
/// TEŞHİS ANAHTARLARI OKUNMUYOR. `_SnowDbgNoFbm` ve kardeşleri yalnız görsel
/// teşhis içindir; fizik onları görseydi anahtarı açan oyuncu zeminin içine
/// düşerdi.
public static class SnowSurfaceHeight
{
    // --- PCG3D hash: `SnowCommon.hlsl` → `SnowPcg3d` ikizi ---
    //
    // [KAYNAK: Jarzynski & Olano, JCGT 2020, "Hash Functions for GPU
    // Rendering".] `frac(sin(dot(p,k)))` büyük girdide çöküyor; tam sayı
    // hash'in o sınırı yok.
    static void Pcg3d(ref uint x, ref uint y, ref uint z)
    {
        unchecked
        {
            x = x * 1664525u + 1013904223u;
            y = y * 1664525u + 1013904223u;
            z = z * 1664525u + 1013904223u;

            x += y * z; y += z * x; z += x * y;

            x ^= x >> 16; y ^= y >> 16; z ^= z >> 16;

            x += y * z; y += z * x; z += x * y;
        }
    }

    /// `SnowRandCell3(int3(cx, cy, 0)).x` ikizi — yalnız birinci bileşen
    /// kullanılıyor, ötekiler hesaplanmak zorunda çünkü karışım aşamaları
    /// üçünü birbirine bağlıyor.
    static float RandCell(int cx, int cy)
    {
        // `asuint`: int bitlerini uint olarak yeniden yorumla.
        uint x = unchecked((uint)cx);
        uint y = unchecked((uint)cy);
        uint z = 0u;

        Pcg3d(ref x, ref y, ref z);

        return x * (1.0f / 4294967296.0f);
    }

    /// `SnowCommon.hlsl` → `SnowValueNoise` ikizi.
    static float ValueNoise(float px, float py)
    {
        float hx = Mathf.Floor(px);
        float hy = Mathf.Floor(py);

        float fx = px - hx;
        float fy = py - hy;

        fx = fx * fx * (3.0f - 2.0f * fx);
        fy = fy * fy * (3.0f - 2.0f * fy);

        int ix = (int)hx;
        int iy = (int)hy;

        float a = RandCell(ix,     iy);
        float b = RandCell(ix + 1, iy);
        float c = RandCell(ix,     iy + 1);
        float d = RandCell(ix + 1, iy + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// `SnowRelief.hlsl` → `SnowOktavAgirligiKipli` ikizi.
    ///
    /// CPU'da `pikselBoyu` sıfır: örnekleme frekansı sonsuz, Nyquist kesimi
    /// yok. Geometri eşiği yine uygulanıyor — fizik yüzeyi GEOMETRİK yüzeyle
    /// aynı olmak zorunda, normal haritasındaki ince oktavlarla değil.
    static float OktavAgirligi(float dalgaBoyu, float pikselBoyu, bool yalnizGeometri)
    {
        if (yalnizGeometri && dalgaBoyu < SnowConstants.TessMinDalga) return 0f;

        return Mathf.Clamp01(dalgaBoyu / Mathf.Max(pikselBoyu * 2.0f, 1e-5f) - 1.0f);
    }

    /// `SnowRelief.hlsl` → `SnowYuzeyRolyef` ikizi.
    ///
    /// Sıra HLSL'dekiyle aynı olmak zorunda: tavan → fBm dört oktav → ripple
    /// → sastrugi → drift. Kayan noktada toplama sırası sonucu değiştiriyor.
    public static float Rolyef(Vector2 worldXZ, float karDerinligi,
                               Vector2 sastrugiWindDir, float maruziyet,
                               float pikselBoyu = 0f, bool yalnizGeometri = true)
    {
        float tavan = karDerinligi * SnowConstants.BedformDepthFrac;

        float sastrugiPay = maruziyet;
        float driftPay    = 1f - maruziyet;

        // --- fBm tabanı: dört oktav, self-affine ---
        float h   = 0f;
        float amp = Mathf.Min(SnowConstants.FbmAmp, tavan);
        float frq = SnowConstants.FbmScale;

        for (int i = 0; i < 4; i++)
        {
            h += (ValueNoise(worldXZ.x * frq + i * 17.3f,
                             worldXZ.y * frq + i * 17.3f) * 2f - 1f) * amp
               * OktavAgirligi(1f / frq, pikselBoyu, yalnizGeometri);

            amp *= SnowConstants.FbmGain;
            frq *= 2f;
        }

        // --- rüzgâr ekseni ---
        Vector2 w = sastrugiWindDir;
        float uz = w.magnitude;
        w = uz > 1e-3f ? w / uz : new Vector2(1f, 0f);

        Vector2 dik = new Vector2(-w.y, w.x);

        float boyunca = worldXZ.x * w.x + worldXZ.y * w.y;
        float enine   = worldXZ.x * dik.x + worldXZ.y * dik.y;

        // --- RIPPLE: rüzgâra dik sırtlar ---
        h += (ValueNoise(boyunca / SnowConstants.RippleLength,
                         enine / (SnowConstants.RippleLength * 6f)) * 2f - 1f)
           * Mathf.Min(SnowConstants.RippleAmp, tavan)
           * OktavAgirligi(SnowConstants.RippleLength, pikselBoyu, yalnizGeometri);

        // --- SASTRUGİ: rüzgâra paralel, keskin ---
        float ns = ValueNoise(boyunca / SnowConstants.SastrugiWidth,
                              enine / SnowConstants.SastrugiLength);
        ns = ns * ns * (3f - 2f * ns);

        h += (ns - 0.5f) * Mathf.Min(SnowConstants.SastrugiHeight, tavan) * sastrugiPay
           * OktavAgirligi(SnowConstants.SastrugiLength, pikselBoyu, yalnizGeometri);

        // --- DRIFT: birikme tepecikleri, yumuşak ---
        h += (ValueNoise(boyunca / SnowConstants.DriftWidth,
                         enine / SnowConstants.DriftLength) - 0.5f)
           * Mathf.Min(SnowConstants.DriftHeight, tavan) * driftPay
           * OktavAgirligi(SnowConstants.DriftLength, pikselBoyu, yalnizGeometri);

        return h;
    }

    /// Dünya konumundan doğrudan yükseklik.
    ///
    /// Kar derinliği ve rüzgâr gölgesi DIŞARIDAN geliyor: bu sınıf saf kalmak
    /// zorunda (co-op kuralı) ve `SnowManager`'a bağımlı olmamalı — sistemler
    /// birbirini doğrudan çağırmıyor (`CLAUDE.md`).
    public static float RolyefDunya(Vector3 posWS, float karDerinligi,
                                    float ruzgarGolgesi, Vector2 sastrugiWindDir)
    {
        if (karDerinligi <= 0f) return 0f;

        // Maruziyet `SampleWindShadow`'un tersi: o fonksiyon korunaklılığı
        // ölçüyor. Katsayı `SnowTessellation.hlsl` ile aynı olmak zorunda.
        float maruziyet = 1f - Mathf.Clamp01(ruzgarGolgesi * 1.2f);

        return Rolyef(new Vector2(posWS.x, posWS.z), karDerinligi,
                      sastrugiWindDir, maruziyet);
    }
}
