// ROL: geometry clipmap halkalarının mesh'ini üretir. Her halka bir kez, başlangıçta.
// Çağıran: SnowClipmap.

using UnityEngine;

public static class SnowMeshBuilder
{
    /// Bir clipmap halkası üretir.
    ///
    /// Mesh YEREL UZAYDA (0,0) merkezli. Konumlandırma çizim matrisiyle yapılıyor;
    /// vertex'lere dünya ofseti gömülseydi halka her snap'te yeniden üretilirdi.
    ///
    /// İÇ DELİK GERÇEK: o quad'lar hiç üretilmiyor. Üst üste bindirme z-fighting yapar
    /// ve iki halka aynı yüzeyi farklı çözünürlükte çizdiği için titrer.
    ///
    /// `bounds` elle geniş ayarlanıyor: vertex yer değiştirmesinden sonra mesh
    /// yükseliyor ve otomatik sınırlar onu kadraj dışı sayıp mesh'i yok ediyor.
    public static Mesh BuildRing(int gridQuads, float quadSize, int holeQuads, string meshName)
    {
        if (gridQuads <= 0 || (gridQuads & 1) != 0)
            throw new System.ArgumentException("Halka ızgarası pozitif ve çift olmalı: " + gridQuads);
        if (holeQuads < 0 || holeQuads >= gridQuads || (holeQuads & 1) != 0)
            throw new System.ArgumentException("İç delik çift ve ızgaradan küçük olmalı: " + holeQuads);

        int verts = gridQuads + 1;
        float extent = gridQuads * quadSize;
        float origin = -extent * 0.5f;

        var positions = new Vector3[verts * verts];
        for (int z = 0; z < verts; z++)
        {
            int row = z * verts;
            for (int x = 0; x < verts; x++)
                positions[row + x] = new Vector3(origin + x * quadSize, 0f, origin + z * quadSize);
        }

        int holeStart = (gridQuads - holeQuads) / 2;
        int holeEnd = holeStart + holeQuads;

        int quadCount = gridQuads * gridQuads - holeQuads * holeQuads;
        var indices = new int[quadCount * 6];

        int write = 0;
        for (int z = 0; z < gridQuads; z++)
        {
            bool insideZ = z >= holeStart && z < holeEnd;

            for (int x = 0; x < gridQuads; x++)
            {
                if (insideZ && x >= holeStart && x < holeEnd) continue;

                int a = z * verts + x;
                int b = a + 1;
                int c = a + verts;
                int d = c + 1;

                indices[write++] = a;
                indices[write++] = c;
                indices[write++] = b;

                indices[write++] = b;
                indices[write++] = c;
                indices[write++] = d;
            }
        }

        var mesh = new Mesh
        {
            name = meshName,
            // 240x240 quad = 58081 köşe; 16 bit indeks 65535'te bitiyor ve bir sonraki
            // kalite seviyesinde taşardı.
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            hideFlags = HideFlags.HideAndDontSave,
        };

        mesh.SetVertices(positions);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);

        // Normal ve teğet YOK: yüzey normali fragment'ta merkezi farkla hesaplanıyor
        // (§7.3), vertex normali düşük çözünürlüklü halkalarda zaten çöküyor.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(extent, 500f, extent));

        return mesh;
    }
}
