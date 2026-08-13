using System;
using UnityEngine;

/// Havanın o anki durumu. Kimseyi tanımaz, sadece değeri tutar ve değişimi duyurur.
/// Sürücüler yazar (yükseklik, senaryo, olay), görsel sistemler okur.
public class WeatherState : MonoBehaviour
{
    [Tooltip("Yağış yoğunluğu. 0 açık hava, 1 en şiddetli.")]
    [SerializeField, Range(0f, 1f)] float precipitation;
    [Tooltip("Yağışın karakteri. 0 yağmur, 1 kar. Aradaki değerler sulu kar.")]
    [SerializeField, Range(0f, 1f)] float snowiness;

    const float Epsilon = 0.001f;

    public event Action<WeatherState> Changed;

    public float Precipitation => precipitation;
    public float Snowiness => snowiness;

    public void Set(float newPrecipitation, float newSnowiness)
    {
        newPrecipitation = Mathf.Clamp01(newPrecipitation);
        newSnowiness = Mathf.Clamp01(newSnowiness);

        if (Mathf.Abs(newPrecipitation - precipitation) < Epsilon &&
            Mathf.Abs(newSnowiness - snowiness) < Epsilon)
            return;

        precipitation = newPrecipitation;
        snowiness = newSnowiness;
        Changed?.Invoke(this);
    }
}
