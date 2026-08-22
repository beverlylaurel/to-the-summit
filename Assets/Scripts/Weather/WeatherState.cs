using System;
using UnityEngine;

/// Havanın o anki durumu. Kimseyi tanımaz, sadece değeri tutar ve değişimi duyurur.
/// Sürücüler yazar (yükseklik, senaryo, olay), görsel sistemler okur.
public class WeatherState : MonoBehaviour
{
    [Tooltip("Yağış yoğunluğu. 0 açık hava, 1 en şiddetli.")]
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
