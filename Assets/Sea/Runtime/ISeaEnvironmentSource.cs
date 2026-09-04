// ROLE: everything the sea system reads from the outside world. The game's
// existing weather, wind, cloud and day/night systems implement this
// interface. The sea system NEVER writes these values.
// CALLED BY: SeaManager.

using UnityEngine;

public enum SeaPrecipitationKind { None, Rain, Snow, Sleet }

/// THE SEA DOES NOT DRIVE, IT READS.
///
/// The core rule of spec §3. There will not be a single line inside the sea
/// system that writes `RenderSettings`, `VolumeProfile` or
/// `Light.intensity`; the Phase 1 acceptance criterion verifies that with a
/// code search.
public interface ISeaEnvironmentSource
{
    // --- Wind: the MAIN input of the wave spectrum (spec §6) ---

    /// Normalized, world space, horizontal.
    Vector3 WindDirection { get; }

    /// m/s at the 10 m reference height (U10).
    float WindSpeed { get; }

    /// SWELL PEAK PERIOD (s) -- the sea's own slow clock, NOT today's wind.
    ///
    /// A groundswell was born in a storm that is far away and days old, so it does not
    /// follow the local weather; that independence is the point. It decides the breaker
    /// type at the shore: short period spills, long period plunges.
    float SwellPeriod { get; }

    /// How much the swell partition's energy is multiplied by right now. One is the
    /// quiet background swell; the peak of an event is several times that.
    float SwellEnergyScale { get; }

    /// Absolute world-space direction the remote swell travels towards. It is not derived
    /// from today's local wind; two storms separated by an ocean do not share a compass.
    Vector3 SwellDirection { get; }

    // --- Day and night ---

    Light Sun { get; }

    /// `saturate(dot(-sunForward, up))`. The night gate for sun glitter
    /// comes from here (spec §12.5).
    float SunElevation01 { get; }

    // --- Atmosphere: the input to surface reflection (spec §12) ---

    /// THE SKY COLOUR IS NOT ASKED FOR ANY MORE. It used to be two constants
    /// entered by hand and the sea reflected a blue that did not exist under a
    /// grey sky. The surface now reflects the environment probe — the sky that
    /// is really drawn. Coverage is still needed: the volumetric clouds are a
    /// render feature drawn after the skybox and never enter the probe.

    /// 0 clear, 1 overcast.
    float CloudCover01 { get; }

    float FogDensity01 { get; }

    // --- Precipitation: foam and surface roughness (spec §13) ---

    SeaPrecipitationKind PrecipKind { get; }

    float PrecipIntensity01 { get; }
}
