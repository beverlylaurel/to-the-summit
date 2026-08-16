using System;
using System.Collections.Generic;
using UnityEngine;

/// TEK MESH'İ BÖLGELERE AYIRIR. Üretilen modelde birbirinden farklı malzemeler aynı
/// mesh'te geliyor: gidon ile tutamak ve kablolar tek parça, bagaj ile zincir muhafazası
/// ve pedal tek parça. Malzeme atanamıyor, çünkü bir çizici tek materyal alıyor.
///
/// AYRI MESH YERİNE ALT-MESH. Parça ikiye kesilseydi köşeler kopyalanır ve bellek
/// katlanırdı; alt-mesh yalnız üçgen listesini bölüyor, köşeler ortak kalıyor. Çizici de
/// alt-mesh başına ayrı materyal alabiliyor.
///
/// BÖLGE SINIRLARI ORANLA veriliyor, metreyle değil: dosya metreyi santimle karışık
/// taşıyor (mesh verisi yüzde bir ölçekte, dönüşümde yüz kat ölçek var). Sınırı parçanın
/// kendi sınır kutusuna oranlayınca birim sorunu ortadan kalkıyor.
public static class MeshZones
{
    /// Üçgenleri bölge numarasına göre ayırır ve her bölgeyi ayrı alt-mesh yapar.
    /// Sınıflandırma üçgenin AĞIRLIK MERKEZİNE bakıyor: köşeye bakmak sınırdaki üçgenleri
    /// iki bölgeye birden sokup her ikisinde de delik bırakıyordu.
    public static Mesh Build(Mesh source, Func<Vector3, int> zoneOf, int zones, string name)
    {
        Vector3[] vertices = source.vertices;
        int[] triangles = source.triangles;

        var buckets = new List<int>[zones];
        for (int i = 0; i < zones; i++) buckets[i] = new List<int>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 centre = (vertices[triangles[t]]
                            + vertices[triangles[t + 1]]
                            + vertices[triangles[t + 2]]) / 3f;

            int zone = Mathf.Clamp(zoneOf(centre), 0, zones - 1);

            buckets[zone].Add(triangles[t]);
            buckets[zone].Add(triangles[t + 1]);
            buckets[zone].Add(triangles[t + 2]);
        }

        var mesh = new Mesh { name = name };
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);

        Vector3[] normals = source.normals;
        if (normals.Length == vertices.Length) mesh.SetNormals(normals);

        Vector2[] uv = source.uv;
        if (uv.Length == vertices.Length) mesh.SetUVs(0, uv);

        // KÖŞE RENGİ SIFIRLANIYOR. Üretilen modelde köşe rengi var ve gölgelendirici onu
        // elle boyanan malzeme maskesi olarak okuyor; taşınsaydı hiç boyanmamış yüzey
        // kendiliğinden boyalı görünürdü. Boyama bu temiz zeminin üstüne yazılıyor.
        mesh.SetColors(new Color32[vertices.Length]);

        // Yuva numarasının durduğu ikinci UV kanalı da boş açılıyor: bölgeli parçalar
        // sonradan elle boyanıyor ve kanal yoksa fırça yazacak yer bulamıyor.
        mesh.SetUVs(1, new Vector2[vertices.Length]);

        mesh.subMeshCount = zones;
        for (int i = 0; i < zones; i++) mesh.SetTriangles(buckets[i], i);

        mesh.RecalculateBounds();
        return mesh;
    }

    /// Noktanın parça sınırları içindeki yüksekliği (0 altta, 1 üstte). Mesh verisi
    /// dosyanın kendi düzeninde geliyor ve o düzende yukarı Z.
    public static float Height(Bounds bounds, Vector3 point) =>
        Mathf.InverseLerp(bounds.min.z, bounds.max.z, point.z);
}
