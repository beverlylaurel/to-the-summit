// ROL: sahnedeki isi kaynaklarini toplar ve shader'a uniform dizi olarak
// yayinlar (spec 18.2).
// Caginan: SnowHeatSource (kayit), SnowManager (yayin).

using System.Collections.Generic;
using UnityEngine;

/// UNIFORM DIZI, STRUCTUREDBUFFER DEGIL (spec 18.2, 20). On alti eleman icin
/// uniform dizi hem daha ucuz hem kurulumu daha basit; StructuredBuffer bir
/// tampon yasam dongusu daha getirirdi.
public static class SnowHeatRegistry
{
    public const int MaxSources = 16;

    static readonly List<SnowHeatSource> Active = new(MaxSources);

    static readonly Vector4[] PosRadius = new Vector4[MaxSources];
    static readonly Vector4[] Params = new Vector4[MaxSources];

    public static int Count { get; private set; }

    public static void Register(SnowHeatSource source)
    {
        if (source == null || Active.Contains(source)) return;
        Active.Add(source);
    }

    public static void Unregister(SnowHeatSource source) => Active.Remove(source);

    /// Kar bolgesi icindekiler filtreleniyor. Bolgenin disindaki bir ates
    /// diziyi doldurup icerdekini disari itebilirdi.
    public static void Publish(Vector2 areaCenter, float areaSize)
    {
        Count = 0;

        float half = areaSize * 0.5f;

        for (int i = 0; i < Active.Count && Count < MaxSources; i++)
        {
            SnowHeatSource source = Active[i];
            if (source == null || !source.isActiveAndEnabled) continue;

            Vector3 p = source.transform.position;

            // Yaricapiyla birlikte bolgeye degiyor mu.
            if (Mathf.Abs(p.x - areaCenter.x) > half + source.radius) continue;
            if (Mathf.Abs(p.z - areaCenter.y) > half + source.radius) continue;

            PosRadius[Count] = new Vector4(p.x, p.y, p.z, source.radius);
            Params[Count] = new Vector4(source.strength, 0f, 0f, 0f);
            Count++;
        }

        Shader.SetGlobalVectorArray(SnowShaderIDs.HeatSources, PosRadius);
        Shader.SetGlobalVectorArray(SnowShaderIDs.HeatParams, Params);
        Shader.SetGlobalInt(SnowShaderIDs.HeatCount, Count);
    }

    /// Play mode domain reload kapaliyken statik liste eski oturumdan kaliyor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad()
    {
        Active.Clear();
        Count = 0;
    }
}
