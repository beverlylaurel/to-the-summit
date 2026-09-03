// ROLE: everything the snow system reads from the outside world. The game's existing
// weather, wind and day/night systems implement this interface.
// The snow system NEVER writes these values.
// CALLED BY: SnowManager and its subcomponents.

using UnityEngine;

public enum PrecipitationKind
{
    None,
    Rain,
    Snow,
    Sleet,
}

/// A SINGLE DOOR. No snow file uses `RenderSettings.fog`, its own day cycle or its own
/// wind noise; they all read from here (spec §3.1).
///
/// Every line setting up its own wind, its own sun or its own fog is a mistake.
public interface ISnowEnvironmentSource
{
    // --- Wind (from the existing wind system) ---

    /// Normalized, world space, horizontal.
    Vector3 WindDirection { get; }

    /// m/s.
    float WindSpeed { get; }

    /// THE PREVAILING WIND DIRECTION — not the instantaneous one.
    ///
    /// The landforms (sastrugi, ripple) use this axis. With the instantaneous direction
    /// the pattern slides across the world: the field is built on `dot(worldXZ, axis)`
    /// and in the middle of the mountain |worldXZ| is seven thousand metres — a gust's
    /// 0.14 radian deviation drags the pattern by 980 metres. The same measurement is
    /// also recorded next to `WindField.PrevailingDirection`.
    Vector3 PrevailingWindDirection { get; }

    // --- From the day/night cycle ---

    /// Ana directional light.
    Light Sun { get; }

    /// 0 = below the horizon, 1 = at the zenith.
    float SunElevation01 { get; }

    /// Celsius. The day cycle + the season drive it.
    float TemperatureC { get; }

    // --- Precipitation (from the existing rain system) ---

    PrecipitationKind PrecipKind { get; }

    /// HOW MUCH OF THE FALLING PRECIPITATION IS SNOW: 1 all snow, 0 all rain.
    ///
    /// It comes through this door rather than being decided inside the snow system, for
    /// the same reason as everything else here: the sky and the sea must not be able to
    /// disagree about what is falling on them.
    float SnowFraction01 { get; }

    /// 0..1, the existing system's intensity value.
    float PrecipIntensity01 { get; }

    // --- Fog (read only, for the snowflake fade) ---

    /// 0..1 normalize, mevcut sis sisteminden.
    float FogDensity01 { get; }
}
