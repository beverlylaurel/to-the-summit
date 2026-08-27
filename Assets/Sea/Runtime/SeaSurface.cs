// ROLE: builds the sea mesh, snaps it to the camera, manages its material.
// CALLED BY: nobody — runs on its own, dependencies come from the Inspector.

using System;
using UnityEngine;

/// THE SEA MESH FOLLOWS THE CAMERA, NOT THE PLAYER.
///
/// The horizon stays correct even when the camera moves away from the sea
/// (spec §10.3).
///
/// **THE SNAP STEP IS THE FINEST QUAD SIZE.** Because every quad size is a
/// power-of-two multiple of it, a single snap step keeps every ring's
/// vertices on its own lattice (spec §10.1 alignment proof).
///
/// The snap step DOES NOT HAVE TO relate to the FFT texel size — the FFT
/// texture is sampled from world coordinates, not from mesh vertex positions.
/// The snow system's `SnapStep / texelSize` integer rule does not apply here
/// (spec §10.3 says so explicitly).
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SeaSurface : MonoBehaviour
{
    [SerializeField] SeaSettings settings;
    [SerializeField] Shader surfaceShader;

    [Tooltip("Camera the mesh follows. Camera.main is used when empty.")]
    [SerializeField] Transform followCamera;

    MeshFilter filter;
    MeshRenderer meshRenderer;
    Material material;
    Mesh mesh;

    float builtQuad = -1f;
    int builtRings = -1;

    public SeaSettings Settings => settings;

    /// WHETHER THE SEA IS INSIDE A CAMERA'S VIEW.
    ///
    /// `MeshRenderer.isVisible` reports whether ANY camera (including the
    /// scene view) sees it. With no mesh it counts as "visible": confusing
    /// absence with invisibility would silence the simulation permanently and
    /// the symptom would be "the sea is frozen".
    public bool IsVisible => meshRenderer == null || meshRenderer.isVisible;

    public void Bind(SeaSettings source, Shader shader, Transform cam)
    {
        settings = source;
        surfaceShader = shader;
        followCamera = cam;
    }

    void OnEnable()
    {
        filter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void OnDisable()
    {
        Cleanup();
    }

    void Cleanup()
    {
        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

        if (material != null)
        {
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }

        builtQuad = -1f;
        builtRings = -1;
    }

    /// THE MATERIAL AND MESH ARE GUARANTEED IN `Update`.
    ///
    /// `AssetDatabase.ImportAsset` drops materials created at runtime;
    /// `TerrainSurface` hit this and the terrain turned magenta. Same pattern
    /// here: existence is checked every frame and rebuilt when missing.
    void Update()
    {
        if (settings == null) return;

        EnsureMesh();
        EnsureMaterial();
        Snap();
    }

    void EnsureMesh()
    {
        // THE MESH COMES FROM THE QUALITY TIER. With a separate field in the
        // settings the preset and the mesh would drift apart and produce
        // "I picked Low but the triangle count did not drop" (spec §15.3).
        SeaQuality.Levels level = SeaQuality.Of(settings.quality);

        if (mesh != null &&
            Mathf.Approximately(builtQuad, level.FinestQuad) &&
            builtRings == level.RingCount)
        {
            if (filter.sharedMesh == mesh) return;
            filter.sharedMesh = mesh;
            return;
        }

        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        mesh = SeaMeshBuilder.Build(level.FinestQuad, level.RingCount);
        mesh.hideFlags = HideFlags.DontSave;

        filter.sharedMesh = mesh;

        builtQuad = level.FinestQuad;
        builtRings = level.RingCount;
    }

    void EnsureMaterial()
    {
        if (material != null && meshRenderer.sharedMaterial == material) return;

        if (surfaceShader == null)
            throw new InvalidOperationException(
                $"{nameof(SeaSurface)}: {nameof(surfaceShader)} is not assigned.");

        if (material == null)
            material = new Material(surfaceShader) { hideFlags = HideFlags.DontSave };

        meshRenderer.sharedMaterial = material;

        // NO SHADOWS. The sea is a flat surface; casting its own shadow is
        // both a cost and wrong (wave shadowing lives in the shader, not in
        // the geometry).
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
    }

    void Snap()
    {
        Transform cam = followCamera != null ? followCamera
                      : (Camera.main != null ? Camera.main.transform : null);

        if (cam == null) return;

        float step = SeaQuality.Of(settings.quality).FinestQuad;

        Vector3 c = cam.position;
        float sx = Mathf.Floor(c.x / step) * step;
        float sz = Mathf.Floor(c.z / step) * step;

        transform.position = new Vector3(sx, settings.seaLevelY, sz);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
