using UnityEngine;

/// Draws the visible channel running from the cloud to the ground on near strikes.
///
/// It does not decide where the strike happened: `LightningFlash` places it and this reads it.
/// Choosing the position a second time would put the light in one place and the bolt in another.
///
/// It is only drawn on near strikes. Distant lightning does not show its channel in reality
/// either — the cloud and air in between swallow it and what is left is the sea lighting up. The
/// bolt being visible carries information about the distance itself, so it must not be drawn
/// independently of the distance.
public class LightningBolt : MonoBehaviour
{
    [SerializeField] LightningFlash flash;
    [SerializeField] Terrain terrain;
    [SerializeField] LightningSettings settings;
    [SerializeField] Material material;

    /// The bolt is a TREE: the main channel and the generations born from it. How many lines are
    /// needed varies with the strike (branching is probabilistic), hence a pool — creating
    /// objects on every strike would produce garbage.
    readonly System.Collections.Generic.List<LineRenderer> lines = new();
    int usedLines;
    Light contact;

    /// A SINGLE BUFFER IS ENOUGH. A branch is born from its parent's TRACED points; but the
    /// point of birth is COPIED as a Vector3 into the queue before the buffer is reused. The
    /// queue carries values, not a reference to the buffer.
    Vector3[] points;

    readonly System.Collections.Generic.Queue<Branch> pending = new();

    /// A branch's birth data. Everything derived from its parent is here; when its turn to be
    /// traced comes, the geometry is produced from these.
    struct Branch
    {
        public Vector3 from;
        public Vector3 direction;
        public float distance;
        public float width;
        public float waviness;
        public float chance;
        public int generation;
    }
    float elapsed;
    float distanceFade;
    float life;
    bool active;

    public void Bind(LightningFlash source, Terrain ground, LightningSettings tuning,
        Material boltMaterial)
    {
        flash = source;
        terrain = ground;
        settings = tuning;
        material = boltMaterial;
    }

    void OnEnable()
    {
        if (flash == null || terrain == null || settings == null || material == null)
            throw new System.InvalidOperationException(
                $"{nameof(LightningBolt)}: dependencies are not assigned.");

        Build();
        flash.Placed += OnPlaced;
        Hide();
    }

    void OnDisable()
    {
        flash.Placed -= OnPlaced;
        Hide();
    }

    /// The lines and the contact light are built once; recreating them on every strike produces garbage.
    ///
    /// Two separate conditions, not one flag. The arrays depend on the node count in the
    /// settings, while the objects should only be built once. Gathering these behind the single
    /// question "is there a channel" led to an array added later silently staying unallocated.
    void Build()
    {
        int count = settings.boltSegments + 1;

        if (points == null || points.Length != count) points = new Vector3[count];

        if (lines.Count > 0) return;

        // The light at the contact point can be a point light: unlike the directional one, this
        // really is nearby, its range stays a few hundred metres and it does not choke the clustering.
        var lit = new GameObject("Contact");
        lit.transform.SetParent(transform, false);
        contact = lit.AddComponent<Light>();
        contact.type = LightType.Point;
        contact.shadows = LightShadows.None;
        contact.color = settings.flashColor;
        contact.range = settings.groundRange;
        contact.intensity = 0f;
    }

    /// Hands out a line from the pool, creating one if there is none. The ceiling is `boltMaxLines`.
    LineRenderer TakeLine()
    {
        if (usedLines < lines.Count) return lines[usedLines++];
        if (lines.Count >= settings.boltMaxLines) return null;

        var line = CreateLine($"Bolt{lines.Count}", settings.boltWidth);
        lines.Add(line);
        usedLines++;
        return line;
    }

    LineRenderer CreateLine(string name, float width)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(transform, false);

        var line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = material;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        return line;
    }

    void OnPlaced(LightningStrike strike)
    {
        if (strike.Distance > settings.boltDistance)
        {
            Hide();
            return;
        }

        // The channel descends from the cloud **base** and touches the ground. The part of the
        // discharge that stays inside the mass is invisible anyway; starting there hung the
        // channel in front of the cloud. The end point is set by the slope itself, not a fixed elevation.
        Vector3 top = new(strike.Origin.x, strike.CloudBase, strike.Origin.z);
        Vector3 foot = new(top.x, terrain.SampleHeight(top) + terrain.transform.position.y, top.z);

        GrowTree(top, foot);

        contact.transform.position = foot;
        contact.range = settings.groundRange;

        // DISTANCE FADE. The bolt being visible carries distance information; fading out rather
        // than a hard cut is both realistic and does not give away where the limit is.
        distanceFade = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(settings.boltFullDistance, settings.boltDistance,
                              strike.Distance));

        elapsed = 0f;
        life = strike.Duration;
        active = true;
    }

    /// Weaves a channel between two points.
    ///
    /// The deviation is a **walk**: each step's offset continues the previous one. Offsetting every
    /// point independently around the straight line produced saw teeth — two consecutive points
    /// fell to opposite ends and the channel turned into a sharp, regular zigzag. A real channel
    /// is not like that: the ionizing path carries its own direction and its bends are linked.
    ///
    /// The offset stays in the plane perpendicular to the channel; throwing it along the axis
    /// folded the channel onto itself and rewound its descent. It goes to zero at the ends: the
    /// points where it leaves the cloud and touches the ground have to stay fixed.
    ///
    /// The buffer is supplied from outside: the main channel's points are still being read while
    /// the forks are being placed, and writing to the same array would corrupt them.
    void Trace(LineRenderer line, Vector3 from, Vector3 to, float waviness, Vector3[] buffer)
    {
        int count = buffer.Length;

        // The deviation is scaled by the channel's own length. Given in absolute metres the forks
        // — which are many times shorter than the main channel — come out proportionally twice as
        // bent, and the sharp break approached the node spacing and turned into saw teeth.
        float wander = waviness * Vector3.Distance(from, to);

        Vector3 axis = (to - from).normalized;
        Vector3 side = Vector3.Normalize(Vector3.Cross(axis, Vector3.forward));
        if (side.sqrMagnitude < 0.5f) side = Vector3.Normalize(Vector3.Cross(axis, Vector3.right));
        Vector3 other = Vector3.Cross(axis, side);

        // A damped walk: a random impulse is added to the velocity, and both the velocity and the
        // offset are pulled back towards the centre. Without the pull the channel breaks away from
        // the straight line and wanders off.
        //
        // The walk is written into the caller's buffer, not a second array. Keeping a separate
        // array meant the loop iterating over one array's length while indexing another; because
        // their lengths were set in two different places, it blows up the moment they do not match.
        var drift = Vector2.zero;
        var speed = Vector2.zero;
        float widest = 0f;

        for (int i = 1; i < count - 1; i++)
        {
            speed = speed * 0.65f + Random.insideUnitCircle;
            drift = (drift + speed) * 0.85f;

            buffer[i] = new Vector3(drift.x, drift.y, 0f);
            widest = Mathf.Max(widest, drift.magnitude);
        }

        // The amplitude is scaled afterwards: the damping coefficients do not say directly how
        // large the offset will grow, while the metre value in the settings must.
        float scale = widest > 0.001f ? wander / widest : 0f;

        // A second scale: an independent, sharp, small break at every node. The walk alone is low
        // frequency — it gives a wide, soft arc and the channel looks lifeless. Using only this
        // second scale produced saw teeth as well.
        // A real channel has both: a crackle riding on a wide oscillation.
        float kink = wander * settings.boltKink;

        for (int i = 1; i < count - 1; i++)
        {
            float t = (float)i / (count - 1);
            float taper = Mathf.Sin(t * Mathf.PI);

            Vector2 sharp = Random.insideUnitCircle * kink;
            Vector3 broad = buffer[i] * scale;

            float x = (broad.x + sharp.x) * taper;
            float y = (broad.y + sharp.y) * taper;

            buffer[i] = Vector3.Lerp(from, to, t) + side * x + other * y;
        }

        buffer[0] = from;
        buffer[count - 1] = to;

        line.positionCount = count;
        line.SetPositions(buffer);
    }

    void Update()
    {
        if (!active) return;

        // If the light is frozen the channel freezes too: both are parts of the same strike
        if (!flash.Held) elapsed += Time.deltaTime;

        if (elapsed >= life)
        {
            Hide();
            return;
        }

        // The channel lives shorter than the glow itself: while the light scatters through the
        // cloud and dies, the channel has long gone out. It flickers frame to frame — the
        // discharge is not continuous.
        float remaining = 1f - elapsed / life;
        float flicker = flash.Held ? 1f : remaining * remaining * Random.Range(0.55f, 1f);

        SetVisible(true);
        contact.intensity = settings.groundIntensity * flicker * distanceFade;

        // The main channel is the brightest and every generation dimmer. The discharge's power
        // falls with each branching; drawing them all at the same brightness turned the tree into
        // a flat ball of wire.
        var tint = settings.flashColor * (flicker * distanceFade);
        for (int i = 0; i < usedLines; i++)
            lines[i].startColor = lines[i].endColor = tint * lineTint[i];
    }

    void Hide()
    {
        active = false;
        SetVisible(false);

        if (contact != null) contact.intensity = 0f;
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < lines.Count; i++)
            lines[i].enabled = visible && i < usedLines;
    }

    /// BUILDS THE TREE. Reed & Wyvill: a branch deviates from its parent by 16 degrees on average
    /// (normal distribution), and at every generation the thickness/probability/length fall while
    /// the sinuosity RISES.
    ///
    /// A breadth-first queue, not recursion: the tree's size is probabilistic and the stack depth
    /// is not known in advance. The queue also applies the budget ceiling in the natural place —
    /// once the ceiling is full the remaining branches are never born and no half-finished branch is left.
    void GrowTree(Vector3 top, Vector3 foot)
    {
        usedLines = 0;
        pending.Clear();

        pending.Enqueue(new Branch
        {
            from = top,
            direction = (foot - top).normalized,
            distance = Vector3.Distance(top, foot),
            width = settings.boltWidth,
            waviness = settings.boltWaviness,
            chance = settings.boltBranchCount,
            generation = 0,
        });

        while (pending.Count > 0)
        {
            var branch = pending.Dequeue();

            var line = TakeLine();
            if (line == null) break;              // the budget is full

            line.widthMultiplier = branch.width;
            EnsureTintCapacity();
            lineTint[usedLines - 1] = Mathf.Pow(0.7f, branch.generation);

            // THE MAIN CHANNEL TOUCHES THE GROUND, the branches end in the air. The channel's end
            // point is the slope itself; a branch's end is its direction and length.
            Vector3 target = branch.generation == 0
                ? foot
                : branch.from + branch.direction * branch.distance;

            Trace(line, branch.from, target, branch.waviness, points);

            if (branch.generation >= settings.boltGenerations) continue;

            // THE CHILDREN ARE BORN FROM THE PARENT'S TRACED POINTS — not from the straight line.
            // Born from the straight line they hang in the air beside the bent channel.
            // The points are COPIED: the buffer will be overwritten on the next branch.
            // THE EXPECTED COUNT is converted into a per-node probability. The number of candidate
            // nodes depends on `boltSegments`; giving the probability directly tied the branch
            // count to the resolution.
            int candidates = points.Length - 2;
            float perNode = candidates > 0 ? branch.chance / candidates : 0f;

            for (int i = 1; i < points.Length - 1; i++)
            {
                if (Random.value >= perNode) continue;

                Vector3 heading = ChildDirection(branch.direction);

                pending.Enqueue(new Branch
                {
                    from = points[i],
                    direction = heading,
                    distance = branch.distance * settings.boltBranchLength,
                    width = branch.width * settings.boltWidthDecay,
                    waviness = branch.waviness * settings.boltWavinessGrowth,
                    chance = branch.chance * settings.boltBranchCountDecay,
                    generation = branch.generation + 1,
                });
            }
        }

        SetVisible(true);
    }

    /// The branch's direction: it deviates from its parent by 16 degrees ON AVERAGE, with the
    /// deviation normally distributed.
    ///
    /// A fixed angle (the old state) lined every fork up on the same cone and the tree looked like
    /// an umbrella. The normal distribution is Reed & Wyvill's single empirical observation:
    /// branches in nature gather around this value, with rare sharp deviations in the tail.
    ///
    /// There is a cap because the normal distribution's tail is unbounded: unclipped, a branch can
    /// turn back upward into the cloud.
    Vector3 ChildDirection(Vector3 parent)
    {
        float deg = settings.boltBranchAngle + Gaussian() * settings.boltBranchSpread;
        deg = Mathf.Clamp(Mathf.Abs(deg), 1f, settings.boltBranchAngleMax);

        // Sapma ekseni: ebeveyne dik, azimutu rastgele.
        Vector3 axis = Vector3.Cross(parent, Random.onUnitSphere);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(parent, Vector3.right);

        return (Quaternion.AngleAxis(deg, axis.normalized) * parent).normalized;
    }

    /// Box-Muller. Unity has no normal distribution; `Random.value` is uniform and a 16 degree
    /// "average" cannot be built from a uniform distribution — there is no gathering around the
    /// mean, only a band.
    static float Gaussian()
    {
        float u1 = Mathf.Max(Random.value, 1e-6f);
        float u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    float[] lineTint = new float[8];

    void EnsureTintCapacity()
    {
        if (lineTint.Length >= lines.Count) return;
        System.Array.Resize(ref lineTint, Mathf.Max(lines.Count, lineTint.Length * 2));
    }
}
