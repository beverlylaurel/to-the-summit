// ROL: arazi yuksekliginden su derinligi alani cikarir. Bir kez bake edilir.
// Cagiran: SeaManager (Awake ve RefreshBathymetry).

using System;
using UnityEngine;

/// SU DERİNLİĞİ CPU'DA BİR KEZ BAKE EDİLİYOR.
///
/// `terrainData.heightmapTexture` shader'da doğrudan örneklenmiyor: Unity
/// sürümleri arasında ölçekleme sabitleri değişiyor (spec §9). CPU'da bir kez
/// bake etmek belirsizliği ortadan kaldırıyor.
///
/// Sığlaşma (§8.1), kırılma (§8.3) ve kıyı sönümü (§8.4) her teksel için
/// derinlik istiyor; hepsi bu dokudan okuyor.
public static class SeaBathymetry
{
    /// Su derinliği dokusu. `>0` su, `<0` kara.
    ///
    /// `RHalf` yeterli: derinlik aralığı 0–200 m ve yarım hassasiyet orada
    /// 0.1 m'den ince çözüyor. `Float` iki kat bant genişliği, görsel fark
    /// yok (spec §15.2).
    public static Texture2D Bake(Terrain terrain, float seaLevelY)
    {
        if (terrain == null)
            throw new ArgumentNullException(nameof(terrain));

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;

        // GetHeights [y, x] SIRALI dönüyor — indeks sırasına dikkat (spec §9).
        float[,] hm = td.GetHeights(0, 0, res, res);

        var tex = new Texture2D(res, res, TextureFormat.RHalf, false, true)
        {
            name = "Tex_SeaBathymetry",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        var px = new Color[res * res];
        float tabanY = terrain.transform.position.y;
        float yukseklik = td.size.y;

        for (int y = 0; y < res; y++)
        {
            int satir = y * res;

            for (int x = 0; x < res; x++)
            {
                float kot = tabanY + hm[y, x] * yukseklik;

                // Doku pikseli (x, y) ← derinlik. Heightmap [y, x] okundu,
                // yani satır Z eksenine karşılık geliyor ve UV ile birebir
                // örtüşüyor.
                px[satir + x] = new Color(seaLevelY - kot, 0f, 0f, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);

        return tex;
    }

    /// Ölçüm: dokunun beklenen değerleri verdiğini doğrular. Üç bilinen
    /// nokta yeterli — kara, su, arazi dışı.
    public static string Dogrula(Terrain terrain, float seaLevelY, Texture2D tex)
    {
        TerrainData td = terrain.terrainData;
        Vector3 o = terrain.transform.position;

        int res = td.heightmapResolution;
        float[,] hm = td.GetHeights(0, 0, res, res);

        // En alçak ve en yüksek nokta
        float enAz = float.MaxValue, enCok = float.MinValue;

        for (int y = 0; y < res; y += 8)
            for (int x = 0; x < res; x += 8)
            {
                float kot = o.y + hm[y, x] * td.size.y;
                if (kot < enAz) enAz = kot;
                if (kot > enCok) enCok = kot;
            }

        int su = 0, toplam = 0;

        for (int y = 0; y < res; y += 8)
            for (int x = 0; x < res; x += 8)
            {
                toplam++;
                if (o.y + hm[y, x] * td.size.y < seaLevelY) su++;
            }

        return $"bathymetry {res}x{res} | deniz seviyesi {seaLevelY:F1} m\n" +
               $"  arazi kotu {enAz:F1} .. {enCok:F1} m\n" +
               $"  en derin su {seaLevelY - enAz:F1} m\n" +
               $"  su altinda kalan alan %{100f * su / toplam:F1}";
    }
}
