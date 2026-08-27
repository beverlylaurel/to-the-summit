// ROL: deniz yuzeyinin tek izgara mesh'ini uretir. Baslangicta bir kez.
// Cagiran: SeaSurface (Awake).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// TEK IZGARA, TEK DRAW CALL — ÇOK SEVİYELİ CLIPMAP DEĞİL.
///
/// Geometry clipmap kurulsaydı `[KAYNAK: Asirvatham & Hoppe, GPU Gems 2
/// Bölüm 2]`'nin şu parçalarının **hepsi** gerekirdi ve hiçbiri
/// atlanamazdı: tek sayı ızgara boyutu, 12 blok (`m = (n+1)/4`), dört
/// `m×3` fix-up şeridi, dört yönelimli L-trim, dejenere üçgen çevresi,
/// `alpha = max(αx, αy)` geçiş harmanlaması. Biri eksikse mesh yırtılır,
/// delik açar veya titrer.
///
/// Bunun yerine tek, sürekli mesh. Merkeze yakın quad'lar küçük,
/// uzaklaştıkça ikinin kuvveti adımlarla büyük.
///
/// **HİZALAMA İSPATI (spec §10.1).** Tüm quad boyutları en ince quad
/// boyutunun ikinin kuvveti katı. Dolayısıyla en ince quad boyutuna eşit
/// TEK BİR snap adımı, her halkanın vertex'lerini kendi kafesinde tutuyor.
/// Seviye başına ayrı snap gerekmez, dolayısıyla seviyeler arası kayma da
/// olamaz.
public static class SeaMeshBuilder
{
    /// Halka başına quad sayısı (kenar boyunca). Halka 0 dolu kare, ötekiler
    /// halka. Spec §10.2 tablosu bu sayıdan çıkıyor:
    ///   halka 0: 0.5 m quad, 128×128 → 32 m yarıçap
    ///   halka 1: 1.0 m quad, halka   → 96 m
    ///   ...her halka bir öncekinin tam 2 katı
    public const int QuadPerSide = 128;

    public static Mesh Build(float finestQuad, int ringCount)
    {
        var vertices = new List<Vector3>(300000);
        var indices = new List<int>(1200000);

        // Vertex paylaşımı için: dünya konumundan indekse. Halkalar arası
        // vertex PAYLAŞILIYOR — T-junction ve dolayısıyla dikiş yapısal
        // olarak imkânsız oluyor (spec §10.2).
        var lookup = new Dictionary<long, int>(400000);

        // --- Halka 0: dolu kare ---
        float q0 = finestQuad;
        int yari0 = QuadPerSide / 2;

        for (int z = -yari0; z < yari0; z++)
            for (int x = -yari0; x < yari0; x++)
                QuadEkle(vertices, indices, lookup,
                         x * q0, z * q0, q0, q0);

        // --- Halka 1..N: her biri bir öncekinin iki katı quad ---
        float icYaricap = yari0 * q0;

        for (int halka = 1; halka < ringCount; halka++)
        {
            float q = finestQuad * (1 << halka);

            // Bu halkanın dış yarıçapı: iç yarıçap + QuadPerSide/2 × quad
            int adim = QuadPerSide / 2;
            float disYaricap = icYaricap + adim * q;

            // İç kareyi çevreleyen halka: dış kareden iç kareyi çıkar.
            int disAdim = Mathf.RoundToInt(disYaricap / q);
            int icAdim = Mathf.RoundToInt(icYaricap / q);

            for (int z = -disAdim; z < disAdim; z++)
                for (int x = -disAdim; x < disAdim; x++)
                {
                    // İç kare bu halkaya ait değil — bir önceki halka çizdi.
                    if (x >= -icAdim && x < icAdim && z >= -icAdim && z < icAdim)
                        continue;

                    QuadEkle(vertices, indices, lookup, x * q, z * q, q, q);
                }

            icYaricap = disYaricap;
        }

        var mesh = new Mesh
        {
            name = "SeaSurfaceGrid",

            // Vertex sayısı 65535'i aşıyor (spec §10.2).
            indexFormat = IndexFormat.UInt32,
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);

        // NORMAL VE TANJANT YOK. Normal fragment shader'da FFT eğim
        // dokusundan geliyor (spec §10.5); mesh'te taşımak boşuna bant
        // genişliği.
        mesh.RecalculateBounds();

        // BOUNDS ELLE GENİŞLETİLİYOR. Displacement vertex shader'da olduğu
        // için CPU sınırları bilmiyor; dar bırakılırsa deniz kamera açısına
        // göre kayboluyor (spec §10.2, §18 tuzak tablosu).
        float yariBoy = icYaricap * 2f;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(yariBoy, 400f, yariBoy));

        mesh.UploadMeshData(false);

        return mesh;
    }

    /// Bir quad'ı iki üçgen olarak ekler. Köşeler paylaşılıyor: aynı dünya
    /// konumundaki vertex bir kez oluşturulup indeksi tekrar kullanılıyor.
    static void QuadEkle(List<Vector3> vertices, List<int> indices,
                         Dictionary<long, int> lookup,
                         float x, float z, float w, float d)
    {
        int a = VertexIndeksi(vertices, lookup, x, z);
        int b = VertexIndeksi(vertices, lookup, x + w, z);
        int c = VertexIndeksi(vertices, lookup, x + w, z + d);
        int e = VertexIndeksi(vertices, lookup, x, z + d);

        indices.Add(a); indices.Add(e); indices.Add(b);
        indices.Add(b); indices.Add(e); indices.Add(c);
    }

    /// Dünya konumundan vertex indeksi. Aynı konum ikinci kez istenirse
    /// mevcut indeks dönüyor — vertex paylaşımı burada oluyor.
    ///
    /// Anahtar milimetreye yuvarlanmış tam sayı çifti: kayan nokta
    /// karşılaştırması yerine tam sayı, çünkü quad boyutları ikinin kuvveti
    /// katı ve toplama hatası birikmiyor.
    static int VertexIndeksi(List<Vector3> vertices, Dictionary<long, int> lookup,
                             float x, float z)
    {
        long ix = Mathf.RoundToInt(x * 1000f);
        long iz = Mathf.RoundToInt(z * 1000f);
        long anahtar = (ix << 32) ^ (iz & 0xFFFFFFFFL);

        if (lookup.TryGetValue(anahtar, out int idx)) return idx;

        idx = vertices.Count;
        vertices.Add(new Vector3(x, 0f, z));
        lookup[anahtar] = idx;

        return idx;
    }
}
