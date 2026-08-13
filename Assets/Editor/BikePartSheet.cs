using System.IO;
using UnityEditor;
using UnityEngine;

/// PARÇA FÖYÜ ÜRETİR. Model 26 parça olarak geliyor ve adları hiçbir şey söylemiyor
/// (`model_part7`). Ölçü nerede durduğunu söylüyor ama ne olduğunu söylemiyor: pedal ile
/// zincir aynı bölgede, kablo ile fren kolu aynı boyda.
///
/// Her parça sırayla VURGULANIP bütün bisiklet çiziliyor — parça tek başına çizilseydi
/// bağlamı kaybolurdu, bisikletin neresinde olduğu görülmezdi. Kareler tek bir resimde
/// ızgara olarak birleşiyor, sıra parça numarasıyla aynı.
///
/// SAHNENİN GERİSİ DIŞARIDA. Parçalar geçici olarak kendi katmanına alınıp kamera yalnız
/// o katmanı çiziyor; sis de kapatılıyor. İlk sürümde arazi ve sis kadraja doluyor,
/// vurgulanan parça seçilemiyordu.
///
/// GEÇİCİ ARAÇ. Parça eşleşmesi oturunca silinecek (bkz. `DECISIONS.md`).
public static class BikePartSheet
{
    const int Columns = 6;
    const int CellWidth = 560;
    const int CellHeight = 420;

    /// Yalnız föy çizimi için kullanılan katman. Sahnede hiçbir şey bu katmanda durmuyor;
    /// parçalar çizim boyunca buraya alınıp sonra kendi katmanlarına dönüyor.
    const int SheetLayer = 31;

    [MenuItem("To The Summit/Model/Bisiklet/Parça Föyü", false, 122)]
    static void Build()
    {
        var bike = Object.FindAnyObjectByType<BikeController>();
        if (bike == null)
        {
            Debug.LogError("[Föy] sahnede bisiklet yok.");
            return;
        }

        MeshRenderer[] parts = bike.GetComponentsInChildren<MeshRenderer>();

        var material = new Material[parts.Length];
        var layer = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            material[i] = parts[i].sharedMaterial;
            layer[i] = parts[i].gameObject.layer;
            parts[i].gameObject.layer = SheetLayer;
        }

        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var dim = new Material(unlit) { color = new Color(0.30f, 0.31f, 0.34f) };
        var lit = new Material(unlit) { color = new Color(1f, 0.36f, 0.05f) };

        Camera camera = BuildCamera(bike.transform, Frame(parts));
        var target = new RenderTexture(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;

        int rows = Mathf.CeilToInt(parts.Length / (float)Columns);
        var sheet = new Texture2D(Columns * CellWidth, rows * CellHeight, TextureFormat.RGB24, false);

        for (int i = 0; i < parts.Length; i++)
        {
            for (int k = 0; k < parts.Length; k++) parts[k].sharedMaterial = dim;
            parts[i].sharedMaterial = lit;

            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var cell = new Texture2D(CellWidth, CellHeight, TextureFormat.RGB24, false);
            cell.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), 0, 0);
            cell.Apply();

            RenderTexture.active = previous;

            // Izgara SOL ÜSTTEN sağa doğru doluyor; okurken sıra parça numarası oluyor.
            int column = i % Columns;
            int row = i / Columns;
            sheet.SetPixels(column * CellWidth, (rows - 1 - row) * CellHeight,
                CellWidth, CellHeight, cell.GetPixels());

            Object.DestroyImmediate(cell);
        }

        sheet.Apply();

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].sharedMaterial = material[i];
            parts[i].gameObject.layer = layer[i];
        }

        RenderSettings.fog = fog;

        camera.targetTexture = null;
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(dim);
        Object.DestroyImmediate(lit);

        // Föy PROJE DIŞINA yazılıyor: teşhis çıktısı, varlık değil. Assets altına
        // yazılsaydı içe aktarılır, depoya girer ve silinmesi ayrıca iş olurdu.
        string path = Path.Combine(Path.GetTempPath(), "claude",
            "D--ME-game-to-the-summit", "BikeParts.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, sheet.EncodeToPNG());

        Object.DestroyImmediate(sheet);

        Debug.Log($"[Föy] {parts.Length} parça yazıldı: {path}\n"
                + $"  ızgara {Columns} sütun, sıra parça numarasıyla aynı "
                + $"(sol üst = ilk parça).");
    }

    static Bounds Frame(MeshRenderer[] parts)
    {
        Bounds bounds = parts[0].bounds;
        foreach (MeshRenderer part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }

    /// Bisikleti kendi yanından çeken dik izdüşümlü kamera. Yön bisikletin kendi
    /// eksenlerinden alınıyor: dünya eksenine göre kurulsaydı bisiklet doğuş noktasında
    /// hangi yöne bakıyorsa föy o açıdan çıkardı.
    static Camera BuildCamera(Transform bike, Bounds bounds)
    {
        var holder = new GameObject("ParçaFöyüKamerası");
        Camera camera = holder.AddComponent<Camera>();

        float aspect = CellWidth / (float)CellHeight;

        // Kadraj HER İKİ EKSENDEN sıkıştırılıyor: yalnız yüksekliğe bakılsaydı uzun
        // bisiklet yanlardan taşar, yalnız uzunluğa bakılsaydı kare boşluk dolardı.
        float size = Mathf.Max(bounds.extents.y, bounds.extents.magnitude / aspect) * 1.15f;

        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 1 << SheetLayer;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = bounds.size.magnitude * 6f;
        camera.allowHDR = false;

        // Hafif önden ve yukarıdan: tam yandan bakınca sağ ve sol taraftaki eş parçalar
        // üst üste biniyor ve hangisinin vurgulandığı seçilemiyor.
        Vector3 direction = (bike.right * -1f + bike.forward * 0.35f + bike.up * 0.25f).normalized;
        holder.transform.position = bounds.center - direction * bounds.size.magnitude * 2f;
        holder.transform.LookAt(bounds.center, bike.up);

        return camera;
    }
}
