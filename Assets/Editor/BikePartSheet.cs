using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// PARÇA FÖYÜ ÜRETİR. Model 26 parça olarak geliyor ve adları hiçbir şey söylemiyor
/// (`model_part7`). Ölçü nerede durduğunu söylüyor ama ne olduğunu söylemiyor: pedal ile
/// zincir aynı bölgede, kablo ile fren kolu aynı boyda.
///
/// Her parça sırayla VURGULANIP bütün bisiklet çiziliyor — parça tek başına çizilseydi
/// bağlamı kaybolurdu, bisikletin neresinde olduğu görülmezdi. Kareler tek bir resimde
/// ızgara olarak birleşiyor, sıra parça numarasıyla aynı.
///
/// ÇİZİM BORU HATTINI ATLIYOR. Sahne kamerasıyla çekildiğinde arazi, sis ve gölge
/// kadraja doluyordu; katman maskesi de kesmedi çünkü sahnenin kendi çizim eklentileri
/// kameradan bağımsız çalışıyor. Mesh'ler doğrudan çiziliyor: sahnede ne olduğunun
/// föye hiçbir etkisi kalmıyor.
///
/// VURGULANAN PARÇA ÖNDE. Derinlik sınaması kapalı çiziliyor, yani kadronun arkasında
/// kalan pedal ya da zincir de görünüyor — föyün tek işi hangi parçanın nerede olduğunu
/// göstermek.
///
/// GEÇİCİ ARAÇ. Parça eşleşmesi oturunca silinecek (bkz. `DECISIONS.md`).
public static class BikePartSheet
{
    const int Columns = 6;
    const int CellWidth = 560;
    const int CellHeight = 420;

    [MenuItem("To The Summit/Model/Bisiklet/Parça Föyü", false, 122)]
    static void Build()
    {
        var bike = Object.FindAnyObjectByType<BikeController>();
        if (bike == null)
        {
            Debug.LogError("[Föy] sahnede bisiklet yok.");
            return;
        }

        MeshFilter[] parts = bike.GetComponentsInChildren<MeshFilter>();
        Bounds bounds = Frame(bike);

        Material dim = Flat(new Color(0.34f, 0.35f, 0.38f), CompareFunction.LessEqual);
        Material lit = Flat(new Color(1f, 0.36f, 0.05f), CompareFunction.Always);

        Matrix4x4 view = View(bike.transform, bounds);
        Matrix4x4 projection = Projection(bounds);

        var target = new RenderTexture(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32);
        int rows = Mathf.CeilToInt(parts.Length / (float)Columns);
        var sheet = new Texture2D(Columns * CellWidth, rows * CellHeight, TextureFormat.RGB24, false);

        RenderTexture previous = RenderTexture.active;

        for (int i = 0; i < parts.Length; i++)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0.10f, 0.10f, 0.12f));

            GL.PushMatrix();
            GL.LoadProjectionMatrix(projection);
            GL.modelview = view;

            for (int k = 0; k < parts.Length; k++)
            {
                if (k == i) continue;

                dim.SetPass(0);
                Graphics.DrawMeshNow(parts[k].sharedMesh, parts[k].transform.localToWorldMatrix);
            }

            lit.SetPass(0);
            Graphics.DrawMeshNow(parts[i].sharedMesh, parts[i].transform.localToWorldMatrix);

            GL.PopMatrix();

            var cell = new Texture2D(CellWidth, CellHeight, TextureFormat.RGB24, false);
            cell.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), 0, 0);
            cell.Apply();

            // Izgara SOL ÜSTTEN sağa doğru doluyor; okurken sıra parça numarası oluyor.
            int column = i % Columns;
            int row = i / Columns;
            sheet.SetPixels(column * CellWidth, (rows - 1 - row) * CellHeight,
                CellWidth, CellHeight, cell.GetPixels());

            Object.DestroyImmediate(cell);
        }

        RenderTexture.active = previous;
        sheet.Apply();

        // Föy PROJE DIŞINA yazılıyor: teşhis çıktısı, varlık değil. Assets altına
        // yazılsaydı içe aktarılır, depoya girer ve silinmesi ayrıca iş olurdu.
        string path = Path.Combine(Path.GetTempPath(), "claude",
            "D--ME-game-to-the-summit", "BikeParts.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, sheet.EncodeToPNG());

        Object.DestroyImmediate(sheet);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(dim);
        Object.DestroyImmediate(lit);

        Debug.Log($"[Föy] {parts.Length} parça yazıldı: {path}\n"
                + $"  ızgara {Columns} sütun, sıra parça numarasıyla aynı "
                + $"(sol üst = ilk parça).");
    }

    /// Anlık çizimde kullanılabilen düz renk materyali. Projenin kendi gölgelendiricileri
    /// boru hattına bağlı ve `SetPass` ile çizilemiyor.
    static Material Flat(Color colour, CompareFunction depth)
    {
        var material = new Material(Shader.Find("ToTheSummit/FlatColor"));
        material.SetColor("_Color", colour);
        material.SetFloat("_ZTest", (float)depth);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    static Bounds Frame(Component bike)
    {
        var renderers = bike.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    /// Bisikleti kendi yanından gören bakış. Yön bisikletin kendi eksenlerinden alınıyor:
    /// dünya eksenine göre kurulsaydı bisiklet doğuş noktasında hangi yöne bakıyorsa föy
    /// o açıdan çıkardı.
    static Matrix4x4 View(Transform bike, Bounds bounds)
    {
        // TAM YANDAN. Eğik bakış siluetleri üst üste bindiriyor ve hangi parçanın
        // vurgulandığı okunmuyordu; yan görünüşte bisikletin her parçası kendi yerinde.
        Vector3 direction = -bike.right;
        Vector3 position = bounds.center - direction * bounds.size.magnitude * 2f;
        Quaternion rotation = Quaternion.LookRotation(direction, bike.up);

        // Unity kamerası -Z yönüne bakıyor; bakış matrisi bu yüzden Z'si ters çevrilmiş
        // bir dönüşümün tersi olarak kuruluyor.
        return Matrix4x4.TRS(position, rotation, new Vector3(1f, 1f, -1f)).inverse;
    }

    static Matrix4x4 Projection(Bounds bounds)
    {
        float aspect = CellWidth / (float)CellHeight;

        // Kadraj HER İKİ EKSENDEN sıkıştırılıyor: yalnız yüksekliğe bakılsaydı uzun
        // bisiklet yanlardan taşar, yalnız uzunluğa bakılsaydı kare boşluk dolardı.
        float height = Mathf.Max(bounds.extents.y, bounds.extents.magnitude / aspect) * 1.1f;
        float width = height * aspect;
        float far = bounds.size.magnitude * 6f;

        return GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(-width, width, -height, height, 0.01f, far), true);
    }
}
