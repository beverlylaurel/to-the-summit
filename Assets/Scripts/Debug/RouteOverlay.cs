using System;
using UnityEngine;

/// ROTA ÇİZGİLERİ OYUN GÖRÜNÜMÜNDE. Fırça hatları yalnız Scene View'da çiziyor; oyunun
/// içinden bakınca üç hat birbirinden ayırt edilemiyor — hepsi aynı ova, aynı zemin.
///
/// Rota tasarımı oyunun içinden doğrulanmak zorunda: hattın nereden geçtiği, nereye
/// çıktığı ve öteki hatlardan nasıl ayrıldığı ancak oyuncunun gözünden görülür.
///
/// GEÇİCİ. Rota kesinleşip arazi ona göre şekillenince silinir.
public class RouteOverlay : MonoBehaviour
{
    [SerializeField] MountainRoute route;
    [SerializeField] Terrain terrain;

    /// Çizginin zeminden yüksekliği (metre). İki metreydi ve yürürken göz hizasında
    /// bir duvar gibi görüşü kesiyordu. Yirmi santim onu zeminin üstünde tutuyor ama
    /// yola serilmiş bırakıyor.
    const float Lift = 0.2f;

    /// Çizgi kalınlığı (metre). Kilometrelerce uzaktan okunabilmeli.
    const float Width = 6f;

    static readonly Color RoadColor = new(0.9f, 0.88f, 0.82f);

    static readonly Color[] BranchColors =
    {
        new(1f, 0.55f, 0.1f),
        new(0.95f, 0.25f, 0.75f),
        new(0.2f, 0.85f, 0.9f),
        new(0.95f, 0.3f, 0.25f),
        new(0.7f, 0.9f, 0.25f),
        new(0.75f, 0.6f, 1f),
    };

    Transform holder;

    /// HAT BAŞINA MATERYAL. Tek materyal kullanılıp renk `startColor`'dan veriliyordu
    /// ama `URP/Unlit` köşe rengini okumuyor: bütün hatlar materyalin beyazıyla
    /// çiziliyor ve birbirinden ayırt edilemiyordu.
    readonly System.Collections.Generic.List<Material> materials = new();

    public void Bind(MountainRoute routeRef, Terrain terrainRef)
    {
        route = routeRef;
        terrain = terrainRef;
    }

    void OnEnable()
    {
        if (route == null || terrain == null)
            throw new InvalidOperationException($"{nameof(RouteOverlay)}: bağımlılıklar atanmadı.");

        Build();
    }

    void OnDisable()
    {
        if (holder != null) DestroyOwned(holder.gameObject);
        holder = null;

        foreach (Material owned in materials) DestroyOwned(owned);
        materials.Clear();
    }

    static void DestroyOwned(UnityEngine.Object owned)
    {
        if (owned == null) return;

        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    void Build()
    {
        holder = new GameObject("Route Lines") { hideFlags = HideFlags.DontSave }.transform;
        holder.SetParent(transform, false);

        AddLine("Yol", route.road, RoadColor);

        for (int i = 0; i < route.branches.Count; i++)
            AddLine(route.branches[i].name, route.branches[i].marks,
                    BranchColors[i % BranchColors.Length]);
    }

    void AddLine(string name, System.Collections.Generic.List<MountainRoute.Mark> marks,
        Color color)
    {
        if (marks.Count < 2) return;

        var line = new GameObject(name) { hideFlags = HideFlags.DontSave }
            .AddComponent<LineRenderer>();

        line.transform.SetParent(holder, false);

        var owned = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            hideFlags = HideFlags.DontSave
        };
        owned.SetColor("_BaseColor", color);
        materials.Add(owned);

        line.sharedMaterial = owned;
        line.useWorldSpace = true;
        line.widthMultiplier = Width;
        line.numCapVertices = 2;
        line.startColor = color;
        line.endColor = color;

        // Gölge YOK: çizgi bir arazi parçası değil, bir gösterge. Gölge düşürdüğünde
        // zeminde kendi izini bırakıyor ve rotanın gerçekten orada bir şey olduğu
        // izlenimi veriyor.
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        line.positionCount = marks.Count;

        for (int i = 0; i < marks.Count; i++)
        {
            Vector3 world = MountainRoute.ToWorld(marks[i].position, terrain);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y + Lift;
            line.SetPosition(i, world);
        }
    }
}
