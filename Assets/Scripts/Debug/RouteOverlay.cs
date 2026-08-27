using System;
using UnityEngine;

/// ROUTE LINES IN THE GAME VIEW. The brush strokes only draw in the Scene View; seen from
/// inside the game the three routes are indistinguishable — all the same plain, the same ground.
///
/// Route design has to be verified from inside the game: where a route runs, where it comes out
/// and how it differs from the others is only visible through the player's eyes.
///
/// TEMPORARY. Once the route is settled and the terrain is shaped to it this is deleted.
public class RouteOverlay : MonoBehaviour
{
    [SerializeField] MountainRoute route;
    [SerializeField] Terrain terrain;

    /// Height of the line above the ground (metres). It was two metres and while walking it cut
    /// the view like a wall at eye level. Twenty centimetres keeps it above the ground while
    /// leaving it laid out on the path.
    const float Lift = 0.2f;

    /// Line thickness (metres). It has to be readable from kilometres away.
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

    /// A MATERIAL PER ROUTE. A single material was used with the color given through
    /// `startColor`, but `URP/Unlit` does not read the vertex color: every route was drawn in
    /// the material's white and they could not be told apart.
    readonly System.Collections.Generic.List<Material> materials = new();

    public void Bind(MountainRoute routeRef, Terrain terrainRef)
    {
        route = routeRef;
        terrain = terrainRef;
    }

    void OnEnable()
    {
        if (route == null || terrain == null)
            throw new InvalidOperationException($"{nameof(RouteOverlay)}: dependencies are not assigned.");

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

        // NO shadow: the line is not a piece of terrain, it is an indicator. Casting a shadow it
        // leaves its own mark on the ground and gives the impression that there really is
        // something there along the route.
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
