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

        /// Eteği var mı — YALNIZ EN DIŞ HALKADA.
        ///
        /// Etek 2 m AŞAĞI iniyor. İç dikişe konursa yamaçta felaket oluyor:
        /// dikişin aşağı tarafındaki yüzey eteğin tepesinden alçakta kalıyor
        /// ve etek açıkta, dik bir duvar olarak görünüyor (ölçüldü —
        /// `SYMPTOMS.md`). Ortak snap ızgarasından sonra iç dikişte boşluk
        /// zaten SIFIR, yani iç etek gereksiz.
        ///
        /// En dışta ise ötesinde yüzey yok; etek arazinin içine giriyor ve
        /// mesh'in kenarını her koşulda kapatıyor.
        public readonly bool Outermost;

        public Ring(int index, float extent, int grid, int holeQuads, float snapStep,
                    bool outermost)
        {
            Index = index;
            Extent = extent;
            Grid = grid;
            QuadSize = extent / grid;
            HoleQuads = holeQuads;
            SnapStep = snapStep;
            Outermost = outermost;
        }
    }

    /// Bütün halkaların ölçüsünü hesaplar. Delik boyu, iç halkanın bu halkaya
    /// göre yapabileceği EN BÜYÜK kaymayı karşılayacak şekilde kısılıyor.
    public static Ring[] Describe(SnowQualityData quality)
    {
        var rings = new Ring[quality.RingCount];

        // ORTAK SNAP ADIMI — EN KABA HALKANINKİ.
        //
        // Her halka kendi adımına snap'lenirse merkezleri birbirine göre
        // kayıyor ve delik iç halkaya tam oturamıyor: ya bindirme kalıyor
        // (iki yüzey aynı anda çiziliyor, kaba olan ince olanın içinden
        // çıkıyor) ya da boşluk (ölçüldü: 2→3 sınırında 5,85 m).
        //
        // Ortak adımda göreli kayma SIFIR. Delik `ızgara / 3` ile birebir
        // oturuyor. Bedeli: en iç halka 3,6 m'lik adımlarla yer değiştiriyor
        // — ±4 m kaplıyor, yani oyuncu her hâlükârda 2,2 m payla içinde.
        float outerExtent = SnowConstants.Ring0Extent
                          * Mathf.Pow(SnowConstants.RingScale, quality.RingCount - 1);

        float commonSnap = outerExtent / quality.Ring0Grid * SnowConstants.RingSnapQuads;

        float extent = SnowConstants.Ring0Extent;

        for (int i = 0; i < quality.RingCount; i++)
        {
            // Delik: iç halkanın kapsaması, dış halkanın quad'ı cinsinden.
            // Genişlik oranı 3 ve ızgara aynı olduğu için bu TAM SAYI.
            int holeQuads = i == 0 ? 0 : quality.Ring0Grid / 3;

            rings[i] = new Ring(i, extent, quality.Ring0Grid, holeQuads, commonSnap,
                                i == quality.RingCount - 1);

            extent *= SnowConstants.RingScale;
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

        Vector3[] vertices = new Vector3[side * side];

        // Halka indeksi köşede taşınıyor. Materyal başına property block
        // kullanmak SRP Batcher'ı kapatırdı (spec §15.2); köşe verisi
        // toplu çizimi bozmuyor.
        Vector2[] ringId = new Vector2[side * side];

        for (int j = 0; j < side; j++)
        for (int i = 0; i < side; i++)
        {
            int v = j * side + i;
            vertices[v] = new Vector3(i * q - half, 0f, j * q - half);

            // T-KAVŞAĞI İŞARETİ (y = 2).
            //
            // Halkanın DIŞ kenarındaki köşeler, bir dış halkanın delik
            // kenarıyla aynı çizgide duruyor. Dış halkanın köşe aralığı 3 kat
            // seyrek; aradaki iki ince köşe kaba kenarın DÜZ çizgisinden
            // sapıyor ve dikiş boyunca ince yarıklar açılıyor (klasik
            // T-kavşağı çatlağı).
            //
            // İşaretli köşeler yüksekliği kaba ızgaradan okuyor; iki yüzey
            // sınırda birebir aynı çizgiyi paylaşıyor.
            bool border = i == 0 || j == 0 || i == grid || j == grid;

            ringId[v] = new Vector2(ring.Index, border && !ring.Outermost ? 2f : 0f);
        }

        int holeLo = (grid - ring.HoleQuads) / 2;
        int holeHi = holeLo + ring.HoleQuads;

        int quads = grid * grid - ring.HoleQuads * ring.HoleQuads;
        int[] indices = new int[quads * 6];
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

        // ETEK (rapor §5). En dış halkanın dört kenarındaki köşeler
        // kopyalanıp aşağı itiliyor ve aradaki şerit üçgenleniyor. Aşağı itme
        // köşe shader'ında yapılıyor; burada yalnız işaret taşınıyor
        // (`ringId.y = 1`).
        if (ring.Outermost)
        {
            var vs = new System.Collections.Generic.List<Vector3>(vertices);
            var ids = new System.Collections.Generic.List<Vector2>(ringId);
            var tri = new System.Collections.Generic.List<int>(indices);

            void Edge(int a, int b)
            {
                int a2 = vs.Count; vs.Add(vertices[a]); ids.Add(new Vector2(ring.Index, 1f));
                int b2 = vs.Count; vs.Add(vertices[b]); ids.Add(new Vector2(ring.Index, 1f));

                tri.Add(a); tri.Add(a2); tri.Add(b);
                tri.Add(b); tri.Add(a2); tri.Add(b2);
            }

            for (int i = 0; i < grid; i++)
            {
                Edge(i + 1, i);                                        // güney
                Edge(grid * side + i, grid * side + i + 1);            // kuzey
                Edge(i * side, (i + 1) * side);                        // batı
                Edge((i + 1) * side + grid, i * side + grid);          // doğu
            }

            vertices = vs.ToArray();
            ringId = ids.ToArray();
            indices = tri.ToArray();
        }

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
