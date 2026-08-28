// ROL: kar yuzeyinin fotogrametri dokularini durum degiskenlerinden harmanlar.
// Cagiran: SnowLighting.hlsl (SnowBuildSurface).

#ifndef SNOW_SURFACE_TEXTURES_INCLUDED
#define SNOW_SURFACE_TEXTURES_INCLUDED

#include "SnowCommon.hlsl"
#include "../../Shaders/StochasticTiling.hlsl"

/// FOUR SNOW SURFACES, FOUR PHYSICAL STATES.
///
/// The albedo used to be lerped between two constants derived from the density alone
/// (fresh 0.90 / compacted 0.70). The surface had no texture of its own: the snow was the
/// same flat colour everywhere and the trail's procedural edge did not sit in the space
/// around it (the user reported it: "there is no harmony between the trail's break-up
/// edges and the snow outside").
///
/// All four feed from the SAME state chain, no separate source is set up:
///
///   FRESH     low density, undisturbed  -> flat, featureless cover
///   POWDER    dry and cold              -> fine grained, granular
///   SETTLED   high density              -> clumpy, medium rough
///   WIND      high wind exposure        -> grooved, sastrugi-streaked
///
/// The temperature and the wind already come from the atmosphere state (`_TemperatureC`,
/// `SampleWindShadow`); the density and the disturbance from the snow texture. So the
/// surface's look derives from the same single state as the weather.
TEXTURE2D(_SnowSurfTazeColor);      TEXTURE2D(_SnowSurfTazeNormal);      TEXTURE2D(_SnowSurfTazeRough);
TEXTURE2D(_SnowSurfTozColor);       TEXTURE2D(_SnowSurfTozNormal);       TEXTURE2D(_SnowSurfTozRough);
TEXTURE2D(_SnowSurfYerlesmisColor); TEXTURE2D(_SnowSurfYerlesmisNormal); TEXTURE2D(_SnowSurfYerlesmisRough);
TEXTURE2D(_SnowSurfWindColor);    TEXTURE2D(_SnowSurfWindNormal);    TEXTURE2D(_SnowSurfWindRough);
/// A SHARED GLOBAL SAMPLER. Declaring a SAMPLER() per texture broke in the DepthNormals
/// pass: because that pass does not use colour the compiler culls the texture, an unpaired
/// sampler is left, and it gives a "does not match any texture" error. URP's global sampler
/// is valid in every pass.
#define SNOW_SURF_SAMPLER sampler_TrilinearRepeat

/// Dokunun dunya olcegi: bir dosemenin kapladigi metre.
float _SnowSurfTileMeters;

/// Doku katkisinin gucu. 0 = eski duz renk, 1 = dokunun tamami.
float _SnowSurfStrength;

struct SnowSurfaceBlend
{
    half3 albedoTint;   // 1 civarinda multiplier
    half  roughAdd;     // puruzluluge eklenen deviation
    half2 normalSlope;  // slope uzayinda detay (n.xy / n.z)
};

/// Dort dokunun agirligi. Toplam 1'e normalize.
half4 SnowSurfaceWeights(float rhoN, float wet, float disturb, float3 posWS)
{
    // YOGUNLUK: taze (dusuk) <-> yerlesmis (yuksek)
    half packed = (half)saturate((SnowDensity(rhoN) - 100.0) / 250.0);

    // DRYNESS: cold, dry snow stays powder; snow that gets wet clumps.
    // Fully powder below -12 C, none above -2 C.
    half kuru = (half)saturate((-_TemperatureC - 2.0) / 10.0) * (half)(1.0 - saturate(wet * 2.0));

    // WIND EXPOSURE: a sheltered surface does not hold grooves.
    //
    // THE LINK WAS INVERTED. `SampleWindShadow` measures SHELTEREDNESS — spec 18.0's own
    // comment is "> 0 -> in shadow (deposition zone), 0 -> open (erosion possible)". The
    // code was tying that value in direct proportion to the "wind texture" weight, i.e. it
    // was drawing the grooved and sastrugi-streaked texture onto the SHELTERED surface.
    // The comment itself said the opposite.
    //
    // Sastrugi and grooves are EROSION forms; spec 18.0 closes erosion in shadow entirely
    // ("curvW is zeroed -> no erosion, deposition only"). In shelter the snow accumulates
    // and stays soft and flat.
    half wind = (half)(1.0 - saturate(SampleWindShadow(posWS) * 1.2));

    // Bozulmus (uzerinden gecilmis) kar dokusunu yitirir, yerlesmise yaklasir.
    packed = max(packed, (half)saturate(disturb));

    half wWind    = wind * (half)0.9;
    half wToz       = kuru * ((half)1.0 - wWind) * (half)0.9;
    half wYerlesmis = packed * ((half)1.0 - wWind) * ((half)1.0 - wToz);
    half wTaze      = (half)1.0 - wWind - wToz - wYerlesmis;

    half4 w = half4(max(wTaze, (half)0.0), wToz, wYerlesmis, wWind);
    return w / max(w.x + w.y + w.z + w.w, (half)1e-4);
}

/// Reads the normal map in slope space. Slope summation preserves the base by construction,
/// unlike RNM (the same reasoning as in `SnowDetailNormals.hlsl`).
/// A PHYSICAL CEILING ON THE SLOPE.
///
/// `n.xy / n.z` blows up as the normal map's blue channel approaches zero. BC7 compression
/// pushes individual texels to that limit and ISOLATED dark blue dots appeared on screen
/// (measured: at strength 1.6 the ground was mottled). The micro relief of a snow surface
/// does not exceed 45 degrees — snow does not hold at that slope, it flows. tan(45)=1 is
/// the ceiling, 0.7 (35 degrees) with a safety margin.
///
/// IT CAME DOWN TO 0.35 = 19 degrees. At 0.7 the WORM pattern of the photogrammetry
/// normals was carried straight to the screen: the `Wind` and `Settled` maps are full of
/// winding grooves and at that slope the surface closed up with dark curls (the user
/// reported it: "where does this dark pattern come from"). The micro relief of a snow
/// surface is not that steep.
#define SNOW_SURF_SLOPE_MAX 0.35

half2 SnowSurfSlopeClamp(half2 e)
{
    half boy = length(e);
    return boy > (half)SNOW_SURF_SLOPE_MAX
         ? e * ((half)SNOW_SURF_SLOPE_MAX / boy)
         : e;
}

half2 SnowSurfSlopeMax(half3 n)
{
    return SnowSurfSlopeClamp(n.xy / max(n.z, (half)0.2));
}

/// A STOCHASTIC READ — THE TILE REPEAT IS BROKEN.
///
/// Plain tiling repeats itself at 2.5 m and the ground reads as a regular grid of patches
/// (measured: the repeat arrived the moment the relief did). Heitz-Neyret's hexagonal grid
/// gives every cell its own offset and the period disappears.
///
/// VARIANCE RECOVERY IS VALID ON THE SLOPE. The weighted average of three samples lowers
/// the amplitude by `length(weights)`; because the slope is a ZERO-MEAN quantity, dividing
/// by the same coefficient brings the amplitude back exactly.
/// (It is not valid on colour, where the mean is not zero — that is why the colour is read
/// with a single sample; the albedo variation is 2% anyway and its repeat is not visible.)
/// A ROTATION PER CELL. The stochastic grid only OFFSETS the texture; the pattern's
/// direction stays the same in every cell and the eye catches it as "the same mark everywhere".
///
/// THE ROTATION IS ABOUT THE CELL CENTRE AND THE DERIVATIVES ROTATE TOO.
///
/// The first version rotated the UV about the ORIGIN. The world coordinate is thousands of
/// units; rotated by a random angle the sample lands somewhere completely different every
/// frame. Worse, the derivatives were not rotated: the mip selection came out wrong and the
/// surface turned into a high-frequency, dense, regular noise (the user reported it:
/// "why are there so many marks, why are they regular").
///
/// The rotation matrix is handed outside so `SAMPLE_TEXTURE2D_GRAD` can be given
/// derivatives that went through the same rotation.
float2x2 SnowSurfDonme(float2 hucre)
{
    float aci = StochasticHash(hucre).x * 6.2831853;
    float sn, cs;
    sincos(aci, sn, cs);
    return float2x2(cs, -sn, sn, cs);
}

half2 SnowSurfSlope(TEXTURE2D_PARAM(tex, samplerState), float2 uv)
{
    float2 v1, v2, v3;
    float3 w;
    StochasticHexGrid(uv, v1, v2, v3, w);

    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    float2x2 R1 = SnowSurfDonme(v1);
    float2x2 R2 = SnowSurfDonme(v2);
    float2x2 R3 = SnowSurfDonme(v3);

    // Rotate about the cell centre, then add its own offset.
    float2 u1 = mul(R1, uv - v1) + v1 + StochasticHash(v1);
    float2 u2 = mul(R2, uv - v2) + v2 + StochasticHash(v2);
    float2 u3 = mul(R3, uv - v3) + v3 + StochasticHash(v3);

    half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, u1, mul(R1, dx), mul(R1, dy)));
    half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, u2, mul(R2, dx), mul(R2, dy)));
    half3 n3 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, u3, mul(R3, dx), mul(R3, dy)));

    // THE SLOPE WAS READ IN ROTATED SPACE AND IS TURNED BACK TO THE WORLD. Without this
    // every cell's relief faces a random direction and the light falls inconsistently.
    half2 e1 = (half2)mul(SnowSurfSlopeMax(n1), R1);
    half2 e2 = (half2)mul(SnowSurfSlopeMax(n2), R2);
    half2 e3 = (half2)mul(SnowSurfSlopeMax(n3), R3);

    half2 harman = (half)w.x * e1 + (half)w.y * e2 + (half)w.z * e3;
    return SnowSurfSlopeClamp(harman / (half)max(length(w), 1e-3));
}

/// TWO SCALES. A texture read at a single scale gives relief OF THE SAME SIZE EVERYWHERE
/// even when it is offset and rotated; the eye recognizes that size and reads the pattern as
/// "repeating". A second read, three times larger, breaks that singularity — the same
/// texture but at a different scale, i.e. like a different surface.
///
/// Amplitude: the large scale contributes less, because on a real snow surface the energy
/// falls with frequency too.
half2 SnowSurfTwoScale(TEXTURE2D_PARAM(tex, samplerState), float2 uv)
{
    return SnowSurfSlope(TEXTURE2D_ARGS(tex, samplerState), uv) * (half)0.62
         + SnowSurfSlope(TEXTURE2D_ARGS(tex, samplerState), uv * 0.27) * (half)0.28;
}

/// The blend of the four textures. A texture whose weight is negligible IS NOT READ:
/// four full reads (colour + normal + roughness) mean twelve texture accesses and for most
/// pixels two of them are at zero weight anyway.
SnowSurfaceBlend SnowSampleSurface(float3 posWS, float rhoN, float wet, float disturb)
{
    SnowSurfaceBlend o;
    o.albedoTint  = half3(1, 1, 1);
    o.roughAdd    = 0;
    o.normalSlope = half2(0, 0);

    if (_SnowSurfStrength <= 0.001) return o;

    // THE RELIEF AND THE COLOUR CLOSE AT DIFFERENT DISTANCES. The reasoning is next to
    // `SNOW_SURF_RELIEF_FADE_START`: relief falling below a pixel produces aliasing and has
    // to be cut, but the mip already averages the colour pattern.
    // Cutting all three together left flat white beyond 28 m.
    float kameraMesafe = distance(posWS, _WorldSpaceCameraPos);

    half colorAmount = (half)(1.0 - smoothstep(SNOW_SURF_RENK_FADE_START,
                                            SNOW_SURF_RENK_FADE_END, kameraMesafe));
    if (colorAmount <= 0.01h) return o;

    half reliefAmount = (half)(1.0 - smoothstep(SNOW_SURF_RELIEF_FADE_START,
                                               SNOW_SURF_RELIEF_FADE_END, kameraMesafe));

    // If the relief is off, DO NOT READ the normal and roughness textures at all: at
    // distance the texture accesses drop from twelve to four.
    bool kabartiVar = reliefAmount > 0.01h;

    half4 w = SnowSurfaceWeights(rhoN, wet, disturb, posWS);
    float2 uv = posWS.xz / max(_SnowSurfTileMeters, 0.01);

    // A TWO-SCALE FIELD WARP — SO THE PATTERN DOES NOT REPEAT ITSELF.
    //
    // The stochastic tiling breaks the period but the read still comes from a REGULAR
    // grid: the same patch appears in the same direction, at the same spacing
    // (the user reported it: "the snow texture is very regular"). On a real snow surface
    // the pattern flows, stretches and curls with the wind.
    //
    // Once the coordinate itself is warped the pattern no longer stands on a straight
    // grid: the long wave drags the patches, the short wave gnaws at their edges. The
    // amplitude is below its own wavelength in both — above it the texture would fold
    // onto itself and blur.
    float2 bukum =
        (float2(SnowValueNoise(posWS.xz * 0.11),
                SnowValueNoise(posWS.xz * 0.11 + 23.7)) * 2.0 - 1.0) * 0.65 +
        (float2(SnowValueNoise(posWS.xz * 0.47),
                SnowValueNoise(posWS.xz * 0.47 + 61.3)) * 2.0 - 1.0) * 0.14;

    uv += bukum;

    half3 renk = 0;
    half  roughVal = 0;
    half2 slope = 0;

    // EACH TEXTURE'S OWN SPATIAL MEAN, MEASURED IN LINEAR SPACE.
    // (Assets/Snow/Textures/Surface, a 256x256 sample.)
    half3 ortToplam = 0;

    const half THRESHOLD = 0.02;

    if (w.x > THRESHOLD)
    {
        renk += w.x * SAMPLE_TEXTURE2D(_SnowSurfTazeColor, SNOW_SURF_SAMPLER, uv).rgb;

        ortToplam += w.x * half3(0.8434, 0.8965, 0.9446);
        if (kabartiVar)
        {
            roughVal += w.x * SAMPLE_TEXTURE2D(_SnowSurfTazeRough, SNOW_SURF_SAMPLER, uv).r;
            slope += w.x * SnowSurfTwoScale(TEXTURE2D_ARGS(_SnowSurfTazeNormal, SNOW_SURF_SAMPLER), uv);
        }
    }
    if (w.y > THRESHOLD)
    {
        renk += w.y * SAMPLE_TEXTURE2D(_SnowSurfTozColor, SNOW_SURF_SAMPLER, uv).rgb;

        ortToplam += w.y * half3(0.2949, 0.2990, 0.3019);
        if (kabartiVar)
        {
            roughVal += w.y * SAMPLE_TEXTURE2D(_SnowSurfTozRough, SNOW_SURF_SAMPLER, uv).r;
            slope += w.y * SnowSurfTwoScale(TEXTURE2D_ARGS(_SnowSurfTozNormal, SNOW_SURF_SAMPLER), uv);
        }
    }
    if (w.z > THRESHOLD)
    {
        renk += w.z * SAMPLE_TEXTURE2D(_SnowSurfYerlesmisColor, SNOW_SURF_SAMPLER, uv).rgb;

        ortToplam += w.z * half3(0.7740, 0.8602, 0.9412);
        if (kabartiVar)
        {
            roughVal += w.z * SAMPLE_TEXTURE2D(_SnowSurfYerlesmisRough, SNOW_SURF_SAMPLER, uv).r;
            slope += w.z * SnowSurfTwoScale(TEXTURE2D_ARGS(_SnowSurfYerlesmisNormal, SNOW_SURF_SAMPLER), uv);
        }
    }
    if (w.w > THRESHOLD)
    {
        renk += w.w * SAMPLE_TEXTURE2D(_SnowSurfWindColor, SNOW_SURF_SAMPLER, uv).rgb;

        ortToplam += w.w * half3(0.7585, 0.8271, 0.8837);
        if (kabartiVar)
        {
            roughVal += w.w * SAMPLE_TEXTURE2D(_SnowSurfWindRough, SNOW_SURF_SAMPLER, uv).r;
            slope += w.w * SnowSurfTwoScale(TEXTURE2D_ARGS(_SnowSurfWindNormal, SNOW_SURF_SAMPLER), uv);
        }
    }

    // THE COLOUR ENTERS AS A MULTIPLIER, IT DOES NOT REPLACE.
    //
    // The albedo's level comes from physics (fresh 0.90 / compacted 0.70) and the
    // lighting chain is tuned to that range. Put in its place, the snow would carry the
    // photogrammetry sample's own exposure. The texture's job is to give the PATTERN: it
    // is divided by its own mean to become a multiplier around 1, then softened by the strength.
    // THE MEAN IS THE TEXTURE'S SPATIAL MEAN, NOT THE PIXEL'S OWN BRIGHTNESS.
    //
    // `(colour.r+colour.g+colour.b)/3` was used first: that is THIS pixel's own
    // brightness. Divided by it every pixel normalizes to 1 and the texture's brightness
    // pattern is erased entirely; only the hue is left.
    // Measured: between strength 0 and 3 the screen deviation went 0.01003 -> 0.00971, i.e.
    // the pattern never arrived. A fixed spatial mean leaves the pattern in place.
    half3 ortalama = max(ortToplam, (half3)1e-3);

    // CONTRAST RECOVERY. Snow's own texture contrast is low (everything is white);
    // divided by the mean the multiplier squeezes into 0.97-1.03 and is not visible on
    // screen at all. The deviation is pushed away from the mean to make the pattern
    // visible; the level still stays around 1, so the albedo's physically derived
    // magnitude is preserved.
    // A CEILING ON THE MULTIPLIER. The spatial variation of snow albedo does not really
    // exceed 20%; a white substance's pattern is in its relief, not its colour. Without a
    // limit, as the strength grew the multiplier diverged channel by channel and turned the
    // surface into a saturated blue/mustard patch (seen while drawing the material at strength 3).
    half3 multiplier = clamp((half)1.0 + (renk / ortalama - (half)1.0) * (half)2.5,
                         (half3)0.8, (half3)1.2);

    // MACRO VARIATION. A real snow field is not equally rough everywhere: what the wind
    // has swept is hard and flat, its lee is puffy. The strength wanders between 0.55 and
    // 1.45 with a 40 m scale noise; even with the pattern unchanged it does not read as
    // "the same everywhere" on screen.
    half makro = (half)(0.55 + SnowValueNoise(posWS.xz * 0.025) * 0.9);
    half baseVal = (half)_SnowSurfStrength * makro;

    o.albedoTint  = lerp(half3(1, 1, 1), multiplier, baseVal * colorAmount);
    o.roughAdd    = (roughVal - (half)0.5) * (half)0.25 * baseVal * reliefAmount;
    o.normalSlope = slope * baseVal * reliefAmount;
    return o;
}

#endif
