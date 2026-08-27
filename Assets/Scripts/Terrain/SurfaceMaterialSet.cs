using UnityEngine;

/// EVERY MAP OF ONE SURFACE. Snow, rock, gravel, soil — all from the same structure.
///
/// Instead of passing "eight separate textures" to the shader, a single asset is passed. The
/// twelve fields written by hand for snow would have been twenty-four on a second surface and
/// thirty-six on a third; at that point adding one map means touching every file.
///
/// The maps are stored in their STOCHASTICALLY TRANSFORMED form (Gaussian histogram + inverse
/// LUT). The raw texture is not used in the scene: stochastic tiling only preserves contrast in
/// Gaussian space.
[CreateAssetMenu(menuName = "To The Summit/Surface Material", fileName = "Surface")]
public class SurfaceMaterialSet : ScriptableObject
{
    [Tooltip("Source folder (may be outside the project). `TextureIngest` extracts the " +
             "maps from this folder into the project; the record stays here so that when " +
             "a texture is refreshed nobody has to hunt for where it came from.")]
    public string sourceFolder;

    [Tooltip("In-project file prefix, e.g. `RockCliff`. The maps are looked up by " +
             "appending _Normal/_Roughness/_Height to this name.")]
    public string assetPrefix;

    [Header("Stochastically transformed maps")]
    public Texture2D normal;
    public Texture2D normalLut;
    public Texture2D roughness;
    public Texture2D roughnessLut;
    public Texture2D height;
    public Texture2D heightLut;

    [Header("Measurement")]
    [Tooltip("Baked lighting: the correlation between color brightness and surface slope. " +
             "Above 0.3 means directional sun is baked into the texture — it cannot be " +
             "used as an albedo. `TextureIngest` measures it and writes it here.")]
    public float bakedLightCorrelation;

    [Tooltip("Directionality: the ratio of the normal's x and y spread. 1.0 is " +
             "directionless (powder snow), below 0.7 clearly directional (layered rock, veined surface).")]
    public float anisotropy;

    /// Whether it can be used in the scene. With a map missing the shader branch never opens.
    public bool IsComplete =>
        normal != null && normalLut != null &&
        roughness != null && roughnessLut != null &&
        height != null && heightLut != null;
}
