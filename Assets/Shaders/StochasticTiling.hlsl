#ifndef TOTHESUMMIT_STOCHASTIC_TILING_INCLUDED
#define TOTHESUMMIT_STOCHASTIC_TILING_INCLUDED

// HEITZ-NEYRET STOCHASTIC TILING.
//
// The problem: tiled over metres a texture repeats and reads as a grid. A plain
// blend weakens the repetition but also lowers the VARIANCE — the average of two
// samples means half the contrast, and the texture goes blurry.
//
// The method: the plane is split into a hexagonal grid and every pixel belongs to
// three vertices. Each vertex samples the texture with ITS OWN random offset; the three samples are blended barycentrically.
// Because the offset differs per cell the repetition period disappears.
//
// Contrast is preserved because the texture was converted to a Gaussian histogram
// IN A PREPASS (StochasticTextureBaker). A weighted sum of Gaussian variables is
// still Gaussian; dividing by the root sum of squared weights keeps the variance at
// one. The result is passed through an inverse LUT back to the original histogram.

/// Hexagonal grid: splits a UV into three vertices and barycentric weights.
void StochasticHexGrid(float2 uv, out float2 vertex1, out float2 vertex2,
                       out float2 vertex3, out float3 weights)
{
    // Coordinate skewed onto an equilateral triangle grid. 1.7320508 = sqrt 3.
    const float2x2 gridToSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
    float2 skewed = mul(gridToSkewed, uv * 3.4641016);

    float2 baseId = floor(skewed);
    float3 temp = float3(frac(skewed), 0.0);
    temp.z = 1.0 - temp.x - temp.y;

    // Vertices and weights depending on which half of the triangle we are in.
    if (temp.z > 0.0)
    {
        weights = float3(temp.z, temp.y, temp.x);
        vertex1 = baseId;
        vertex2 = baseId + float2(0.0, 1.0);
        vertex3 = baseId + float2(1.0, 0.0);
    }
    else
    {
        weights = float3(-temp.z, 1.0 - temp.y, 1.0 - temp.x);
        vertex1 = baseId + float2(1.0, 1.0);
        vertex2 = baseId + float2(1.0, 0.0);
        vertex3 = baseId + float2(0.0, 1.0);
    }
}

/// A random offset per cell. The same cell always gets the same offset: the pattern
/// is stable and does not boil as the camera moves.
float2 StochasticHash(float2 cell)
{
    const float2x2 mixer = float2x2(127.1, 311.7, 269.5, 183.3);
    return frac(sin(mul(mixer, cell)) * 43758.5453);
}

/// Three-sample stochastic read. `texture`/`samplerState` is the Gaussian-transformed
/// texture, `lut` the inverse transform table.
///
/// The derivatives are passed IN BY HAND: because each sample is read from a
/// different offset, the hardware-computed derivative jumps at cell boundaries and
/// the mip level skipped in one-pixel-wide lines.
///
/// The textures are taken with TEXTURE2D_PARAM: `TEXTURE2D(x)` in a parameter list
/// produces a DECLARATION, not a parameter — the texture never reaches the function.
float4 SampleStochastic(TEXTURE2D_PARAM(tex, samplerState),
                        TEXTURE2D_PARAM(lut, lutSampler),
                        float2 uv, float2 ddxUV, float2 ddyUV)
{
    float2 vertex1, vertex2, vertex3;
    float3 weights;
    StochasticHexGrid(uv, vertex1, vertex2, vertex3, weights);

    float4 sample1 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex1), ddxUV, ddyUV);
    float4 sample2 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex2), ddxUV, ddyUV);
    float4 sample3 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex3), ddxUV, ddyUV);

    // VARIANCE PRESERVATION: the standard deviation of a weighted sum of Gaussian
    // samples shrinks by the root sum of the weights. Dividing by it keeps the
    // distribution at one; skip this step and the blend goes blurry again, which defeats the whole method.
    float4 mixed = weights.x * sample1 + weights.y * sample2 + weights.z * sample3;
    mixed = (mixed - 0.5) / length(weights) + 0.5;

    // Inverse LUT: from Gaussian space back to the original histogram. A separate
    // read per channel — the transform was done per channel too.
    float4 result;
    result.r = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.r), 0.5), 0).r;
    result.g = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.g), 0.5), 0).g;
    result.b = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.b), 0.5), 0).b;
    result.a = 1.0;
    return result;
}

/// STOCHASTIC READ WITHOUT A LUT — FOR MASKS.
///
/// `SampleStochastic` wants a Gaussian-transformed texture and an inverse histogram
/// table. Masks (like the snow edge noise) have neither, and do not need them:
/// the mask is thresholded anyway and exact histogram preservation is invisible.
/// What remains is the blend of three offset samples and the VARIANCE RECOVERY —
/// skip that step and the blend goes blurry and the method is pointless.
///
/// Why it is needed: with plain tiling the same pattern repeats at a fixed period
/// and from above the ground looks like A REGULAR GRID (reported by the user:
/// "the trails are too regular, they don't look procedural").
float SampleStochasticMask(TEXTURE2D_PARAM(tex, samplerState), float2 uv)
{
    float2 vertex1, vertex2, vertex3;
    float3 weights;
    StochasticHexGrid(uv, vertex1, vertex2, vertex3, weights);

    // Derivatives BY HAND: because each sample is read from a different offset the
    // hardware derivative jumps at cell boundaries and the mip skips in one-pixel lines.
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    float a = SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(vertex1), dx, dy).r;
    float b = SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(vertex2), dx, dy).r;
    float c = SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(vertex3), dx, dy).r;

    float mixed = weights.x * a + weights.y * b + weights.z * c;

    return saturate((mixed - 0.5) / length(weights) + 0.5);
}

#endif
