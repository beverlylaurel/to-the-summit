using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// Controls the model-free headlamp mounted above the player's rendered view.
///
/// The mount is a child of Main Camera. It therefore inherits the same head pitch and restrained
/// PlayerViewMotion used by the image; no independent procedural wobble can make the beam slide
/// against the player's body. A focused shadow-casting cone and a cheap wide spill reproduce the
/// two-part beam of an outdoor LED headlamp without a screen-space overlay.
[DisallowMultipleComponent]
public sealed class HeadlampController : MonoBehaviour
{
    [SerializeField] HeadlampSettings settings;
    [SerializeField] Transform mount;
    [SerializeField] Light hotspot;
    [SerializeField] Light spill;

    bool isOn;
    bool started;

    public bool IsOn => isOn;

    public void Bind(HeadlampSettings sharedSettings, Transform lampMount,
                     Light focusedBeam, Light peripheralSpill)
    {
        settings = sharedSettings;
        mount = lampMount;
        hotspot = focusedBeam;
        spill = peripheralSpill;
        ApplyConfiguration();

        if (!Application.isPlaying)
            SetLightOutput(0f, false);
    }

    void Start()
    {
        ValidateDependencies();
        ApplyConfiguration();
        started = true;
        SetOn(settings.startsOn);
    }

    void OnEnable()
    {
        if (!Application.isPlaying || !started) return;
        SetLightOutput(isOn ? 1f : 0f, isOn);
    }

    void OnDisable()
    {
        if (hotspot != null) hotspot.enabled = false;
        if (spill != null) spill.enabled = false;
    }

    void OnValidate()
    {
        if (settings == null || mount == null || hotspot == null || spill == null) return;
        ApplyConfiguration();
        if (!Application.isPlaying) SetLightOutput(0f, false);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame
                             && Cursor.lockState == CursorLockMode.Locked)
            SetOn(!isOn);
    }

    public void SetOn(bool value)
    {
        isOn = value;
        SetLightOutput(value ? 1f : 0f, value);
    }

    void ApplyConfiguration()
    {
        if (settings == null || mount == null || hotspot == null || spill == null) return;

        mount.localPosition = settings.mountOffset;
        mount.localRotation = Quaternion.Euler(settings.mountEulerAngles);
        ConfigureSpot(hotspot, settings.hotspotRange, settings.hotspotOuterAngle,
            settings.hotspotInnerAngle, LightShadows.Soft, settings.hotspotShadowStrength);
        ConfigureSpot(spill, settings.spillRange, settings.spillOuterAngle,
            settings.spillInnerAngle, LightShadows.None, 0f);
    }

    static void ConfigureSpot(Light light, float range, float outerAngle, float innerAngle,
                              LightShadows shadows, float shadowStrength)
    {
        light.type = LightType.Spot;
        light.lightUnit = LightUnit.Lumen;
        light.range = range;
        light.spotAngle = outerAngle;
        light.innerSpotAngle = Mathf.Min(innerAngle, outerAngle);
        light.color = Color.white;
        light.useColorTemperature = true;
        light.bounceIntensity = 0f;
        light.shadows = shadows;
        light.shadowStrength = shadowStrength;
        light.shadowBias = 0.075f;
        light.shadowNormalBias = 0.25f;
        light.shadowNearPlane = 0.1f;
    }

    void SetLightOutput(float output, bool enabled)
    {
        if (settings == null) return;

        if (hotspot != null)
        {
            hotspot.colorTemperature = settings.colorTemperatureKelvin;
            hotspot.intensity = settings.hotspotLumens * output;
            hotspot.enabled = enabled;
        }

        if (spill != null)
        {
            spill.colorTemperature = settings.colorTemperatureKelvin;
            spill.intensity = settings.spillLumens * output;
            spill.enabled = enabled;
        }
    }

    void ValidateDependencies()
    {
        if (settings == null || mount == null || hotspot == null || spill == null)
            throw new InvalidOperationException($"{nameof(HeadlampController)}: dependencies are not assigned.");
    }
}
