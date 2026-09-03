// include-rev: 43  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemeyebiliyor; bu satir degisince derleme zorlanir)
Shader "ToTheSummit/Precipitation"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Precipitation"

            // OCCLUSION `[Garg 2006, §5]`, the last step: "we use the user-specified depth
            // map of the scene to find the pixels for which the rain streak is not
            // occluded by the scene. The streak is rendered only over those pixels."
            //
            // The paper has to do this as a separate step because its input is a photograph
            // and all it has is a COARSE depth map. We already have a depth buffer, exact per
            // pixel: because `ZTest` defaults to `LEqual`, streak fragments falling behind the
            // terrain are culled in the rasterizer.
            //
            // `ZWrite Off` — streaks must not occlude each other, transparents accumulate.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
            CBUFFER_END

            // Snow wraps in its own box: the same particle budget is packed more tightly
            // around the camera. A point-shaped flake does not cover as much screen area as an elongated drop.
            float3 _BoxSize;

            // Drops and flakes fall at different speeds and take different amounts of wind:
            // every population has its own accumulated drift and its own direction vector.
            // Rain is further split into eight speed classes: because drop size sets both the
            // fall speed and the wind resistance, each class comes down at a different angle.
            #define RAIN_SPEED_CLASSES 8

            // THE WIND'S BOUNDARY LAYER. The reasoning is inside `vert`, where it is used.
            #define WIND_Z0          0.1      // roughness length, metres (rocky terrain)
            #define WIND_REF_HEIGHT  24.0     // free stream elevation = top of the visible volume
            #define WIND_MIN_HEIGHT  0.1      // = z0; the profile is exactly zero here, the wind stops at the ground
            #define WIND_PROFILE_L   5.4806   // ln(24/0.1)
            #define WIND_LAG_TOP     4.3791   // G(24), the upper limit of the lag integral

            // The spatial wave number of the eddy octaves and their own temporal frequency.
            // A mirror of the coefficients inside `Turbulence`; the inertia filter reads them.
            #define TURB_COARSE_K    0.60
            #define TURB_COARSE_W    1.30
            #define TURB_FINE_K      1.80
            #define TURB_FINE_W      2.60
            #define TURB_COARSE_LAMBDA 10.472   // 2*pi/0.60, wavelength of the coarse octave
            #define KARMAN           0.4        // von Karman; in the surface layer l = KARMAN*z

            float4 _RainDrifts[RAIN_SPEED_CLASSES];
            float4 _RainDriftsNear[RAIN_SPEED_CLASSES];   // the inner box's own drift
            float3 _NearBoxSize;
            float4 _RainDirections[RAIN_SPEED_CLASSES];
            float _Density;          // visual density, a bent version of the intensity
            float _Precipitation;    // raw intensity, for the drop size distribution
            float _RainTurbulence;  // amplitude with which a drop is caught by an eddy, metres
            float3 _WindSweep;      // accumulated wind-driven translation of the eddy field, metres

            // ---- GARG-NAYAR STREAK DATABASE `[Garg 2006, §5]`, `rain-spec.md` §6 ----
            //
            // The image streak of a drop is not a bar of constant brightness: an oscillating
            // drop refracts light into speckles, smeared highlights and curved contours.
            // Because the pattern needs ray tracing it is baked offline and looked up here.
            TEXTURE2D_ARRAY(_StreakPoint);      SAMPLER(sampler_StreakPoint);
            TEXTURE2D_ARRAY(_StreakAmbient);    SAMPLER(sampler_StreakAmbient);

            // Slice layout in the working set: ((corner * 5) + dcam) * 10 + osc.
            // Corner order (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
            float2 _StreakCellBlend;      // the share within the (v, h) cell
            float4 _StreakCornerPresent;  // whether the corner exists in the database (0/1)
            float  _StreakMirror;         // above 180° of azimuth the texture is mirrored horizontally
            float  _StreakDcamFraction[5];// the array was filled according to the longest `dcam`
            float  _StreakExposure;       // the camera's exposure time, seconds
            float  _StreakDbPeriod;       // the oscillation period the database was baked at
            float  _StreakSourceScale;    // ratio of the database's source to our sun

            /// DIAGNOSTIC MODE. 0 off, 1 magnify, 2 raw pattern, 3 alpha.
            ///
            /// At physical scale a distant streak is thinner than a pixel and only a few pixels
            /// long, at an alpha of a few thousandths — the eye cannot tell "is it there or not".
            /// MEASURED at 60° FOV / 888 px: the mean drop is 3.1 px at 24 m with `T_exp` 1/60 s
            /// and 9.3 px at 1/20 s; the width sits on the `MinPixelWidth` floor either way.
            /// The three modes separate three different questions: is the size too small, is the
            /// pattern empty, or is the alpha low.

            /// RADIANCE OF THE SUN DISC — the source of the directional channel.
            ///
            /// `_HeightFogSunColor` CANNOT BE USED, its name is misleading: its own comment
            /// says "the sky toward the sun, 2° above the horizon", i.e. a SKY color. The
            /// directional channel wants the sun itself, and the disc is orders of magnitude
            /// brighter than the sky. Measured: with that global the radiance stayed in the
            /// 0.08-0.32 band and the drops fell darker than the sky.
            ///
            /// The ambient channel still uses `_HeightFogColor` — that really is a sky color,
            /// and it is the illumination a drop receives from the dome.
            float3 _StreakSunRadiance;

            /// The screen share of the cluster of drops one particle represents. The reasoning
            /// is where it is used: our density is a thousandth of reality's.
            /// (outer box density, inner box density) drops/m³. The representation share is
            /// derived from this BY POSITION; it is not a constant.
            float4 _RainDensity;

            #define STREAK_DCAM_COUNT 5
            #define STREAK_OSC_COUNT 10

            /// The drop's real radius (metres). Mapped from the class ratio onto the
            /// Marshall-Palmer range: 0.25 mm fine drizzle, 2.5 mm a large drop.
            ///
            /// The quad's width comes from here too (diameter = 2r0) and the transparency
            /// formula wants the same value. One source: coming from separate numbers, the
            /// alpha and the on-screen thickness could drift independently.
            /// CONTINUOUS SAMPLING FROM MARSHALL-PALMER.
            ///
            /// `N(D) = N0·exp(-LD)`,  `L = 4.1·R^(-0.21)` mm^-1, `R` the rain rate (mm/h).
            /// A sample from the exponential distribution: `D = -ln(u)/L`.
            ///
            /// IT USED TO BE 8 DISCRETE VALUES. The radius derived from the speed class index
            /// (`dropClass/7`), so length and thickness were locked to eight steps and the
            /// drops read as "all the same" (reported by the user). The speed class MUST stay
            /// discrete — the wind response is held per class in an array — but there is no
            /// reason for the radius to be discrete; the paper also wants a continuous
            /// distribution (`[Garg 2006, §5]`, footnote 11).
            ///
            /// The diameter is clamped to 0.5-5 mm: below that it is the fog's business, above it a drop breaks up as it falls.
            float DropRadius(float3 u, float intensity)
            {
                float rate = lerp(0.5, 50.0, intensity);          // mm/sa
                float lambda = 4.1 * pow(rate, -0.21);            // mm⁻¹

                // SAMPLED BY COVERAGE — not by count.
                //
                // The Marshall-Palmer number distribution: the overwhelming majority of drops
                // are tiny. Measured: at R = 50 mm/h, L = 1.82 and the MEDIAN DIAMETER is
                // 0.38 mm. Sampled by count, almost the entire particle budget goes to drops
                // invisible on screen and the rain disappears (reported by the user).
                //
                // Those tiny drops exist in real rain too, but the large ones carry the image,
                // and our drop count is a thousandth of reality's — the budget has to be spent
                // on what is visible.
                //
                // Screen share ~ diameter x speed ~ D². The D²-weighted form of the
                // exponential distribution is Gamma(3): the sum of three exponential samples.
                // The mean diameter is 3/L = 1.65 mm.
                //
                // The paper grants this freedom in its own footnote (`[Garg 2006]`,
                // footnote 11): "The size distribution can also be customized to include
                // larger drop sizes to create more dramatic rain effects."
                float sum = -(log(max(u.x, 1e-4)) + log(max(u.y, 1e-4)) + log(max(u.z, 1e-4)));
                float diameter = sum / lambda;                    // mm

                // 0.5-5 mm: below that it is the fog's business, above it a drop breaks up as it falls.
                return clamp(diameter, 0.5, 5.0) * 0.0005;        // radius, metres
            }

            /// Terminal velocity (m/s), the Atlas relation fitted to Gunn & Kinzer's
            /// measurements:
            ///   v(D) = 9.65 - 10.3·exp(-0.6·D),  D = diameter (mm)
            ///
            /// NOT IN THE PAPER. `[Garg 2006]` uses `v` in the formula `a = 2r0/(vT_exp)` but
            /// does not give its model; `rain-spec.md` §11.2-2 flags that gap and points to
            /// Gunn & Kinzer.
            ///
            /// THIS IS ALSO THE PARTICLES' VISUAL FALL SPEED. `_RainDirections` used to carry an
            /// exaggerated 16 m/s "because the particles sit 16-24 m away"; that belonged to the
            /// old visual model and IS GONE — the CPU fills the array from this same relation.
            /// Motion and streak length have to come from one speed: the streak is the path the
            /// drop travelled during the exposure, so a drop moving at one speed and streaking at
            /// another leaves a mark shorter than its own path (measured: 57%).
            float TerminalVelocity(float radius)
            {
                float diameterMm = radius * 2000.0;
                return 9.65 - 10.3 * exp(-0.6 * diameterMm);
            }

            // The terrain height, the snow profile on the ground and the curtain's color are
            // here. Near particles are fed from the SAME sources as the distant curtain: with
            // separate rules a wind threshold could be crossed in one layer and not the other.
            #include "HeightFog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;   // y: inner box. x and z unused.
                float2 corner     : TEXCOORD0;
                float2 seedXY     : TEXCOORD1;
                float2 seedZW     : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner     : TEXCOORD0;
                float  alpha      : TEXCOORD1;
                float3 color      : TEXCOORD3;
                float3 streak     : TEXCOORD7;    // (osc, dcam lower index, dcam share)
                float2 streakCrop : TEXCOORD8;    // (v scale, whether merging happened)
                float3 airColor   : TEXCOORD9;    // radiance of the sky BEHIND the drop
            };

            // Shifts the particle grid around the camera by whole box multiples.
            // Because the shift is an exact multiple of the box size the particles appear fixed in the world.
            float3 WrapAroundCamera(float3 worldPos, float3 cameraPos, float3 box)
            {
                float3 relative = worldPos - cameraPos + box * 0.5;
                relative -= box * floor(relative / box);
                return cameraPos - box * 0.5 + relative;
            }

            float Hash(float3 seed)
            {
                return frac(sin(dot(seed, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            // The narrowest width a quad may shrink to on screen. Below it a particle is
            // swallowed by the rasterizer or boils.
            #define MinPixelWidth 1.2

            // How many pixels one radian of angle falls on. Element [1][1] of the projection
            // matrix is 1/tan(fov/2), and multiplied by the vertical resolution it gives the
            // scale. The abs is required: drawing into a D3D render target flips the y axis and
            // this element goes negative; without taking the sign the scale breaks and everything disappears.
            float PixelsPerRadian()
            {
                return abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
            }

            // The turbulence field: eddies in the air. Sampled from the particle's position
            // rather than its own seed — particles in the same eddy have to be thrown together.
            // Independent randomness looks like a swarm of ants, not precipitation.
            //
            // The field flows with the wind (Taylor's hypothesis: turbulence is carried by the
            // mean flow). A coarse eddy follows the wind exactly, a fine one lags at partial
            // speed. A world-fixed field made the particles shiver in place like taut wires as
            // they passed through it — it read as the flake shaking, not the air turning.
            //
            // The vertical component is deliberately weak: real turbulence is horizontally
            // dominated, and strong in the vertical the particles rise and break the physics.
            /// `gainCoarse` / `gainFine`: the particle's INERTIA FILTER, per octave.
            /// The reasoning and the numbers are where it is called.
            float3 Turbulence(float3 worldPos, float t, float gainCoarse, float gainFine)
            {
                // THE SCALE MUST BE SMALLER THAN THE VISIBLE VOLUME.
                //
                // It used to be 0.15, i.e. a wavelength of 42 m. The visible rain volume is
                // 32 m; the field did not complete even one period across the whole volume and
                // two drops a metre apart were only 9 degrees of phase apart. The result was
                // not eddies but the volume swinging AS A WHOLE — the user said "the eddies
                // are all the same, I'm not sure it's working". It was working, but it produced
                // no difference between neighbouring drops.
                //
                // 0.60 -> a wavelength of 10.5 m, 3.1 periods in the box, 34 degrees per metre.
                float3 p = (worldPos - _WindSweep) * 0.60;
                float3 coarse = float3(
                    sin(p.y + t * 1.3) * cos(p.z * 0.7 + t * 0.9),
                    sin(p.z + t * 1.1) * 0.35,
                    cos(p.x + t * 1.7) * sin(p.y * 0.8 + t * 1.2));

                // Second octave: small eddies, three times the frequency, a third of the
                // amplitude. 1.80 -> a wavelength of 3.5 m, 9.2 periods in the box, 103 degrees
                // per metre — neighbouring drops are inside different eddies.
                float3 q = (worldPos - _WindSweep * 0.55) * 1.80;
                float3 fine = float3(
                    sin(q.z + t * 2.6) * cos(q.x * 0.8 + t * 2.1),
                    cos(q.x + t * 2.3) * 0.35,
                    sin(q.y + t * 3.1) * cos(q.z * 0.9 + t * 2.4));

                return coarse * gainCoarse + fine * (0.33 * gainFine);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 cameraPos = _WorldSpaceCameraPos;
                float4 seed = float4(IN.seedXY, IN.seedZW);

                float isNear = IN.positionOS.y;   // whether it belongs to the inner box


                // Marshall-Palmer: small drops are very common, large ones rare. The scale
                // parameter changes with the rain intensity (L = 4.1·R^-0.21), so in a downpour
                // the distribution shifts toward the large end. Drizzle sifts down thin and
                // slow, a downpour comes down large and fast. Because the intensity already
                // fluctuates through two Perlin layers, the rhythm of the rain changes on its
                // own even with no wind — no separate noise is needed
                // ONE SOURCE: THE RADIUS FIRST, THE CLASS DERIVES FROM IT.
                //
                // For a while it was the other way round: the class came from an independent
                // hash and the radius from another. The result was inconsistent — a drop could
                // be in the slowest class and leave the longest streak, because MOTION came
                // from the class and STREAK LENGTH from the radius. The wind and eddy responses
                // are class dependent too, so the same disconnect was there.
                //
                // Now the radius is sampled from a continuous distribution and the class is its
                // bucket. The class MUST STAY DISCRETE: wind drift is integrated per class on
                // the CPU (`_RainDrifts`) and cannot be done per drop.
                float dropRadius = DropRadius(
                    float3(Hash(seed.yxw), Hash(seed.wxy), Hash(seed.xwz)), _Precipitation);

                // Use the 0.25-2.5 mm radius range as the class axis.
                float dropSize = saturate((dropRadius - 0.00025) / 0.00225);
                int dropClass = (int)min(dropSize * RAIN_SPEED_CLASSES,
                                         RAIN_SPEED_CLASSES - 1);

                float physicalSpeed = TerminalVelocity(dropRadius);
                // THE INERTIA FILTER'S RELAXATION TIME COMES FROM THE FALL SPEED: `tau = v_t/g`.
                // All three populations differ: a drop 2-9 m/s, a snowflake 1.4, a broken
                // particle lifted from the ground ~0.5 (small and irregular, it settles into the air at once).
                float fallSpeed = physicalSpeed;

                // THE INNER BOX. A separate particle population; it wraps in its own box with
                // its own drift. Snow can enter the inner box too — there is no representation
                // share there, the particles are drawn one to one, so the thickening gives the
                // right result by itself.
                float3 box = isNear > 0.5
                           ? _NearBoxSize
                           : _BoxSize;
                float3 drift = isNear > 0.5
                             ? _RainDriftsNear[dropClass].xyz
                             : _RainDrifts[dropClass].xyz;

                // ---- THE WIND'S BOUNDARY LAYER ----
                //
                // The wind drops to zero at the ground and opens logarithmically with height:
                // `f(z) = ln(z/z0)/ln(z_ref/z0)`, `z0 = 0.1 m` (rocky). Drifting snow had this,
                // falling precipitation did not — a drop took the full free stream at every
                // elevation, so near the ground the wind was twice what it should have been.
                //
                // BANDS WERE TRIED AND ELIMINATED BY MEASUREMENT. A separate drift had been
                // integrated for four height bands per class. The bands' drifts diverge WITHOUT
                // BOUND over time (101 m in 30 s) and once wrapped into the box the difference
                // between them becomes a random number (±24 m). Because a drop crosses bands as
                // it falls, that random difference rode on it as a FAKE HORIZONTAL VELOCITY of
                // up to 21 m/s — larger than the wind itself. The symptom: "the rain drifts
                // through the air like snow".
                //
                // THE RIGHT ANSWER IS CLOSED FORM. A drop lags behind the free stream for as
                // long as it spends in the slow air; that LAG is a bounded integral:
                //
                //     L(z) = (U/v_t) · integral_z^{z_ref} (1 - f(z')) dz'
                //
                // The analytic form of the integral is `G(z) = z - z·(ln(z/z0) - 1)/L`,
                // `L = ln(z_ref/z0)`. L is single-variable, smooth and MONOTONIC — its
                // derivative is `dL/dt = U(1 - f(z))`, i.e. the drop's horizontal velocity is
                // exactly `U·f(z)`. There is neither a random jump nor unbounded accumulation.
                float3 probe = WrapAroundCamera(seed.xyz * box + drift, cameraPos, box);

                // THE HEIGHT IS RELATIVE TO THE CAMERA'S TERRAIN, NOT THE DROP'S.
                //
                // Every drop used to sample the terrain beneath ITSELF. Right on a flat plain;
                // not on a steep mountain. Inside the 48 m box the terrain moves tens of metres
                // and of two drops side by side one came out "2 m above ground" and the other
                // "30 m above ground". With `profile` jumping between 0.3 and 1.0 the wind
                // responses jumped too and the rain had no COMMON DIRECTION.
                //
                // The symptom: "the long particles nearby fall to the left while the particles
                // just ahead fall to the right". Measured — forcing `profile` to 1.0 collected
                // the streaks into one direction, reverting scattered them again.
                //
                // The boundary layer rises WITH the terrain; it does not break from drop to
                // drop. One reference is valid for the whole box: the box is only 48 m, and at
                // that scale the profile has to be continuous.
                float groundRef = TerrainHeightAt(cameraPos.xz);

                float aboveGround = clamp(probe.y - groundRef,
                                          WIND_MIN_HEIGHT, WIND_REF_HEIGHT);

                float profile = log(aboveGround / WIND_Z0) / WIND_PROFILE_L;

                // `G(z_ref)` sabit: 24 − 24·(ln240 − 1)/ln240 = 4.3789
                float integral = WIND_LAG_TOP
                               - (aboveGround - aboveGround
                                  * (log(aboveGround / WIND_Z0) - 1.0) / WIND_PROFILE_L);

                // Horizontal wind direction and magnitude from the class vector; because the
                // vertical component is the terminal velocity, `.xz` is the wind itself.
                // THE MAGNITUDE IS REQUIRED, a unit direction is not enough: both the lag and
                // the inertia filter compute a RATIO. With a normalized vector snow reads as if
                // it were travelling at 1.4 m/s, the wind's share is lost and the flake never
                // sees the boundary layer.
                float3 classVelocity = _RainDirections[dropClass].xyz * _RainDirections[dropClass].w;
                float2 windFlat = classVelocity.xz;
                float windSpeed = length(windFlat);
                float2 windUnit = windSpeed > 1e-4 ? windFlat / windSpeed : float2(0.0, 0.0);

                float lag = (windSpeed / max(fallSpeed, 0.1)) * integral;
                drift.xz -= windUnit * lag;
                float3 worldPos = WrapAroundCamera(seed.xyz * box + drift, cameraPos, box);

                float variation = Hash(seed.xyz);


                // ---- THE PARTICLE'S RESPONSE TO AN EDDY: THE INERTIA FILTER ----
                //
                // A particle cannot follow every twist of the air. The drag equation is first
                // order, i.e. the particle is a low-pass filter: with a relaxation time
                // `tau = v_t/g` it responds to a forcing of frequency `omega` with an amplitude
                // ratio of `1/sqrt(1+(omega·tau)²)`. It AVERAGES the fast twists, it does not take them.
                //
                // Because the drop PASSES THROUGH the field the frequency it sees comes from the
                // spatial scale: `omega ~ k·|V| + omega_time`. The eddy scale had been tightened
                // fourfold a step earlier and the fine octave had risen to 27.5 rad/s ~ 4 Hz at
                // 13.85 m/s — the drop's tau is 0.21 s and cannot follow 4 Hz. Because the model
                // applied the full amplitude the drop fluttered like a leaf: "the rain drifts
                // through the air like snow".
                //
                // MEASURED (wind 13.7 m/s):
                //
                //   0.5 mm drop   tau 0.206  coarse 0.451  fine 0.174
                //   1.1 mm drop   tau 0.455  coarse 0.223  fine 0.080
                //   5.0 mm drop   tau 0.932  coarse 0.111  fine 0.039
                //   snowflake     tau 0.102  coarse 0.714  fine 0.336
                //
                // THIS IS WHAT SEPARATES RAIN FROM SNOW. A flake follows 1.6 times more than the
                // finest drop and 9 times more than a large one — snow sifts, a drop comes down.
                // Both used to take the same field at the same amplitude; the difference was
                // imitated with a hand-placed `lerp(1.5, 0.4, dropSize)` coefficient. THAT
                // COMPENSATION TERM WAS DELETED; the difference now comes from the physics.
                float3 meanVelocity = classVelocity;
                meanVelocity.xz *= profile;

                float3 dropVelocity = float3(meanVelocity.x, -fallSpeed, meanVelocity.z);
                float dropSpeed = length(dropVelocity);

                float tau = fallSpeed / 9.81;
                float wCoarse = TURB_COARSE_K * dropSpeed + TURB_COARSE_W;
                float wFine   = TURB_FINE_K   * dropSpeed + TURB_FINE_W;
                float gainCoarse = rsqrt(1.0 + wCoarse * tau * wCoarse * tau);
                float gainFine   = rsqrt(1.0 + wFine   * tau * wFine   * tau);

                // ---- EDDY SCALE SHRINKS WITH ELEVATION: ENERGY MOVES BETWEEN OCTAVES ----
                //
                // In the surface layer an eddy's SIZE grows with height (`l ~ kappa·z`) while
                // the velocity variance stays roughly constant. Near the ground a 10.5 m eddy
                // physically DOES NOT FIT — the ground cuts it.
                //
                // IT WAS FIRST CUT WITH `min(1, kappa·z/lambda)` AND ELIMINATED BY MEASUREMENT:
                // that dropped the deviation eighteenfold (3.6 cm -> 0.2 cm), the rain flattened
                // like a knife and snow stopped drifting on the ground entirely (40.8 cm ->
                // 3.5 cm, i.e. the ground blizzard disappeared). The error was that the formula
                // DESTROYS ENERGY: energy that does not fit is not lost, it moves to smaller scales.
                //
                // The right answer is to shift the share. Because the field's wavelength is
                // fixed we cannot change the scale; what we can do is let the coarse octave hold
                // only as much energy as fits and pass the rest to the fine octave. The total
                // velocity variance is preserved and the displacement falls — because a small
                // eddy's displacement is smaller by `1/k`.
                //
                // The base is 50/50: the field's existing octave weights (0.5 / 0.165) are
                // already in the ratio `k_fine/k_coarse = 3`, i.e. the velocity variance is
                // equal in the two octaves.
                //
                // MEASURED — the deviation/streak ratio in the extreme 10%:
                //   moderate air  1.55 -> 0.75
                //   storm         1.58 -> 0.42
                // Snow at 2 m goes 40.8 -> 15.0 cm (streak length 11 cm), i.e. it keeps drifting.
                float coarseShare = 0.5 * saturate(KARMAN * aboveGround / TURB_COARSE_LAMBDA);
                gainCoarse *= sqrt(coarseShare / 0.5);
                gainFine   *= sqrt((1.0 - coarseShare) / 0.5);

                // The eddy share is scaled BY THE WIND: in still air there is no drift and no
                // turbulence either. The scaling happens on the CPU (`RainTurbulenceCalm` 0.03 ->
                // `RainTurbulenceStorm` 0.25, through `felt`). Scaling once more here would
                // count it twice; the `response = response;` line that used to sit here did
                // nothing and was deleted.
                float response = _RainTurbulence;

                // Turbulence arrives in patches (intermittency): energy passes in clumps, it
                // does not spread evenly. The product of two waves at different frequencies
                // breaks the repeat pattern; the clumps flow with the wind. Drop and flake read
                // the same envelope — the same air.
                float3 gustPos = worldPos - _WindSweep;
                float patch = (sin(dot(gustPos.xz, float2(0.021, 0.017)) + _Time.y * 0.31) * 0.5 + 0.5)
                            * (sin(dot(gustPos.xz, float2(-0.013, 0.024)) + _Time.y * 0.23) * 0.5 + 0.5);
                response *= 0.5 + patch * 1.5;

                // PER-DROP DIRECTION DEVIATION — from the eddy field's own derivative.
                //
                // The class has to stay discrete (wind drift is integrated per class on the
                // CPU), so every drop in a class came down in exactly the same direction: there
                // were only eight streak angles on screen.
                //
                // THE DEVIATION IS NOT INVENTED, IT IS DERIVED FROM WHAT ALREADY EXISTS. The
                // drop's drawn position is `x + response·T(x,t)`; the real velocity of that
                // position is the TOTAL DERIVATIVE of the composite velocity, i.e.
                // `V + response·(dT/dt + (V·grad)T)`. The total derivative is taken with a single
                // extra sample: the field is resampled where the drop will be `dt` later. The
                // step is 0.28 m in a storm — well below the fine octave's 3.5 m wavelength
                // (kh = 0.45 rad, a finite difference error of 0.8%).
                //
                // The filter enters here too: a twist that is not followed cannot deflect the direction either.
                float3 turbHere = Turbulence(worldPos, _Time.y, gainCoarse, gainFine);

                const float dt = 0.02;   // saniye
                float3 turbNext = Turbulence(worldPos + meanVelocity * dt, _Time.y + dt,
                                             gainCoarse, gainFine);
                float3 velocityFluctuation = (turbNext - turbHere) * (response / dt);

                worldPos += turbHere * response;

                // Flutter: the vortex shedding behind a falling flake makes it sift from side
                // to side like a leaf. The phase and frequency are per flake; a drop does not flutter.
                //
                // MEASURED AGAINST THE FALL, not against the second. Snow descends at 1 m/s: a
                // 1-3 Hz oscillation means a full turn every 30-100 centimetres and read as a
                // narrow sawtooth on screen. At 0.2-0.5 Hz a turn spreads over 2-5 metres and
                // the flake looks like it is sifting.
                //
                // Two octaves, their ratio NOT an integer (2.7): a single sine is periodic and
                // the eye catches the repeat. Two curves that never close never draw the same
                // shape along the path. The second octave's amplitude is small — it adds
                // irregularity, not a separate vibration.
                float flutterFreq = 1.4 + 2.2 * variation;
                float px = _Time.y * flutterFreq + seed.x * 12.57;
                float pz = _Time.y * flutterFreq * 0.83 + seed.w * 12.57;

                float2 glide = float2(sin(px), cos(pz)) * 0.22
                             + float2(sin(px * 2.7 + seed.z * 6.28),
                                      cos(pz * 2.7 + seed.y * 6.28)) * 0.06;

                // Flutter belongs ONLY to a FALLING flake: it comes from the vortex shedding
                // behind a sifting crystal. A flake lifted from the ground is not falling and
                // does not flutter — it is carried by the wind. Applied, a 22 cm oscillation in
                // light wind made the flake zigzag in place: it advanced slower than it swung.

                // A real snowflake varies between 1 mm and 15 mm. A narrow distribution made
                // them all the same size and gave a marble-like feel.
                // For a drop the thickness comes from the same class as the speed: a large drop is both fast and thick
                // ---- THE RAIN QUAD IS PHYSICAL `[Garg 2006, §5]` ----
                //
                // "Based on the drop's distance from the camera and the angle that
                // drop's velocity vector makes with the camera's optical axis, we scale
                // the final streak texture to its projected size in the image."
                //
                // The projected size is not computed separately: the quad is built in WORLD
                // units and the perspective projection does the scaling itself. The length is
                // the distance travelled during the exposure, the width is the drop's diameter.
                //
                // IT USED TO BE `_RainSize` x `_RainStretch`, i.e. it came from a visual
                // setting. That setting belonged to a model that did not take the streak's
                // appearance from a database; the texture now carries a real drop's streak and
                // its scale has to be real too, otherwise the pattern's frequency comes out at
                // the wrong size on screen.
                float radius = dropRadius;

                float rainWidth = 2.0 * radius;
                float rainLength = dropSpeed * _StreakExposure;

                float sizeSpread = 0.4 + 1.4 * variation;
                float size = rainWidth;

                // Particles above the density threshold are culled with zero size.
                // Drops also thin out with snowiness: their count should drop through the transition too.
                // A strict less-than here as well: no particle should hang in the air at zero precipitation
                float densityLimit = _Density;
                size *= 1.0 - step(densityLimit, seed.w);


                float3 viewDirection = normalize(cameraPos - worldPos);
                float3 cameraRight = normalize(UNITY_MATRIX_I_V._m00_m10_m20);
                float3 cameraUp = normalize(UNITY_MATRIX_I_V._m01_m11_m21);

                // A drop stretches along the composite velocity + turbulence fluctuation
                // (derived above); a flake turns to the camera and reads no direction.
                float3 rainAxis = normalize(dropVelocity + velocityFluctuation);
                float3 fallAxis = normalize(rainAxis);
                float3 streakRight = normalize(cross(fallAxis, viewDirection));

                float3 right = streakRight;
                float3 up = fallAxis;

                // The stretch is no longer a free setting: the length/width ratio is the ratio
                // of the distance the drop travels during the exposure to its diameter. A large
                // fast-falling drop leaves a longer streak on its own.
                float stretch = rainLength / max(rainWidth, 1e-6);

                // A quad thinner than a pixel is either drawn as a single pixel or skipped
                // entirely by the rasterizer; the thickness difference disappears before it
                // reaches the screen and the particles boil as they enter and leave the pixel
                // grid. Pinning the width to a floor and taking the carried light out of the
                // alpha solves both: a thin one stays faint.
                // ---- STREAK DATABASE INDICES (rain only) ----
                //
                // `osc` is RANDOM per drop. The paper, `§5`: "Each drop is also randomly
                // assigned oscillation parameters Osc from the set of parameters used
                // to create our streak database." Which index corresponds to which amplitude
                // pair is written neither in the paper nor in the archive (`rain-spec.md`
                // §11.2-7); a random choice does not require it.
                float oscIndex = min(floor(Hash(seed.zyw) * STREAK_OSC_COUNT),
                                     STREAK_OSC_COUNT - 1.0);

                // `theta_v` is the angle between the camera's view direction and the drop's
                // FALL direction. The database folder holds the deviation from vertical:
                // `dcam = |90° - theta_v|`. Measured: the streak length ratio is `cos(dcam)`
                // (paper footnote 10 — "the lengths of the streaks for theta_v != 90° are
                // smaller since the viewing direction is not orthogonal to the fall direction").
                float thetaV = degrees(acos(clamp(dot(viewDirection, fallAxis), -1.0, 1.0)));
                float dcamPos = clamp(abs(90.0 - thetaV) / 20.0, 0.0,
                                      STREAK_DCAM_COUNT - 1.0);

                // ---- DROP SIZE: CROP / MERGE `[Garg 2006, §5]` ----
                //
                // By Equation 2 the drop size only changes the oscillation FREQUENCY, not the
                // pattern. So drops of different sizes go through the same pattern, but with a
                // different period: `omega_n ~ r0^{-3/2}` -> `T_new = 2pi/omega_2 ~ r0^{3/2}`.
                //
                // Within the exposure only `T_exp/T_new` of the texture is visible. Below 1 the
                // texture is CROPPED, above it copies are MERGED and then cropped — paper
                // footnote 13: "For long exposure times, the streak texture repeats itself with
                // the time period of oscillation."
                float newPeriod = _StreakDbPeriod * pow(radius / 0.0016, 1.5);
                float vScale = _StreakExposure / max(newPeriod, 1e-6);

                float centerDistance = length(worldPos - cameraPos);
                float pixelWidth = size * PixelsPerRadian() / max(centerDistance, 0.01);
                float widen = max(1.0, MinPixelWidth / max(pixelWidth, 1e-4));

                float2 offset = IN.corner - 0.5;
                worldPos += right * offset.x * size * widen + up * offset.y * size * stretch;

                float camDistance = length(worldPos - cameraPos);
                // THE FADE IS PRESSED AGAINST THE BOX SURFACE.
                //
                // The fade's only job is to hide the pop at the wrap boundary: the particles
                // wrap in a cube around the camera and at its surface (0.5·box) the alpha has to
                // be zero, otherwise a drop appears and disappears abruptly.
                //
                float fade = 1.0 - smoothstep(box.x * 0.45, box.x * 0.5, camDistance);

                // As a crystal's flat faces turn they catch the light and release it. The
                // sparkle of falling snow comes from here, not from the silhouette.

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.corner = IN.corner;
                OUT.streak = float3(oscIndex, floor(dcamPos), frac(dcamPos));
                // Inclination from the horizontal: the drop's REAL trajectory angle, including
                // the eddy deviation. In rain `fallAxis` is the unit of `dropVelocity + velocityFluctuation`.
                OUT.streakCrop = float2(vScale, vScale > 1.0 ? 1.0 : 0.0);

                // THE SKY BEHIND THE DROP. A drop does not produce light, it refracts what
                // comes from behind; the ambient channel's source is therefore the sky's
                // radiance IN THAT DIRECTION.
                //
                // `_HeightFogColor` CANNOT BE USED: that is the fog's color, dimmer than the
                // sky. Measured — with it the radiance stayed in the 0.08-0.32 band and the
                // drops fell darker than the sky, reading as BLACK BLOBS. The function that
                // draws the sky is `AirColor` (`Sky.shader` calls it too), the single source.
                // TONE AND BRIGHTNESS ARE SEPARATE.
                //
                // A drop does not select a wavelength; its color comes from the light falling on
                // it. But in daylight it cannot take the color of a single direction: what
                // illuminates it is the WHOLE sky dome and the result is close to neutral.
                // Passing raw `AirColor` through made the drops read blue — the same mistake was
                // made with blowing snow in this project and its rule was written into
                // `HeightFog.hlsl` ("carrying the horizon blue turned the slope fluorescent blue").
                //
                // The split comes from the sun's elevation: low down the light is directional and
                // colored, and rain at dawn should be red; higher up it is diffuse and neutral.
                //
                // LUMINANCE IS PRESERVED — only the tone is pulled toward neutral. Brightness is
                // a separately measured quantity (`SourceScale`) and is not touched.
                float3 sky = AirColor(-viewDirection);
                float skyLuma = dot(sky, float3(0.2126, 0.7152, 0.0722));
                float3 skyHue = sky / max(1e-4, skyLuma);
                float lowSun = 1.0 - smoothstep(0.02, 0.28, _SunHeight);
                OUT.airColor = lerp(1.0, skyHue, lowSun * 0.9) * skyLuma;

                OUT.color = _RainColor.rgb;

                // ---- TRANSPARENCY `[Garg 2006, §5]`, `[Garg & Nayar 2005]` ----
                //
                //   I_r = (1-a)·I_b + a·I_streak,     a = 2r0 / (v·T_exp)
                //
                // A drop travels during the exposure; the time it spends on one pixel is the
                // ratio of its diameter to the distance travelled. At a short exposure the
                // streak is MORE OPAQUE — the paper's own emphasis.
                //
                // The speed is the PHYSICAL terminal velocity, not the particle's visual fall
                // speed (the reasoning is at the top of `TerminalVelocity`). The radius and the
                // speed were already computed while building the quad.
                // THE REPRESENTATION SHARE ENTERS THE COVERAGE, NOT THE GEOMETRY.
                //
                // For a while it multiplied the quad's width and length. It fell out on paper:
                // the streak length became 40-183 cm, while a real drop travels 3.4-15 cm in
                // 1/60 s. A 1.8 metre "drop" is a rod, not rain.
                //
                // If a particle represents N drops it should not be N times LARGER but N times
                // MORE OPAQUE: the coverage of N overlapping drops is `1 - (1-a)^N`. The size
                // stays physical and the streak becomes visible. Alpha 0.02 -> 0.21.
                // The speed is `dropSpeed`, NOT the terminal velocity: `[Garg 2006]`'s alpha is
                // what fraction of the path swept during the exposure the drop covers. The
                // length comes from the same path; if the two read different speeds the streak
                // stretches while its transparency stays fixed, i.e. energy is created from nothing.
                float singleDrop = saturate(2.0 * radius
                                            / max(dropSpeed * _StreakExposure, 1e-6));
                // THE REPRESENTATION SHARE DERIVES FROM POSITION, not from the box.
                //
                // Two particles at the same point must represent the same number of real drops
                // regardless of which box they came from; tied to the box, the same place would
                // produce two different opacities.
                //
                // The inner box's share enters on the SAME curve as ITS OWN fade: inner particles
                // start fading at 0.45·12 = 5.4 m and finish at 6 m, so beyond that they do not
                // contribute to the density either. Were the two to diverge the opacity would
                // jump at the boundary.
                float nearShare = 1.0 - smoothstep(_NearBoxSize.x * 0.45,
                                                   _NearBoxSize.x * 0.5, centerDistance);
                float localDensity = _RainDensity.x + _RainDensity.y * nearShare;
                float representation = 1000.0 / max(localDensity, 1e-4);

                float rainAlpha = 1.0 - pow(1.0 - singleDrop, representation);

                // Per-flake opacity: with all of them at the same density the depth was lost.
                // The ranges are narrow; because two multipliers stack, wide bands made the snow
                // transparent — keep the variety, lose the feebleness.

                // A turning face flares when it comes to the light and goes out when it turns edge-on

                // The widening was artificial; without a drop in alpha distant particles look
                // brighter than they are. Full light conservation (divide by widen) pushes thin
                // drops into invisibility — the square root is the balance that carries the
                // thickness difference while keeping the particle alive
                // HALF CONSERVATION — REVERTED BY MEASUREMENT.
                //
                // For a while full conservation (`/widen`) was written, with the reasoning "let
                // the thickness difference pass into brightness". Measured, and the reasoning
                // collapsed: the thickness difference ALREADY cannot reach the screen, the drops
                // are 0.1-1.0 pixels wide and all of them settle on the 1.2 pixel raster floor.
                // Full conservation gained nothing and only divided every drop by `widen` — the
                // alpha of a typical drop at 5 m fell from 0.45 to 0.17 and the user said "they
                // are far too transparent".
                //
                // The gradation is also BETTER under half conservation: because `widen` is
                // inversely proportional to the drop size the divisor varies with the drop too. A
                // thin drop gets /3.46, a thick one /1.1 — the final alpha ranges from 0.17 to
                // 0.79, a factor of 4.6. Under full conservation that factor was 2.4.
                // THE EXPONENT IS DELIBERATELY 0.35 — full conservation would be 0.5.
                //
                // The binding constraint is not the representation share but the `widen`
                // division: the median `widen` is 11, so under full conservation the alpha is
                // divided by 3.3 and the drops stay transparent. An exponent sweep (box 32 m,
                // intensity 0.4, 20 000 samples):
                //
                //   exp 0.50 -> median alpha 0.262, thin/thick difference 2.49x
                //   exp 0.35 -> median alpha 0.377, difference 1.94x
                //   exp 0.25 -> median alpha 0.479, difference 1.64x
                //   exp 0.00 -> median alpha 0.873, difference 1.09x   (the difference disappears)
                //
                // 0.35: the alpha rises 1.44x and three quarters of the gradation survives.
                //
                // The paper makes this deviation as well. `[Tatarchuk 2006, §3.6.1]`: "Realistic
                // rain is very faint in bright regions... While this may be physically
                // accurate, it doesn't create a perception of strong rainfall."
                float rainThin = pow(widen, -0.35);
                OUT.alpha = rainAlpha * fade * rainThin;
                return OUT;
            }

            /// The weights of the four `(v,h)` corners. The order matches the slice order in
            /// the baker: (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
            float4 StreakCornerWeights()
            {
                float vT = _StreakCellBlend.x, hT = _StreakCellBlend.y;
                float4 w = float4((1.0 - vT) * (1.0 - hT), (1.0 - vT) * hT,
                                  vT * (1.0 - hT), vT * hT);

                // A MISSING COMBINATION: not in the database (at extreme vertical angles the
                // streak degenerates, `rain-spec.md` §5.4.5 — measured, only at the `v = ±90`
                // poles and even there only outside `h170`). Its weight is zeroed and the rest
                // are renormalized, otherwise the streak goes out in that cell.
                return w * _StreakCornerPresent;
            }

            /// The blend of four corners at a single `dcam` level.
            float SampleStreakAtDcam(float2 uv, float osc, int dcam, float4 weights)
            {
                float sum = 0.0, total = 0.0;

                // The array was filled according to the longest `dcam`; the tail of the shorter ones is empty.
                float2 st = float2(uv.x, uv.y * _StreakDcamFraction[dcam]);

                [unroll]
                for (int c = 0; c < 4; c++)
                {
                    float w = weights[c];
                    if (w <= 0.0) continue;
                    float slice = (c * STREAK_DCAM_COUNT + dcam) * STREAK_OSC_COUNT + osc;
                    sum += w * SAMPLE_TEXTURE2D_ARRAY(_StreakPoint, sampler_StreakPoint,
                                                      st, slice).r;
                    total += w;
                }

                return total > 0.0 ? sum / total : 0.0;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.corner - 0.5;

                // ---- THE RAIN STREAK: FROM THE DATABASE `[Garg 2006, §5]` ----
                //
                // The procedural line WAS DELETED. The paper's own measurement (`§3`): "a
                // spherical drop model is simply not adequate when rendering close-up rain
                // streaks" — a bar of constant brightness cannot produce its speckles, smeared
                // highlights and curved contours.
                //
                // Eight neighbours, two per angular dimension: `(theta_l, phi_l)` four corners x
                // `theta_v` two neighbours. The paper calls this "bilinear" but it is two
                // neighbours in three dimensions, i.e. trilinear.
                float streakU = _StreakMirror > 0.5 ? 1.0 - IN.corner.x : IN.corner.x;

                // Crop / merge: above a ratio of 1 the texture repeats itself.
                float streakV = IN.corner.y * IN.streakCrop.x;
                streakV = IN.streakCrop.y > 0.5 ? frac(streakV) : streakV;
                float2 streakUV = float2(streakU, streakV);

                float4 cornerWeights = StreakCornerWeights();
                int dcamLow = (int)IN.streak.y;
                int dcamHigh = min(dcamLow + 1, STREAK_DCAM_COUNT - 1);

                float pointStreak = lerp(
                    SampleStreakAtDcam(streakUV, IN.streak.x, dcamLow, cornerWeights),
                    SampleStreakAtDcam(streakUV, IN.streak.x, dcamHigh, cornerWeights),
                    IN.streak.z);

                // AMBIENT IS SAMPLED SEPARATELY and added (`§5`: "we scale each of these
                // textures individually with the corresponding source intensity and
                // color. These scaled textures are added"). Because it has no light direction it
                // is indexed only by `(theta_v, Osc)`.
                float ambientStreak = lerp(
                    SAMPLE_TEXTURE2D_ARRAY(_StreakAmbient, sampler_StreakAmbient,
                        float2(streakU, streakV * _StreakDcamFraction[dcamLow]),
                        dcamLow * STREAK_OSC_COUNT + IN.streak.x).r,
                    SAMPLE_TEXTURE2D_ARRAY(_StreakAmbient, sampler_StreakAmbient,
                        float2(streakU, streakV * _StreakDcamFraction[dcamHigh]),
                        dcamHigh * STREAK_OSC_COUNT + IN.streak.x).r,
                    IN.streak.z);

                // THE CROP ENDS ARE BLURRED (`§5`: "The streaks ends are then blurred
                // to smooth out the sharp edges due to cropping"). The radius is not in the paper
                // (`rain-spec.md` §11.2-5); a band at the texture's own resolution was chosen —
                // two texels, 1/262 at `size16`.
                float endFade = smoothstep(0.0, 0.008, IN.corner.y)
                              * smoothstep(0.0, 0.008, 1.0 - IN.corner.y);

                // Each source is scaled by ITS OWN color and summed (end of `§5`).
                // THE HALO MASK IS NOT APPLIED — the reason is geometric, not convenience.
                // `§5`: "we use a mask whose intensity at a pixel i is equal to 1/d_i²,
                // where d_i is the distance in 3D of the falling drop from the light
                // source". The sun is at infinity; `d_i` is the same for every drop, so the mask
                // reduces to a constant factor and is already carried in the source's intensity.
                // Both the halo and the light cone are the work of a source at a FINITE distance.
                // Add a lamp, a torch or lightning to the scene and this mask will be needed —
                // `DECISIONS.md`.
                //
                // The anisotropic mask is missing for the same reason: the sun is isotropic.
                float3 rainRadiance = (pointStreak * _StreakSunRadiance
                                     + ambientStreak * IN.airColor)
                                    * _StreakSourceScale;

                // THE AREA THE DROP COVERS. The alpha cannot be constant over the WHOLE quad.
                //
                // In the paper `a` is constant because the texture IS the drop's image — the
                // quad and the streak are the same thing. Here the quad is a rectangle enlarged
                // by the representation share; the drop does not cover all of it. Left at a
                // constant alpha every drop printed a SOLID RECTANGLE instead of a thin streak,
                // and being dimmer than the sky it read as a black blob (reported by the user).
                //
                // The coverage comes from the texture itself: the ambient channel carries the
                // drop's image streak across its full width, the directional channel is the
                // bright filament on top of it. The larger of the two says whether the drop
                // covers that pixel.
                float coverage = saturate(max(ambientStreak, pointStreak));

                float rainMask = coverage * endFade;

                return half4(rainRadiance, IN.alpha * rainMask);
            }
            ENDHLSL
        }
    }
}
