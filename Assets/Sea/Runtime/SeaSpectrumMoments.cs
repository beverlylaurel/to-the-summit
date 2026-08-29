// ROLE: integrates the two spectrum partitions on the CPU and returns Hs and Tp.
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

        /// Rms surface elevation of the wind sea alone (m), and its peak frequency.
        public readonly float WindRms;
        public readonly float WindOmega;

        /// The same for the swell.
        public readonly float SwellRms;
        public readonly float SwellOmega;

        public Result(float significantHeight, float peakPeriod,
                      float windRms, float windOmega,
                      float swellRms, float swellOmega)
        {
            SignificantHeight = significantHeight;
            PeakPeriod = peakPeriod;
            WindRms = windRms;
            WindOmega = windOmega;
            SwellRms = swellRms;
            SwellOmega = swellOmega;
        }
    }

    // The integration band and step. The peak sits between 0.6 and 2.5 rad/s at
    // every wind speed in use; 0.05..8 rad/s at 0.005 covers it with room to
    // spare. Checked against a ten times finer step: Hs agrees to 0.01 m.
    const float OmegaMin = 0.05f;
    const float OmegaMax = 8.0f;
    const float OmegaStep = 0.005f;

    public static Result Integrate(float windSpeed, SeaSettings settings)
    {
        float u = Mathf.Max(windSpeed, 0.1f);
        float fetch = settings.fetch;
        float depth = settings.spectrumDepth;

        float omegaPWind = PeakOmega(u, fetch);
        float omegaPSwell = SeaConstants.TwoPi / Mathf.Max(settings.swellPeriod, 0.1f);

        // HOISTED OUT OF THE LOOP. `alpha` and the depth scale depend only on the
        // wind, the fetch and the depth; left inside, the integration cost was
        // 0.295 ms and two thirds of it was `Mathf.Pow` recomputing constants.
        float g = SeaConstants.G;
        float alpha = 0.076f * Mathf.Pow(Mathf.Max(u * u / (fetch * g), 1e-12f), 0.22f);
        float gg = g * g;
        float depthScale = Mathf.Sqrt(depth / g);
        float swellAlpha = settings.swellAlpha;
        float swellGamma = settings.swellGamma;

        // THE TWO PARTITIONS ARE KEPT APART.
        //
        // The shore wave is a TRAIN, not a spectrum, and a single train has no beat.
        // Measured, the two peaks beat with a period of 4 s in a calm and 89 s at
        // 20 m/s, at a modulation depth of 0.29 to 0.97 — that is the wave-to-wave
        // size change the eye reads on a real shore. It is already in the FFT field,
        // because both partitions live in the same spectrum; only a one-frequency
        // shore wave would lose it.
        double m0Wind = 0.0, m0Swell = 0.0;
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
            m0Wind += wind * attenuation * OmegaStep;
            m0Swell += swell * attenuation * OmegaStep;

            if (total > peakDensity)
            {
                peakDensity = total;
                peakOmega = omega;
            }
        }

        double m0 = m0Wind + m0Swell;

        return new Result(4f * Mathf.Sqrt((float)m0),
                          SeaConstants.TwoPi / Mathf.Max(peakOmega, 1e-4f),
                          Mathf.Sqrt((float)m0Wind), omegaPWind,
                          Mathf.Sqrt((float)m0Swell), omegaPSwell);
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
