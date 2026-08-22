// ROL: kar yüzeyinin tek ızgara mesh'ini üretir. Bir kez çağrılır, sonuç saklanır.
// Çağıran: SnowSurface.

using UnityEngine;
using UnityEngine.Rendering;

/// TEK IZGARA, TEK DRAW CALL (spec §8.1, §8.2).
///
/// `[KAYNAK: Asirvatham & Hoppe, GPU Gems 2 Bölüm 2, §2.3.1]` — makaleden
/// birebir: *"Only the finest level is rendered as a complete grid square."*
///
/// Geometry clipmap ufka kadar arazi çizmek için tasarlanmış. Bizim
/// deformasyon alanımız 24 metre ve tek çözünürlük seviyesi yeterli. Bu
/// durumda clipmap'in tanımı gereği doğru yapı, en ince seviyenin tek başına
/// tam kare ızgara olarak çizilmesi.
///
/// ÇOK SEVİYELİ CLIPMAP KURULMUYOR. Kurulsaydı makalenin tek sayı ızgara,
/// 12 blok, kenar fix-up, L-trim, dejenere çevre ve geçiş harmanlaması
/// parçalarının HEPSİ gerekirdi; biri bile eksikse mesh yırtılır, delik açar
/// veya titrer (spec §8.1). 24 metre için bu karmaşıklığın karşılığı yok.
public static class SnowMeshBuilder
{
    /// Mesh'in ölçüleri. Hepsi kalite presetinden türüyor; çağrı yerinde sayı yok.
    public readonly struct Grid
    {
        /// Kenar uzunluğu, metre.
        public readonly float Extent;

        /// Kenar başına quad sayısı.
        public readonly int Quads;

        /// Tek quad'ın kenarı, metre.
        public readonly float QuadSize;

        /// Konumun oturduğu ızgara adımı, metre.
        public readonly float SnapStep;

        public Grid(SnowQualityData quality)
        {
            Extent = quality.AreaSize;
            Quads = quality.MeshGrid;
            QuadSize = quality.QuadSize;
            SnapStep = quality.SnapStep;
        }

        public int VertexCount => (Quads + 1) * (Quads + 1);
        public int TriangleCount => Quads * Quads * 2;
    }

    public static Grid Describe(SnowQualityData quality) => new Grid(quality);

    /// Mesh, yerel uzayda (0,0) merkezli. Konumlandırma Transform ile (spec §8.2).
    public static Mesh Build(Grid grid)
    {
        int quads = grid.Quads;
        int side = quads + 1;

        var vertices = new Vector3[side * side];

        // YEREL UZAYDA (0,0) MERKEZLİ (spec §8.2):
        //   x = (i / MeshGrid − 0.5) × AreaSize
        //
        // Vertex'ler her kare yeniden HESAPLANMIYOR; mesh bir kez üretilip
        // Transform ile taşınıyor.
        for (int j = 0; j < side; j++)
        for (int i = 0; i < side; i++)
        {
            vertices[j * side + i] = new Vector3(
                ((float)i / quads - 0.5f) * grid.Extent,
                0f,
                ((float)j / quads - 0.5f) * grid.Extent);
        }

        var indices = new int[quads * quads * 6];
        int w = 0;

        for (int j = 0; j < quads; j++)
        for (int i = 0; i < quads; i++)
        {
            int v0 = j * side + i;
            int v1 = v0 + 1;
            int v2 = v0 + side;
            int v3 = v2 + 1;

            indices[w++] = v0; indices[w++] = v2; indices[w++] = v1;
            indices[w++] = v1; indices[w++] = v2; indices[w++] = v3;
        }

        var mesh = new Mesh
        {
            name = "SnowSurface",

            // ÜÇ PRESETTE DE ZORUNLU (spec §8.2). Vertex sayısı `(MeshGrid+1)²`
            // ve Low preset'te bile 257² = 66 049 > 65535. 16 bit indeks
            // sessizce sarar.
            indexFormat = IndexFormat.UInt32,
            hideFlags = HideFlags.HideAndDontSave,
        };

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);

        // SINIRLAR ELLE (spec §8.2). Köşeler yerel uzayda düz bir düzlem;
        // gerçek yükseklik vertex shader'da veriliyor. Otomatik sınır sıfır
        // kalınlıkta çıkar ve mesh kameranın önündeyken bile elenir (§22:
        // "Kar mesh'i kayboluyor → mesh.bounds dar").
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(grid.Extent, SnowConstants.MeshBoundsHeight, grid.Extent));

        return mesh;
    }
}
