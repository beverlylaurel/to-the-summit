using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// BÖLGE FÖYÜ ÜRETİR. Bölgeli parçalarda (gidon, bagaj, pedal) sınırın nereye düştüğü
/// oyunda ancak "biraz taşmış" diye anlaşılıyor; sayıyı o tarife göre oynatmak tur
/// katlıyor. Föy sınırı doğrudan gösteriyor: her alt-mesh ayrı renk, parça kadrajı
/// dolduruyor, iki açıdan.
///
/// Renkler: 0 gri, 1 turuncu, 2 mavi — kurulum betiğindeki materyal sırasıyla aynı.
///
/// GEÇİCİ ARAÇ. Sınırlar oturunca silinecek (bkz. `DECISIONS.md`).
public static class BikeZoneSheet
{
    const int CellWidth = 700;
    const int CellHeight = 520;

    static readonly string[] Parts = { "model_part8", "model_part10", "model_part11" };

    static readonly Color[] Zones =
    {
        new Color(0.55f, 0.56f, 0.60f),
        new Color(1f, 0.35f, 0.02f),
        new Color(0.10f, 0.65f, 1f),
    };

    [MenuItem("To The Summit/Model/Bisiklet/Bölge Föyü", false, 123)]
    static void Build()
    {
        var bike = Object.FindAnyObjectByType<BikeController>();
        if (bike == null)
        {
            Debug.LogError("[Bölge] sahnede bisiklet yok.");
            return;
        }

        var materials = new Material[Zones.Length];
        for (int i = 0; i < Zones.Length; i++) materials[i] = Flat(Zones[i]);

        var target = new RenderTexture(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32);
        var sheet = new Texture2D(CellWidth * 2, CellHeight * Parts.Length,
            TextureFormat.RGB24, false);

        RenderTexture previous = RenderTexture.active;
        var report = new System.Text.StringBuilder("[Bölge] föy yazıldı");

        for (int p = 0; p < Parts.Length; p++)
        {
            Transform part = Find(bike.transform, Parts[p]);
            if (part == null) continue;

            var filter = part.GetComponent<MeshFilter>();
            Bounds bounds = part.GetComponent<Renderer>().bounds;

            report.Append($"\n  {Parts[p]}: {filter.sharedMesh.subMeshCount} bölge");

            for (int view = 0; view < 2; view++)
            {
                // İki bakış: yandan ve önden. Tutamak yandan bakınca bara karışıyor,
                // önden bakınca uçtaki sınır seçiliyor.
                Vector3 direction = view == 0 ? -bike.transform.right : -bike.transform.forward;
                Matrix4x4 look = View(direction, bike.transform.up, bounds);

                Graphics.SetRenderTarget(target);
                GL.Clear(true, true, Color.black);

                GL.PushMatrix();
                GL.LoadProjectionMatrix(Projection(look, bounds));
                GL.modelview = look;

                for (int zone = 0; zone < filter.sharedMesh.subMeshCount; zone++)
                {
                    materials[Mathf.Min(zone, materials.Length - 1)].SetPass(0);
                    Graphics.DrawMeshNow(filter.sharedMesh,
                        part.localToWorldMatrix, zone);
                }

                GL.PopMatrix();

                var cell = new Texture2D(CellWidth, CellHeight, TextureFormat.RGB24, false);
                cell.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), 0, 0);
                cell.Apply();

                sheet.SetPixels(view * CellWidth, (Parts.Length - 1 - p) * CellHeight,
                    CellWidth, CellHeight, cell.GetPixels());

                Object.DestroyImmediate(cell);
            }
        }

        RenderTexture.active = previous;
        sheet.Apply();

        string path = Path.Combine(Path.GetTempPath(), "claude",
            "D--ME-game-to-the-summit", "BikeZones.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, sheet.EncodeToPNG());

        Object.DestroyImmediate(sheet);
        Object.DestroyImmediate(target);
        foreach (Material material in materials) Object.DestroyImmediate(material);

        ToolLog.Write($"{report}\n  {path}\n  satırlar: {string.Join(", ", Parts)} "
                + "(üstten alta), sol sütun yandan, sağ sütun önden.");
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
            if (child.name == name) return child;

        return null;
    }

    static Material Flat(Color colour)
    {
        var material = new Material(Shader.Find("ToTheSummit/FlatColor"));
        material.SetColor("_Color", colour);
        material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    static Matrix4x4 View(Vector3 direction, Vector3 up, Bounds bounds)
    {
        Vector3 position = bounds.center - direction * bounds.size.magnitude * 3f;
        return Matrix4x4.TRS(position, Quaternion.LookRotation(direction, up),
            new Vector3(1f, 1f, -1f)).inverse;
    }

    /// Kadraj sınır kutusunun köşelerinden: parça hangi eksende uzun olursa olsun
    /// kareyi dolduruyor.
    static Matrix4x4 Projection(Matrix4x4 view, Bounds bounds)
    {
        Vector3 min = bounds.min, max = bounds.max;
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

            left = Mathf.Min(left, seen.x); right = Mathf.Max(right, seen.x);
            bottom = Mathf.Min(bottom, seen.y); top = Mathf.Max(top, seen.y);
            near = Mathf.Min(near, -seen.z); far = Mathf.Max(far, -seen.z);
        }

        float margin = Mathf.Max(right - left, top - bottom) * 0.08f;
        left -= margin; right += margin; bottom -= margin; top += margin;

        float aspect = CellWidth / (float)CellHeight;
        float width = right - left, height = top - bottom;

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

        return GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(left, right, bottom, top,
                Mathf.Max(0.01f, near - 1f), far + 1f), false);
    }
}
