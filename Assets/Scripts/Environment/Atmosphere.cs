using UnityEngine;

/// Computes what the atmosphere does to light — PURE MATHS, no Unity dependency.
///
/// THE COLOUR IS NOT CHOSEN HERE. Dawn's orange, twilight's purple and the night's blue all
/// fall out of the optical depth of the same three components:
///
///   Rayleigh — air molecules. It goes as λ⁻⁴, so it scatters blue about 7 times more than
///              red. This is what makes the daytime sky blue and turns the beam orange at sunset.
///   Mie      — aerosol. Independent of wavelength, sharply forward scattering (g = 0.8).
///              The white halo around the sun and the milky colour of hazy air come from here.
///   Ozone    — the Chappuis band. It absorbs 500-700 nm (green to orange) and holds almost no
///              blue. Twilight's PURPLE comes from this; not from Rayleigh.
///
/// The critical distinction: with the sun below the horizon the beam enters the atmosphere
/// TANGENTIALLY at 20-25 km. The air is thin there (the Rayleigh scale height is 8 km) but the
/// ozone layer sits exactly at that altitude (peak 25 km). While Rayleigh saturates the ozone path
/// keeps growing — the balance tips and the colour goes from orange to pink and on to purple. The
/// order is not coded, it is born from this geometry.
///
/// Coefficients: Bruneton & Neyret, Precomputed Atmospheric Scattering (2008).
public static class Atmosphere
{
    /// The SINGLE gain carrying the raw sky radiance into scene units. There used to be two
    /// separate constants (`AtmosphereController` for the sky colour, `TimeOfDay` for the exposure
    /// level) and although they carried the same name they did different jobs: changing one left
    /// the other in place, and the sky and the value derived from it drifted apart.
    /// Its value is calibrated against the zenith brightness (see SkyRadiance).
    public const float SceneGain = 3.6f;

    public const float PlanetRadius = 6360000f;
    public const float AtmosphereRadius = 6420000f;

    // Scattering/absorption coefficients at sea level (1/m).
    // Rayleigh: 1.24062e-6 / λ⁴, λ = 680 / 550 / 440 nm.
    static readonly Vector3 RayleighBeta = new(5.80e-6f, 13.56e-6f, 33.10e-6f);
    const float RayleighScaleHeight = 8000f;

    // Mie is equal in all three channels: an aerosol particle is larger than the wavelength and
    // does not discriminate. Extinction is larger than scattering (albedo 0.9): aerosol also absorbs.
    const float MieBeta = 3.996e-6f;
    const float MieExtinction = MieBeta / 0.9f;
    const float MieScaleHeight = 1200f;

    /// Absorption at the ozone layer's PEAK density. Green is the highest — the Chappuis band
    /// peaks around 600 nm. That it does not hold blue is what turns twilight purple.
    static readonly Vector3 OzoneBeta = new(0.650e-6f, 1.881e-6f, 0.085e-6f);
    const float OzonePeak = 25000f;    // the layer's peak elevation
    const float OzoneWidth = 15000f;   // half width of the tent profile

    /// Relative densities at the given elevation. The ozone tent profile: 1 at the peak, 0 at ±15 km.
    static void Densities(float altitude, out float rayleigh, out float mie, out float ozone)
    {
        rayleigh = Mathf.Exp(-altitude / RayleighScaleHeight);
        mie = Mathf.Exp(-altitude / MieScaleHeight);
        ozone = Mathf.Max(0f, 1f - Mathf.Abs(altitude - OzonePeak) / OzoneWidth);
    }

    /// The optical depth along the path the ray travels until it leaves the spherical atmosphere
    /// (or hits the ground). If it hits the ground it counts as infinite: no light comes from that direction.
    ///
    /// Spherical geometry is mandatory: the planar approximation gets the air mass wrong by orders
    /// of magnitude at angles near the horizon, and the sunset colour is decided exactly there.
    static bool OpticalDepth(float startAltitude, Vector3 direction, int steps,
                             out Vector3 depth)
    {
        depth = Vector3.zero;

        Vector3 origin = new(0f, PlanetRadius + startAltitude, 0f);
        float top = RaySphere(origin, direction, AtmosphereRadius);
        if (top <= 0f) return false;

        // A ray hitting the ground: the source is not visible.
        if (BelowHorizon(startAltitude, direction)) return false;

        float step = top / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 p = origin + direction * (step * (i + 0.5f));
            float altitude = Mathf.Max(0f, p.magnitude - PlanetRadius);

            Densities(altitude, out float r, out float m, out float o);

            depth += (RayleighBeta * r + Vector3.one * (MieExtinction * m) + OzoneBeta * o)
                     * step;
        }

        return true;
    }

    /// Does the ray look below the ground?
    ///
    /// The sphere intersection is the reference — the look at the hours near the horizon was
    /// calibrated against it. But that computation is unreliable at planetary scale:
    /// `|origin|² − R²` is the difference of two 4·10¹³ numbers and float32's step at that
    /// magnitude is ~4·10⁶. With the observer at sea level the result was rounding noise and its
    /// sign varied with the sun's elevation — it worked with the sun at 27°, and at 29° the ray
    /// counted as "hit the ground", the beam was zeroed and the scene went dark. Because it was
    /// discontinuous it did not even look like a threshold.
    ///
    /// The fix: drop the sphere computation entirely and ask the question with an ANGLE — no
    /// cancellation, and the result is independent of the hardware and the compiler.
    ///
    /// NO MARGIN IS ADDED to the threshold. One was, and it produced a hard on/off: the moment
    /// the sun crossed 5° the beam and the horizon samples were zeroed and came back together,
    /// leaving a knife-edge jump on screen around 17:37. Reining in a low sun is the job of a
    /// separate and CONTINUOUS multiplier (`LowSunFade`).

    static bool BelowHorizon(float altitude, Vector3 direction)
        => direction.y < HorizonDipSine(altitude);

    /// The sine of the horizon's dip angle (negative: the horizon drops as the observer rises).
    static float HorizonDipSine(float altitude)
    {
        float ratio = Mathf.Clamp01(PlanetRadius / (PlanetRadius + altitude));
        return -Mathf.Sqrt(Mathf.Max(0f, 1f - ratio * ratio));
    }

    /// THE LOW SUN LIMITER — a look decision. Zero at the horizon, full at five degrees.
    ///
    /// The physics gives a strong and red sky near the horizon; the dawn we want is more measured
    /// than that. The limiting comes from one place and is applied to all three of the BEAM, the
    /// SUN COLOUR and the HORIZON SAMPLES — with one reined in and the other not, the clouds went
    /// pink all at once (because `Tint()` normalizes, the colour stays fully saturated even when
    /// the beam has died).
    ///
    /// It has to be continuous: built as a threshold, the whole scene jumped the moment the sun
    /// crossed that angle.
    public static float LowSunFade(float altitude, Vector3 direction)
    {
        float dip = HorizonDipSine(altitude);
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(dip, dip + LowSunFadeSine, direction.y));
    }

    /// The elevation at which the limiter reaches full strength (sine). 0.0872 ≈ 5°.
    public const float LowSunFadeSine = 0.0872f;

    /// The first positive distance at which the ray enters the sphere; −1 if it does not intersect.
    ///
    /// CATASTROPHIC CANCELLATION GUARD. `c = |origin|² - radius²` is the difference of two
    /// 4·10¹³ numbers at planetary scale and float32's step at that magnitude is ~4·10⁶ — with the
    /// source exactly on the surface (an observer at sea level, the Earth sphere) c is
    /// theoretically zero but the computation itself is noise. Then `t1 = -b + sqrt(b·b - c)` is
    /// also theoretically zero; when `sqrt(b·b)` rounds one ulp up, t1 comes out a small POSITIVE
    /// number and a ray going upward counted as "hit the ground". Because the rounding direction
    /// depends on b, i.e. on the sun's elevation, the error is discontinuous: it worked with the
    /// sun at 27°, and at 29° the beam was zeroed and the scene went dark. Calls at cloud altitude
    /// are unaffected because c ≈ 3·10¹⁰ there, far above the noise.
    ///
    /// The fix is geometric, not numerical: if the source is on or outside the sphere (c ≥ 0) the
    /// ray can only intersect if it goes TOWARDS the sphere. If b ≥ 0 it is moving away and there
    /// is no intersection — the square root never has to be entered.
    static float RaySphere(Vector3 origin, Vector3 direction, float radius)
    {
        float b = Vector3.Dot(origin, direction);
        float c = Vector3.Dot(origin, origin) - radius * radius;

        if (c >= 0f && b >= 0f) return -1f;

        float d = b * b - c;
        if (d < 0f) return -1f;

        d = Mathf.Sqrt(d);
        float t0 = -b - d, t1 = -b + d;
        return t0 > 0f ? t0 : (t1 > 0f ? t1 : -1f);
    }

    /// The multiplier of the direct light REACHING the observer, per channel. Zero if the sun is
    /// below the horizon or the ray hits the ground. NO normalization: the colour and the
    /// extinction are both here, together. Recovering the brightness by pulling the brightest
    /// channel to 1 — the old state — locks the sunset into a red that never dies and dazzles.
    public static Vector3 BeamTransmittance(float altitude, Vector3 sunDirection, int steps = 24)
    {
        float visible = DiscVisibility(altitude, sunDirection);
        if (visible <= 0f) return Vector3.zero;

        Vector3 direction = sunDirection;

        // If the ray hits the ground but the disc is still partly visible (refraction) we measure
        // the path along the tangent: the colour of a ray grazing the geometric horizon is the
        // colour of a setting sun.
        if (BelowHorizon(altitude, direction))
            direction = GrazingDirection(altitude, sunDirection);

        if (!OpticalDepth(altitude, direction, steps, out Vector3 depth))
            return Vector3.zero;

        return new Vector3(Mathf.Exp(-depth.x), Mathf.Exp(-depth.y), Mathf.Exp(-depth.z))
             * (visible * LowSunFade(altitude, sunDirection));
    }


    /// Atmospheric refraction bends the light about 0.57° upward at the horizon: the sun stays
    /// visible for a while after it has really set. The disc itself is also 0.53° wide, so a
    /// sunset is not a moment but a passage.
    const float HorizonRefraction = 0.00995f;   // 0.57° radyan
    const float SunDiscRadius = 0.00463f;       // 0.265° radyan

    /// The share of the disc above the horizon, 0-1. The measure is taken against the GEOMETRIC
    /// horizon: as the observer rises the horizon drops (1.64° at cloud altitude) and the sun
    /// becomes visible from there earlier.
    ///
    /// In the old state `OpticalDepth` zeroed a ray hitting the ground and the transition width
    /// was EXACTLY ZERO: the beam was 0.106 in one frame and 0 in the next. Because the beam
    /// carries not only the light's intensity but its colour, the sun disc, the terrain's dawn
    /// colour, the cloud's dusk tone and the palette all jumped to black at once.
    static float DiscVisibility(float altitude, Vector3 sunDirection)
    {
        float dip = HorizonDip(altitude);
        float elevation = Mathf.Asin(Mathf.Clamp(sunDirection.y, -1f, 1f));

        // The share ABOVE the horizon: positive = the disc's centre is above the horizon.
        float margin = elevation - dip;

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            -HorizonRefraction - SunDiscRadius,
            -HorizonRefraction + SunDiscRadius, margin));
    }

    /// The elevation angle of the geometric horizon from the observer's altitude (negative: it drops).
    static float HorizonDip(float altitude)
        => -Mathf.Acos(Mathf.Clamp01(PlanetRadius / (PlanetRadius + altitude)));

    /// The direction at the same azimuth that exactly grazes the horizon. It is used in place of a
    /// ray that hits the ground; the tangent path is the atmosphere's longest section and the
    /// sunset's red comes from there.
    static Vector3 GrazingDirection(float altitude, Vector3 sunDirection)
    {
        Vector3 flat = new(sunDirection.x, 0f, sunDirection.z);
        flat = flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.forward;

        // A small margin upward: at the exact tangent the sphere intersection is numerically on a knife edge.
        float dip = HorizonDip(altitude) + 1e-4f;
        return flat * Mathf.Cos(dip) + Vector3.up * Mathf.Sin(dip);
    }

    /// The light the sky scatters to the observer. This is what lights the landscape after the sun
    /// has set — not the direct beam. It is also the source of the alpenglow: the summit takes
    /// light from a sky painted red.
    ///
    /// Two components are summed. SINGLE scattering: for each point, the transmittance along the
    /// path to the sun, the scattering coefficient there and the phase function. MULTIPLE
    /// scattering: the second and later scatterings forget the direction, arrive isotropically and
    /// are read from the `MultipleScattering` table.
    ///
    /// Without multiple scattering the sunset horizon's saturation rose to 0.98 — the blue channel
    /// stayed at 0.005 and the colour on screen was not orange but almost pure red. A real sunset
    /// horizon is at 0.5-0.7 saturation; what fills the blue back in is exactly multiple
    /// scattering. Its absence also darkened the sky as a whole, and the gain compensating for that
    /// loss carried the brightest place — the horizon on the sun's side — to twice 1.0 and had it
    /// clipped by the tone mapping.
    public static Vector3 SkyRadiance(float altitude, Vector3 viewDirection,
                                      Vector3 sunDirection, int steps = 16)
    {
        Vector3 origin = new(0f, PlanetRadius + altitude, 0f);
        float top = RaySphere(origin, viewDirection, AtmosphereRadius);
        if (top <= 0f) return Vector3.zero;

        if (BelowHorizon(altitude, viewDirection))
        {
            float ground = RaySphere(origin, viewDirection, PlanetRadius);
            if (ground > 0f) top = ground;
        }

        float cosTheta = Vector3.Dot(viewDirection, sunDirection);
        float rayleighPhase = 3f / (16f * Mathf.PI) * (1f + cosTheta * cosTheta);
        float miePhase = MiePhase(cosTheta, 0.8f);

        float step = top / steps;
        Vector3 accumulated = Vector3.zero;
        Vector3 viewDepth = Vector3.zero;

        for (int i = 0; i < steps; i++)
        {
            Vector3 p = origin + viewDirection * (step * (i + 0.5f));
            float h = Mathf.Max(0f, p.magnitude - PlanetRadius);

            Densities(h, out float r, out float m, out float o);

            Vector3 extinction = RayleighBeta * r + Vector3.one * (MieExtinction * m)
                               + OzoneBeta * o;
            viewDepth += extinction * step;

            Vector3 viewTransmittance = new(
                Mathf.Exp(-viewDepth.x), Mathf.Exp(-viewDepth.y), Mathf.Exp(-viewDepth.z));

            // Does direct light from the sun reach this point? None if it hits the ground —
            // but skipping the point entirely was wrong: air in shadow still glows with the
            // light scattered from its neighbours. That skip was the reason twilight came out
            // pitch black; multiple scattering contributes there too.
            Vector3 scatteringIsotropic = RayleighBeta * r + Vector3.one * (MieBeta * m);

            if (OpticalDepth(h, sunDirection, 8, out Vector3 sunDepth))
            {
                Vector3 transmittance = new(
                    Mathf.Exp(-sunDepth.x - viewDepth.x),
                    Mathf.Exp(-sunDepth.y - viewDepth.y),
                    Mathf.Exp(-sunDepth.z - viewDepth.z));

                Vector3 scattering = RayleighBeta * (r * rayleighPhase)
                                   + Vector3.one * (MieBeta * m * miePhase);

                accumulated += Vector3.Scale(transmittance, scattering) * step;
            }

            // Multiple scattering is isotropic: no phase function, the sun path is inside Ψ.
            Vector3 psi = MultipleScattering(h, sunDirection);
            accumulated += Vector3.Scale(viewTransmittance,
                                         Vector3.Scale(scatteringIsotropic, psi)) * step;
        }

        return accumulated;
    }

    // --- Multiple scattering (Hillaire 2020, "A Scalable and Production Ready Sky and
    //     Atmosphere Rendering Technique") ---
    //
    // Light does not scatter once in the atmosphere and stop. After the second scattering it
    // forgets the direction it came from; the sum behaves like an isotropic source. The infinite
    // order closes as a geometric series: Ψ = L₂ / (1 − f), f = the share one scattering returns.
    //
    // The table (elevation × sun elevation) is built once. Two axes are enough: because Ψ is
    // isotropic it does not depend on the VIEW direction — that is where the real saving comes from.
    const int MsAltitudes = 16;

    /// THE ANGLE AXIS HAS TO BE FINE. The axis is divided uniformly over sin(sun elevation); at 24
    /// bins that is 5° per bin. Because twilight fades about 2.24× per degree, one bin spans a
    /// factor of 28 and bilinear interpolation was helpless there: with the sun at −6° the sky came
    /// out at 0.041 of its value at sunrise — the truth is 0.0085, i.e. 4.8 times too bright. At 48
    /// it comes down to 0.0039.
    ///
    /// Measured and separated: the direction count (16→32) and the step count (12→24) change
    /// nothing, and neither does the elevation axis (16→24). ALL of the gain is on this axis.
    /// Beyond 48 there is nothing to gain either — the movement at 64 and 96 is not convergence but
    /// the noise of the 16-direction sampling.
    ///
    /// Daytime is unaffected: at +5° and above the difference is under 2.5%.
    const int MsAngles = 48;
    const float MsTopAltitude = 60000f;
    static Vector3[] msTable;

    /// Directions spread evenly over the sphere (a Fibonacci spiral). Random sampling comes out
    /// noisier for the same number of directions and the table jumps between elevations.
    static Vector3 SphereDirection(int index, int count)
    {
        float y = 1f - 2f * (index + 0.5f) / count;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        float phi = (index + 0.5f) * Mathf.PI * (3f - Mathf.Sqrt(5f));
        return new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi));
    }

    static void BuildMultipleScattering()
    {
        msTable = new Vector3[MsAltitudes * MsAngles];
        const int Directions = 16;
        const int Steps = 12;
        const float Isotropic = 1f / (4f * Mathf.PI);

        for (int a = 0; a < MsAngles; a++)
        {
            // The sun's vertical component is −1..1; the horizontal makes up the rest.
            float sinSun = MsAngles > 1 ? -1f + 2f * a / (MsAngles - 1f) : 0f;
            Vector3 sun = new(Mathf.Sqrt(Mathf.Max(0f, 1f - sinSun * sinSun)), sinSun, 0f);

            for (int k = 0; k < MsAltitudes; k++)
            {
                float altitude = MsTopAltitude * k / (MsAltitudes - 1f);
                Vector3 origin = new(0f, PlanetRadius + altitude, 0f);

                Vector3 second = Vector3.zero;   // L₂ — ikinci mertebe
                Vector3 transfer = Vector3.zero; // f — the share that comes back

                for (int d = 0; d < Directions; d++)
                {
                    Vector3 dir = SphereDirection(d, Directions);

                    float top = RaySphere(origin, dir, AtmosphereRadius);
                    if (top <= 0f) continue;

                    if (BelowHorizon(altitude, dir))
                    {
                        float ground = RaySphere(origin, dir, PlanetRadius);
                        if (ground > 0f) top = ground;
                    }

                    float step = top / Steps;
                    Vector3 depth = Vector3.zero;

                    for (int i = 0; i < Steps; i++)
                    {
                        Vector3 p = origin + dir * (step * (i + 0.5f));
                        float h = Mathf.Max(0f, p.magnitude - PlanetRadius);

                        Densities(h, out float r, out float m, out float o);

                        depth += (RayleighBeta * r + Vector3.one * (MieExtinction * m)
                                  + OzoneBeta * o) * step;

                        Vector3 scattering = RayleighBeta * r + Vector3.one * (MieBeta * m);
                        Vector3 travelled = new(Mathf.Exp(-depth.x), Mathf.Exp(-depth.y),
                                                Mathf.Exp(-depth.z));

                        Vector3 common = Vector3.Scale(travelled, scattering)
                                         * (Isotropic * step);
                        transfer += common;

                        if (OpticalDepth(h, sun, 6, out Vector3 sunDepth))
                        {
                            second += Vector3.Scale(common, new Vector3(
                                Mathf.Exp(-sunDepth.x), Mathf.Exp(-sunDepth.y),
                                Mathf.Exp(-sunDepth.z)));
                        }
                    }
                }

                float weight = 4f * Mathf.PI / Directions;
                second *= weight;
                transfer *= weight;

                msTable[k * MsAngles + a] = new Vector3(
                    second.x / (1f - Mathf.Min(0.98f, transfer.x)),
                    second.y / (1f - Mathf.Min(0.98f, transfer.y)),
                    second.z / (1f - Mathf.Min(0.98f, transfer.z)));
            }
        }
    }

    /// The isotropic multiple scattering source at the given elevation and sun elevation.
    /// Bilinear on both axes: because the table is coarse the interpolation is mandatory, otherwise
    /// the sky jumps in steps as the sun rises.
    static Vector3 MultipleScattering(float altitude, Vector3 sunDirection)
    {
        if (msTable == null) BuildMultipleScattering();

        float fk = Mathf.Clamp01(altitude / MsTopAltitude) * (MsAltitudes - 1);
        float fa = Mathf.Clamp01((sunDirection.y + 1f) * 0.5f) * (MsAngles - 1);

        int k0 = Mathf.FloorToInt(fk), a0 = Mathf.FloorToInt(fa);
        int k1 = Mathf.Min(k0 + 1, MsAltitudes - 1), a1 = Mathf.Min(a0 + 1, MsAngles - 1);
        float tk = fk - k0, ta = fa - a0;

        Vector3 lower = Vector3.Lerp(msTable[k0 * MsAngles + a0], msTable[k0 * MsAngles + a1], ta);
        Vector3 upper = Vector3.Lerp(msTable[k1 * MsAngles + a0], msTable[k1 * MsAngles + a1], ta);
        return Vector3.Lerp(lower, upper, tk);
    }

    /// Henyey-Greenstein: aerosol scatters light sharply forward. The reason for the white halo
    /// around the sun and for the sunset "exploding" in hazy air.
    static float MiePhase(float cosTheta, float g)
    {
        float g2 = g * g;
        float denom = 1f + g2 - 2f * g * cosTheta;
        return (1f - g2) / (4f * Mathf.PI * denom * Mathf.Sqrt(Mathf.Max(1e-4f, denom)));
    }
}
