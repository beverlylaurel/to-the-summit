using System;
using UnityEngine;

/// Measures how exposed the terrain is to the wind and pushes it into `WindField`.
///
/// The wind does not know the terrain — nor should it, it has to remain a source. But the
/// terrain knows the wind: a ridge compresses and accelerates the air crossing it, a hollow cuts
/// it. That is one of the biggest differences felt on a mountain and it never formed at all
/// while the wind was global.
///
/// THE MEASUREMENT IS NOT ITS OWN COMPUTATION BUT A BAKED MAP. Two height samples along the wind
/// axis used to be taken here and the relief computed; if another system answers the same
/// question ("how sheltered is this point from the wind") the two diverge — the player could
/// feel full wind where the surface counted a leeward drift. There cannot be two sources for the
/// same quantity.
///
/// The baked map builds the same physics better: slope AND curvature, a 103 metre Gaussian
/// kernel, the prevailing wind axis (see `SurfaceMapBaker.BakeWindWeight`). The two-point sample
/// here could mistake a single boulder for a ridge.
[RequireComponent(typeof(WindField))]
public class TerrainWindShelter : MonoBehaviour
{
    [Tooltip("Where the exposure is measured. The player themselves.")]
    [SerializeField] Transform observer;
    [Tooltip("The component holding the deposition weight map.")]
    [SerializeField] TerrainSurface surface;
    [SerializeField] WindField wind;

    /// Arrival time of the exposure (seconds). Read instantaneously the wind jumps when the
    /// player takes a single step; an air mass does not change direction that fast.
    const float Smoothing = 1.5f;

    float exposure = 0.6f;

    void OnEnable()
    {
        if (observer == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(observer)} is not assigned.");
        if (surface == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(surface)} is not assigned.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(TerrainWindShelter)}: {nameof(wind)} is not assigned.");
    }

    public void Bind(Transform target, TerrainSurface terrainSurface, WindField field)
    {
        observer = target;
        surface = terrainSurface;
        wind = field;
    }

    void Update()
    {
        // The map carries the DEPOSITION weight (0.67-2.0); the wind speed multiplier is its
        // inverse (0.5-1.5). The exposure contract is 0-1 with 0.5 in the middle — subtracting a
        // half from the multiplier lines them up exactly.
        float windSpeedFactor = 1f / surface.WindWeightAt(observer.position);
        float target = Mathf.Clamp01(windSpeedFactor - 0.5f);

        exposure = Mathf.Lerp(exposure, target,
            1f - Mathf.Exp(-Time.deltaTime / Smoothing));

        wind.Exposure = exposure;
    }
}
