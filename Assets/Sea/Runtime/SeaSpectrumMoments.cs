// ROLE: integrates the two spectrum partitions on the CPU and returns Hs, Tp and the per-tier slope variance.
// CALLED BY: SeaManager (UpdateState).

using UnityEngine;

/// THE SEA STATE IS READ FROM THE SPECTRUM THAT IS ACTUALLY RUNNING.
///
/// Hs and Tp used to come from the fetch-limited JONSWAP relations, which know
/// only the WIND SEA. The spectrum on the GPU has a second partition — a swell
/// with its own peak period and a fixed energy (`SeaSpectrum.compute`,
/// `SeaSwellSpectrum`) — and it does not fall to zero when the wind does.
///
/// Measured, at the settings in use (fetch 150 km, swell T 10 s):
///
///     U10     old Tp    true Tp    old Hs    true Hs
///     0.5      2.63       9.97      0.10       0.74
///     3.0      4.78       9.97      0.59       1.17
///     8.0      6.62       6.63      1.58       2.31
///    20.0      8.99       8.98      3.96       4.91
///
/// In a dead calm the shore was surging with a 2.6 second period. The swell is
/// what the shore actually feels there, and it is ten seconds long.
///
/// The formulas below MIRROR the compute shader's. If one changes the other has
/// to change with it — they describe the same spectrum, once for the GPU and
/// once for the number the HUD, the audio and the breaking criterion read.
public static class SeaSpectrumMoments
{
    /// Result of one integration.
    public readonly struct Result
    {
        /// Significant wave height Hs (m) of BOTH partitions together.
        public readonly float SignificantHeight;

        /// Peak period Tp (s) of the SUM of the two partitions — that is, of
        /// whichever partition actually carries the energy.
        public readonly float PeakPeriod;

        /// BEAT OF THE TWO PEAKS: `2pi / |w_wind - w_swell|` (s), and how deep the
        /// modulation goes, `2 A1 A2 / (A1^2 + A2^2)`, 0..1.
        ///
        /// This is the wave-to-wave size change on a real shore — the reason the surf
        /// zone breathes instead of standing on one depth contour. It is not an
        /// invented envelope: both partitions are already in the spectrum, and their
        /// interference is what a "set" is.
        public readonly float BeatPeriod;
        public readonly float BeatDepth;

        /// SLOPE VARIANCE PER TIER, `INTEGRAL k^2 S(w) dw` over that tier's own
        /// wavenumber band.
        ///
        /// It is the surface roughness a tier CARRIES. When a tier's waves fall
        /// below one pixel the shader stops sampling it, and the variance it was
        /// carrying has to reappear as reflection lobe width or the far water turns
        /// into a mirror that flickers. Cox & Munk 1954 give the total for a real
        /// sea, `0.003 + 0.00512 U10`, which is what these three sum towards.
        public readonly Vector3 TierSlopeVariance;

        public Result(float significantHeight, float peakPeriod,
                      float beatPeriod, float beatDepth, Vector3 tierSlopeVariance)
        {
            SignificantHeight = significantHeight;
            PeakPeriod = peakPeriod;
            BeatPeriod = beatPeriod;
            BeatDepth = beatDepth;
            TierSlopeVariance = tierSlopeVariance;
        }
    }

    // The integration band and step. The peak sits between 0.6 and 2.5 rad/s at
    // every wind speed in use; 0.05..8 rad/s at 0.005 covers it with room to
    // spare. Checked against a ten times finer step: Hs agrees to 0.01 m.
    const float OmegaMin = 0.05f;
    const float OmegaMax = 8.0f;
    const float OmegaStep = 0.005f;

    public static Result Integrate(float windSpeed, SeaSettings settings,
                                   float swellPeriod, float swellEnergy,
                                   Vector2 tierBandLimits)
    {
        float u = Mathf.Max(windSpeed, 0.1f);
        float fetch = settings.fetch;
        float depth = settings.spectrumDepth;

        float omegaPWind = PeakOmega(u, fetch);
        float omegaPSwell = SeaConstants.TwoPi / Mathf.Max(swellPeriod, 0.1f);

        // HOISTED OUT OF THE LOOP. `alpha` and the depth scale depend only on the
        // wind, the fetch and the depth; left inside, the integration cost was
        // 0.295 ms and two thirds of it was `Mathf.Pow` recomputing constants.
        float g = SeaConstants.G;
        float alpha = 0.076f * Mathf.Pow(Mathf.Max(u * u / (fetch * g), 1e-12f), 0.22f);
        float gg = g * g;
        float depthScale = Mathf.Sqrt(depth / g);
        float swellAlpha = settings.swellAlpha * swellEnergy;
        float swellGamma = settings.swellGamma;

        double m0 = 0.0, m0Wind = 0.0, m0Swell = 0.0;
        double mss0 = 0.0, mss1 = 0.0, mss2 = 0.0;

        // Waves shorter than the cutoff are not in the field at all, so their
        // slope is not this surface's to carry.
        float kCutoff = SeaConstants.TwoPi / Mathf.Max(settings.smallWaveCutoff, 1e-3f);
        float peakDensity = -1f;
        float peakOmega = omegaPSwell;

        for (float omega = OmegaMin; omega < OmegaMax; omega += OmegaStep)
        {
            float attenuation = Kitaigorodskii(omega * depthScale);

            // `1 / omega^5` by multiplication: `Mathf.Pow` on a fixed integer
            // exponent is a call into a general power function.
            float o2 = omega * omega;
            float invO5 = 1f / (o2 * o2 * omega);

            float shape = gg * invO5;

            float wind = alpha * shape
                       * Peak(omega, omegaPWind, SeaConstants.JonswapGamma);
            float swell = swellAlpha * shape
                        * Peak(omega, omegaPSwell, swellGamma);

            float total = (wind + swell) * attenuation;
            m0 += total * OmegaStep;
            m0Wind += wind * attenuation * OmegaStep;
            m0Swell += swell * attenuation * OmegaStep;

            if (total > peakDensity)
            {
                peakDensity = total;
                peakOmega = omega;
            }

            // THE SLOPE MOMENT, SPLIT THE WAY THE TIERS ARE SPLIT.
            //
            // Deep-water dispersion puts this frequency at `k = w^2/g`, and the
            // tier that carries that wavenumber is the one whose band contains it
            // -- the same rule `SeaSettings.TierBandLimits` hands the compute
            // shader, so no band is counted twice and none is missed.
            float k = o2 / g;
            if (k <= kCutoff)
            {
                double contribution = k * k * total * OmegaStep;
                if (k < tierBandLimits.x) mss0 += contribution;
                else if (k < tierBandLimits.y) mss1 += contribution;
                else mss2 += contribution;
            }
        }

        // Amplitudes of the two partitions, and their beat.
        float aWind = Mathf.Sqrt((float)m0Wind) * SeaConstants.Sqrt2;
        float aSwell = Mathf.Sqrt((float)m0Swell) * SeaConstants.Sqrt2;

        float dOmega = Mathf.Abs(omegaPWind - omegaPSwell);
        float beatPeriod = dOmega > 1e-4f ? SeaConstants.TwoPi / dOmega : 1e4f;
        float beatDepth = 2f * aWind * aSwell
                        / Mathf.Max(aWind * aWind + aSwell * aSwell, 1e-8f);

        return new Result(4f * Mathf.Sqrt((float)m0),
                          SeaConstants.TwoPi / Mathf.Max(peakOmega, 1e-4f),
                          beatPeriod, beatDepth,
                          new Vector3((float)mss0, (float)mss1, (float)mss2));
    }

    /// JONSWAP peak frequency (rad/s). Mirrors `SeaPeakOmega`.
    static float PeakOmega(float u10, float fetch)
    {
        float g = SeaConstants.G;
        return 22f * Mathf.Pow(Mathf.Max(g * g / (u10 * fetch), 1e-12f), 1f / 3f);
    }

    /// The part both partitions share: the Pierson-Moskowitz exponential and the
    /// JONSWAP peak enhancement. `SeaJonswap` and `SeaSwellSpectrum` differ only
    /// in what multiplies this — an `alpha` from the fetch, or one given directly.
    static float Peak(float omega, float omegaP, float gamma)
    {
        float sigma = omega <= omegaP ? SeaConstants.JonswapSigmaLo
                                      : SeaConstants.JonswapSigmaHi;
        float delta = omega - omegaP;
        float r = Mathf.Exp(-(delta * delta) / (2f * sigma * sigma * omegaP * omegaP));

        float w4 = omegaP / omega;
        w4 = w4 * w4 * w4 * w4;

        return Mathf.Exp(-1.25f * w4) * Mathf.Pow(gamma, r);
    }

    /// Mirrors `SeaKitaigorodskii`. Takes `omega * sqrt(h/g)` already formed —
    /// the square root does not belong inside the loop.
    static float Kitaigorodskii(float omegaH)
    {
        if (omegaH <= 1f) return 0.5f * omegaH * omegaH;
        if (omegaH < 2f) return 1f - 0.5f * (2f - omegaH) * (2f - omegaH);
        return 1f;
    }
}
