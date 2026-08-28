using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Mountain building workbench: starts from flat plain, shapes via brushes and batch operations,
/// exports to terrain heightmap.
///
/// TWO GRIDS: Editing grid at 1025^2 (29.3 m/cell), preview grid at 513^2.
/// Export upsamples to 4097^2.
public class MountainBuilderWindow : EditorWindow
{
    const int Grid = 1025;
    const int View = 513;
    const int Export = 4097;
    const float ArenaM = 30000f;
    const float MaxM = 8000f;   // Terrain vertical ceiling (`TerrainData.size.y`)

    const string SculptDir = "Assets/Terrain/Sculpts";
    const string AutoName = "_latest";
    const string RescueName = "_rescue";

    bool dirtySinceSave;
    double lastAutoSave;

    static float CellM => ArenaM / (Grid - 1);

    enum Tab { Brush, Ops, Mask, Route, Measure, Save }
    enum BrushKind { Raise, Lower, Smooth, Flatten, Ridge, Valley, Erode, Sharpen, Noise }
    enum MaskKind { None, Height, Slope, Ridges, Valleys }

    static readonly string[] TabNames =
    { "Brush", "Operations", "Mask", "Route", "Measure", "Save" };

    static readonly string[] BrushNames =
    { "Raise", "Lower", "Smooth", "Flatten", "Ridge", "Valley", "Erode", "Sharpen", "Noise" };

    static readonly string[] BrushHelp =
    {
        // Raise
        "TARGET: Mountain mass. Click center with large radius (2-4 km) to build main mass.\n"
        + "Use smaller radius for shoulders, steps, and sub-peaks.\n"
        + "TIP: Avoid heavy single stamps; layered light passes look more natural.",

        // Lower
        "TARGET: Hollows within terrain mass — cirques (glacial bowls below peaks),\n"
        + "cols / saddles between peaks, lowland lake beds.\n"
        + "For valleys use the Valley brush: valleys are linear channels, not radial bowls.",

        // Smooth
        "TARGET: Brush seam artifacts, jagged stepping, single-cell spikes.\n"
        + "Softens harsh edges remaining after erosion passes.\n"
        + "CAUTION: Overusing wide-radius smoothing flattens fine mountain relief.",

        // Flatten
        "TARGET: Campsite benches, ridge shoulders, cols / saddles between peaks,\n"
        + "valley terraces. In real mountains, flats form on RIDGES and COLS —\n"
        + "flat benches mid-cliff look unnatural.\n"
        + "Click at desired target elevation first, then brush outward.",

        // Ridge
        "TARGET: Downward radial spines radiating from peak. In real mountains,\n"
        + "ridges descend from summit down to valley floors (3-5 major ridges).\n"
        + "Ridges DIVIDE VALLEYS: every two valleys are separated by a ridge.\n"
        + "USAGE: Aspect 3-5, align Angle to direction, drag outward from summit.\n"
        + "Routes follow ridges (25-30 deg) rather than sheer walls.",

        // Valley
        "TARGET: BETWEEN TWO RIDGES, from top to bottom. Valleys never cross peaks\n"
        + "and never run uphill — valleys follow natural drainage pathways.\n"
        + "Narrow and steep near tops, widening and flattening downhill.\n"
        + "USAGE: Aspect 3-5, start below summit, drag to base plain.",

        // Erode
        "TARGET: Overly steep faces and razor edges. Lowers faces exceeding angle\n"
        + "of repose (38 deg) while preserving ridge axes.",

        // Sharpen
        "TARGET: Ridge axes softened after erosion or smoothing.\n"
        + "Amplifies existing relief — draw ridge spine first, then sharpen.",

        // Noise
        "TARGET: Overly smooth slopes and foreland plains. Provides rolling mounds.\n"
        + "Use low strength across broad areas; avoid heavy application near summit.",
    };

    // --- Data
    float[] h;
    Mesh mesh;
    Material mat;

    Vector3[] vVerts;
    Color[] vCols;
    bool topoBuilt;

    int pdx0 = int.MaxValue, pdz0 = int.MaxValue, pdx1 = int.MinValue, pdz1 = int.MinValue;

    float[] heat;
    int hx0 = int.MaxValue, hz0 = int.MaxValue, hx1 = int.MinValue, hz1 = int.MinValue;
    double lastHeatTick;
    PreviewRenderUtility prev;

    // --- Brush
    BrushKind brush = BrushKind.Raise;
    float radiusM = 900f, strength = 0.5f, hardness = 0.35f, aspect = 1f, angleDeg = 0f;
    float flattenTarget = float.NaN;

    // --- Mask
    MaskKind maskKind = MaskKind.None;
    float maskLo = 500f, maskHi = 4000f, maskFeather = 200f;
    float maskSlopeLo = 0f, maskSlopeHi = 30f, maskSlopeFeather = 5f;
    float maskCurveRadius = 400f, maskCurveStrength = 40f;
    bool maskPreview;
    float[] maskCache;

    // --- Operation Settings
    float opTalus = 36f, opRate = 0.5f; int opIters = 60;
    int hydDroplets = 120000, hydSteps = 30;
    float hydInertia = 0.35f, hydCapacity = 4f, hydErode = 0.3f, hydDeposit = 0.3f,
          hydEvap = 0.01f, hydGravity = 4f;
    int hydBrush = 5;
    float noiseWl = 1200f, noiseAmp = 120f, noisePers = 0.5f, noiseLac = 2f; int noiseOct = 6;
    float warpWl = 3000f, warpAmp = 400f;

    // Naturalize settings
    float natSeedWl = 2000f, natSeedAmp = 1200f, natSeedPers = 0.75f;
    int natSeedOct = 7;
    float natFlowK = 0.15f, natFlowDiffuse = 0.20f;
    int natFlowIters = 6;
    float natGlacierFrom = 3800f, natGlacierGain = 0.5f, natGlacierRadius = 700f;
    float natStrength = 0.4f;
    float terraceStep = 120f, terraceSharp = 0.6f;
    float sharpRadius = 200f, sharpGain = 1.6f;
    float remapMin = 0f, remapMax = 5709f;
    float coneRadius = 9000f, coneHeight = 4500f;
    float apronInner = 7500f, apronOuter = 14500f, apronKeep = 0.12f;
    float calmBelow = 900f, calmFeather = 350f, calmKeep = 0.18f, calmScale = 700f;
    float plainM = 0f;

    bool fineDetail = true;
    float fineWavelength = 420f, fineAmplitude = 26f;
    int fineOctaves = 5;
    float fineSteepBias = 0.7f;
    int opSeed = 12345;

    // --- Camera / State
    float yaw = 35f, pitch = 30f, vScale = 1f, viewShare = 0.5f;
    Vector3 flyPos = new Vector3(0f, 12f, -26f);   // km
    float flySpeed = 1.2f;                          // km/s
    readonly HashSet<KeyCode> keysDown = new HashSet<KeyCode>();
    double lastFlyTick;
    Tab tab = Tab.Brush;
    bool painting, meshDirty;
    double lastMeshBuild;
    Vector3 cursor; bool cursorValid;

    Vector3 camPos; Quaternion camRot; float camFov = 32f; Rect viewRect;

    Vector2 scroll;
    string sculptName = "mountain";

    [System.Serializable]
    class RoutePath
    {
        public string name;
        public Color color;
        public List<Vector2> pts = new List<Vector2>();
    }

    [SerializeField] RoutePath[] paths =
    {
        new RoutePath { name = "Bus road", color = new Color(1.00f, 0.60f, 0.10f) },
        new RoutePath { name = "Climbing route", color = new Color(0.95f, 0.25f, 0.25f) },
    };

    int activePath;
    float routeSpacingM = 25f;
    float routeRadiusM = 3.2f;
    bool drawingRoute;

    struct RouteEdit { public int path; public List<Vector2> pts; }
    readonly List<RouteEdit> routeUndo = new List<RouteEdit>();
    readonly List<RouteEdit> routeRedo = new List<RouteEdit>();
    int strokeStartCount;
    [SerializeField] Vector2 spawn = new Vector2(float.NaN, float.NaN);
    bool placingSpawn;

    Texture2D blankCursor;
    bool cursorHidden;
    string info = "", report = "", stats = "";
    readonly HashSet<string> openHelp = new HashSet<string>();

    struct Edit { public int x0, z0, w, d; public float[] before, after; }
    readonly List<Edit> undoStack = new List<Edit>();
    readonly List<Edit> redoStack = new List<Edit>();
    const int UndoLimit = 40;
    float[] strokeSnapshot;
    int sx0, sz0, sx1, sz1;

    [MenuItem("To The Summit/Terrain/Mountain Builder", false, 12)]
    static void Open()
    {
        var w = GetWindow<MountainBuilderWindow>("Mountain Builder");
        w.minSize = new Vector2(620f, 760f);
        w.Show();
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        EditorApplication.update += Fly;
        if (h == null && !LoadSculpt(AutoName)) NewFlat();
        else if (h != null && RoutesEmpty()) LoadRoutesOnly(AutoName);
    }

    void OnDisable()
    {
        EditorApplication.update -= Fly;
        keysDown.Clear();

        if (h != null) SaveSculpt(AutoName);
        ShowSystemCursor();
        if (blankCursor != null) { DestroyImmediate(blankCursor); blankCursor = null; }
        if (prev != null) { prev.Cleanup(); prev = null; }
        if (mesh != null) { DestroyImmediate(mesh); mesh = null; topoBuilt = false; }
        if (mat != null) { DestroyImmediate(mat); mat = null; }
    }

    // ==================================================================== UI

    void OnGUI()
    {
        HandleShortcuts();
        DrawToolbar();
        DrawViewport();
        DrawInfoBar();

        tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, GUILayout.Height(24f));

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case Tab.Brush: DrawBrushTab(); break;
            case Tab.Ops: DrawOpsTab(); break;
            case Tab.Mask: DrawMaskTab(); break;
            case Tab.Route: DrawRouteTab(); break;
            case Tab.Measure: DrawMeasureTab(); break;
            case Tab.Save: DrawSaveTab(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            bool routeMode = tab == Tab.Route;
            using (new EditorGUI.DisabledScope((routeMode ? routeUndo.Count : undoStack.Count) == 0))
                if (GUILayout.Button("↶ Undo", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                { if (routeMode) RouteUndo(); else Undo(); }
            using (new EditorGUI.DisabledScope((routeMode ? routeRedo.Count : redoStack.Count) == 0))
                if (GUILayout.Button("↷ Redo", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                { if (routeMode) RouteRedo(); else Redo(); }

            GUILayout.Space(10f);
            if (GUILayout.Button("Flat Plain", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                NewFlat();
            plainM = EditorGUILayout.FloatField(plainM, EditorStyles.toolbarTextField,
                                                GUILayout.Width(44f));
            GUILayout.Label("m", EditorStyles.miniLabel, GUILayout.Width(12f));

            GUILayout.Space(10f);
            GUILayout.Label("vertical scale", EditorStyles.miniLabel, GUILayout.Width(76f));
            vScale = GUILayout.HorizontalSlider(vScale, 1f, 4f, GUILayout.Width(70f));
            GUILayout.Label("view split", EditorStyles.miniLabel, GUILayout.Width(74f));
            viewShare = GUILayout.HorizontalSlider(viewShare, 0.25f, 0.75f, GUILayout.Width(70f));

            GUILayout.Space(10f);
            GUILayout.Label($"speed {flySpeed:F1} km/s", EditorStyles.miniLabel, GUILayout.Width(78f));
            if (GUILayout.Button("reset camera", EditorStyles.toolbarButton, GUILayout.Width(96f)))
            { flyPos = new Vector3(0f, 12f, -26f); yaw = 35f; pitch = 30f; flySpeed = 1.2f; Repaint(); }

            GUILayout.FlexibleSpace();
            GUILayout.Label("WASD fly · Q/E up/down · Shift fast · Right click look · "
                            + "Scroll speed · Left click paint",
                            EditorStyles.miniLabel);
        }
    }

    void Section(string title, string help)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            bool on = openHelp.Contains(title);
            if (GUILayout.Button(on ? "ⓘ hide" : "ⓘ info", EditorStyles.miniButton,
                                 GUILayout.Width(74f)))
            {
                if (on) openHelp.Remove(title); else openHelp.Add(title);
            }
        }
        if (openHelp.Contains(title)) EditorGUILayout.HelpBox(help, MessageType.Info);
    }

    static float Slider(string label, string tip, float v, float lo, float hi)
        => EditorGUILayout.Slider(new GUIContent(label, tip), v, lo, hi);

    static int IntSlider(string label, string tip, int v, int lo, int hi)
        => EditorGUILayout.IntSlider(new GUIContent(label, tip), v, lo, hi);

    // ------------------------------------------------------------- Brush

    void DrawBrushTab()
    {
        Section("Mountain anatomy — placement guide",
            "A mountain follows structural geological rules:\n\n"
            + "SUMMIT — Single peak point where ridge spines converge.\n"
            + "RIDGE  — Radial descending spines radiating outward (3-5 spines).\n"
            + "         Climbing routes follow ridges (25-30 deg).\n"
            + "VALLEY — DRAINAGE BETWEEN TWO RIDGES. Narrow/steep at top, wide at base.\n"
            + "COL    — Saddle dip between peaks. Formed with Flatten brush.\n"
            + "APRON  — Foot transition into plain. Gradient attenuates rapidly.\n"
            + "PLAIN  — Rolling lowland foreland.\n\n"
            + "ORDER: Mass first (Raise) -> Ridges -> Valleys -> Erosion -> Surface detail.\n\n"
            + "RULE: Every valley sits between two ridges (ridge-valley-ridge-valley alternating).");

        Section("Brush",
            "Left click paints on terrain. Wire ring indicates brush footprint and orientation.\n\n"
            + "Large radius for mountain mass, small radius for detail features.\n"
            + "Ridge and Valley brushes use stretched aspect ratio (3-5): set Angle along drawing direction.");

        int b = GUILayout.SelectionGrid((int)brush, BrushNames, 3, GUILayout.Height(60f));
        if (b != (int)brush) { brush = (BrushKind)b; Repaint(); }
        EditorGUILayout.HelpBox(BrushHelp[(int)brush], MessageType.None);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            radiusM = Slider("Radius (m)", "Brush influence radius.", radiusM, 60f, 8000f);
            if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                radiusM = Mathf.Max(60f, radiusM * 0.8f);
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                radiusM = Mathf.Min(8000f, radiusM * 1.25f);
        }

        strength = Slider("Strength", "Amount modified per stroke.", strength, 0.02f, 1f);
        hardness = Slider("Hardness", "0 = soft radial falloff, 1 = hard disk edge.", hardness, 0f, 1f);
        aspect = Slider("Aspect", "1 = circular. Values > 1 stretch brush along angle axis.", aspect, 1f, 8f);
        using (new EditorGUI.DisabledScope(aspect <= 1.01f))
            angleDeg = Slider("Angle (deg)", "Orientation angle of brush elongation.", angleDeg, 0f, 180f);

        EditorGUILayout.LabelField($"Footprint: {radiusM:F0} x {radiusM / aspect:F0} m  ·  "
                                   + $"Cell: {CellM:F1} m", EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------- Operations

    void DrawOpsTab()
    {
        EditorGUILayout.HelpBox(
            "Operations apply to the ENTIRE terrain (or masked area if mask is active).", MessageType.None);

        opSeed = EditorGUILayout.IntField(
            new GUIContent("Seed", "Random seed for procedural generation and erosion."), opSeed);

        EditorGUILayout.Space(8f);
        Section("★ Naturalize — One-click",
            "Closes the measured gap between sculpted mass and realistic alpine morphology.\n\n"
            + "THREE PHASES:\n"
            + "1. SEED — 60-2000 m mid-scale structural wrinkles.\n"
            + "2. STREAM INCISION — Carves dendritic drainage networks proportional to catchment area.\n"
            + "3. GLACIAL CARVING — Sharpens cirque hollows and arête ridges above 3800 m.\n\n"
            + "Preserves base plain (elevations below 450 m untouched). Fully undoable (Ctrl+Z).");

        natStrength = Slider("Strength", "0 = untouched, 1 = full dosage.", natStrength, 0f, 1f);

        using (new EditorGUI.DisabledScope(h == null))
        if (GUILayout.Button("★ Naturalize", GUILayout.Height(34f)))
            Naturalize();

        if (!string.IsNullOrEmpty(natDiag))
            EditorGUILayout.TextArea(natDiag, EditorStyles.label);

        EditorGUILayout.Space(6f);
        Section("Thermal erosion",
            "Loose material exceeding talus slope angle slips downhill. Lowers cliffs while PRESERVING RIDGES.");
        opTalus = Slider("Talus angle (deg)", "Slopes steeper than this angle erode.", opTalus, 25f, 55f);
        opRate = Slider("Rate", "Material transport fraction per iteration.", opRate, 0.02f, 0.5f);
        opIters = IntSlider("Iterations", "Number of simulation passes.", opIters, 1, 120);
        if (GUILayout.Button("Apply Thermal Erosion", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Thermal(g, Grid, CellM, opTalus, opRate, opIters, Mask()));

        EditorGUILayout.Space(8f);
        Section("Hydraulic erosion",
            "Droplet simulation carving valleys, rills, and alluvial fans.");
        hydDroplets = IntSlider("Droplets", "Droplet count.", hydDroplets, 5000, 400000);
        hydSteps = IntSlider("Droplet lifetime", "Max steps per droplet.", hydSteps, 8, 128);
        hydInertia = Slider("Inertia", "Directional inertia avoiding grid artifacts.", hydInertia, 0f, 0.95f);
        hydBrush = IntSlider("Brush radius (cells)", "Erosion brush radius.", hydBrush, 1, 10);
        hydCapacity = Slider("Capacity", "Sediment carrying capacity.", hydCapacity, 0.5f, 16f);
        hydErode = Slider("Erosion rate", "Sediment pickup rate.", hydErode, 0.02f, 1f);
        hydDeposit = Slider("Deposition rate", "Sediment settling rate.", hydDeposit, 0.02f, 1f);
        hydEvap = Slider("Evaporation", "Evaporation rate per step.", hydEvap, 0.001f, 0.1f);
        hydGravity = Slider("Gravity", "Acceleration coefficient.", hydGravity, 0.5f, 12f);
        if (GUILayout.Button("Apply Hydraulic Erosion", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Hydraulic(g, Grid, CellM, hydDroplets, hydSteps, hydInertia,
                                            hydCapacity, hydErode, hydDeposit, hydEvap,
                                            hydGravity, opSeed, hydBrush, Mask()));

        EditorGUILayout.Space(8f);
        Section("Fractal noise", "Multiscale rotated octave noise.");
        noiseWl = Slider("Wavelength (m)", "Base octave wavelength.", noiseWl, 120f, 8000f);
        noiseOct = IntSlider("Octaves", "Octave count.", noiseOct, 1, 10);
        noiseAmp = Slider("Amplitude (m)", "Total amplitude.", noiseAmp, 2f, 800f);
        noisePers = Slider("Persistence", "Amplitude decay per octave.", noisePers, 0.2f, 0.8f);
        noiseLac = Slider("Lacunarity", "Frequency multiplier per octave.", noiseLac, 1.6f, 3f);
        if (GUILayout.Button("Add Noise", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.FractalNoise(g, Grid, CellM, noiseWl, noiseOct, noiseAmp,
                                               noisePers, noiseLac, opSeed, Mask()));

        EditorGUILayout.Space(8f);
        Section("Domain warp", "Horizontally distorts terrain via noise.");
        warpWl = Slider("Warp wavelength (m)", "Warp scale.", warpWl, 300f, 12000f);
        warpAmp = Slider("Warp amplitude (m)", "Warp strength.", warpAmp, 10f, 2000f);
        if (GUILayout.Button("Warp", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Warp(g, Grid, CellM, warpWl, warpAmp, opSeed, Mask()));

        EditorGUILayout.Space(8f);
        Section("Terrace / Stratification", "Quantizes elevations into stepped terraces.");
        terraceStep = Slider("Step height (m)", "Elevation step interval.", terraceStep, 10f, 600f);
        terraceSharp = Slider("Sharpness", "0 = soft ramp, 1 = cliff edge.", terraceSharp, 0f, 1f);
        if (GUILayout.Button("Apply Terraces", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Terrace(g, Grid, terraceStep, terraceSharp, Mask()));

        EditorGUILayout.Space(8f);
        Section("Sharpen / Smooth", "High-pass spatial gain filtering.");
        sharpRadius = Slider("Radius (m)", "Filter radius.", sharpRadius, 60f, 2000f);
        sharpGain = Slider("Gain", "1 = identity, > 1 sharpens, < 1 smooths.", sharpGain, 0.2f, 3f);
        if (GUILayout.Button("Apply Filter", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Sharpen(g, Grid, CellM, sharpRadius, sharpGain, Mask()));

        EditorGUILayout.Space(8f);
        Section("Apron transition",
            "Smoothly attenuates outer perimeter band toward base plain using Chebyshev bounds.");
        apronInner = Slider("Inner radius (m)", "Inner boundary where mountain mass ends.", apronInner, 3000f, 14000f);
        apronOuter = Slider("Outer radius (m)", "Outer boundary where plain level is reached.", apronOuter, 5000f, 15000f);
        apronKeep = Slider("Relief retention", "Fraction of relief retained at outer boundary.", apronKeep, 0f, 0.5f);
        if (GUILayout.Button("Apply Apron Transition", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Apron(g, Grid, CellM, apronInner, apronOuter, plainM, apronKeep));

        EditorGUILayout.Space(8f);
        Section("Calm lowland", "Dampens relief below target elevation toward local mean.");
        calmBelow = Slider("Elevation ceiling (m)", "Elevations below this threshold are calmed.", calmBelow, 50f, 3000f);
        calmFeather = Slider("Feather (m)", "Transition feather band.", calmFeather, 20f, 1000f);
        calmKeep = Slider("Relief retention", "0 = flat, 1 = untouched.", calmKeep, 0f, 1f);
        calmScale = Slider("Scale (m)", "Features larger than this scale are preserved.", calmScale, 100f, 2000f);
        if (GUILayout.Button("Calm Lowland", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.CalmLowland(g, Grid, CellM, calmBelow, calmFeather, calmKeep, calmScale));

        EditorGUILayout.Space(8f);
        Section("Elevation remap", "Remaps full terrain elevations between min and max bounds.");
        remapMin = Slider("Min (m)", "Plain level.", remapMin, 0f, 2000f);
        remapMax = Slider("Max (m)", "Peak level.", remapMax, 500f, MaxM);
        if (GUILayout.Button("Remap Elevation Range", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Remap(g, remapMin, remapMax, Mask()));

        EditorGUILayout.Space(8f);
        Section("Initial cone stamp", "Stamps quadratic cone tangent to base plain.");
        coneRadius = Slider("Radius (m)", "Cone base radius.", coneRadius, 1000f, 15000f);
        coneHeight = Slider("Height (m)", "Peak height.", coneHeight, 100f, MaxM);
        if (GUILayout.Button("Stamp Cone", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Cone(g, Grid, CellM, (Grid - 1) * 0.5f, (Grid - 1) * 0.5f,
                                       coneRadius, coneHeight, Mask()));
    }

    // ------------------------------------------------------------- Mask

    void DrawMaskTab()
    {
        Section("Mask",
            "Restricts where operations apply (e.g., above 3000 m, steep cliffs only, ridges only).\n"
            + "Enable preview to highlight mask in yellow.");

        var k = (MaskKind)EditorGUILayout.EnumPopup(
            new GUIContent("Type", "Mask generator mode."), maskKind);
        if (k != maskKind) { maskKind = k; maskCache = null; meshDirty = true; }

        EditorGUI.BeginChangeCheck();
        switch (maskKind)
        {
            case MaskKind.Height:
                maskLo = Slider("Min elevation (m)", "", maskLo, 0f, MaxM);
                maskHi = Slider("Max elevation (m)", "", maskHi, 0f, MaxM);
                maskFeather = Slider("Feather (m)", "", maskFeather, 1f, 800f);
                break;
            case MaskKind.Slope:
                maskSlopeLo = Slider("Min slope (deg)", "", maskSlopeLo, 0f, 90f);
                maskSlopeHi = Slider("Max slope (deg)", "", maskSlopeHi, 0f, 90f);
                maskSlopeFeather = Slider("Feather (deg)", "", maskSlopeFeather, 0.5f, 20f);
                break;
            case MaskKind.Ridges:
            case MaskKind.Valleys:
                maskCurveRadius = Slider("Scale (m)", "Curvature filter radius.", maskCurveRadius, 60f, 3000f);
                maskCurveStrength = Slider("Threshold (m)", "Curvature amplitude threshold.", maskCurveStrength, 2f, 300f);
                break;
        }
        if (EditorGUI.EndChangeCheck()) { maskCache = null; if (maskPreview) meshDirty = true; }

        bool p = EditorGUILayout.ToggleLeft("Preview mask (yellow)", maskPreview);
        if (p != maskPreview) { maskPreview = p; meshDirty = true; }
    }

    float[] Mask()
    {
        if (maskKind == MaskKind.None) return null;
        if (maskCache != null) return maskCache;

        switch (maskKind)
        {
            case MaskKind.Height:
                maskCache = TerrainOps.MaskByHeight(h, Grid, maskLo, maskHi, maskFeather); break;
            case MaskKind.Slope:
                maskCache = TerrainOps.MaskBySlope(h, Grid, CellM, maskSlopeLo, maskSlopeHi,
                                                   maskSlopeFeather); break;
            case MaskKind.Ridges:
                maskCache = TerrainOps.MaskByCurvature(h, Grid, CellM, maskCurveRadius, true,
                                                       maskCurveStrength); break;
            case MaskKind.Valleys:
                maskCache = TerrainOps.MaskByCurvature(h, Grid, CellM, maskCurveRadius, false,
                                                       maskCurveStrength); break;
        }
        return maskCache;
    }

    // ------------------------------------------------------------- Route

    void DrawRouteTab()
    {
        Section("Route",
            "Left click marks points on selected route. Right click rotates camera.\n"
            + "Points store horizontal positions; elevations are sampled dynamically from terrain.");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Spawn point", GUILayout.Width(110f));
            GUILayout.Label(float.IsNaN(spawn.x) ? "Unset"
                            : $"({spawn.x * 1000f:F0}, {spawn.y * 1000f:F0}) m  ·  "
                              + $"Elev {HeightAtKm(spawn.x, spawn.y):F0} m");
            GUILayout.FlexibleSpace();
            bool was = placingSpawn;
            placingSpawn = GUILayout.Toggle(placingSpawn, was ? "Click map..." : "Set Spawn",
                                            EditorStyles.miniButton, GUILayout.Width(80f));
        }

        EditorGUILayout.Space(6f);
        for (int i = 0; i < paths.Length; i++)
        {
            var pth = paths[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                var sw = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                EditorGUI.DrawRect(sw, pth.color);

                bool on = activePath == i && !placingSpawn;
                if (GUILayout.Toggle(on, $"{pth.name}  ({pth.pts.Count})",
                                     EditorStyles.miniButton) && !on)
                { activePath = i; placingSpawn = false; }

                using (new EditorGUI.DisabledScope(pth.pts.Count == 0))
                {
                    if (GUILayout.Button("Smooth", EditorStyles.miniButton, GUILayout.Width(64f)))
                    { SmoothPath(pth); Repaint(); }
                    if (GUILayout.Button("Pop Point", EditorStyles.miniButton, GUILayout.Width(72f)))
                    { pth.pts.RemoveAt(pth.pts.Count - 1); Repaint(); }
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60f)))
                    { pth.pts.Clear(); Repaint(); }
                }
            }
        }

        EditorGUILayout.Space(6f);
        routeSpacingM = EditorGUILayout.Slider(
            new GUIContent("Point spacing (m)", "Sampling distance between route points."), routeSpacingM, 5f, 200f);
        routeRadiusM = EditorGUILayout.Slider(
            new GUIContent("Half-width (m)", "Corridor half-width written to MountainRoute.Mark.radius."), routeRadiusM, 0.5f, 20f);

        EditorGUILayout.Space(6f);
        var a = paths[activePath];
        if (a.pts.Count > 1)
        {
            float len = 0f, gain = 0f, loss = 0f;
            for (int i = 1; i < a.pts.Count; i++)
            {
                var p0 = a.pts[i - 1]; var p1 = a.pts[i];
                len += Vector2.Distance(p0, p1);
                float d = HeightAtKm(p1.x, p1.y) - HeightAtKm(p0.x, p0.y);
                if (d > 0f) gain += d; else loss -= d;
            }
            EditorGUILayout.HelpBox(
                $"{a.name}:  Length {len:F2} km  ·  Ascent {gain:F0} m  ·  Descent {loss:F0} m  ·  "
                + $"Mean Grade {Mathf.Atan(gain / Mathf.Max(len * 1000f, 1f)) * Mathf.Rad2Deg:F1}°",
                MessageType.None);
        }
    }

    static void SmoothPath(RoutePath pth)
    {
        if (pth.pts.Count < 3) return;

        var src = new List<Vector2>(pth.pts);
        for (int i = 1; i < src.Count - 1; i++)
            pth.pts[i] = (src[i - 1] + src[i] * 2f + src[i + 1]) * 0.25f;
    }

    // ------------------------------------------------------------- Measure

    void DrawMeasureTab()
    {
        Section("Measure",
            "Quantifies slopes across altitude bands to verify ridge/wall distributions.");

        if (GUILayout.Button("Measure Terrain", GUILayout.Height(26f))) Measure();
        if (!string.IsNullOrEmpty(report)) EditorGUILayout.TextArea(report, EditorStyles.label);

        EditorGUILayout.Space(10f);
        Section("Naturalness — Real terrain metrics",
            "Quantifies slope distributions and drainage curvature skewness.");

        if (GUILayout.Button("Measure Naturalness", GUILayout.Height(26f))) MeasureNaturalness();
        if (!string.IsNullOrEmpty(natReport))
            EditorGUILayout.TextArea(natReport, EditorStyles.label);
    }

    string natReport, natDiag;

    void MeasureNaturalness()
    {
        if (h == null) { natReport = "No grid loaded."; return; }

        float[] mids = { 9000f, 4500f, 2250f, 1125f, 575f, 300f, 150f };
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Relief (RMS m) — Before / after comparison:");
        for (int b = 0; b < mids.Length; b++)
            sb.AppendLine($"  {mids[b],6:F0} m   {BandRms(mids[b]),6:F1}");

        var slopes = new System.Collections.Generic.List<float>(Grid * Grid / 4);
        for (int z = 1; z < Grid - 1; z++)
        for (int x = 1; x < Grid - 1; x++)
        {
            int i = z * Grid + x;
            if (h[i] <= 300f) continue;
            float gx = (h[i + 1] - h[i - 1]) / (2f * CellM);
            float gz = (h[i + Grid] - h[i - Grid]) / (2f * CellM);
            slopes.Add(Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg);
        }
        slopes.Sort();

        float med = slopes.Count > 0 ? slopes[slopes.Count / 2] : 0f;
        float p90 = slopes.Count > 0 ? slopes[(int)(slopes.Count * 0.9f)] : 0f;
        int over60 = 0;
        for (int i = 0; i < slopes.Count; i++) if (slopes[i] > 60f) over60++;
        float pct60 = slopes.Count > 0 ? 100f * over60 / slopes.Count : 0f;

        sb.AppendLine();
        sb.AppendLine($"Slope median    {med,5:F1}°     (alpine target 30-38)");
        sb.AppendLine($"Slope p90       {p90,5:F1}°     (alpine target 50-58)");
        sb.AppendLine($">60° fraction   %{pct60,4:F1}      (alpine target 5-12%)");
        sb.AppendLine($"Curvature skew  {CurvatureSkew(),5:F2}  (Positive indicates incised valleys)");
        sb.AppendLine($"Pit fraction    %{PitFraction() * 100f,5:F2}  (should be low)");
        natReport = sb.ToString();
    }

    int BlurRadiusFor(float wavelengthM)
    {
        float sigmaCells = wavelengthM / (2f * Mathf.PI * CellM);
        return Mathf.Max(1, Mathf.RoundToInt(sigmaCells * 1.732f));
    }

    float CurvatureSkew()
    {
        int n = Grid;
        var lap = new System.Collections.Generic.List<float>(n * n / 4);
        for (int z = 1; z < n - 1; z++)
        for (int x = 1; x < n - 1; x++)
        {
            int i = z * n + x;
            if (h[i] <= 300f) continue;
            lap.Add(h[i - 1] + h[i + 1] + h[i - n] + h[i + n] - 4f * h[i]);
        }
        if (lap.Count < 16) return 0f;

        double mean = 0.0;
        for (int i = 0; i < lap.Count; i++) mean += lap[i];
        mean /= lap.Count;

        double m2 = 0.0, m3 = 0.0;
        for (int i = 0; i < lap.Count; i++)
        {
            double d = lap[i] - mean;
            m2 += d * d; m3 += d * d * d;
        }
        m2 /= lap.Count; m3 /= lap.Count;
        return m2 <= 1e-9 ? 0f : (float)(m3 / System.Math.Pow(m2, 1.5));
    }

    float PitFraction()
    {
        int n = Grid, pits = 0, cnt = 0;
        for (int z = 1; z < n - 1; z++)
        for (int x = 1; x < n - 1; x++)
        {
            int i = z * n + x;
            if (h[i] <= 300f) continue;
            cnt++;
            bool pit = true;
            for (int dz = -1; dz <= 1 && pit; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                if (h[i + dz * n + dx] < h[i]) { pit = false; break; }
            }
            if (pit) pits++;
        }
        return cnt > 0 ? (float)pits / cnt : 0f;
    }

    void Measure()
    {
        var bands = new (float lo, float hi, string name)[]
            { (0, 3, "Mass 0-3 km"), (3, 6, "Slope 3-6 km"), (6, 9, "Apron 6-9 km"), (9, 15, "Plain 9-15 km") };
        var lists = new List<float>[bands.Length];
        for (int i = 0; i < bands.Length; i++) lists[i] = new List<float>();

        float top = 0f, low = float.MaxValue, wide = 0f, edge = 0f;
        float c = (Grid - 1) * 0.5f;

        for (int z = 0; z < Grid; z++)
        for (int x = 0; x < Grid; x++)
        {
            float m = h[z * Grid + x];
            if (m > top) top = m;
            if (m < low) low = m;
            if (z == 0 || x == 0 || z == Grid - 1 || x == Grid - 1) edge = Mathf.Max(edge, m);

            float km = Mathf.Sqrt((x - c) * (x - c) + (z - c) * (z - c)) * CellM / 1000f;
            if (m > low + 100f) wide = Mathf.Max(wide, km);

            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, Grid - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, Grid - 1);
            float gx = (h[z * Grid + xp] - h[z * Grid + xm]) / ((xp - xm) * CellM);
            float gz = (h[zp * Grid + x] - h[zm * Grid + x]) / ((zp - zm) * CellM);
            float deg = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;

            for (int b = 0; b < bands.Length; b++)
                if (km >= bands[b].lo && km < bands[b].hi) { lists[b].Add(deg); break; }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendFormat("Summit {0:F0} m · Base {1:F0} m · Relief {2:F0} m\n", top, low, top - low);
        sb.AppendFormat("Span {0:F1} km · Edge {1:F0} m\n\n", wide * 2f, edge);
        sb.AppendLine("Band           Median Slope   Walkable (<30°)");
        for (int b = 0; b < bands.Length; b++)
        {
            if (lists[b].Count == 0) continue;
            lists[b].Sort();
            int walk = 0;
            foreach (var d in lists[b]) if (d < 30f) walk++;
            sb.AppendFormat("{0,-14} {1,8:F1}°   {2,10:F0}%\n", bands[b].name,
                            lists[b][lists[b].Count / 2], 100f * walk / lists[b].Count);
        }
        report = sb.ToString().TrimEnd();
        Repaint();
    }

    // ------------------------------------------------------------- Save

    void DrawSaveTab()
    {
        Section("Terrain Base", "Manages base plain elevation and terrain loading.");
        plainM = Slider("Base plain (m)", "Starting plain elevation.", plainM, 0f, 2000f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Flat Plain", GUILayout.Height(24f))) NewFlat();
            if (GUILayout.Button("Load From Scene Terrain", GUILayout.Height(24f))) LoadFromTerrain();
        }

        EditorGUILayout.Space(10f);
        Section("Fine Detail (Export Time)",
            "Upsamples from 29.3 m grid to 7.32 m terrain resolution, injecting high-frequency rock textures.");

        fineDetail = EditorGUILayout.ToggleLeft("Add Fine Detail on Export", fineDetail);
        using (new EditorGUI.DisabledScope(!fineDetail))
        {
            fineWavelength = EditorGUILayout.Slider(
                new GUIContent("Wavelength (m)", "Base octave wavelength."), fineWavelength, 80f, 1200f);
            fineOctaves = EditorGUILayout.IntSlider(
                new GUIContent("Octaves", "Octave count."), fineOctaves, 1, 8);
            fineAmplitude = EditorGUILayout.Slider(
                new GUIContent("Amplitude (m)", "Relief amplitude."), fineAmplitude, 2f, 120f);
            fineSteepBias = EditorGUILayout.Slider(
                new GUIContent("Steep Slope Bias", "1 = cliffs only, 0 = uniform."), fineSteepBias, 0f, 1f);
        }

        EditorGUILayout.Space(10f);
        Section("Scene Reset", "Flattens scene terrain to base plain.");
        if (GUILayout.Button("Flatten Scene Terrain", GUILayout.Height(24f)))
            FlattenScene();

        EditorGUILayout.Space(10f);
        Section("Sculpt Assets", "Save and load binary float32 sculpt files.");

        using (new EditorGUILayout.HorizontalScope())
        {
            sculptName = SanitizeName(EditorGUILayout.TextField("Name", sculptName));
            if (GUILayout.Button("Save", GUILayout.Width(70f)))
            { SaveSculpt(sculptName, force: true); info = $"Sculpt saved: {sculptName}"; }
        }

        if (Directory.Exists(SculptDir))
        {
            foreach (var f in Directory.GetFiles(SculptDir, "*.bytes"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(n == AutoName ? $"{n}  (auto)" : n);
                    if (GUILayout.Button("Load", GUILayout.Width(60f))) LoadSculpt(n);
                    using (new EditorGUI.DisabledScope(n == AutoName))
                        if (GUILayout.Button("Delete", GUILayout.Width(50f)))
                        { AssetDatabase.DeleteAsset(f.Replace("\\", "/")); }
                }
            }
        }

        EditorGUILayout.Space(10f);
        Section("Save & Apply", "Exports to terrain data, bakes surface maps, updates routes.");
        if (GUILayout.Button("SAVE AND APPLY TO SCENE", GUILayout.Height(36f))) SaveAndApply();
        if (!string.IsNullOrEmpty(info)) EditorGUILayout.HelpBox(info, MessageType.None);
    }

    // ================================================================ Operation Runner

    void Naturalize()
    {
        if (h == null) return;

        var snap = (float[])h.Clone();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            float[] land = TerrainOps.MaskByHeight(h, Grid, plainM + 450f, MaxM, 350f);

            var before = (float[])h.Clone();
            var diag = new System.Text.StringBuilder();
            double maskMean = 0.0; int maskCnt = 0;
            for (int i = 0; i < land.Length; i++) { if (h[i] > 300f) { maskMean += land[i]; maskCnt++; } }
            diag.AppendLine($"Mask mean (mountain)      {(maskCnt > 0 ? maskMean / maskCnt : 0):F3}   (target: 1.000)");
            diag.AppendLine($"Strength                   {natStrength:F2}");
            diag.AppendLine($"Stream K / iters / diffuse {natFlowK} / {natFlowIters} / {natFlowDiffuse}");

            EditorUtility.DisplayProgressBar("Naturalize", "1/3 — Mid-band seed", 0.05f);
            TerrainOps.FractalNoise(h, Grid, CellM, natSeedWl, natSeedOct,
                                    natSeedAmp * natStrength,
                                    natSeedPers, 2f, opSeed, land);
            diag.AppendLine($"1 Seed added               {DeltaRms(before):F1} m");
            diag.AppendLine($"   -> {Bands()}");

            for (int c = 0; c < natFlowIters; c++)
            {
                EditorUtility.DisplayProgressBar("Naturalize",
                    $"2/3 — Stream incision ({c + 1}/{natFlowIters})",
                    0.10f + 0.70f * c / natFlowIters);
                TerrainOps.FlowIncise(h, Grid, CellM, natFlowK * natStrength, 0.5f, 1f, 1,
                                      natFlowDiffuse, land);
            }
            diag.AppendLine($"2 Stream incision          {DeltaRms(before):F1} m (cumulative)");
            diag.AppendLine($"   -> {Bands()}");

            EditorUtility.DisplayProgressBar("Naturalize", "3/3 — Glacial features", 0.85f);
            float[] high = TerrainOps.MaskByHeight(h, Grid, natGlacierFrom, MaxM, 400f);
            TerrainOps.Sharpen(h, Grid, CellM, natGlacierRadius,
                               natGlacierGain * natStrength, high);
            diag.AppendLine($"3 Glacial sharpening       {DeltaRms(before):F1} m (cumulative)");
            diag.AppendLine($"   -> {Bands()}");

            natDiag = diag.ToString();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        for (int i = 0; i < h.Length; i++) h[i] = Mathf.Clamp(h[i], 0f, MaxM);
        PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null;
        meshDirty = true;
        info = $"Naturalization completed — {sw.ElapsedMilliseconds / 1000f:F1} s";
        Repaint();
    }

    float BandRms(float wavelengthM)
    {
        var a = (float[])h.Clone();
        var c = (float[])h.Clone();
        TerrainOps.Blur(a, Grid, BlurRadiusFor(wavelengthM * 0.7071f));
        TerrainOps.Blur(c, Grid, BlurRadiusFor(wavelengthM * 1.4142f));

        double sum = 0.0; int cnt = 0;
        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] <= 300f) continue;
            double d = a[i] - c[i];
            sum += d * d; cnt++;
        }
        return cnt > 0 ? Mathf.Sqrt((float)(sum / cnt)) : 0f;
    }

    string Bands() => $"2250m {BandRms(2250f):F0} · 1125m {BandRms(1125f):F0} · "
                    + $"575m {BandRms(575f):F0} · 300m {BandRms(300f):F0}";

    float DeltaRms(float[] before)
    {
        double sum = 0.0; int cnt = 0;
        for (int i = 0; i < h.Length; i++)
        {
            if (before[i] <= 300f && h[i] <= 300f) continue;
            double d = h[i] - before[i];
            sum += d * d; cnt++;
        }
        return cnt > 0 ? Mathf.Sqrt((float)(sum / cnt)) : 0f;
    }

    void RunOp(System.Action<float[]> op)
    {
        var snap = (float[])h.Clone();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        op(h);
        for (int i = 0; i < h.Length; i++) h[i] = Mathf.Clamp(h[i], 0f, MaxM);
        PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null;
        meshDirty = true;
        info = $"Operation completed in {sw.ElapsedMilliseconds} ms";
        Repaint();
    }

    // ================================================================ Undo / Redo

    static readonly KeyCode[] FlyKeys =
    { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Q, KeyCode.E,
      KeyCode.Space, KeyCode.LeftShift, KeyCode.LeftAlt };

    void HandleShortcuts()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && (e.control || e.command))
        {
            if (e.keyCode == KeyCode.Z)
            { if (tab == Tab.Route) RouteUndo(); else Undo(); e.Use(); return; }
            if (e.keyCode == KeyCode.Y)
            { if (tab == Tab.Route) RouteRedo(); else Redo(); e.Use(); return; }
        }

        if (e.type == EventType.KeyDown && System.Array.IndexOf(FlyKeys, e.keyCode) >= 0)
        {
            if (keysDown.Add(e.keyCode)) lastFlyTick = EditorApplication.timeSinceStartup;
            e.Use();
        }
        else if (e.type == EventType.KeyUp && System.Array.IndexOf(FlyKeys, e.keyCode) >= 0)
        {
            keysDown.Remove(e.keyCode);
            e.Use();
        }
    }

    void RouteUndo()
    {
        if (routeUndo.Count == 0) return;
        var ed = routeUndo[routeUndo.Count - 1];
        routeUndo.RemoveAt(routeUndo.Count - 1);

        var list = paths[ed.path].pts;
        int n = Mathf.Min(ed.pts.Count, list.Count);
        list.RemoveRange(list.Count - n, n);

        routeRedo.Add(ed);
        Repaint();
    }

    void RouteRedo()
    {
        if (routeRedo.Count == 0) return;
        var ed = routeRedo[routeRedo.Count - 1];
        routeRedo.RemoveAt(routeRedo.Count - 1);

        paths[ed.path].pts.AddRange(ed.pts);
        routeUndo.Add(ed);
        Repaint();
    }

    void PushEdit(int x0, int z0, int x1, int z1, float[] snapshot)
    {
        int w = x1 - x0 + 1, d = z1 - z0 + 1;
        if (w <= 0 || d <= 0) return;

        var ed = new Edit { x0 = x0, z0 = z0, w = w, d = d,
                            before = new float[w * d], after = new float[w * d] };
        for (int z = 0; z < d; z++)
        for (int x = 0; x < w; x++)
        {
            int src = (z0 + z) * Grid + (x0 + x);
            ed.before[z * w + x] = snapshot[src];
            ed.after[z * w + x] = h[src];
        }
        undoStack.Add(ed);
        if (undoStack.Count > UndoLimit) undoStack.RemoveAt(0);
        redoStack.Clear();
        dirtySinceSave = true;
    }

    void Blit(Edit ed, float[] src)
    {
        for (int z = 0; z < ed.d; z++)
        for (int x = 0; x < ed.w; x++)
            h[(ed.z0 + z) * Grid + (ed.x0 + x)] = src[z * ed.w + x];
        maskCache = null;
        meshDirty = true;
        Repaint();
    }

    void Undo()
    {
        if (undoStack.Count == 0) return;
        var ed = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        Blit(ed, ed.before);
        redoStack.Add(ed);
    }

    void Redo()
    {
        if (redoStack.Count == 0) return;
        var ed = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);
        Blit(ed, ed.after);
        undoStack.Add(ed);
    }

    // ================================================================ Viewport

    void DrawViewport()
    {
        float hgt = Mathf.Max(240f, position.height * viewShare);
        Rect r = GUILayoutUtility.GetRect(10f, 10000f, hgt, hgt);
        HandleViewInput(r);

        if (Event.current.type != EventType.Repaint) return;
        if (r.width < 1f || r.height < 1f) return;

        if (prev == null) prev = new PreviewRenderUtility();
        if (mesh == null || meshDirty) BuildMesh();
        else UpdatePaintedRegion();
        DecayHeat();

        prev.BeginPreview(r, GUIStyle.none);
        var cam = prev.camera;
        camRot = Quaternion.Euler(pitch, yaw, 0f);
        camPos = flyPos;
        viewRect = r;
        cam.transform.position = camPos;
        cam.transform.rotation = camRot;

        cam.nearClipPlane = 0.005f;
        cam.farClipPlane = 300f;
        cam.fieldOfView = camFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
        prev.DrawMesh(mesh, Matrix4x4.Scale(new Vector3(1f, vScale, 1f)), mat, 0);
        cam.Render();
        GUI.DrawTexture(r, prev.EndPreview(), ScaleMode.StretchToFill, false);

        DrawRoutes(r);
        DrawBrushRing(r);

        if (cursorValid && tab == Tab.Brush) HideSystemCursor(r);
        else ShowSystemCursor();
    }

    bool Project(Vector3 world, Rect r, out Vector2 g)
    {
        g = Vector2.zero;
        Vector3 v = Quaternion.Inverse(camRot) * (world - camPos);
        if (v.z < 0.05f) return false;

        float t = Mathf.Tan(camFov * 0.5f * Mathf.Deg2Rad);
        float aspect = r.width / Mathf.Max(r.height, 1f);
        float ndcX = v.x / (v.z * t * aspect);
        float ndcY = v.y / (v.z * t);

        g = new Vector2(r.x + (ndcX * 0.5f + 0.5f) * r.width,
                        r.y + (0.5f - ndcY * 0.5f) * r.height);
        return true;
    }

    Ray MakeRay(Rect r, Vector2 mouse)
    {
        float u = (mouse.x - r.x) / Mathf.Max(r.width, 1f);
        float v = (mouse.y - r.y) / Mathf.Max(r.height, 1f);
        float t = Mathf.Tan(camFov * 0.5f * Mathf.Deg2Rad);
        float aspect = r.width / Mathf.Max(r.height, 1f);

        var dir = camRot * new Vector3((u * 2f - 1f) * t * aspect, (1f - v * 2f) * t, 1f);
        return new Ray(camPos, dir.normalized);
    }

    void DrawRoutes(Rect r)
    {
        Handles.BeginGUI();
        Color old = Handles.color;

        foreach (var pth in paths)
        {
            if (pth.pts.Count == 0) continue;

            var line = new List<Vector3>(pth.pts.Count);
            foreach (var q in pth.pts)
            {
                var w = new Vector3(q.x, HeightAtKm(q.x, q.y) / 1000f * vScale + 0.012f, q.y);
                if (Project(w, r, out Vector2 g)) line.Add(new Vector3(g.x, g.y, 0f));
            }

            if (line.Count > 1)
            {
                Handles.color = new Color(0f, 0f, 0f, 0.6f);
                Handles.DrawAAPolyLine(6f, line.ToArray());
                Handles.color = pth.color;
                Handles.DrawAAPolyLine(3f, line.ToArray());
            }

            foreach (var g in line)
                EditorGUI.DrawRect(new Rect(g.x - 2.5f, g.y - 2.5f, 5f, 5f), pth.color);
        }

        if (!float.IsNaN(spawn.x))
        {
            var w = new Vector3(spawn.x, HeightAtKm(spawn.x, spawn.y) / 1000f * vScale + 0.012f,
                                spawn.y);
            if (Project(w, r, out Vector2 g))
            {
                Handles.color = Color.black;
                Handles.DrawAAPolyLine(5f, new Vector3(g.x - 9f, g.y, 0f), new Vector3(g.x + 9f, g.y, 0f));
                Handles.DrawAAPolyLine(5f, new Vector3(g.x, g.y - 9f, 0f), new Vector3(g.x, g.y + 9f, 0f));
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(2f, new Vector3(g.x - 9f, g.y, 0f), new Vector3(g.x + 9f, g.y, 0f));
                Handles.DrawAAPolyLine(2f, new Vector3(g.x, g.y - 9f, 0f), new Vector3(g.x, g.y + 9f, 0f));
            }
        }

        Handles.color = old;
        Handles.EndGUI();
    }

    void DrawBrushRing(Rect r)
    {
        if (!cursorValid) return;
        if (tab != Tab.Brush && tab != Tab.Route) return;

        float ringR = tab == Tab.Route ? Mathf.Max(routeRadiusM * 6f, 40f) : radiusM;
        float ringAspect = tab == Tab.Route ? 1f : aspect;

        Handles.BeginGUI();
        Color old = Handles.color;

        const int seg = 56;
        var pts = new List<Vector3>(seg + 1);
        float ca = Mathf.Cos(angleDeg * Mathf.Deg2Rad), sa = Mathf.Sin(angleDeg * Mathf.Deg2Rad);

        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            float ex = Mathf.Cos(a) * ringR / 1000f;
            float ez = Mathf.Sin(a) * ringR / ringAspect / 1000f;
            var w = cursor + new Vector3(ex * ca - ez * sa, 0f, ex * sa + ez * ca);
            w.y = HeightAtKm(w.x, w.z) / 1000f * vScale;
            if (Project(w, r, out Vector2 g)) pts.Add(new Vector3(g.x, g.y, 0f));
        }

        if (pts.Count > 2)
        {
            Handles.color = new Color(0f, 0f, 0f, 0.55f);
            Handles.DrawAAPolyLine(4f, pts.ToArray());
            Handles.color = new Color(1f, 0.85f, 0.15f, 1f);
            Handles.DrawAAPolyLine(2f, pts.ToArray());
        }

        if (Project(cursor, r, out Vector2 c0))
        {
            Handles.color = new Color(1f, 0.85f, 0.15f, 1f);
            Handles.DrawAAPolyLine(2f, new Vector3(c0.x - 6f, c0.y, 0f), new Vector3(c0.x + 6f, c0.y, 0f));
            Handles.DrawAAPolyLine(2f, new Vector3(c0.x, c0.y - 6f, 0f), new Vector3(c0.x, c0.y + 6f, 0f));
        }

        Handles.color = old;
        Handles.EndGUI();
    }

    void Fly()
    {
        AutoSaveTick();

        if (keysDown.Count == 0) { lastFlyTick = EditorApplication.timeSinceStartup; return; }
        if (focusedWindow != this) { keysDown.Clear(); return; }

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - lastFlyTick), 0f, 0.1f);
        lastFlyTick = now;

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;

        Vector3 move = Vector3.zero;
        if (keysDown.Contains(KeyCode.W)) move += fwd;
        if (keysDown.Contains(KeyCode.S)) move -= fwd;
        if (keysDown.Contains(KeyCode.D)) move += right;
        if (keysDown.Contains(KeyCode.A)) move -= right;
        if (keysDown.Contains(KeyCode.E) || keysDown.Contains(KeyCode.Space)) move += Vector3.up;
        if (keysDown.Contains(KeyCode.Q)) move -= Vector3.up;
        if (move.sqrMagnitude < 1e-6f) return;

        float sp = flySpeed;
        if (keysDown.Contains(KeyCode.LeftShift)) sp *= 5f;
        if (keysDown.Contains(KeyCode.LeftAlt)) sp *= 0.15f;

        float ground = HeightAtKm(flyPos.x, flyPos.z) / 1000f * vScale;
        float above = Mathf.Max(0f, flyPos.y - ground);
        sp *= Mathf.Lerp(0.16f, 1f, Mathf.Clamp01(above / 2f));

        flyPos += move.normalized * sp * dt;

        flyPos.x = Mathf.Clamp(flyPos.x, -40f, 40f);
        flyPos.z = Mathf.Clamp(flyPos.z, -40f, 40f);
        flyPos.y = Mathf.Clamp(flyPos.y, -1f, 60f);

        Repaint();
    }

    void AutoSaveTick()
    {
        if (!dirtySinceSave || h == null) return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastAutoSave < 120.0) return;
        lastAutoSave = now;

        SaveSculpt(AutoName);
        dirtySinceSave = false;
    }

    void HideSystemCursor(Rect r)
    {
        if (blankCursor == null)
        {
            blankCursor = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave };
            blankCursor.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            blankCursor.Apply();
        }

        EditorGUIUtility.AddCursorRect(r, MouseCursor.CustomCursor);
        if (!cursorHidden)
        {
            Cursor.SetCursor(blankCursor, Vector2.zero, CursorMode.Auto);
            cursorHidden = true;
        }
    }

    void ShowSystemCursor()
    {
        if (!cursorHidden) return;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        cursorHidden = false;
    }

    void HandleViewInput(Rect r)
    {
        Event e = Event.current;

        if (e.type == EventType.Layout || e.type == EventType.Used) return;
        if (!r.Contains(e.mousePosition)) { cursorValid = false; ShowSystemCursor(); return; }

        if (e.type == EventType.MouseDrag && e.button == 1)
        { yaw += e.delta.x * 0.5f; pitch = Mathf.Clamp(pitch + e.delta.y * 0.5f, 2f, 88f); e.Use(); Repaint(); return; }
        if (e.type == EventType.ContextClick) { e.Use(); return; }
        if (e.type == EventType.ScrollWheel)
        {
            flySpeed = Mathf.Clamp(flySpeed * (1f - e.delta.y * 0.12f), 0.05f, 20f);
            e.Use(); Repaint(); return;
        }

        if (tab != Tab.Brush && tab != Tab.Route) { cursorValid = false; return; }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
        {
            cursorValid = Raycast(r, e.mousePosition, out cursor);
            Repaint();
        }

        if (tab == Tab.Route)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && cursorValid)
            {
                if (placingSpawn)
                {
                    spawn = new Vector2(cursor.x, cursor.z);
                    placingSpawn = false;
                }
                else
                {
                    drawingRoute = true;
                    strokeStartCount = paths[activePath].pts.Count;
                    paths[activePath].pts.Add(new Vector2(cursor.x, cursor.z));
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && drawingRoute && cursorValid)
            {
                var list = paths[activePath].pts;
                var q = new Vector2(cursor.x, cursor.z);

                if (list.Count == 0) { list.Add(q); }
                else
                {
                    var last = list[list.Count - 1];
                    float distM = Vector2.Distance(last, q) * 1000f;
                    int steps = Mathf.FloorToInt(distM / Mathf.Max(routeSpacingM, 1f));
                    steps = Mathf.Min(steps, 400);
                    for (int i = 1; i <= steps; i++)
                        list.Add(Vector2.Lerp(last, q, i / (float)steps));
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && drawingRoute)
            {
                drawingRoute = false;
                var list = paths[activePath].pts;
                int added = list.Count - strokeStartCount;
                if (added > 0)
                {
                    routeUndo.Add(new RouteEdit
                    {
                        path = activePath,
                        pts = list.GetRange(strokeStartCount, added),
                    });
                    if (routeUndo.Count > UndoLimit) routeUndo.RemoveAt(0);
                    routeRedo.Clear();
                    dirtySinceSave = true;
                }
                e.Use(); Repaint();
            }
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && cursorValid)
        {
            painting = true;
            flattenTarget = HeightAtKm(cursor.x, cursor.z);
            strokeSnapshot = (float[])h.Clone();
            sx0 = sz0 = int.MaxValue; sx1 = sz1 = int.MinValue;
            Paint(cursor); e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && painting && cursorValid)
        { Paint(cursor); e.Use(); }
        else if (e.type == EventType.MouseUp && e.button == 0 && painting)
        {
            painting = false;
            flattenTarget = float.NaN;
            if (strokeSnapshot != null && sx1 >= sx0) PushEdit(sx0, sz0, sx1, sz1, strokeSnapshot);
            strokeSnapshot = null;
            maskCache = null;
            meshDirty = true;
            e.Use();
        }
    }

    bool Raycast(Rect r, Vector2 mouse, out Vector3 hit)
    {
        hit = Vector3.zero;
        if (prev == null) return false;

        Ray ray = MakeRay(r, mouse);

        float t = 0f; bool wasAbove = false;
        for (int i = 0; i < 700; i++)
        {
            t += 0.1f;
            if (t > 220f) return false;
            Vector3 p = ray.origin + ray.direction * t;
            if (Mathf.Abs(p.x) > 16f || Mathf.Abs(p.z) > 16f) { wasAbove = false; continue; }

            float d = p.y - HeightAtKm(p.x, p.z) / 1000f * vScale;
            if (wasAbove && d <= 0f)
            {
                float lo = t - 0.1f, hi = t;
                for (int k = 0; k < 22; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Vector3 q = ray.origin + ray.direction * mid;
                    if (q.y - HeightAtKm(q.x, q.z) / 1000f * vScale > 0f) lo = mid; else hi = mid;
                }
                hit = ray.origin + ray.direction * hi;
                hit.y = HeightAtKm(hit.x, hit.z) / 1000f * vScale;
                return true;
            }
            wasAbove = d > 0f;
        }
        return false;
    }

    float HeightAtKm(float xKm, float zKm)
    {
        float fx = (xKm * 1000f + ArenaM * 0.5f) / CellM;
        float fz = (zKm * 1000f + ArenaM * 0.5f) / CellM;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, Grid - 2);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, Grid - 2);
        float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
        float a = Mathf.Lerp(h[z0 * Grid + x0], h[z0 * Grid + x0 + 1], tx);
        float b = Mathf.Lerp(h[(z0 + 1) * Grid + x0], h[(z0 + 1) * Grid + x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // ================================================================ Brush Application

    void Paint(Vector3 worldKm)
    {
        int cx = Mathf.RoundToInt((worldKm.x * 1000f + ArenaM * 0.5f) / CellM);
        int cz = Mathf.RoundToInt((worldKm.z * 1000f + ArenaM * 0.5f) / CellM);
        int rad = Mathf.Max(1, Mathf.CeilToInt(radiusM / CellM));

        int x0 = Mathf.Max(0, cx - rad), x1 = Mathf.Min(Grid - 1, cx + rad);
        int z0 = Mathf.Max(0, cz - rad), z1 = Mathf.Min(Grid - 1, cz + rad);
        if (x1 <= x0 || z1 <= z0) return;

        if (x0 < sx0) sx0 = x0;
        if (z0 < sz0) sz0 = z0;
        if (x1 > sx1) sx1 = x1;
        if (z1 > sz1) sz1 = z1;

        int sw = x1 - x0 + 1, sd = z1 - z0 + 1;
        float[] src = null;
        if (brush == BrushKind.Smooth || brush == BrushKind.Erode || brush == BrushKind.Sharpen)
        {
            src = new float[sw * sd];
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                src[(z - z0) * sw + (x - x0)] = h[z * Grid + x];
        }

        float amp = strength * 40f;
        float ca = Mathf.Cos(-angleDeg * Mathf.Deg2Rad), sa = Mathf.Sin(-angleDeg * Mathf.Deg2Rad);
        float maxStep = Mathf.Tan(38f * Mathf.Deg2Rad) * CellM;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = (x - cx) * CellM, dz = (z - cz) * CellM;
            float ux = dx * ca - dz * sa;
            float uz = (dx * sa + dz * ca) * aspect;
            float d = Mathf.Sqrt(ux * ux + uz * uz) / radiusM;
            if (d > 1f) continue;

            float w = Mathf.Lerp(Mathf.SmoothStep(1f, 0f, d), 1f, hardness);
            int i = z * Grid + x;

            switch (brush)
            {
                case BrushKind.Raise: h[i] += amp * w; break;
                case BrushKind.Lower: h[i] -= amp * w; break;

                case BrushKind.Flatten:
                    if (!float.IsNaN(flattenTarget))
                        h[i] = Mathf.Lerp(h[i], flattenTarget, w * strength);
                    break;

                case BrushKind.Smooth:
                {
                    float sum = 0f; int n = 0;
                    for (int oz = -2; oz <= 2; oz++)
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        int px = x - x0 + ox, pz = z - z0 + oz;
                        if (px < 0 || pz < 0 || px >= sw || pz >= sd) continue;
                        sum += src[pz * sw + px]; n++;
                    }
                    if (n > 0) h[i] = Mathf.Lerp(h[i], sum / n, w * strength);
                    break;
                }

                case BrushKind.Ridge:
                case BrushKind.Valley:
                {
                    float perp = Mathf.Abs(uz) / radiusM;
                    float crest = Mathf.Max(0f, 1f - perp * 3f);
                    float v = amp * w * crest * crest;
                    h[i] += brush == BrushKind.Ridge ? v : -v;
                    break;
                }

                case BrushKind.Erode:
                {
                    int px = x - x0, pz = z - z0;
                    float me = src[pz * sw + px];
                    float move = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        int ox = k == 0 ? 1 : k == 1 ? -1 : 0;
                        int oz = k == 2 ? 1 : k == 3 ? -1 : 0;
                        int qx = px + ox, qz = pz + oz;
                        if (qx < 0 || qz < 0 || qx >= sw || qz >= sd) continue;
                        float diff = me - src[qz * sw + qx] - maxStep;
                        if (diff > 0f) move += diff;
                    }
                    h[i] -= move * 0.15f * w * strength;
                    break;
                }

                case BrushKind.Sharpen:
                {
                    float sum = 0f; int n = 0;
                    for (int oz = -3; oz <= 3; oz++)
                    for (int ox = -3; ox <= 3; ox++)
                    {
                        int px = x - x0 + ox, pz = z - z0 + oz;
                        if (px < 0 || pz < 0 || px >= sw || pz >= sd) continue;
                        sum += src[pz * sw + px]; n++;
                    }
                    if (n > 0)
                    {
                        float avg = sum / n;
                        h[i] += (h[i] - avg) * w * strength;
                    }
                    break;
                }

                case BrushKind.Noise:
                {
                    float n1 = Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(x * 0.55f + 31.7f, z * 0.55f + 11.3f) - 0.5f;
                    h[i] += (n1 + n2 * 0.5f) * amp * w;
                    break;
                }
            }

            h[i] = Mathf.Clamp(h[i], 0f, MaxM);
        }

        float toView = (View - 1) / (float)(Grid - 1);
        pdx0 = Mathf.Min(pdx0, Mathf.FloorToInt(x0 * toView) - 1);
        pdz0 = Mathf.Min(pdz0, Mathf.FloorToInt(z0 * toView) - 1);
        pdx1 = Mathf.Max(pdx1, Mathf.CeilToInt(x1 * toView) + 1);
        pdz1 = Mathf.Max(pdz1, Mathf.CeilToInt(z1 * toView) + 1);

        if (EditorApplication.timeSinceStartup - lastMeshBuild > 0.03)
        {
            lastMeshBuild = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    // ================================================================ Data Persistence

    void NewFlat()
    {
        var snap = h;
        h = new float[Grid * Grid];
        for (int i = 0; i < h.Length; i++) h[i] = plainM;
        if (snap != null) PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null; meshDirty = true; Repaint();
    }

    void LoadFromTerrain()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "No terrain in scene."; return; }
        var data = gen.GetComponent<Terrain>().terrainData;
        int res = data.heightmapResolution;
        var src = data.GetHeights(0, 0, res, res);

        var snap = h;
        h = new float[Grid * Grid];
        float sc = (res - 1) / (float)(Grid - 1);
        for (int z = 0; z < Grid; z++)
        for (int x = 0; x < Grid; x++)
            h[z * Grid + x] = src[Mathf.Min(res - 1, Mathf.RoundToInt(z * sc)),
                                  Mathf.Min(res - 1, Mathf.RoundToInt(x * sc))] * data.size.y;
        if (snap != null) PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null; meshDirty = true; Repaint();
    }

    bool IsFlat()
    {
        if (h == null || h.Length == 0) return true;
        float first = h[0];
        for (int i = 1; i < h.Length; i++)
            if (Mathf.Abs(h[i] - first) > 0.01f) return false;
        return true;
    }

    static void Backup(string path)
    {
        if (!File.Exists(path)) return;

        string dir = System.IO.Path.GetDirectoryName(path);
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        string ext = System.IO.Path.GetExtension(path);

        for (int i = 3; i > 1; i--)
        {
            string older = $"{dir}/{name}.backup{i}{ext}";
            string newer = $"{dir}/{name}.backup{i - 1}{ext}";
            if (File.Exists(older)) File.Delete(older);
            if (File.Exists(newer)) File.Move(newer, older);
        }
        File.Copy(path, $"{dir}/{name}.backup1{ext}", true);
    }

    static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "mountain";
        var bad = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name.Trim())
            sb.Append(System.Array.IndexOf(bad, c) >= 0 ? '_' : c);
        string clean = sb.ToString().Trim(' ', '.');
        return clean.Length == 0 ? "mountain" : clean;
    }

    static string SculptPath(string name) => $"{SculptDir}/{SanitizeName(name)}.bytes";

    bool RoutesEmpty()
    {
        if (!float.IsNaN(spawn.x)) return false;
        foreach (var pth in paths) if (pth != null && pth.pts.Count > 0) return false;
        return true;
    }

    void LoadRoutesOnly(string name)
    {
        string path = SculptPath(name);
        if (!File.Exists(path)) return;

        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                int first = br.ReadInt32();
                if (first >= 0) return;
                int res = br.ReadInt32();
                if (res != Grid) return;

                br.ReadSingle();
                fs.Seek((long)Grid * Grid * 4, SeekOrigin.Current);

                spawn = new Vector2(br.ReadSingle(), br.ReadSingle());
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int c = br.ReadInt32();
                    var list = i < paths.Length ? paths[i].pts : null;
                    list?.Clear();
                    for (int k = 0; k < c; k++)
                    {
                        var q = new Vector2(br.ReadSingle(), br.ReadSingle());
                        list?.Add(q);
                    }
                }
            }
            info = "Routes loaded from sculpt file.";
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not read route block ({name}): {e.Message}");
        }
    }

    void SaveSculpt(string name, bool force = false)
    {
        if (h == null) return;
        Directory.CreateDirectory(SculptDir);

        string path = SculptPath(name);
        bool hasRoutes = !RoutesEmpty();

        if (!hasRoutes && File.Exists(path)) LoadRoutesOnly(name);

        if (!force && !hasRoutes && IsFlat() && File.Exists(path)
            && new FileInfo(path).Length > 1024)
        {
            info = $"Grid is FLAT, {name} not overwritten (autosave).";
            return;
        }
        Backup(path);

        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-2);
                bw.Write(Grid);
                bw.Write(plainM);
                for (int i = 0; i < h.Length; i++) bw.Write(h[i]);

                bw.Write(spawn.x); bw.Write(spawn.y);
                bw.Write(paths.Length);
                foreach (var pth in paths)
                {
                    bw.Write(pth.pts.Count);
                    foreach (var q in pth.pts) { bw.Write(q.x); bw.Write(q.y); }
                }
            }
        }
        catch (System.Exception e)
        {
            info = $"Failed to save ({name}): {e.Message}";
            Debug.LogWarning(info);
            return;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    bool LoadSculpt(string name)
    {
        string path = SculptPath(name);
        if (!File.Exists(path)) return false;

        if (h != null && !IsFlat()) SaveSculpt(RescueName, force: true);

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            int first = br.ReadInt32();
            int version = first < 0 ? -first : 1;
            int res = first < 0 ? br.ReadInt32() : first;

            if (res != Grid)
            {
                info = $"Sculpt file is {res}^2, window is {Grid}^2 — cannot load.";
                return false;
            }

            float newPlain = br.ReadSingle();
            var g = new float[Grid * Grid];
            for (int i = 0; i < g.Length; i++) g[i] = br.ReadSingle();

            var newSpawn = new Vector2(float.NaN, float.NaN);
            var newPaths = new List<List<Vector2>>();
            for (int i = 0; i < paths.Length; i++) newPaths.Add(new List<Vector2>());

            if (version >= 2)
            {
                newSpawn = new Vector2(br.ReadSingle(), br.ReadSingle());
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int c = br.ReadInt32();
                    for (int k = 0; k < c; k++)
                    {
                        var q = new Vector2(br.ReadSingle(), br.ReadSingle());
                        if (i < paths.Length) newPaths[i].Add(q);
                    }
                }
            }

            h = g;
            plainM = newPlain;
            spawn = newSpawn;
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i].pts.Clear();
                paths[i].pts.AddRange(newPaths[i]);
            }
        }

        undoStack.Clear(); redoStack.Clear();
        routeUndo.Clear(); routeRedo.Clear();
        maskCache = null; meshDirty = true;
        dirtySinceSave = false;
        info = $"Sculpt loaded: {name}";
        Repaint();
        return true;
    }

    void FlattenScene()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "No terrain in scene."; return; }
        var terrain = gen.GetComponent<Terrain>();
        var data = terrain.terrainData;

        data.heightmapResolution = Export;
        data.size = new Vector3(ArenaM, MaxM, ArenaM);
        data.SetHeights(0, 0, new float[Export, Export]);
        terrain.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        info = $"Scene flattened · Ceiling {MaxM:F0} m";
        ToolLog.Write($"Terrain flattened, vertical ceiling {MaxM:F0} m.");
        Repaint();
    }

    const string RouteAssetPath = "Assets/Settings/MountainRoute.asset";

    void SaveRoutes(Terrain terrain)
    {
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RouteAssetPath);
        if (route == null)
        {
            route = ScriptableObject.CreateInstance<MountainRoute>();
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(route, RouteAssetPath);
        }

        route.road.Clear();
        route.branches.Clear();

        for (int i = 0; i < paths.Length; i++)
        {
            var marks = new List<MountainRoute.Mark>(paths[i].pts.Count);
            foreach (var q in paths[i].pts)
                marks.Add(new MountainRoute.Mark { position = ToNorm(q), radius = routeRadiusM });

            if (i == 0) route.road.AddRange(marks);
            else route.branches.Add(new MountainRoute.Branch { name = paths[i].name, marks = marks });
        }

        if (!float.IsNaN(spawn.x))
        {
            route.spawn = ToNorm(spawn);
            route.spawnSet = true;

            int bi = 0; float best = float.MinValue;
            for (int i = 0; i < h.Length; i++) if (h[i] > best) { best = h[i]; bi = i; }
            float sx = ((bi % Grid) * CellM - ArenaM * 0.5f) / 1000f;
            float sz = ((bi / Grid) * CellM - ArenaM * 0.5f) / 1000f;
            route.spawnYaw = Mathf.Atan2(sz - spawn.y, sx - spawn.x) * Mathf.Rad2Deg;
        }

        EditorUtility.SetDirty(route);
    }

    static Vector2 ToNorm(Vector2 km)
        => new Vector2(km.x * 1000f / ArenaM + 0.5f, km.y * 1000f / ArenaM + 0.5f);

    void AddFineDetail(float[,] norm)
    {
        int n = Export;
        float cell = ArenaM / (n - 1);
        var g = new float[n * n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            g[z * n + x] = norm[z, x] * MaxM;

        var mask = new float[n * n];
        float lowRef = plainM + 60f;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, n - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, n - 1);
            float dx = (g[z * n + xp] - g[z * n + xm]) / ((xp - xm) * cell);
            float dz = (g[zp * n + x] - g[zm * n + x]) / ((zp - zm) * cell);
            float deg = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

            float steep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 38f, deg));
            float high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lowRef, lowRef + 500f,
                                                                    g[z * n + x]));
            mask[z * n + x] = Mathf.Lerp(1f - fineSteepBias, 1f, steep) * high;
        }

        TerrainOps.FractalNoise(g, n, cell, fineWavelength, fineOctaves, fineAmplitude,
                                0.5f, 2f, 9137, mask);
        TerrainOps.Thermal(g, n, cell, 38f, 0.5f, 8);

        float inv = 1f / MaxM;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            norm[z, x] = Mathf.Clamp01(g[z * n + x] * inv);
    }

    void SaveAndApply()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "No terrain in scene."; return; }

        if (IsFlat() && !EditorUtility.DisplayDialog(
                "Saving Flat Terrain",
                "Working grid is completely flat. Saving will flatten the scene terrain.\n\nContinue?",
                "Yes, Save Flat", "Cancel"))
        {
            info = "Save cancelled.";
            return;
        }

        var terrain = gen.GetComponent<Terrain>();
        var data = terrain.terrainData;

        var big = new float[Export, Export];
        float inv = 1f / MaxM;
        float sc = (Grid - 1) / (float)(Export - 1);
        for (int z = 0; z < Export; z++)
        for (int x = 0; x < Export; x++)
        {
            float fx = x * sc, fz = z * sc;
            int x0 = Mathf.Min(Grid - 2, (int)fx), z0 = Mathf.Min(Grid - 2, (int)fz);
            float tx = fx - x0, tz = fz - z0;
            float a = Mathf.Lerp(h[z0 * Grid + x0], h[z0 * Grid + x0 + 1], tx);
            float b = Mathf.Lerp(h[(z0 + 1) * Grid + x0], h[(z0 + 1) * Grid + x0 + 1], tx);
            big[z, x] = Mathf.Clamp01(Mathf.Lerp(a, b, tz) * inv);
        }

        if (fineDetail) AddFineDetail(big);

        data.heightmapResolution = Export;
        data.size = new Vector3(ArenaM, MaxM, ArenaM);
        data.SetHeights(0, 0, big);
        terrain.Flush();

        SurfaceMapBaker.Invalidate();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        SaveRoutes(terrain);
        SaveSculpt(AutoName, force: true);
        dirtySinceSave = false;

        EditorUtility.DisplayProgressBar("Mountain Builder", "Baking surface maps...", 0.8f);
        try { MountainSceneBootstrap.Rebuild(); }
        finally { EditorUtility.ClearProgressBar(); }

        info = "Saved: Terrain, surface maps, and spawn updated.";
        ToolLog.Write("Mountain builder applied to terrain; bootstrap rebuild executed.");
        Repaint();
    }

    // ================================================================ Mesh

    void UpdatePaintedRegion()
    {
        if (pdx1 < pdx0 || vVerts == null || mesh == null) return;

        float cellKm = ArenaM / 1000f / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        int x0 = Mathf.Max(0, pdx0), x1 = Mathf.Min(View - 1, pdx1);
        int z0 = Mathf.Max(0, pdz0), z1 = Mathf.Min(View - 1, pdz1);
        pdx0 = pdz0 = int.MaxValue; pdx1 = pdz1 = int.MinValue;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;
            float val = h[gi];
            int vi = z * View + x;

            float delta = Mathf.Abs(val - vVerts[vi].y * 1000f);
            float scale = Mathf.Max(2f, strength * 40f);
            heat[vi] = Mathf.Clamp01(Mathf.Max(heat[vi], delta / scale));

            vVerts[vi] = new Vector3((x - (View - 1) * 0.5f) * cellKm, val / 1000f,
                                     (z - (View - 1) * 0.5f) * cellKm);

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;

            float lam = Mathf.Clamp01(Vector3.Dot(nrm, SunDir)) * 0.8f + 0.2f;
            vCols[vi] = VertexColor(gi, val, lam, m, heat[vi]);
        }

        hx0 = Mathf.Min(hx0, x0); hz0 = Mathf.Min(hz0, z0);
        hx1 = Mathf.Max(hx1, x1); hz1 = Mathf.Max(hz1, z1);

        mesh.SetVertices(vVerts);
        mesh.SetColors(vCols);
        mesh.RecalculateBounds();
    }

    void DecayHeat()
    {
        if (heat == null || hx1 < hx0 || vCols == null || mesh == null) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - lastHeatTick), 0f, 0.25f);
        lastHeatTick = now;
        if (dt <= 0f) return;

        float k = Mathf.Exp(-dt / 0.35f);
        float cellKm = ArenaM / 1000f / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        float peak = 0f;
        for (int z = hz0; z <= hz1; z++)
        for (int x = hx0; x <= hx1; x++)
        {
            int vi = z * View + x;
            if (heat[vi] <= 0.001f) { heat[vi] = 0f; continue; }

            heat[vi] *= k;
            if (heat[vi] < 0.004f) heat[vi] = 0f;
            peak = Mathf.Max(peak, heat[vi]);

            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;
            float lam = Mathf.Clamp01(Vector3.Dot(nrm, SunDir)) * 0.8f + 0.2f;

            vCols[vi] = VertexColor(gi, h[gi], lam, m, heat[vi]);
        }

        mesh.SetColors(vCols);
        if (peak <= 0f) { hx0 = hz0 = int.MaxValue; hx1 = hz1 = int.MinValue; }
        else Repaint();
    }

    void DrawInfoBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(stats, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(info)) GUILayout.Label(info, EditorStyles.miniLabel);
            if (cursorValid)
                GUILayout.Label($"Brush: ({cursor.x * 1000f:F0}, {cursor.z * 1000f:F0}) m  ·  "
                                + $"Elev: {HeightAtKm(cursor.x, cursor.z):F0} m",
                                EditorStyles.miniLabel);
        }
    }

    static readonly Vector3 SunDir = new Vector3(0.45f, 0.62f, -0.64f).normalized;

    Color VertexColor(int gi, float val, float lam, float[] m, float glow)
    {
        float band = Mathf.Clamp01((val - plainM) / 5000f);
        var rock = Color.Lerp(new Color(0.30f, 0.32f, 0.28f),
                              new Color(0.88f, 0.90f, 0.94f), Mathf.SmoothStep(0f, 1f, band));
        var c = rock * lam;
        if (m != null) c = Color.Lerp(c, new Color(1f, 0.85f, 0.15f), m[gi] * 0.65f);
        if (glow > 0.001f) c = Color.Lerp(c, new Color(0.25f, 0.95f, 1f), glow * 0.8f);
        return c;
    }

    void BuildMesh()
    {
        meshDirty = false;
        pdx0 = pdz0 = int.MaxValue; pdx1 = pdz1 = int.MinValue;

        float sizeKm = ArenaM / 1000f;
        float cellKm = sizeKm / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        if (mesh == null)
        {
            mesh = new Mesh { name = "MountainBuilder", indexFormat = IndexFormat.UInt32 };
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.MarkDynamic();
        }
        if (vVerts == null) { vVerts = new Vector3[View * View]; vCols = new Color[View * View]; }
        if (heat == null) heat = new float[View * View];

        var verts = vVerts;
        var cols = vCols;
        var sun = SunDir;

        float top = 0f, low = float.MaxValue;
        for (int z = 0; z < View; z++)
        for (int x = 0; x < View; x++)
        {
            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;
            float val = h[gi];
            if (val > top) top = val;
            if (val < low) low = val;

            verts[z * View + x] = new Vector3((x - (View - 1) * 0.5f) * cellKm, val / 1000f,
                                              (z - (View - 1) * 0.5f) * cellKm);

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;

            float lam = Mathf.Clamp01(Vector3.Dot(nrm, sun)) * 0.8f + 0.2f;
            cols[z * View + x] = VertexColor(gi, val, lam, m, 0f);
        }

        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        if (!topoBuilt)
        {
            var tris = new int[(View - 1) * (View - 1) * 6];
            int t = 0;
            for (int z = 0; z < View - 1; z++)
            for (int x = 0; x < View - 1; x++)
            {
                int i = z * View + x;
                tris[t++] = i; tris[t++] = i + View; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + View; tris[t++] = i + View + 1;
            }
            mesh.SetTriangles(tris, 0, true);
            topoBuilt = true;
        }
        else mesh.RecalculateBounds();

        if (mat == null)
        {
            mat = new Material(Shader.Find("Hidden/Internal-Colored"))
            { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.SetInt("_Cull", (int)CullMode.Back);
        }

        stats = $"Summit {top:F0} m · Base {low:F0} m · Relief {top - low:F0} m · "
              + $"Ceiling {MaxM:F0} m · Arena {sizeKm:F0} km · Cell {CellM:F1} m";
    }
}
