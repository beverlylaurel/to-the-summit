using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// MESH'İ BÖLGEYE GÖRE AYIRIR. Üretilen modeller tek gövde geliyor; tekerleğin dönmesi,
/// gidonun çevrilmesi için parçaların ayrı nesne olması gerekiyor.
///
/// NEDEN BAĞLANTIYA GÖRE DEĞİL: denendi ve ölçüldü — bisiklet modelinde üç milyon
/// üçgenin 2.97 milyonu tek bağlantılı ada. Jant gövdeye, gidon çatala değiyor. Meshy'nin
/// kendi bölmesi ANLAMSAL (model "burası tekerlek" diye biliyor) ve remesh'li modele
/// uygulanamıyor. Geriye şekle bakmak kalıyor.
///
/// Kesim yeri gözle seçiliyor: kutu sahnede taşınıp ölçekleniyor, içine düşen üçgenler
/// ayrılıyor. Bisiklette kesim göbeğin ve çatal ucunun içinde kalıyor, açılan kenar
/// dışarıdan görünmüyor.
///
/// ÜÇGEN AĞIRLIK MERKEZİNE bakılıyor, köşelerine değil: köşeye bakmak kutunun kenarında
/// duran üçgenleri ikiye bölünmüş gibi gösteriyor ve her iki parçada da eksik yüzey
/// bırakıyordu.
public class MeshRegionSplitter : EditorWindow
{
    MeshFilter target;
    Transform region;
    string pieceName = "parca";

    [MenuItem("To The Summit/Model/Mesh'i Bölgeye Göre Ayır", false, 120)]
    static void Open() => GetWindow<MeshRegionSplitter>("Mesh Ayırıcı").Show();

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Ayrılacak mesh'i seç.\n" +
            "2. Kutu oluştur, sahnede tekerleğin üstüne getir ve ölçekle.\n" +
            "3. Ayır — kutunun içindeki üçgenler yeni nesne olur, kalanı yerinde kalır.\n\n" +
            "Kutu yalnız bölge tarif ediyor; çizilmiyor, kaydedilmiyor.",
            MessageType.None);

        target = (MeshFilter)EditorGUILayout.ObjectField("Mesh", target, typeof(MeshFilter), true);
        region = (Transform)EditorGUILayout.ObjectField("Bölge kutusu", region, typeof(Transform), true);
        pieceName = EditorGUILayout.TextField("Parça adı", pieceName);

        EditorGUILayout.Space();

        if (GUILayout.Button("Bölge kutusu oluştur")) CreateRegion();

        using (new EditorGUI.DisabledScope(target == null || region == null))
            if (GUILayout.Button("Ayır", GUILayout.Height(28f))) Extract();

        if (target == null || target.sharedMesh == null) return;

        EditorGUILayout.Space();
        Mesh mesh = target.sharedMesh;
        EditorGUILayout.LabelField("Üçgen", $"{mesh.triangles.Length / 3}");
        EditorGUILayout.LabelField("Boyut", $"{mesh.bounds.size.x:F2} x {mesh.bounds.size.y:F2} x {mesh.bounds.size.z:F2} m");
    }

    /// Kutu, seçili mesh'in ortasında ve onun çeyreği boyunda açılıyor: sıfır boyutta
    /// açılırsa sahnede bulunamıyor, dünya boyutunda açılırsa her şeyi kapsıyor.
    void CreateRegion()
    {
        var box = new GameObject("Bölge kutusu");

        if (target != null)
        {
            Bounds bounds = target.GetComponent<Renderer>() != null
                ? target.GetComponent<Renderer>().bounds
                : new Bounds(target.transform.position, Vector3.one);

            box.transform.position = bounds.center;
            box.transform.localScale = bounds.size * 0.25f;
        }
        else box.transform.localScale = Vector3.one;

        region = box.transform;
        Selection.activeGameObject = box;
        Undo.RegisterCreatedObjectUndo(box, "Bölge kutusu");
    }

    void Extract()
    {
        Mesh mesh = target.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        var inside = new List<int>();
        var outside = new List<int>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 centre = (vertices[triangles[t]]
                            + vertices[triangles[t + 1]]
                            + vertices[triangles[t + 2]]) / 3f;

            Vector3 world = target.transform.TransformPoint(centre);
            Vector3 local = region.InverseTransformPoint(world);

            bool within = Mathf.Abs(local.x) <= 0.5f
                       && Mathf.Abs(local.y) <= 0.5f
                       && Mathf.Abs(local.z) <= 0.5f;

            List<int> bucket = within ? inside : outside;
            bucket.Add(triangles[t]);
            bucket.Add(triangles[t + 1]);
            bucket.Add(triangles[t + 2]);
        }

        if (inside.Count == 0)
        {
            Debug.LogWarning("[Ayırma] Kutunun içinde üçgen yok. Kutuyu mesh'in üstüne taşı.");
            return;
        }

        if (outside.Count == 0)
        {
            Debug.LogWarning("[Ayırma] Kutu mesh'in tamamını kapsıyor. Küçült.");
            return;
        }

        // Ayrılan parça KAYNAK NESNENİN ÇOCUĞU ve yerel dönüşümü sıfır: böylece dünya
        // konumu değişmiyor, kendi ekseninde döndürülmeye hazır oluyor.
        var piece = new GameObject(pieceName);
        piece.transform.SetParent(target.transform, false);
        piece.AddComponent<MeshFilter>().sharedMesh = Build(mesh, inside, $"{mesh.name}_{pieceName}");

        var renderer = target.GetComponent<MeshRenderer>();
        var pieceRenderer = piece.AddComponent<MeshRenderer>();
        if (renderer != null) pieceRenderer.sharedMaterials = renderer.sharedMaterials;

        // Kalan kısım kaynağın üstüne yazılıyor: ikinci bir nesne açmak hiyerarşiyi
        // her ayırmada ikiye katlıyordu.
        target.sharedMesh = Build(mesh, outside, $"{mesh.name}_kalan");

        Undo.RegisterCreatedObjectUndo(piece, "Mesh'i ayır");
        Selection.activeGameObject = piece;

        Debug.Log($"[Ayırma] {pieceName}: {inside.Count / 3} üçgen ayrıldı, "
                + $"{outside.Count / 3} üçgen kaldı.");
    }

    /// Verilen üçgenlerden yeni mesh kurar. Köşeler yeniden numaralanıyor: parça kaynak
    /// mesh'in bütün köşe dizisini taşısaydı bellek parça sayısıyla çarpılırdı.
    static Mesh Build(Mesh source, List<int> indices, string name)
    {
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Vector2[] uv = source.uv;
        Vector4[] tangents = source.tangents;

        var remap = new Dictionary<int, int>();
        var newVertices = new List<Vector3>();
        var newNormals = new List<Vector3>();
        var newUv = new List<Vector2>();
        var newTangents = new List<Vector4>();
        var newTriangles = new List<int>(indices.Count);

        foreach (int index in indices)
        {
            if (!remap.TryGetValue(index, out int mapped))
            {
                mapped = newVertices.Count;
                remap[index] = mapped;

                newVertices.Add(vertices[index]);
                if (normals.Length > 0) newNormals.Add(normals[index]);
                if (uv.Length > 0) newUv.Add(uv[index]);
                if (tangents.Length > 0) newTangents.Add(tangents[index]);
            }

            newTriangles.Add(mapped);
        }

        var piece = new Mesh { name = name };
        if (newVertices.Count > 65535)
            piece.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        piece.SetVertices(newVertices);
        if (newNormals.Count == newVertices.Count) piece.SetNormals(newNormals);
        if (newUv.Count == newVertices.Count) piece.SetUVs(0, newUv);
        if (newTangents.Count == newVertices.Count) piece.SetTangents(newTangents);
        piece.SetTriangles(newTriangles, 0);

        if (newNormals.Count != newVertices.Count) piece.RecalculateNormals();
        piece.RecalculateBounds();

        return piece;
    }
}
