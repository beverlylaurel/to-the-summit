// ROL: clipmap halkalarının mesh'lerini üretir. Bir kez çağrılır, sonuç saklanır.
// Çağıran: SnowClipmap.

using UnityEngine;
using UnityEngine.Rendering;

/// İÇ DELİK GERÇEK (spec §13.1). Halkalar üst üste bindirilmiyor; iç halkanın
/// kapladığı alan dış halkanın mesh'inde HİÇ ÜÇGEN TAŞIMIYOR. Bindirme aynı
/// yüzeyi iki kez çizer ve z-fighting üretir.
///
/// Deliğin boyu spec'in verdiği 134² değil HESAPLANIYOR — sebebi
/// `DECISIONS.md`'de: her halka kendi ızgarasına snap'lendiği için iç halka
/// dış halkanın deliğine göre kayabiliyor; delik o kaymayı karşılayacak kadar
/// küçük olmalı, yoksa aralarında gerçek bir yarık açılıyor.
public static class SnowMeshBuilder
{
    /// Bir halkanın ölçüleri. Hepsi `SnowConstants` ve kalite presetinden
    /// türüyor; çağrı yerinde sayı yok.
    public readonly struct Ring
    {
        public readonly int Index;

        /// Kenar uzunluğu, metre.
        public readonly float Extent;

        /// Tek quad'ın kenarı, metre.
        public readonly float QuadSize;

        /// Kenar başına quad sayısı.
        public readonly int Grid;

        /// Ortadaki boş alanın quad sayısı. 0 = delik yok.
        public readonly int HoleQuads;

        /// Konumun oturduğu ızgara adımı, metre.
        public readonly float SnapStep;

        public Ring(int index, float extent, int grid, int holeQuads)
        {
            Index = index;
            Extent = extent;
            Grid = grid;
            QuadSize = extent / grid;
            HoleQuads = holeQuads;
            SnapStep = QuadSize * SnowConstants.RingSnapQuads;
        }
    }

    /// Bütün halkaların ölçüsünü hesaplar. Delik boyu, iç halkanın bu halkaya
    /// göre yapabileceği EN BÜYÜK kaymayı karşılayacak şekilde kısılıyor.
    public static Ring[] Describe(SnowQualityData quality)
    {
        var rings = new Ring[quality.RingCount];

        float extent = SnowConstants.Ring0Extent;
        rings[0] = new Ring(0, extent, quality.Ring0Grid, 0);

        for (int i = 1; i < quality.RingCount; i++)
        {
            Ring inner = rings[i - 1];
            extent *= SnowConstants.RingScale;

            float quadSize = extent / quality.Ring0Grid;

            // İç halkanın merkezi bu halkanınkinden en çok bu kadar sapabilir:
            // ikisi de kendi adımına aşağı yuvarlanıyor, kaba adım ince adımın
            // katı olduğu için fark (kabaAdım − inceAdım) ile sınırlı.
            float maxOffset = quadSize * SnowConstants.RingSnapQuads - inner.SnapStep;

            // Delik iç halkanın DAİMA örttüğü kadar olmalı.
            int holeQuads = Mathf.FloorToInt((inner.Extent - 2f * maxOffset) / quadSize);
            holeQuads = Mathf.Max(0, holeQuads);

            rings[i] = new Ring(i, extent, quality.Ring0Grid, holeQuads);
        }

        return rings;
    }

    /// Halkanın mesh'i. Yerel uzayda (0,0) merkezli; konumlandırma Transform ile
    /// (spec §13.1).
    public static Mesh Build(Ring ring)
    {
        int grid = ring.Grid;
        int side = grid + 1;
        float half = ring.Extent * 0.5f;
        float q = ring.QuadSize;

        var vertices = new Vector3[side * side];

        // Halka indeksi köşede taşınıyor. Materyal başına property block
        // kullanmak SRP Batcher'ı kapatırdı (spec §15.2); köşe verisi
        // toplu çizimi bozmuyor.
        var ringId = new Vector2[side * side];

        for (int j = 0; j < side; j++)
        for (int i = 0; i < side; i++)
        {
            int v = j * side + i;
            vertices[v] = new Vector3(i * q - half, 0f, j * q - half);
            ringId[v] = new Vector2(ring.Index, 0f);
        }

        int holeLo = (grid - ring.HoleQuads) / 2;
        int holeHi = holeLo + ring.HoleQuads;

        int quads = grid * grid - ring.HoleQuads * ring.HoleQuads;
        var indices = new int[quads * 6];
        int w = 0;

        for (int j = 0; j < grid; j++)
        for (int i = 0; i < grid; i++)
        {
            if (ring.HoleQuads > 0 && i >= holeLo && i < holeHi && j >= holeLo && j < holeHi)
                continue;

            int v0 = j * side + i;
            int v1 = v0 + 1;
            int v2 = v0 + side;
            int v3 = v2 + 1;

            indices[w++] = v0; indices[w++] = v2; indices[w++] = v1;
            indices[w++] = v1; indices[w++] = v2; indices[w++] = v3;
        }

        var mesh = new Mesh
        {
            name = "SnowRing" + ring.Index,
            // 401² köşe 65535'i aşıyor; 16 bit indeks sessizce sarar.
            indexFormat = IndexFormat.UInt32,
            hideFlags = HideFlags.HideAndDontSave,
        };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, ringId);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);

        // SINIRLAR ELLE. Köşeler yerel uzayda düz bir düzlem; gerçek yükseklik
        // vertex shader'da veriliyor. Otomatik sınır sıfır kalınlıkta çıkar ve
        // mesh kameranın önündeyken bile elenir (spec §22).
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(ring.Extent, 600f, ring.Extent));

        return mesh;
    }
}
