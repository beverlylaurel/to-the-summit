using UnityEngine;

/// Measures what the player is exposed to at the listening point. A roof, a closed wall and
/// an open doorway are different questions: precipitation uses overhead cover, while weather
/// audio and wind chill also use the amount of open horizontal solid angle.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class ShelterExposure : MonoBehaviour
{
    public static ShelterExposure Active { get; private set; }

    const float SampleInterval = 0.12f;
    const float RoofRange = 15f;
    const float WallRange = 12f;
    const float EyeInset = 0.08f;
    const int HorizontalSamples = 16;

    static readonly int ShelterCenterRadiusId = Shader.PropertyToID("_ShelterCenterRadius");
    static readonly int ShelterVisualBlockId = Shader.PropertyToID("_ShelterVisualBlock");

    static readonly Vector3[] RoofDirections =
    {
        Vector3.up,
        new(-0.34f, 0.88f, 0f), new(0.34f, 0.88f, 0f),
        new(0f, 0.88f, -0.34f), new(0f, 0.88f, 0.34f),
        new(-0.25f, 0.90f, -0.25f), new(0.25f, 0.90f, -0.25f),
        new(-0.25f, 0.90f, 0.25f), new(0.25f, 0.90f, 0.25f),
    };

    readonly RaycastHit[] hits = new RaycastHit[32];
    Transform observer;
    Transform ignoredRoot;
    float nextSample;
    float coverVelocity;
    float openingVelocity;
    float radiusVelocity;
    bool initialized;

    public float Cover01 { get; private set; }
    public float Opening01 { get; private set; } = 1f;
    public float DryRadius { get; private set; }
    public float Interior01 => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, Cover01));
    public float PrecipitationExposure => 1f - Interior01;
    public bool IsIndoors => Interior01 > 0.5f;

    // Rain remains audible as low-frequency roof impacts. Openings admit a little more direct
    // sound, but never turn an otherwise roofed room back into the exterior mix.
    public float RainTransmission => Mathf.Lerp(1f,
        0.10f + 0.24f * Mathf.Sqrt(Opening01), Interior01);
    public float RainBrightness => Mathf.Lerp(1f,
        0.08f + 0.20f * Mathf.Sqrt(Opening01), Interior01);
    public float WindTransmission => Mathf.Lerp(1f,
        0.015f + 0.12f * Opening01, Interior01);
    public float ThunderTransmission => Mathf.Lerp(1f,
        0.42f + 0.20f * Mathf.Sqrt(Opening01), Interior01);
    public float LightningDirectTransmission => Mathf.Lerp(1f,
        0.025f + 0.22f * Mathf.Sqrt(Opening01), Interior01);

    public void Bind(Transform listener)
    {
        observer = listener;
        ignoredRoot = listener != null ? listener.root : null;
        nextSample = 0f;
    }

    void OnEnable()
    {
        Active = this;
        Publish();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        Shader.SetGlobalVector(ShelterCenterRadiusId, Vector4.zero);
        Shader.SetGlobalFloat(ShelterVisualBlockId, 0f);
    }

    void Update()
    {
        if (observer == null) return;

        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + SampleInterval;
            Measure(out float targetCover, out float targetOpening, out float targetRadius);

            if (!initialized)
            {
                Cover01 = targetCover;
                Opening01 = targetOpening;
                DryRadius = targetRadius;
                initialized = true;
            }
            else
            {
                // Crossing a threshold should feel like entering a room, not like toggling an
                // effect. The short arrival time also rejects single-frame ray gaps at beams.
                Cover01 = Mathf.SmoothDamp(Cover01, targetCover, ref coverVelocity, 0.24f,
                                           Mathf.Infinity, SampleInterval);
                Opening01 = Mathf.SmoothDamp(Opening01, targetOpening, ref openingVelocity, 0.32f,
                                             Mathf.Infinity, SampleInterval);
                DryRadius = Mathf.SmoothDamp(DryRadius, targetRadius, ref radiusVelocity, 0.30f,
                                             Mathf.Infinity, SampleInterval);
            }
        }

        Publish();
    }

    void Measure(out float cover, out float opening, out float dryRadius)
    {
        Vector3 origin = observer.position + Vector3.up * EyeInset;

        float roofHits = 0f;
        for (int i = 0; i < RoofDirections.Length; i++)
        {
            bool hit = Cast(origin, RoofDirections[i].normalized, RoofRange, out _);
            // The vertical ray decides whether precipitation can reach the listener. Oblique
            // rays make the transition near eaves and openings gradual.
            roofHits += hit ? (i == 0 ? 4f : 1f) : 0f;
        }
        cover = roofHits / (RoofDirections.Length + 3f);

        int openCount = 0;
        float furthestWall = 0f;
        for (int i = 0; i < HorizontalSamples; i++)
        {
            float angle = i * Mathf.PI * 2f / HorizontalSamples;
            Vector3 direction = new(Mathf.Cos(angle), 0.06f, Mathf.Sin(angle));
            if (Cast(origin, direction.normalized, WallRange, out float distance))
                furthestWall = Mathf.Max(furthestWall, distance);
            else
                openCount++;
        }

        opening = openCount / (float)HorizontalSamples;
        dryRadius = cover > 0.35f
            ? Mathf.Clamp(furthestWall + 0.65f, 2.2f, 10f)
            : 0f;
    }

    bool Cast(Vector3 origin, Vector3 direction, float distance, out float nearest)
    {
        nearest = distance;
        bool found = false;
        CollectHits(origin, direction, distance, false, ref nearest, ref found);

        // Imported architectural shells are allowed to use one-sided roof faces. PhysX ignores
        // their back face when queried from the room, so repeat the same segment from outside
        // toward the listener. This is local to the query and does not change the project's
        // global Physics.queriesHitBackfaces setting.
        CollectHits(origin + direction * distance, -direction, distance, true,
                    ref nearest, ref found);
        return found;
    }

    void CollectHits(Vector3 origin, Vector3 direction, float distance, bool reverse,
                     ref float nearest, ref bool found)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance,
                                            Physics.DefaultRaycastLayers,
                                            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            if (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot)) continue;
            float fromListener = reverse ? distance - hits[i].distance : hits[i].distance;
            if (fromListener < nearest)
            {
                nearest = fromListener;
                found = true;
            }
        }
    }

    void Publish()
    {
        Vector3 center = observer != null ? observer.position : transform.position;
        Shader.SetGlobalVector(ShelterCenterRadiusId,
            new Vector4(center.x, center.y, center.z, Mathf.Max(DryRadius, 0.01f)));
        Shader.SetGlobalFloat(ShelterVisualBlockId, Interior01);
    }

#if UNITY_EDITOR
    public void EditorSampleNow()
    {
        Measure(out float cover, out float opening, out float radius);
        Cover01 = cover;
        Opening01 = opening;
        DryRadius = radius;
        initialized = true;
        Publish();
    }
#endif
}
