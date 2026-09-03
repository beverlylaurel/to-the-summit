using UnityEngine;

/// ROLE: stops the player wading before the water reaches their eyes.
/// CALLED BY: nobody. It registers a limit on `FirstPersonController` and answers it.
///
/// THE BOUNDARY IS DEPTH, NOT THE WATERLINE. Walking in shallow water is fine and looks
/// right: the swash, the foam, the wet sand and the sand showing through all work there
/// already. What does not work is the camera going under -- there is no underwater render
/// path, and the screen goes black (`DECISIONS.md`, "Deniz: kapsam disi birakilanlar").
///
/// So the player is stopped where the water would reach the EYES, and every term of that
/// is measured rather than assumed: the eye height off this frame's transform, the depth
/// off the terrain, the crest off the sea's own spectrum.
public class SeaWadeLimit : MonoBehaviour
{
    [Header("Existing systems")]
    [SerializeField] FirstPersonController player;
    [SerializeField] SeaSettings settings;
    [SerializeField] Terrain terrain;

    [Tooltip("The eye. Its height above the feet is read from here every frame rather " +
             "than written down: crouching or a different rig changes it.")]
    [SerializeField] Transform eye;

    /// HOW HIGH THE CREST STANDS ABOVE THE MEAN, AS A SHARE OF `Hs`, IN DEEP WATER.
    ///
    /// Not `Hs / 2`. `Hs` is the mean of the highest third, so half of it is the crest of a
    /// merely typical wave and every larger one would still wash over the eyes. Under the
    /// Rayleigh distribution the one-in-ten wave is `H(1/10) = 1.27 Hs`, whose crest stands
    /// at `0.635 Hs`. The player is stopped before THAT one arrives, not before the average
    /// one does. [SOURCE: Longuet-Higgins 1952, Rayleigh wave-height statistics]
    const float CrestShareOfHs = 0.635f;

    /// BUT NOT WHERE THE PLAYER STANDS. `Hs` is the OPEN WATER height; by the time a wave
    /// reaches wading depth it has already broken, and in the saturated surf zone its height
    /// is set by the depth, not by the storm: `H ~ 0.5 h`, so the crest stands at `0.25 h`.
    /// [SOURCE: Thornton & Guza 1982 -- the same relation the shore break already uses]
    ///
    /// MEASURED (2026-09-03): with `Hs` alone the limit put the player at a depth of 0.34 m,
    /// i.e. ankle deep, because it carried a 2.06 m open-water crest into water 34 cm deep.
    /// Depth-limited, the same conditions stop them at about 1.3 m -- chest deep, which is
    /// where a person actually stops walking into surf.
    const float CrestShareOfDepth = 0.25f;

    void OnEnable()
    {
        if (player == null)
            throw new System.InvalidOperationException($"{nameof(SeaWadeLimit)}: {nameof(player)} is not assigned.");
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SeaWadeLimit)}: {nameof(settings)} is not assigned.");
        if (terrain == null)
            throw new System.InvalidOperationException($"{nameof(SeaWadeLimit)}: {nameof(terrain)} is not assigned.");
        if (eye == null)
            throw new System.InvalidOperationException($"{nameof(SeaWadeLimit)}: {nameof(eye)} is not assigned.");

        player.LimitHorizontal = Limit;
    }

    void OnDisable()
    {
        // Only let go of the limit if it is still ours: a second one may have replaced it.
        if (player != null && player.LimitHorizontal == Limit) player.LimitHorizontal = null;
    }

    public void Bind(FirstPersonController controller, SeaSettings sea, Terrain ground, Transform viewer)
    {
        player = controller;
        settings = sea;
        terrain = ground;
        eye = viewer;
    }

    /// The water's height over the seabed at a point, minus how much of it the player can
    /// take. Positive means the water would be over their eyes there.
    float Excess(Vector3 at, float eyeHeight)
    {
        float groundY = terrain.SampleHeight(new Vector3(at.x, 0f, at.z))
                      + terrain.transform.position.y;
        float depth = settings.seaLevelY - groundY;
        if (depth <= 0f) return -eyeHeight;   // dry ground: nothing to be over the eyes

        // Whichever is SMALLER governs: out in deep water the storm sets the crest, in the
        // surf zone the depth does, and the crossover is where the wave breaks.
        float crest = Mathf.Min(CrestShareOfHs * SeaRuntimeState.SignificantWaveHeight,
                                CrestShareOfDepth * depth);
        return depth + crest - eyeHeight;
    }

    Vector3 Limit(Vector3 position, Vector3 wish)
    {
        if (wish.sqrMagnitude < 1e-6f) return wish;

        // MEASURED, NOT WRITTEN DOWN. The eye sits wherever the rig puts it this frame.
        float eyeHeight = eye.position.y - position.y;
        if (eyeHeight <= 0f) return wish;

        // Look one step ahead: stopping on arrival would already have shown a black frame.
        Vector3 ahead = position + wish * Time.deltaTime;
        if (Excess(ahead, eyeHeight) <= 0f) return wish;

        // ALONG THE SHORE IS STILL ALLOWED. Only the part of the step that goes deeper is
        // taken away; blocking the whole step would glue the player to the spot and they
        // could not walk out sideways.
        Vector3 axisX = new Vector3(wish.x, 0f, 0f);
        Vector3 axisZ = new Vector3(0f, 0f, wish.z);
        bool keepX = Excess(position + axisX * Time.deltaTime, eyeHeight) <= 0f;
        bool keepZ = Excess(position + axisZ * Time.deltaTime, eyeHeight) <= 0f;

        return new Vector3(keepX ? wish.x : 0f, 0f, keepZ ? wish.z : 0f);
    }
}
