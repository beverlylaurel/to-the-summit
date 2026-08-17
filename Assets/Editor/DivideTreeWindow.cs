using UnityEditor;
using UnityEngine;

/// Divide Tree'yi tepeden çizer. Referans repodaki `PlotDivtrees.ipynb`'in karşılığı.
///
/// L0'ın TEK doğrulama aracı: iskelet doğru mu, oyun alanından sırt geçiyor mu, her yön
/// dağ mı yoksa ova/plato ayrımı var mı. Yükseklik haritası (L1) daha yokken bunlar
/// yalnız buradan görülebilir.
///
/// İşi bitince SİLİNMEZ — L1, L2 ve içerik yerleştirmesi boyunca lazım olacak; içerik
/// bu grafiğin düğümlerine çapalanıyor ve hangi düğüm olduğunu görmek gerekiyor.
public class DivideTreeWindow : EditorWindow
{
    DivideTree tree;
    Vector2 pan;
    float halfSpanMetres = 270000f;
    float minProminence;
    bool showSaddles = true;
    bool showRidges = true;

    /// Ölçek kademeleri: bölge, çevre, oyun alanı. Üçü de `DECISIONS.md`'deki üç bantlı
    /// mesafe temsiline karşılık geliyor.
    static readonly (string label, float halfMetres, float prom)[] Zooms =
    {
        ("Bölge 540 km", 270000f, 400f),
        ("Çevre 120 km", 60000f, 150f),
        ("Oyun alanı 24 km", 12000f, 0f),
    };

    [MenuItem("To The Summit/Arazi/Divide Tree Penceresi", false, 11)]
    static void Open() => GetWindow<DivideTreeWindow>("Divide Tree").minSize = new Vector2(560f, 620f);

    void OnEnable() => Acquire();

    void Acquire() => tree = AssetDatabase.LoadAssetAtPath<DivideTree>("Assets/Terrain/DivideTree.asset");

    void OnGUI()
    {
        if (tree == null || tree.peaks == null || tree.peaks.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Divide Tree asset'i yok ya da boş.\n\n" +
                "To The Summit / Arazi / Divide Tree'yi İçe Aktar",
                MessageType.Info);
            if (GUILayout.Button("Tekrar ara")) Acquire();
            return;
        }

        DrawToolbar();
        Rect canvas = GUILayoutUtility.GetRect(position.width, position.height - 92f);
        DrawTree(canvas);
        DrawReadout();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            foreach (var z in Zooms)
                if (GUILayout.Button(z.label, EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    halfSpanMetres = z.halfMetres;
                    minProminence = z.prom;
                    pan = Vector2.zero;
                }

            GUILayout.FlexibleSpace();
            showRidges = GUILayout.Toggle(showRidges, "Sırtlar", EditorStyles.toolbarButton, GUILayout.Width(70f));
            showSaddles = GUILayout.Toggle(showSaddles, "Boyunlar", EditorStyles.toolbarButton, GUILayout.Width(78f));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"En küçük prominence {minProminence:F0} m", GUILayout.Width(190f));
            minProminence = GUILayout.HorizontalSlider(minProminence, 0f, 1200f);
        }
    }

    /// Dünya (doğu, kuzey) → ekran. Kuzey YUKARI: ekranın y'si aşağı arttığı için
    /// kuzey ters çevriliyor. Bu bir kez burada yapılıyor; başka yerde ikinci bir
    /// çevrim yok.
    Vector2 ToScreen(Rect r, float east, float north)
    {
        float s = Mathf.Min(r.width, r.height) * 0.5f / halfSpanMetres;
        return new Vector2(r.center.x + east * s + pan.x,
                           r.center.y - north * s + pan.y);
    }

    void DrawTree(Rect r)
    {
        if (Event.current.type != EventType.Repaint)
        {
            HandlePan(r);
            return;
        }

        EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.12f));
        GUI.BeginClip(r);
        Rect local = new Rect(0f, 0f, r.width, r.height);
        Handles.BeginGUI();

        float lim = halfSpanMetres * 1.25f;
        bool Visible(float e, float n) => Mathf.Abs(e) < lim && Mathf.Abs(n) < lim;

        if (showRidges)
        {
            Handles.color = new Color(0.88f, 0.55f, 0.14f, 0.75f);
            for (int i = 0; i < tree.saddles.Length; i++)
            {
                var s = tree.saddles[i];
                if (!Visible(s.east, s.north)) continue;
                var a = tree.peaks[s.peakA];
                var b = tree.peaks[s.peakB];
                if (a.prominence < minProminence && b.prominence < minProminence) continue;

                Vector2 ps = ToScreen(local, s.east, s.north);
                Handles.DrawLine(ToScreen(local, a.east, a.north), ps);
                Handles.DrawLine(ToScreen(local, b.east, b.north), ps);
            }
        }

        if (showSaddles)
        {
            Handles.color = new Color(0.85f, 0.25f, 0.25f, 0.85f);
            for (int i = 0; i < tree.saddles.Length; i++)
            {
                var s = tree.saddles[i];
                if (!Visible(s.east, s.north)) continue;
                if (tree.peaks[s.peakA].prominence < minProminence &&
                    tree.peaks[s.peakB].prominence < minProminence) continue;
                Vector2 p = ToScreen(local, s.east, s.north);
                Handles.DrawSolidDisc(p, Vector3.forward, 1.6f);
            }
        }

        float top = tree.summitElevation;
        for (int i = 0; i < tree.peaks.Length; i++)
        {
            var p = tree.peaks[i];
            if (p.prominence < minProminence || !Visible(p.east, p.north)) continue;

            float t = Mathf.Clamp01(p.elevation / Mathf.Max(1f, top));
            Handles.color = Color.Lerp(new Color(0.25f, 0.55f, 0.35f),
                                       new Color(0.98f, 0.98f, 0.94f), t * t);
            float radius = 1.6f + 5.5f * t * t * t;
            Handles.DrawSolidDisc(ToScreen(local, p.east, p.north), Vector3.forward, radius);
        }

        // Oyun alanı: oynanan arazinin sınırı
        Handles.color = new Color(0.95f, 0.25f, 0.25f);
        float h = tree.PlayHalfSize;
        Vector2 a0 = ToScreen(local, -h, -h), a1 = ToScreen(local, h, -h);
        Vector2 a2 = ToScreen(local, h, h), a3 = ToScreen(local, -h, h);
        Handles.DrawLine(a0, a1); Handles.DrawLine(a1, a2);
        Handles.DrawLine(a2, a3); Handles.DrawLine(a3, a0);

        Handles.EndGUI();
        GUI.EndClip();
    }

    void HandlePan(Rect r)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDrag && r.Contains(e.mousePosition))
        {
            pan += e.delta;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
        {
            halfSpanMetres = Mathf.Clamp(halfSpanMetres * (1f + e.delta.y * 0.05f), 2000f, 400000f);
            e.Use();
            Repaint();
        }
    }

    void DrawReadout()
    {
        int summit = tree.SummitId;
        var s = tree.peaks[summit];
        float d = Mathf.Sqrt(s.east * s.east + s.north * s.north);

        int shown = 0, inPlay = 0;
        for (int i = 0; i < tree.peaks.Length; i++)
        {
            if (tree.peaks[i].prominence >= minProminence) shown++;
            if (tree.InPlayArea(tree.peaks[i])) inPlay++;
        }

        EditorGUILayout.LabelField(
            $"zirve {tree.peaks.Length}  ·  çizilen {shown}  ·  oyun alanında {inPlay}  ·  " +
            $"tohum {tree.seed}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"en yüksek #{summit}: {s.elevation:F0} m, merkezden {d:F0} m  ·  " +
            $"görüş {halfSpanMetres * 2f / 1000f:F0} km  ·  sürükle: kaydır, tekerlek: yakınlaş",
            EditorStyles.miniLabel);
    }
}
