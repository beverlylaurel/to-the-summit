// ROL: damga atlasını prosedürel olarak üretir (§5.2). Elle doku çizilmiyor.
// Çağıran: SnowDebugWindow > Sahneyi kur, ve menüden elle.

using UnityEditor;
using UnityEngine;

public static class SnowStampGenerator
{
    public const string AtlasPath = "Assets/Settings/Snow/T_SnowStamps.asset";

    const int SliceCount = 6;

    /// Kenar mevduatı bandının dış yarıçapı, taban şeklin katı olarak.
    /// Taban yarıçapları BÜYÜTÜLMÜŞ hâli dilime sığacak şekilde seçildi; sığmasaydı
    /// halka kenarda kesilir ve mevduatın bir kısmı kaybolurdu.
    const float RimGrow = 1.38f;

    [MenuItem("To The Summit/Kar/Damga Atlasını Üret", false, 51)]
    public static Texture2DArray Generate()
    {
        int size = SnowConstants.StampAtlasSize;

        var atlas = new Texture2DArray(size, size, SliceCount, TextureFormat.RG16, false, true)
        {
            name = "T_SnowStamps",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color[size * size];

        for (int slice = 0; slice < SliceCount; slice++)
        {
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                int row = y * size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    float contact = Coverage(slice, u, v, 1f);
                    float outer = Coverage(slice, u, v, RimGrow);

                    // Halka BANDI: büyütülmüş şeklin içinde ama tabanın dışında.
                    float rim = Mathf.Max(0f, outer - contact);
                    rim *= RimBias(slice, u, v);

                    pixels[row + x] = new Color(Pressure(slice, u, v, contact), rim, 0f, 1f);
                }
            }

            atlas.SetPixels(pixels, slice);
        }

        atlas.Apply(false, false);

        if (!AssetDatabase.IsValidFolder("Assets/Settings/Snow"))
            AssetDatabase.CreateFolder("Assets/Settings", "Snow");

        var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AtlasPath);
        if (existing != null)
        {
            // Aynı asset'i yerinde güncelle: referans veren her şey bağlı kalsın.
            Graphics.CopyTexture(atlas, existing);
            existing.Apply(false, false);
            Object.DestroyImmediate(atlas);

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        AssetDatabase.CreateAsset(atlas, AtlasPath);
        AssetDatabase.SaveAssets();
        return atlas;
    }

    /// Şeklin kapsama alanı. `grow` yarıçapları büyütüyor; halka bandı bundan çıkıyor.
    /// u uzunluk ekseni (topuktan buruna), v genişlik.
    static float Coverage(int slice, float u, float v, float grow)
    {
        switch (slice)
        {
            case 0: return Ellipse(u, v, 0.5f, 0.5f, 0.33f * grow, 0.33f * grow);

            case 1: return Boot(u, v, grow, false);
            case 2: return Boot(u, v, grow, true);

            case 3: return Hoof(u, v, grow);

            // Tekerlek/kızak: uzunluk boyunca kesintisiz şerit.
            case 4: return Ellipse(u, v, 0.5f, 0.5f, 0.355f * grow, 0.32f * grow);

            case 5: return Ellipse(u, v, 0.5f, 0.5f, 0.34f * grow, 0.33f * grow);
        }

        return 0f;
    }

    /// BOT BASINÇ DAĞILIMI. Topuk ve ön taban (metatars) parlak, orta kavis karanlık —
    /// gerçek bot izinin basınç dağılımı bu (§5.2).
    static float Boot(float u, float v, float grow, bool mirrored)
    {
        float side = mirrored ? 1f - v : v;

        float heel = Ellipse(u, side, 0.21f, 0.50f, 0.135f * grow, 0.30f * grow);
        float fore = Ellipse(u, side, 0.68f, 0.50f, 0.215f * grow, 0.335f * grow);

        // Kavis: iki bölgeyi birleştiren dar bant. İç kenardan (küçük v) kesiliyor,
        // bu yüzden sol ve sağ bot birbirinin aynası.
        float arch = Ellipse(u, side, 0.45f, 0.60f, 0.18f * grow, 0.19f * grow);

        return Mathf.Max(Mathf.Max(heel, fore), arch);
    }

    /// TOYNAK: C şekli. Dış duvar basıyor, ortadaki taban (frog) basmıyor.
    static float Hoof(float u, float v, float grow)
    {
        float outer = Ellipse(u, v, 0.5f, 0.5f, 0.34f * grow, 0.33f * grow);
        if (outer <= 0f) return 0f;

        float inner = Ellipse(u, v, 0.55f, 0.5f, 0.21f, 0.20f);

        // Arka taraf açık: C'nin ağzı.
        float opening = u < 0.24f ? 1f : 0f;

        return Mathf.Clamp01(outer - Mathf.Max(inner, opening));
    }

    /// R kanalı: temas basıncı. 1 = tam temas.
    static float Pressure(int slice, float u, float v, float contact)
    {
        if (contact <= 0f) return 0f;

        switch (slice)
        {
            case 1:
            case 2:
            {
                bool mirrored = slice == 2;
                float side = mirrored ? 1f - v : v;

                float heel = Ellipse(u, side, 0.21f, 0.50f, 0.135f, 0.30f);
                float fore = Ellipse(u, side, 0.68f, 0.50f, 0.215f, 0.335f);
                float arch = Ellipse(u, side, 0.45f, 0.60f, 0.18f, 0.19f);

                // Kavis belirgin şekilde daha sönük: yük topuk ve ön tabanda.
                return Mathf.Clamp01(Mathf.Max(Mathf.Max(heel, fore), arch * 0.42f));
            }

            case 3:
                // Toynak duvarı sert ve dar: basınç neredeyse tekdüze.
                return Mathf.Clamp01(contact * 1.35f);

            default:
                return contact;
        }
    }

    /// G kanalının şekle göre ağırlığı. İTİŞ ANINDA KAR ÖNE SAVRULUYOR: botta parmak
    /// ucu bandı daha kalın.
    static float RimBias(int slice, float u, float v)
    {
        switch (slice)
        {
            case 1:
            case 2: return Mathf.Lerp(0.65f, 1.60f, u);
            case 4: return 1.25f;                        // tekerlek yanlara çok atıyor
            default: return 1f;
        }
    }

    /// Yumuşak kenarlı elips. 1 = merkez, 0 = dışı.
    static float Ellipse(float u, float v, float cu, float cv, float ru, float rv)
    {
        float du = (u - cu) / Mathf.Max(ru, 1e-4f);
        float dv = (v - cv) / Mathf.Max(rv, 1e-4f);

        float d = Mathf.Sqrt(du * du + dv * dv);

        // Kenar bir teksel değil, birkaç teksel içinde iniyor: sert kenar damgayı
        // ızgaraya oturtup basamaklı bir iz bırakıyor.
        return Mathf.Clamp01(1f - Mathf.SmoothStep(0.82f, 1f, d));
    }
}
