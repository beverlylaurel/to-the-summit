#ifndef TOTHESUMMIT_HEIGHT_FOG_INCLUDED
#define TOTHESUMMIT_HEIGHT_FOG_INCLUDED

// Height fog. The air's own computation — independent of which surface is drawn.
//
// Unity's fog is height independent: it stays at the same density at the summit and at
// the foot, and climbing had no visual consequence. That is why it is never used and the
// attenuation is computed here.
//
// It lives in its own file because it is not a property of any one surface: the mountain,
// rock, props, whatever is drawn stands in the same air. Kept inside a surface shader it
// would either be copied when a second surface arrived or that surface would go unfogged —
// both mean two different visibilities in the same air.
//
// The density model lives in a SEPARATE FILE: the compute shader driving the froxel volume
// includes it and cannot include this one. The reasoning is written there.
#include "VolumetricFogShared.hlsl"

// Outside the constant buffer: AtmosphereController writes these globally.
float4 _HeightFogColor;
float4 _HeightFogShadowColor; // the anti-solar horizon: Earth's shadow, shared with the sky
float4 _HeightFogZenith;   // the sky's zenith color: the air darkens toward it as the ray steepens
float4 _HeightFogSunColor; // the sky toward the sun, 2° above the horizon
float3 _SunDirection;      // AtmosphereController writes it globally; sky and clouds read it too

// Lightning: LightningFlash writes it, cloud and sky read the same value.
//
// The cloud compositing pass includes this file and `VolumetricCloudsDefs.hlsl` declares
// the same global itself. When both meet in one compilation unit a redeclaration error
// follows; if it was declared there it is skipped here. Value and behaviour are identical.
#ifndef URP_VOLUMETRIC_CLOUDS_DEFINES_HLSL
float4 _LightningFlash;
float4 _LightningPosition;   // xyz world position of the flash, w blob radius
#endif

/// The fog's share of the flash. Less than the cloud's: the cloud mass is kilometres deep
/// with the flash inside it, while the fog is thin and sits below the discharge.
static const float LightningFogScatter = 0.6;

/// LIGHTNING SCATTERING TABLE — `[Dobashi 2001, §4.4]`. Baked by `LightningLutBaker`.
/// The axes are SIGNED SQUARE ROOT; the mapping here is the INVERSE of the baker's, and
/// if the two drift apart the table shifts.
TEXTURE2D(_LightningScatterLut);
SAMPLER(sampler_LightningScatterLut);
float _LightningScatterT;

/// POINT SOURCES ALONG THE CHANNEL. Filled in by `LightningFlash`; evenly spaced from the
/// discharge point down to the slope. With a single source the glow looks like a sphere and
/// says nothing about where the bolt reaches.
/// Clear-air extinction, per channel. The SAME model as `LightningLutBaker`, which bakes
/// the table: 30 km visibility at 550 nm, Rayleigh (lambda^-4). The occlusion correction needs it.
/// 675 / 520 / 460 nm respectively.
static const float3 LightningExtinction = float3(1.469313e-05, 4.171729e-05, 6.812369e-05);

#ifndef URP_VOLUMETRIC_CLOUDS_DEFINES_HLSL
#define LIGHTNING_MAX_SOURCES 8
float4 _LightningSources[LIGHTNING_MAX_SOURCES];
float _LightningSourceCount;
#endif

/// The glow around a flash: light scattered by particles in the air reaching the eye.
///
/// THIS TERM IS ADDED, NOT MULTIPLIED BY THE LOCAL FOG. It used to enter `AirColor` as
/// `_LightningFlash.rgb * 0.6` and was weighted by the fog's opacity — so in clear air the
/// glow was ZERO. But what scatters is the AIR, which is always there; that is the whole
/// subject of the paper (§3.2, atmospheric particles). Fog is a separate medium with its own path.
///
/// `u` and `v` are the view point's coordinates in the local system centred on the source
/// (Denklem 7): `u = -d (g·e)`, `v = √(d²-u²)`.
///
/// LIMIT: like the paper, there is no TERRAIN OCCLUSION here. A slope in front of the source
/// does not cut the glow. The paper does not cut it either (§4.5, pixel intensity is a direct
/// sum). If the symptom shows, clipping by ray distance is added.
float3 LightningScatter(float3 cameraPos, float3 worldPos)
{
    if (_LightningFlash.a <= 1e-5) return 0.0;

    int count = (int)min(_LightningSourceCount, (float)LIGHTNING_MAX_SOURCES);
    if (count <= 0) return 0.0;

    float3 e = worldPos - cameraPos;
    float len = length(e);
    if (len < 1e-3) return 0.0;
    e /= len;

    float T = max(_LightningScatterT, 1.0);
    float3 sum = 0.0;

    [loop]
    for (int i = 0; i < count; i++)
    {
        float3 toSource = _LightningSources[i].xyz - cameraPos;
        float d = length(toSource);
        if (d < 1.0) continue;

        // SIGN: `u = +d (g·e)`, NOT minus.
        //
        // The paper's Equation 7 gives two forms — `d cos θ` and `-d (g·e)` — and they
        // contradict. Which is right follows from the integration limits of Equation 5:
        // the integral runs from `-T` to `u_eye` and `t = u_eye - u` is the distance
        // between the eye and P, i.e. it CANNOT BE NEGATIVE. So if the source (u=0) is in
        // front of the eye, `u_eye > 0` must hold.
        //
        // With the minus form the glow appeared not where the flash was but in EXACTLY the
        // opposite direction (measured: 348x in the wrong direction, 0.1x in the right one).
        float u = d * dot(toSource / d, e);
        float v = sqrt(max(d * d - u * u, 0.0));

        // The inverse of the baker's `sign(t)·t²·T` mapping.
        float tu = sign(u) * sqrt(min(abs(u) / T, 1.0));
        float tv = sqrt(min(v / T, 1.0));

        float2 uv = float2(tu, tv) * 0.5 + 0.5;

        float3 full = SAMPLE_TEXTURE2D_LOD(_LightningScatterLut,
                                           sampler_LightningScatterLut, uv, 0).rgb;

        // TERRAIN OCCLUSION. The table integrates the WHOLE ray; the part BEHIND the surface
        // must not show. There was glow above an intervening mountain and glow below it too,
        // and the eye read that as "the mountain is transparent".
        //
        // It falls out on paper. The integrand is `exp(-k(s + u_eye - u))` and `u_eye` can be
        // factored out: `I(x,v) = e^(-k·x)·G(x,v)`. The part up to the surface is the
        // difference of two full integrals:
        //
        //     visible = I(u_eye, v) - I(u_eye - L, v) · e^(-k·L)
        //
        // Verified numerically: in five different geometries the difference from a direct
        // computation was 0.000%.
        //
        // On a sky pixel L is the far plane, `u_eye - L` falls outside the table and the
        // second term goes to zero — so nothing changes in the sky.
        float ub = u - len;
        float tub = sign(ub) * sqrt(min(abs(ub) / T, 1.0));
        float2 uvBack = float2(tub, tv) * 0.5 + 0.5;

        float3 behind = SAMPLE_TEXTURE2D_LOD(_LightningScatterLut,
                                             sampler_LightningScatterLut, uvBack, 0).rgb;

        sum += max(full - behind * exp(-LightningExtinction * len), 0.0);
    }

    // THE ENERGY IS SPLIT ACROSS THE SOURCES (the `I_k · dl` share in Equation 6). Dividing
    // the sum by the count keeps the total brightness constant: changing the number of
    // sources does not brighten or darken the scene, only the DISTRIBUTION of the glow changes.
    return _LightningFlash.rgb * (sum / count) * LightningFogScatter;
}

// Published by TimeOfDay. Declared here because the fog file is included BEFORE the
// surface. A second name had been invented; when that global did not arrive the value
// stayed zero and the curtain, assumed to mean "the sun is low", was painted raw sky blue.
float _SunHeight;

// THE CLOUD SYSTEM WAS REMOVED — only two traces remain here.
//
// The `_CloudBottom` declaration STAYS: the lightning shader (`LightningBolt.shader`)
// intersects the flash with the cloud base sphere and reads the global from there. If it
// opened its own declaration the compiler would clash.
//
// `CloudShadowAt` WAS DELETED. The ground cloud shadow now comes through the cloud system's
// own path: `VolumetricCloudsURP` writes the shadow into the main light's cookie texture and
// URP applies it to every surface with `_LIGHT_COOKIES` enabled.
//
// The contract is then satisfied by construction (`CLOUDS_REBUILD.md` item 1): the shadow
// derives from the very density field that draws the sky. There is no second approach, and
// therefore no "shadow on the ground with no cloud in the sky".
float _CloudBottom;        // base of the layer (metres)

/// Height fog: the density integral along the path the ray travels.
///
/// Constant density fog cannot do this — it applies the same amount looking down from the
/// summit into the valley as looking up from the valley, while in the first case the ray
/// enters the dense layer and in the second it leaves it.
///
/// Because the inversion ceiling sharpens the profile there is no closed solution for the
/// integral; a few samples are taken along the path. Eight samples catch where the ceiling
/// cuts without leaving a visible step.
/// The density integral along the ray, without the bank multiplier. The caller chooses the
/// bank: the terrain samples along the path, the cloud veil reads only the camera's locality.
/// Fog and drifting snow IN ONE SWEEP. With two separate loops the same ray was swept twice
/// at the same `t` values — identical result, twice the cost.
/// Drifting snow returns separately because its color and falloff curve differ from the fog's.
float HeightFogIntegral(float3 cameraPos, float3 worldPos)
{

    float3 ray = worldPos - cameraPos;
    float distance = length(ray);

    bool hasFog = _HeightFogDensity > 0.0 || _FogSeaDensity > 0.0 || _FogFreeDensity > 0.0;

    if (distance < 0.01 || !hasFog) return 0.0;

    const int Steps = 8;

    float startHeight = cameraPos.y - _HeightFogBase;
    float endHeight = worldPos.y - _HeightFogBase;

    float sum = 0.0;

    [unroll]
    for (int i = 0; i < Steps; i++)
    {
        float t = (i + 0.5) / Steps;
        sum += FogDensityAt(lerp(startHeight, endHeight, t));
    }

    // `FogDensityAt` now returns absolute density: there is no second multiplication
    // outside, otherwise one of the two layers would be scaled twice.
    return distance * sum / Steps;
}

float HeightFogAmount(float3 cameraPos, float3 worldPos)
{
    float integral = HeightFogIntegral(cameraPos, worldPos)
                   * FogBankPath(cameraPos.xz, worldPos.xz);
    return saturate(1.0 - exp(-integral));
}

/// The fog OPTICAL DEPTH of a ray going to the sky. The terrain path is finite and
/// integrated by sampling; the sky path is infinite — each layer's exponential profile is
/// integrated in closed form. Without fogging the sky the atmosphere was not one: with fog
/// applied only to the terrain a player looking up inside the soup saw stars, and banks were
/// never drawn where there was no terrain in front of them.
///
/// It returns depth rather than metres: the three layers have different densities and
/// multiplying a single "path" number by a single density outside cannot represent all three.
/// `maxPath` is applied to EACH layer's OWN path separately — so the sun disc is not
/// extinguished by an infinite path (see Sky.shader).
float SkyFogDepth(float3 cameraPos, float3 dir, float maxPath)
{
    // DIAGNOSTIC: this function has its OWN closed formula and does not call `FogDensityAt` —
    // so the diagnostic gate there DOES NOT REACH here. The gate is placed separately,
    // otherwise the sky would stay at the old density in one place and the tool would LIE
    // with "there is a hole in the sky".
    //
    float h0 = cameraPos.y - _HeightFogBase;

    // A ray descending to the horizon: as the slope approaches zero the path settles on the
    // horizontal capacity (~100 km equivalent). The horizon saturates to the air color in every weather — as it does in reality.
    //
    // A SEPARATE INTEGRAL FOR DESCENDING RAYS WAS TRIED AND REVERTED. "A downward ray
    // crosses the column beneath it" is physically true but does not apply HERE: this
    // function only runs for SKY pixels and there is no sky below the horizon — there is
    // terrain there, or the void where the terrain ends. With the term added, in order:
    // an 87x jump at the horizon, a thin black line, the whole lower half saturating to the air color.
    // Its justification disappeared, so the term did too.
    // A SOFT FLOOR INSTEAD OF A HARD CLAMP. `max(dir.y, 0.02)` is continuous but its
    // DERIVATIVE breaks 1.15° above the horizon; the eye reads that break as a Mach band and
    // it leaves a thin line stuck to the horizon, travelling with the camera. It becomes visible as the night sky darkens.
    //
    // `sqrt(y² + floor²)` gives the same floor but is continuous to every order: it goes to
    // y for large y and to the floor at y zero, with no break in between. BELOW the horizon
    // nothing changes — negative y is clamped to zero, so the value there is exactly the floor.
    const float HorizonFloor = 0.02;
    float up = max(dir.y, 0.0);
    float s = sqrt(up * up + HorizonFloor * HorizonFloor);

    float k = _HeightFogFalloff;

    // The boundary layer ENDS at the inversion: whatever is above belongs to the free layer.
    float boundaryPath = h0 < _FogInversionHeight
        ? (exp(-k * h0) - exp(-k * _FogInversionHeight)) / (k * s)
        : 0.0;

    // Neither has a ceiling; their profiles end themselves.
    float seaPath = exp(-_FogSeaFalloff * max(0.0, h0)) / (_FogSeaFalloff * s);
    float freePath = exp(-_FogFreeFalloff * max(0.0, h0)) / (_FogFreeFalloff * s);

    // Drifting snow is only read from around the camera: the layer clings to the terrain and
    // the terrain height field cannot be integrated in closed form along the ray. A ray going
    // to the sky leaves the layer within a few tens of metres anyway; the curtain of a distant
    // ridge is computed on the terrain path.
    float sky = _HeightFogDensity * min(boundaryPath, maxPath)
              + _FogSeaDensity * min(seaPath, maxPath)
              + _FogFreeDensity * min(freePath, maxPath);

    return sky;
}

float SkyFogAmount(float3 cameraPos, float3 dir)
{
    // DIAGNOSTIC: the early exit is disabled — same reasoning as in `HeightFogIntegral`.
    if (_HeightFogDensity <= 0.0 && _FogSeaDensity <= 0.0 && _FogFreeDensity <= 0.0)
        return 0.0;

    // A bank in front of the camera leaves a visible patch in the empty sky: "fog wandering
    // through the valley" can only exist once the sky is fogged too.
    //
    // THIS MULTIPLIER WAS REMOVED AND PUT BACK. "Two samples cannot represent an infinite
    // path" is a correct argument, but removing it makes the multiplier a constant 1 and the
    // sky fog rises where the bank thins it — it showed up as blue brightness in the night
    // sky. Sampling is a separate job; a change that is right in principle but unverified
    // cannot stay inside the stack.
    float2 ahead = cameraPos.xz + normalize(dir.xz + 0.0001) * 900.0;
    float bank = (FogBankAt(cameraPos.xz) + FogBankAt(ahead)) * 0.5;

    return saturate(1.0 - exp(-SkyFogDepth(cameraPos, dir, 1e9) * bank));
}

/// The air's own color: the sky gradient itself. Both the sky and the fog call it
/// — one formula, two consumers; fully fogged terrain is indistinguishable from the sky. As
/// long as the fog carried a separate color, every weather/hour corner produced a new
/// "glowing cardboard mountain" and got patched by hand.
///
/// The redness concentrates in the direction of the sun; on the anti-solar horizon Earth's
/// shadow rises (blue-violet, and darker — with the light arriving horizontally it does not
/// fall that way). The split only happens while the sun is near the horizon. Higher up it darkens to the zenith color.
float3 AirColor(float3 direction)
{
    float3 sunward = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z) + 0.0001);
    float3 viewFlat = normalize(float3(direction.x, 0.0, direction.z) + 0.0001);
    float towardSun = smoothstep(-0.85, 0.85, dot(viewFlat, sunward));

    // The region where the horizontal direction is MEANINGFUL. Two conditions at once: near
    // the poles (straight up, straight down) the azimuth becomes undefined; and BELOW the
    // horizon there is no such thing as a sky band — a downward ray sees the air above the
    // ground, not the sky's sun band. The second was missing: the band collected to a point
    // toward the nadir and left a cone when looking at the ground at dawn. 1 at horizon level,
    // 0 at 14.5° below — near the horizon distant terrain keeps its warmth, steeply down the structure dies out.
    float azimuth = saturate(length(direction.xz) * 3.0)
                  * saturate(direction.y * 4.0 + 1.0);
    towardSun = lerp(0.5, towardSun, azimuth);

    float lowSun = 1.0 - saturate(abs(_SunDirection.y) / 0.3);

    // Twilight palette — the numbers come from a Python simulation (dusk_palette_sim.py,
    // the "vivid" variant): the whole chain — the filtered sun, the controller's mixes,
    // this formula, the golden hour grading, ACES — was plotted without a screen and fitted
    // to the ramp of a reference sunset photograph.
    //
    // A sunset sky is not one color: around the sun it is GOLD, opening out it falls to
    // orange and red, and the opposite half drops to a cold grey-blue. Gold cannot be produced
    // by multiplying the filtered sun color — green is exhausted in that color and a product
    // cannot yield yellow; the gold end is written explicitly, the red end comes from the filtered sun, and orange follows on its own.
    // The bands derive from the raw angle rather than towardSun: towardSun saturates at 0.85
    // and the band came out three times wider than intended, making the brightness unwatchable.
    // The same gate applies here: the gold end is computed independently of towardSun, and
    // ungated it is enough on its own to produce the cone.
    float sunDot = saturate(dot(viewFlat, sunward)) * azimuth;
    // The gold end is turned down a notch: full bright gold filled the sun side enough to
    // hurt the eye. The moon side comes from the shadow color, independent of this part.
    // THE GOLD END IS WRITTEN EXPLICITLY — it is not a physics sample. Tried and measured:
    // with the sun exactly on the horizon (06:00) the sky sample's luminance was 0.151 and
    // this constant 0.571 — a factor of 3.8. On screen dawn went out completely.
    //
    // The cause is not an error in the model but its SCOPE: `Atmosphere` draws a clean
    // atmosphere (Bruneton's pristine Mie coefficient). The golden burst of dawn is the work
    // of AEROSOL — dust, moisture, soot. The Mie scattering that builds the halo around the
    // sun is many times stronger in real air than in ours. So this constant is not an artistic
    // invention but the approximate equivalent of a dusty atmosphere; model the aerosol and physics takes its place.
    //
    // The constant only dominates at the sun's EXACT azimuth: it dies out with `pow(sunDot, 1.8)`,
    // and at the periphery the physics sample is in charge and tracks the hour.
    // THE TONE REDDENS WITH THE SUN'S ELEVATION. A single gold constant gave the same yellow
    // whether the sun was ten degrees above the horizon or right on it — at sunset the redness
    // never arrived.
    //
    // The physics: redness comes from PATH LENGTH. As the sun drops the light travels further
    // through the atmosphere, blue is swept out first and then green, leaving red. The
    // constant's brightness stays a deliberate exaggeration (see DECISIONS.md), but its TONE
    // now follows the sun's elevation.
    //
    // The red end cuts green to two thirds of the yellow: (0.9, 0.52) -> (0.85, 0.20).
    // Mavi zaten ihmal edilebilir.
    float3 gold = lerp(float3(0.9, 0.52, 0.11), float3(0.85, 0.20, 0.05),
                       1.0 - smoothstep(0.0, 0.09, _SunHeight));

    float3 duskHue = lerp(_HeightFogSunColor.rgb, gold, pow(sunDot, 1.8));

    float3 warm = lerp(_HeightFogColor.rgb, duskHue, pow(saturate(towardSun), 1.2) * lowSun);
    warm *= 1.0 + pow(sunDot, 8.0) * lowSun * 0.10;

    float3 horizon = lerp(_HeightFogColor.rgb,
                          lerp(_HeightFogShadowColor.rgb, warm, towardSun),
                          lowSun);

    // Exponent 0.55 -> 0.35: the warmth has to stay near the horizon. A high exponent carried
    // the horizon color halfway up the sky and made the band far wider than intended.
    //
    // The exponent's slope at 0 is INFINITE: within the first half degree of eye level the
    // blend jumps from zero to 0.15, and below it is clamped by `saturate`. With a warm horizon
    // and a deep blue zenith that break leaves a straight line like a Mach band. It was not
    // noticeable in the sky; once the terrain hazed the air color also dominated over the
    // distant mountain and the line looked like it passed through it, "as if the mountain were transparent".
    //
    // The exponent is kept — the warmth staying near the horizon comes from it. Only the first
    // three degrees are made C1 continuous with a smoothstep; above 3.4° the curve is identical.
    // Exponent 0.55 -> 0.35: the warmth has to stay near the horizon. A high exponent carried
    // the horizon color halfway up the sky and made the band far wider than intended.
    //
    // THIS CURVE WAS CHANGED AND REVERTED. Because its slope at zero is infinite it had been
    // replaced with `y(1+k)/(y+k)`; a finite slope is correct but the curve gives LESS zenith
    // over a wide band of the sky (0.208 at y=0.05 versus 0.305 before). With a bright blue
    // horizon color at night the result was "blue brightness in the air" and the horizon broke.
    // The step left by the infinite slope is already softened with `smoothstep`.
    float rise = pow(saturate(direction.y), 0.35)
               * smoothstep(0.0, 0.06, direction.y);

    float3 air = lerp(horizon, _HeightFogZenith.rgb, saturate(rise));

    // The opposite half darkens but is not pitch black: a real anti-solar horizon is a soft grey-violet
    air *= lerp(1.0, lerp(0.55, 1.0, towardSun), lowSun);

    // Forward scattering, a double lobe: a broad haze glow plus a narrow bright core. Through
    // fog the sun appears not as a sharp disc but as a glowing ball — that is the sun in dawn
    // fog; climb above the fog sea and the real disc returns.
    float sunUp = smoothstep(-0.08, 0.12, _SunDirection.y);
    float alignment = saturate(dot(direction, normalize(_SunDirection + 0.0001)));
    // The narrow lobe is kept measured: enlarged it filled the place the disc sits in and the
    // sun itself disappeared inside its own glow
    // THE NARROW LOBE DIES IN FOG, THE BROAD HALO REMAINS. The narrow lobe is the direct image
    // of the sun's DISC; in dense fog the disc must be extinguished, leaving only the broad
    // halo built by multiple scattering. With both on the same coefficient the sun left a sharp,
    // blinding blob in a storm despite 140 m of visibility.
    //
    // The extinction comes from the fog's OWN column optical depth: `tau = beta/k`, the ratio of
    // base density to falloff coefficient — both already exist, no invented number. On paper:
    //   berrak 25 km  → τ=0.09  → disk 0.91 (neredeyse tam)
    //   rain 1.5 km  -> tau=1.51  -> disc 0.22
    //   storm 140 m  -> tau=16.2  -> disc 0.00 (fully extinguished)
    float discVisibility = exp(-_HeightFogDensity / max(_HeightFogFalloff, 1e-6));

    float forward = pow(alignment, 8.0) * 0.05
                  + pow(alignment, 64.0) * 0.12 * discVisibility;
    air += _HeightFogSunColor.rgb * (forward * sunUp);

    return air;
}

/// The fog PATH between the camera and a point: transmittance and in-scattering separately.
///
/// Fog scatters lightning too. With its color held constant, in a storm — the only weather
/// lightning strikes in — visibility drops to seven hundred metres and most of the terrain
/// stayed under that unchanging color: even when the surface lit up, nothing showed because
/// it was covered. In reality the opposite happens, at the moment of a strike the fog itself glows from within.
///
/// They are kept separate because an opaque surface is not the only thing that has to apply
/// fog. A cloud also stands at a distance from the camera and the fog in front of it has to
/// attenuate it too — but the cloud arrives PREMULTIPLIED, carrying its own coverage.
/// The `color x T + scattering` formula cannot be applied to it as is; the scattering share
/// has to be scaled by how much the cloud covers. The caller can only do that if it gets the
/// two parts separately.
///
/// The path is LINEAR in color — even when a curtain and fog stack in sequence. So the
/// decomposition is not an approximation, it is the same expression written out.
void FogPath(float3 cameraPos, float3 worldPos, out float3 scattering, out float transmittance)
{
    // The flash was TAKEN OUT of here: it was multiplied by the fog's opacity and the glow
    // vanished in clear air. It now enters the sum through `LightningScatter`.
    float3 air = AirColor(normalize(worldPos - cameraPos));

    // THE VOLUME AND THE TAIL. The froxel volume carries 0-`far` with shadowing; beyond it the
    // analytic integral takes over. Because both read the SAME density model the structure
    // does not change at the boundary (`VolumetricFogShared.hlsl`).
    //
    // The composition follows Beer-Lambert: transmittances multiply, in-scattering is weighted
    // by the transmittance in front of it and summed. No separate blend window is needed —
    // the transition is continuous by construction.
    float3 volumeScatter = 0.0;
    float volumeTransmittance = 1.0;
    float3 tailStart = cameraPos;

    // With no volume `_FogVolumeDepth` stays zero; the tail then starts at the camera and the
    // behaviour is IDENTICAL to before the volume existed. That is the verification step.
    if (_FogVolumeDepth.z > 0.0)
    {
        float viewDepth = dot(worldPos - cameraPos, _FogCameraForward.xyz);

        if (viewDepth > _FogVolumeDepth.x)
        {
            float2 screenUV = ComputeNormalizedDeviceCoordinates(worldPos, UNITY_MATRIX_VP);
            float sampleDepth = min(viewDepth, _FogVolumeDepth.y);

            float4 volume = SAMPLE_TEXTURE3D_LOD(_FogScatteringVolume, sampler_FogScatteringVolume,
                                                 FogVolumeUVW(screenUV, sampleDepth), 0);

            volumeScatter = volume.rgb;
            volumeTransmittance = volume.a;

            // The tail starts where the volume ends. The direction is scaled so its forward-axis
            // projection is 1, i.e. `dir · depth` is directly the point at that depth.
            float3 dir = (worldPos - cameraPos) / max(viewDepth, 1e-4);
            tailStart = cameraPos + dir * min(viewDepth, _FogVolumeDepth.y);
        }
    }

    // PER-CHANNEL EXTINCTION WAS REMOVED (`_HeightFogChroma`). Rayleigh sweeping blue before
    // red is real, but its OWNER is now the sky package's aerial perspective: modelling the
    // same atmosphere in two places means double counting. The medium this file carries is
    // LOCAL — valley fog, banks, drifting snow — and because water droplets dominate its
    // extinction is neutral anyway (Mie does not pick a color).
    float integral = HeightFogIntegral(tailStart, worldPos)
                   * FogBankPath(tailStart.xz, worldPos.xz);

    float surfacePass = exp(-integral);

    transmittance = volumeTransmittance * surfacePass;
    scattering = volumeScatter
               + volumeTransmittance * air * (1.0 - surfacePass)
               + LightningScatter(cameraPos, worldPos);
}

/// Places the drawn color inside the air. The caller does not need to take the amount
/// separately and write the lerp itself — those two lines would be identical on every surface.
float3 ApplyHeightFog(float3 color, float3 cameraPos, float3 worldPos)
{
    float3 scattering;
    float transmittance;
    FogPath(cameraPos, worldPos, scattering, transmittance);

    return color * transmittance + scattering;
}

#endif
