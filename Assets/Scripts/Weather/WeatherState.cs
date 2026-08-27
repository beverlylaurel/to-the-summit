using System;
using UnityEngine;

/// The current state of the weather. It knows nobody, it only holds the values and announces
/// changes. Drivers write (altitude, scenario, event), visual systems read.
public class WeatherState : MonoBehaviour
{
    [Tooltip("Precipitation intensity. 0 clear, 1 heaviest.")]
    [SerializeField, Range(0f, 1f)] float precipitation;

    const float Epsilon = 0.001f;

    public event Action<WeatherState> Changed;

    public float Precipitation => precipitation;

    public void Set(float newPrecipitation)
    {
        newPrecipitation = Mathf.Clamp01(newPrecipitation);

        if (Mathf.Abs(newPrecipitation - precipitation) < Epsilon) return;

        precipitation = newPrecipitation;
        Changed?.Invoke(this);
    }
}
