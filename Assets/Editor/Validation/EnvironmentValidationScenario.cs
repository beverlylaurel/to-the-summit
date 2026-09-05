using System;
using System.Collections.Generic;
using UnityEngine;

/// One deterministic visual acceptance point. Values are code-owned so a scene save cannot
/// silently change the reference conditions.
[Serializable]
public sealed class EnvironmentValidationScenario
{
    public readonly string id;
    public readonly string title;
    public readonly Vector2 playerXZ;
    public readonly float yaw;
    public readonly float pitch;
    public readonly float hour;
    public readonly float storm;
    public readonly float temperatureC;
    public readonly float windSeverity;
    public readonly float windAngle;
    public readonly float snowDepth;
    public readonly float seaTime;
    public readonly float swashPhase;
    public readonly float fieldOfView;
    public readonly bool headlamp;
    public readonly bool viewfinder;
    public readonly bool forceClearWindow;

    public EnvironmentValidationScenario(
        string id, string title, Vector2 playerXZ, float yaw, float pitch,
        float hour, float storm, float temperatureC,
        float windSeverity, float windAngle, float snowDepth,
        float seaTime, float swashPhase, float fieldOfView,
        bool headlamp = false, bool viewfinder = false, bool forceClearWindow = false)
    {
        this.id = id;
        this.title = title;
        this.playerXZ = playerXZ;
        this.yaw = yaw;
        this.pitch = pitch;
        this.hour = hour;
        this.storm = storm;
        this.temperatureC = temperatureC;
        this.windSeverity = windSeverity;
        this.windAngle = windAngle;
        this.snowDepth = snowDepth;
        this.seaTime = seaTime;
        this.swashPhase = swashPhase;
        this.fieldOfView = fieldOfView;
        this.headlamp = headlamp;
        this.viewfinder = viewfinder;
        this.forceClearWindow = forceClearWindow;
    }
}

public static class EnvironmentValidationCatalog
{
    // Measured east-coast acceptance point. Terrain height is sampled at runtime; only XZ is
    // stored, so rebuilding the terrain does not leave the player below or above the ground.
    static readonly Vector2 Coast = new(13474f, 2000f);

    public static IReadOnlyList<EnvironmentValidationScenario> All { get; } =
        new EnvironmentValidationScenario[]
        {
            new("coast-clear-noon", "Kıyı · açık öğlen", Coast, 0f, 10f,
                11.05f, 0f, 8.5f, 0.10f, 35f, 0f, 120f, 0.12f, 60f,
                forceClearWindow: true),

            new("coast-rain-morning", "Kıyı · yağmurlu sabah", Coast, 0f, 10f,
                8.38f, 0.90f, 8.5f, 0.85f, 35f, 0f, 420f, 0.36f, 60f),

            new("coast-snow-uprush", "Karlı kıyı · ilerleyen swash", Coast, 0f, 12f,
                9f, 0.68f, -6f, 0.55f, 35f, 0.35f, 260f, 0.20f, 60f),

            new("coast-snow-backwash", "Karlı kıyı · çekilen swash", Coast, 0f, 12f,
                9f, 0.68f, -6f, 0.55f, 35f, 0.35f, 260f, 0.72f, 60f),

            new("sea-distant-horizon", "Uzak deniz · dar görüş", Coast, 0f, 1f,
                11.05f, 0.35f, 8f, 0.35f, 35f, 0f, 640f, 0.32f, 25f),

            new("night-headlamp", "Gece · kafa feneri", Coast, 180f, 8f,
                22f, 0.28f, 1f, 0.25f, 35f, 0f, 80f, 0.18f, 60f,
                headlamp: true),

            new("camera-viewfinder", "Kamera · canlı vizör", Coast, 0f, 8f,
                17.25f, 0.12f, 6f, 0.20f, 35f, 0f, 180f, 0.42f, 60f,
                viewfinder: true)
        };

    public static EnvironmentValidationScenario Find(string id)
    {
        foreach (EnvironmentValidationScenario scenario in All)
            if (scenario.id == id) return scenario;
        return null;
    }
}
