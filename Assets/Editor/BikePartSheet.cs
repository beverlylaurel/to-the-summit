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
    const int CellWidth = 700;
    const int CellHeight = 520;

    [MenuItem("To The Summit/Model/Bisiklet/Parça Föyü", false, 122)]
    static void Build()
    {
        var bike = Object.FindAnyObjectByType<BikeController>();
        if (bike == null)
        {
            Debug.LogError("[Föy] sahnede bisiklet yok.");
            return;
        }

        // SIRA ADA GÖRE. Hiyerarşi sırası rig kurulurken bozuluyor: tekerlekler ve ön
        // takım kendi pivotlarının altına taşınıyor. Föydeki kare numarası parça
        // numarasıyla aynı olmazsa föy yanlış okunuyor — ilk sürümde tam bunu yaptı.
        MeshFilter[] parts = bike.GetComponentsInChildren<MeshFilter>();
        System.Array.Sort(parts, (a, b) => Number(a.name).CompareTo(Number(b.name)));

        Bounds bounds = Frame(bike);

        // Renkler AYNI ALANDA seçiliyor: temizleme rengi gama dönüşümünden geçiyor,
        // gölgelendiricinin yazdığı renk geçmiyor. İkisi aynı sayıyla verildiğinde
        // zemin ile parça aynı griye düşüyor ve föy okunmuyordu.
        Material dim = Flat(new Color(0.55f, 0.56f, 0.60f), CompareFunction.LessEqual);
        Material lit = Flat(new Color(1f, 0.35f, 0.02f), CompareFunction.Always);

        Matrix4x4 view = View(bike.transform, bounds);
        Matrix4x4 projection = Projection(view, bounds);

        var target = new RenderTexture(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32);
        int rows = Mathf.CeilToInt(parts.Length / (float)Columns);
        var sheet = new Texture2D(Columns * CellWidth, rows * CellHeight, TextureFormat.RGB24, false);

        RenderTexture previous = RenderTexture.active;

        for (int i = 0; i < parts.Length; i++)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, Color.black);

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

        ToolLog.Write($"[Föy] {parts.Length} parça yazıldı: {path}\n"
                + $"  ızgara {Columns} sütun, sıra parça numarasıyla aynı "
                + $"(sol üst = ilk parça).");
    }

    /// Parça adının sonundaki sayı (`model_part7` → 7). Ad sırası düz metin olarak
    /// sıralanırsa `model_part10`, `model_part2`'den önce geliyor.
    static int Number(string name)
    {
        int index = name.Length;
        while (index > 0 && char.IsDigit(name[index - 1])) index--;

        return index < name.Length && int.TryParse(name.Substring(index), out int value)
            ? value : 0;
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

    /// Kadraj SINIR KUTUSUNUN KÖŞELERİNDEN kuruluyor: sekiz köşe bakış uzayına taşınıp
    /// en ve boy oradan okunuyor. Dünya eksenlerine bakılarak kurulduğunda hangi eksenin
    /// bisikletin boyu olduğu varsayım oluyordu ve kadraj arka tekerleği kesiyordu.
    static Matrix4x4 Projection(Matrix4x4 view, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        float left = float.MaxValue, right = float.MinValue;
        float bottom = float.MaxValue, top = float.MinValue;
        float near = float.MaxValue, far = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            Vector3 seen = view.MultiplyPoint3x4(corner);

            left = Mathf.Min(left, seen.x);
            right = Mathf.Max(right, seen.x);
            bottom = Mathf.Min(bottom, seen.y);
            top = Mathf.Max(top, seen.y);

            // Bakış uzayında kamera -Z yönüne bakıyor: uzaklık eksi Z.
            near = Mathf.Min(near, -seen.z);
            far = Mathf.Max(far, -seen.z);
        }

        float margin = Mathf.Max(right - left, top - bottom) * 0.06f;
        left -= margin; right += margin;
        bottom -= margin; top += margin;

        // Kare oranına tamamlama: eksik kalan eksen İKİ YANDAN büyütülüyor, yoksa
        // bisiklet karenin bir kenarına yaslanıyor.
        float aspect = CellWidth / (float)CellHeight;
        float width = right - left;
        float height = top - bottom;

        if (width < height * aspect)
        {
            float pad = (height * aspect - width) * 0.5f;
            left -= pad; right += pad;
        }
        else
        {
            float pad = (width / aspect - height) * 0.5f;
            bottom -= pad; top += pad;
        }

        // Doku hedefi için Y ÇEVİRMESİ İSTENMİYOR: `ReadPixels` zaten alttan okuyor,
        // ikisi üst üste gelince bisiklet baş aşağı çıkıyordu.
        return GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(left, right, bottom, top,
                Mathf.Max(0.01f, near - 1f), far + 1f), false);
    }
}
