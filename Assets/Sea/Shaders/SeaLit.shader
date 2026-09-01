// ROLE: drawing the sea surface. Shallow water transform (vertex) + optics
// (fragment).
// CALLED BY: SeaSurface (as its material).

Shader "ToTheSummit/SeaLit"
{
    Properties
    {
        // Empty — every value is a global uniform. No per-material property,
        // so the CBUFFER is empty too, which keeps SRP Batcher compatibility
        // (spec 15.2).
    }

    SubShader
    {
        // DRAWN OPAQUE. The sense of transparency comes from refraction and
        // absorption, not from alpha. Alpha blending ghosts under TAA and
        // creates sorting problems (spec 12.6, 18).
        Tags
        {
            "RenderType" = "Opaque"
            // THE SEA STAYS IN THE TRANSPARENT QUEUE -- IT READS THE SCENE.
            //
            // `Geometry+450` was tried so the sea would reach `_CameraDepthTexture` and the
            // clouds could occlude against it. It works for the clouds and breaks the water:
            // the depth and colour copies happen AFTER the opaque queue, so from inside it the
            // sea reads a texture that is not there yet. Measured with a thickness probe at
            // the waterline -- transparent queue: green near the shore, blue offshore, a real
            // depth gradient; opaque queue: one flat value across the whole band. The water
            // column, the refraction and the shallow-water colour all hang off that read.
            //
            // The cloud order is solved on the CLOUD side instead: its composite runs after
            // the transparent queue.
            "Queue" = "Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite On
        Cull Back
        Blend Off

        Pass
        {
            Name "SeaForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // THE CLOUD SHADOW ARRIVES THROUGH THIS KEYWORD. The cloud system writes
            // its shadow into the main light's cookie and URP applies it wherever the
            // keyword is on. Without it the sky closed over and the sea kept drawing a
            // full sun path — the atmosphere rule this project sets for itself
            // ("the weather and the light cannot contradict") forbids exactly that.
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // THE QUALITY KEYWORD MUST BE A `multi_compile`.
            //
            // A keyword enabled with `Shader.EnableKeyword` but not declared
            // here means the variant is NEVER compiled and `#if defined(...)`
            // silently stays false. In the snow system three detail layers
            // never ran for exactly this reason.
            #pragma multi_compile _SEA_QUALITY_LOW _SEA_QUALITY_MEDIUM _SEA_QUALITY_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            // THE SAME AIR AS EVERY OTHER SURFACE. Unity's own fog was called here
            // (`ComputeFogFactor` / `MixFog`) and with `m_Fog: 0` in the scene that
            // call was DEAD: in a storm the mountain vanished at 300 m while the sea
            // stayed sharp to the horizon. Unity's fog is height independent anyway,
            // which is why the project never uses it. The bike hit the same wall once
            // (`BikeSurface.shader`) and this is the same fix.
            #include "../../Shaders/HeightFog.hlsl"

            #include "SeaCommon.hlsl"

            // Optics globals
            float3 _SeaExtinctionRGB;
            float4 _SeaUpwellingColor;
            float  _SeaRefractionStrength;
            float  _SeaRoughnessCalm;
            float  _SeaRoughnessRough;

            float  _SeaCloudCover01;
            float  _SeaSunElevation01;
            float  _SeaPrecipIntensity01;

            float  _SeaRunupMaxDepth;
            float  _SeaShoreFoamPhase;
            float  _SeaSwashUprush;
            float  _SeaShoreFoamDepth;
            float4 _SeaFoamColor;
            float  _SeaFoamRoughness;
            float  _SeaFoamTiling;
            float  _SeaFoamBreakupTiling;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            Varyings SeaVertex(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y = _SeaLevelY;

                // THE WAVE FIELD AND THE SHALLOW WATER TRANSFORM LIVE IN
                // `SeaCommon`.
                //
                // The forward pass and the depth pass call the SAME function;
                // written separately the two buffers would see different
                // surfaces.
                SeaSurfacePoint surf = SeaDeform(posWS);
                surf.posWS = SeaFlattenFar(surf.posWS, posWS, _WorldSpaceCameraPos.xz);

                // THE CURVATURE BENDS WHAT IS DRAWN, NOT WHAT IS SHADED.
                //
                // `positionWS` stays flat because the fragment reads crest
                // height out of it (`positionWS.y - _SeaLevelY`); dropping it
                // would make every distant crest read as a trough and kill the
                // foam. Only what goes to clip space is curved.
                float3 curvedWS = surf.posWS;
                curvedWS.y -= SeaCurvatureDrop(curvedWS.xz, _WorldSpaceCameraPos.xz);

                OUT.positionWS = surf.posWS;
                OUT.positionCS = TransformWorldToHClip(curvedWS);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);

                return OUT;
            }

            /// FULL FRESNEL — NOT SCHLICK.
            ///
            /// Schlick deviates noticeably at grazing angles and that is
            /// exactly where a sea gets its character (spec 12.1, Tessendorf
            /// 6.2 Figure 24). The two-branch form comes straight from
            /// Tessendorf's sample shader.
            float SeaFresnel(float3 N, float3 V)
            {
                float cosThetaI = abs(dot(V, N));
                float thetaI    = acos(saturate(cosThetaI));
                float sinThetaT = sin(thetaI) / SEA_WATER_IOR;

                if (sinThetaT >= 1.0) return 1.0;      // total internal reflection

                float thetaT = asin(sinThetaT);

                if (thetaI < 1e-4)
                {
                    float r = (SEA_WATER_IOR - 1.0) / (SEA_WATER_IOR + 1.0);
                    return r * r;
                }

                float fs = sin(thetaT - thetaI) / sin(thetaT + thetaI);
                float ts = tan(thetaT - thetaI) / tan(thetaT + thetaI);

                return 0.5 * (fs * fs + ts * ts);
            }

            /// WATER VOLUME ABSORPTION.
            ///
            /// Red decays fastest, blue slowest — the reason water looks
            /// blue. [SOURCE: Tessendorf 2004 7.1]
            float3 SeaVolumeColor(float pathLength)
            {
                return exp(-_SeaExtinctionRGB * pathLength);
            }

            half4 SeaFragment(Varyings IN) : SV_Target
            {
                // ANYTHING OVER LAND IS DISCARDED — every pixel reads its own
                // depth, so the shoreline does not snap to quad boundaries.
                float depth = SeaSampleDepth(IN.positionWS.xz);

                // THE WATERLINE IS NOT A DRAWN CURVE.
                //
                // `clip(depth)` cuts on the depth field alone, and that field is a
                // smooth interpolation of a 7.32 m heightmap: the cut came out as a
                // clean geometric line, while the foam right next to it — which carries
                // the same noise the swash does — reads soft. The edge is displaced by
                // the SAME noise, so the waterline breaks into tongues and hollows
                // instead of ending on a curve.
                //
                // The amplitude is bounded by the noise's own feature size: at
                // `_SeaFoamBreakupTiling` the features are about 2.9 m across, and
                // `SEA_SHORE_EDGE_NOISE` on the measured 5% shore slope moves the line
                // by 1.2 m. A bend larger than the feature that makes it would smear.
                float edgeNoise = SeaFoamNoise(IN.positionWS.xz * _SeaFoamBreakupTiling) - 0.5;
                clip(depth + edgeNoise * SEA_SHORE_EDGE_NOISE);

                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float  dist = length(_WorldSpaceCameraPos - IN.positionWS);

                // --- NORMAL, FROM THE FFT SLOPE TEXTURE (spec 10.5) ---
                //
                // NO central difference: the slope already comes from the FFT
                // and that is more accurate (spec 6.7).
                // EACH TIER FADES BY ITS OWN WAVELENGTH, NOT BY DISTANCE.
                //
                // The old rule zeroed every tier at 520 m and left the rest of the
                // water -- 98% of the mesh -- a flat mirror. Measured: a 2 m wave
                // spans 7.1 pixels at 520 m and does not shrink to one pixel until
                // 4 km. Distance was the wrong quantity; what aliases is a wave
                // shorter than a pixel, so the pixel's own footprint decides it and
                // the long swell now runs to the horizon.
                // THE FOOTPRINT IS A LENGTH, NOT THE LARGER OF TWO AXES.
                //
                // `max(fwidth(x), fwidth(z))` was tried first and drew itself on
                // the water: its iso-contour is a SQUARE, so a square outline sat
                // around the camera and travelled with it, and along the view axis
                // -- where the winning axis swaps -- it left a straight crease.
                // The real footprint is how far the world moves per pixel step.
                float2 dx = ddx(IN.positionWS.xz);
                float2 dy = ddy(IN.positionWS.xz);
                float pixelSize = max(length(dx), length(dy));
                float2 slopeSum = SeaSampleSlope(IN.positionWS.xz, pixelSize);

                float3 N = normalize(float3(-slopeSum.x, 1.0, -slopeSum.y));

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // --- WATER THICKNESS (spec 12.3) ---
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(sceneEyeDepth - IN.screenPos.w, 0.0);

                // --- REFRACTION ---
                //
                // DISABLED ON LOW, FLAT COLOR (spec 15.3). Refraction reads
                // the opaque texture and the depth texture; two full-screen
                // samples.
                float3 refracted = _SeaUpwellingColor.rgb;

            #if !defined(_SEA_QUALITY_LOW)
                {
                    float2 refrOffset = N.xz * _SeaRefractionStrength / max(dist, 1.0);
                    float2 refrUV = screenUV + refrOffset;

                    // OFFSET GUARD. If the offset sample is shallower than the
                    // surface, cancel the offset; otherwise water at the shore
                    // "pulls in" the color of the rock in front of it
                    // (spec 12.3).
                    float offsetDepth = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                    if (offsetDepth < IN.screenPos.w) refrUV = screenUV;

                    refracted = SampleSceneColor(refrUV);
                }
            #endif

                // THE MAIN LIGHT CARRIES ITS ATTENUATION. `GetMainLight()` with no
                // argument returns `shadowAttenuation = 1` and cannot sample a cookie:
                // water standing in a mountain's shadow, or under a cloud, still drew a
                // full sun path.
                //
                // ONE MULTIPLY, THE WHOLE CHAIN FOLLOWS. The glitter, the water's own
                // colour (`waterLight`) and the foam all read `mainLight.color`, so the
                // cookie has to be applied here and only here.
                float4 seaShadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(seaShadowCoord);

            #ifdef _LIGHT_COOKIES
                mainLight.color *= SampleMainLightCookie(IN.positionWS);
            #endif

                float3 L = mainLight.direction;

                // --- SURFACE ROUGHNESS ---
                //
                // It is read before the reflection, because BOTH the sun lobe and
                // the environment lookup want it. Read separately the sea would
                // have a sharp sun and a blurred sky, or the reverse.
                float perceptualRoughness =
                    lerp(_SeaRoughnessCalm, _SeaRoughnessRough,
                         saturate(length(_SeaWindWS) / 20.0));

                // Rain roughens the surface (spec 13.5).
                perceptualRoughness = lerp(perceptualRoughness, 0.22,
                                           _SeaPrecipIntensity01 * 0.7);

                // DISTANT GLITTER IS DIFFUSE. Far waves live below what the
                // camera can resolve, so the glitter spreads out
                // [SOURCE: Tessendorf 2004 6 introduction].
                perceptualRoughness = lerp(perceptualRoughness, 0.35,
                                           saturate((dist - 200.0) / 1500.0));

                // --- THE WATER BODY'S OWN COLOUR ---
                //
                // WHAT COMES OUT OF THE WATER IS LIGHT THAT WENT IN. The upwelling
                // colour used to be written as a constant, so the sea stayed the
                // same turquoise at night and under a storm — a colour with no
                // light behind it. It is now treated as an albedo: the sky
                // irradiance and the sun lay on top of it.
                float3 waterLight = SampleSH(float3(0, 1, 0))
                                  + mainLight.color * saturate(L.y);
                float3 upwelling = _SeaUpwellingColor.rgb * waterLight;

                float3 volume = SeaVolumeColor(thickness);
                float3 belowSurface = lerp(upwelling, refracted * volume, volume);

                // --- SKY REFLECTION (spec 12.4) ---
                //
                // THE REAL SKY IS REFLECTED, NOT AN INVENTED COLOUR. Two constants
                // used to be read (`_SeaSkyColor`, `_SeaHorizonColor`) and they
                // came from hand-entered fields on `SeaEnvironmentBridge`: under a
                // grey storm sky the sea reflected a blue that did not exist, and
                // at grazing angles — where Fresnel goes to 1 and the surface is
                // ALL reflection — the horizon stayed turquoise instead of taking
                // the sky's colour. That is what made the surface read as plastic.
                //
                // The scene already bakes the environment every frame
                // (`SkyAmbientBaker` -> `DynamicGI.UpdateEnvironment()`), so the
                // reflection probe is the sky that is really drawn. One source.
                float3 R = reflect(-V, N);

                // THE PROBE IS NEVER READ BELOW THE HORIZON.
                //
                // Under the horizon the probe holds the LAND -- a brown that has no business
                // on water. The band further down keeps below-horizon rays on the water's own
                // upwelling, but the band is now soft, and a soft band means a ray a hair
                // under the horizon still takes a share of whatever the probe returns there.
                // Measured: hard-edged brown patches came back along the surf line, where the
                // normal tilts hardest. The ray that is looked up is clamped to the horizon,
                // so the worst the probe can ever answer is the sky's own lowest colour.
                float3 rLookup = normalize(float3(R.x, max(R.y, 0.0), R.z));
                float3 skyRefl = GlossyEnvironmentReflection(rLookup, IN.positionWS,
                                                             perceptualRoughness,
                                                             1.0, screenUV);

                // THE PROBE CARRIES THE SKY BUT NOT THE CLOUDS. The volumetric
                // clouds are a render feature drawn after the skybox, so they never
                // enter the baked cube and an overcast sky still arrives here as
                // blue. Coverage pulls the reflection towards a grey, dimmer dome —
                // which is what a cloud layer physically is. The term goes away the
                // day the clouds reach a probe (`DECISIONS.md`).
                float3 overcast = dot(skyRefl, float3(0.299, 0.587, 0.114)) * 0.85;
                skyRefl = lerp(skyRefl, overcast, _SeaCloudCover01 * 0.85);

                // A REFLECTED RAY THAT POINTS DOWN NEVER REACHES THE SKY.
                //
                // `GlossyEnvironmentReflection` answers for every direction, including the
                // ones below the horizon, and returns whatever the environment holds down
                // there. On a wave face tilted towards the viewer the reflected ray dips
                // under the horizon and the sea came back carrying DARK BROWN BLOTCHES —
                // hard-edged patches that slid over the water and read as dirt on glass.
                //
                // MEASURED: painting `skyRefl` magenta put the magenta exactly on those
                // blotches, in the same shapes and the same places. It was the reflection,
                // not the refraction (disabling that left them) and not the sea bed.
                //
                // Physically the ray hits WATER, so what comes back is the water's own
                // upwelling — the same quantity the surface already computes for the volume
                // below it. The band is narrow (0 to 0.06 in R.y, about 3.5 degrees) because
                // a real horizon is sharp; wider than that and the whole sea flattens.
                // THE BAND IS AS WIDE AS THE PIXEL IS UNCERTAIN.
                //
                // 0.06 is the physical width -- a real horizon is sharp, and wider than
                // about 3.5 degrees the whole sea flattens. But far from the camera one
                // pixel covers tens of metres of water and `R.y` sweeps that whole range
                // inside it, so a step at a fixed threshold flips neighbouring pixels
                // between the dark upwelling and the bright sky. Measured with the water
                // FROZEN and the camera still: removing the band dropped the band under
                // the horizon from 2.63 to 1.94 luma of change per frame, a quarter of
                // the shimmer, and the sky in the same frame changed 0.64.
                //
                // `fwidth` is not a tuning knob: it IS how much `R.y` moves across this
                // pixel. Near the camera it is tiny and the band keeps its 0.06; far away
                // it opens to cover the spread the pixel actually holds.
                // IT OPENS BOTH WAYS. Growing only the upper edge moves the band's MIDDLE
                // up, and the far sea went 26% darker because more of it fell on the dark
                // side of a threshold that had quietly shifted. The middle stays at 0.03;
                // the pixel's own spread is added to each side.
                float horizonSlack = 0.5 * fwidth(R.y);
                skyRefl = lerp(upwelling, skyRefl,
                               smoothstep(-horizonSlack, 0.06 + horizonSlack, R.y));

                // --- SUN GLITTER (spec 12.5) ---
                //
                // GGX, NOT A BLINN LOBE. `pow(dot(N,H), 2/r^2)` is a shape with no
                // grazing tail: the glitter path stayed the same width whatever the
                // angle, and a surface whose highlight does not stretch reads as
                // plastic. GGX's long tail is exactly the sun path that stretches
                // out on a real sea.
                float3 H = normalize(V + L);
                float a  = perceptualRoughness * perceptualRoughness;
                float a2 = a * a;

                float NoH = saturate(dot(N, H));
                float NoV = saturate(dot(N, V));
                float NoL = saturate(dot(N, L));

                float d = (NoH * a2 - NoH) * NoH + 1.0;
                float D = a2 / (PI * d * d + 1e-7);
                float Vis = 0.5 / max(1e-4, lerp(2.0 * NoL * NoV, NoL + NoV, a));

                float spec = D * Vis * NoL;

                // NO GLITTER AT NIGHT (spec 12.5, 18 pitfall).
                spec *= saturate(_SeaSunElevation01 * 20.0);

                float3 glitter = mainLight.color * spec;

                // --- COMBINE (spec 12.6) ---
                //
                // THE SUN LOBE IS ALSO WEIGHTED BY FRESNEL. It used to be added
                // raw, so looking straight down — where the surface reflects almost
                // nothing — the sun still burned on it.
                float F = SeaFresnel(N, V);
                float3 color = lerp(belowSurface, skyRefl, F) + glitter * F;

                // --- LIGHT THROUGH THE WAVE (subsurface) ---
                //
                // A backlit crest glows. That glow was missing entirely: every path in
                // this shader returned light that came from ABOVE the surface (the sky,
                // the sun's lobe) or from BELOW it (the bottom, the upwelling constant).
                // Nothing carried light that went INTO the water and came back out of
                // the same face — which is the one thing that makes a wave look like
                // water rather than like a moving surface.
                //
                // THE CREST IS THE THIN PART. Where the surface stands above the still
                // level the light has less water to cross, so more of it gets through.
                // Sea of Thieves reads this from the choppiness offset; the elevation
                // above still water says the same thing here and is already to hand.
                // [SOURCE: Ang 2018, The Technical Art of Sea of Thieves]
                //
                // THE TINT IS NOT A NEW COLOUR. It is sunlight attenuated over the path
                // it took through the crest, using the same extinction the depth colour
                // uses.
                //
                // THE PATH IS A SLANT, NOT THE HEIGHT. Light crosses the crest along
                // the sun's own direction, so a low sun travels much further through
                // the same water: `h / sin(elevation)`. That is why a backlit wave at
                // sunset is deep green and the same wave at noon is almost white —
                // measured here, a 0.6 m crest passes (0.84, 0.95, 0.97) with the sun
                // overhead and (0.30, 0.73, 0.82) with it near the horizon.
                //
                // IT ENTERS THROUGH THE SURFACE, so Fresnel's TRANSMITTED share carries
                // it: at grazing angles, where the surface is all mirror, there is no
                // glow — which is what a real sea does.
                float crestHeight = max(IN.positionWS.y - _SeaLevelY, 0.0);
                float slant = crestHeight / max(L.y, 0.15);
                float3 through = exp(-_SeaExtinctionRGB * slant);

                float crestMask = saturate(crestHeight
                                           / max(0.5 * _SeaSignificantHeight, 0.05));
                float forward = pow(saturate(dot(V, -L)), SEA_SSS_POWER);

                color += mainLight.color * through
                       * (crestMask * forward * SEA_SSS_GAIN * (1.0 - F));

                // --- FOAM (spec 13) — THREE SOURCES ---
                float foam = 0.0;

                {
                    // 1. WHITECAP FOAM, STRETCHED ALONG THE FOLD DIRECTION.
                    //
                    // The `e-` eigenvector says which horizontal direction the
                    // surface folds along (spec 13.2, Tessendorf equation 48).
                    // Without stretching the pattern along it the foam looks
                    // identical in every direction and unrelated to the wave.
                    float2 foldDir;
                    float whitecap = SeaSampleFoam(IN.positionWS.xz, foldDir);

                    // DIRECTION STRETCH DISABLED ON LOW (spec 15.3): the
                    // pattern is not rotated, it is read straight from world
                    // coordinates.
                #if defined(_SEA_QUALITY_LOW)
                    float2 foamUV = IN.positionWS.xz * _SeaFoamTiling;
                #else
                    // THE FOLD DIRECTION IS ONLY TRUSTED WHERE THERE IS A FOLD.
                    //
                    // `foldDir` is the derivative texture's zw; on a nearly flat sea
                    // it is numerical noise, so `atan2` gave a DIFFERENT rotation per
                    // pixel and the stretched pattern smeared into streaks that had
                    // nothing to do with the waves. In calm water the fold falls back
                    // to the wind axis, which is the direction real streaks line up on.
                    float foldLen = length(foldDir);
                    float2 axis = foldLen > 1e-3 ? foldDir / foldLen
                                                 : normalize(_SeaWindWS.xy + float2(1e-4, 0.0));
                    axis = normalize(lerp(normalize(_SeaWindWS.xy + float2(1e-4, 0.0)),
                                          axis, saturate(foldLen * 40.0)));

                    float2x2 rot = float2x2(axis.x, -axis.y, axis.y, axis.x);

                    float2 foamUV = mul(rot, IN.positionWS.xz * _SeaFoamTiling);
                    foamUV.x *= 0.35;
                #endif

                    // BUBBLE STRUCTURE, NOT A FLAT WASH. The noise used to
                    // scale the coverage between 0.55 and 1.30, i.e. it only
                    // dimmed it; the foam still read as one solid sheet. It
                    // now EATS INTO the coverage from below, so the pattern
                    // has holes and the foam breaks into clumps.
                    // The `* 1.4` that used to be here put back everything the bubbles
                    // ate and the foam closed into a sheet again. The holes stay open now.
                    // OPEN WATER PAYS NOTHING. `saturate(0 - x)` is 0 for every
                    // non-negative x, so with no whitecap here the bubbles cannot
                    // change the result — and they are two cellular lookups plus a
                    // domain warp, i.e. four more on top. Bit-identical: the branch
                    // only skips a line whose output is already known to be 0.
                    if (whitecap > 0.0)
                    {
                        // TWO SCALES, BECAUSE ONE OF THEM IS ALWAYS THE WRONG SIZE.
                        //
                        // A single bubble octave at `_SeaFoamTiling` gives features about
                        // 1.25 m across. Close up that is right; at 100 m and beyond it is
                        // two or three pixels, so it averages to a flat wash and a whitecap
                        // reads as a pale blob pasted on the water — which is exactly what
                        // the wide shots showed.
                        //
                        // Real foam is built at two scales: a coarse LACE of clumps and
                        // channels a few metres across, and the bubbles inside it.
                        // [SOURCE: shipped ocean foam is authored as a coarse 4-8 m clump
                        // layer plus a 0.25-1 m bubble layer.] The coarse one is the one that
                        // survives the distance, and it was the missing octave.
                        //
                        // The erosion budget is SPLIT, not added to: 0.55 was already tuned
                        // against the Monahan coverage the Jacobian threshold was solved for,
                        // and taking more would push the whitecap area below that law.
                        float bubbles = SeaFoamBubbles(foamUV);
                        float lace = SeaFoamBubbles(foamUV * 0.20 + 31.7);

                        whitecap = saturate(whitecap
                                            - (1.0 - bubbles) * 0.30
                                            - (1.0 - lace) * 0.25);
                    }

                    // 2. BREAKING FOAM (spec 8.3). When the ratio of wave
                    //    height to water depth exceeds the breaker index the
                    //    wave breaks.
                    //
                    // THE WAVE'S HEIGHT IS NOT THE PIXEL'S ELEVATION. This read
                    // `2 * |y - seaLevel|`, i.e. it took the surface's instantaneous
                    // displacement AT THIS POINT for the height of the wave. Two
                    // things follow and both were measured on the shore transect
                    // (waterline -> 1.68 m depth in 29 m):
                    //
                    //   - EVERY point that is not exactly at the still level counts
                    //     as a wave. With |y - seaLevel| = 0.2 m the criterion is met
                    //     out to 6 m, with 0.4 m out to 20 m, and the foam alpha
                    //     there is 0.75. From eye height 1.7 m the first twenty
                    //     metres of water fill about 87% of the screen: the shore
                    //     came out as one white sheet.
                    //   - It DOES NOT depend on the sea state. A dead calm
                    //     (measured: wind 0.5 m/s) breaks exactly as hard as a storm,
                    //     because a trough is as far from the still level as a crest.
                    //
                    // Hs shoaled to the local depth is the height the criterion is
                    // written for, and it carries the weather: Hs is 0.10 m at 0.5 m/s
                    // and 3.96 m at 20 m/s, so the breaker line moves out with the sea.
                    float slope = SeaSampleBottomSlope(IN.positionWS.xz);
                    float gamma = SeaBreakerIndex(slope);

                    float shoal  = min(SeaShoalingGain(depth, _SeaSpectrumDepth),
                                       _SeaMaxShoalingGain);

                    // THE SETS MOVE THE BREAKER LINE.
                    //
                    // With a fixed Hs the criterion depends on depth alone, so the
                    // outer edge of the surf sits on one depth contour and draws the
                    // shoreline's own curve — from a height it reads as a clean arc
                    // that has nothing to do with the water.
                    //
                    // A real surf zone breathes: a big set breaks further out and the
                    // lull lets the edge fall back. That is not an invented envelope.
                    // The spectrum has TWO peaks and their interference is exactly the
                    // wave-to-wave size change. Measured: the beat runs 4 s in a calm
                    // and 89 s at 20 m/s, at a modulation depth of 0.29 to 0.97.
                    //
                    // The phase carries the travel across the shelf, so the set arrives
                    // rather than the whole coast pulsing at once: deeper water is
                    // reached earlier, which `depth` stands in for.
                    float setPhase = _SeaWaveGroups.x * (_SeaTime - depth * 0.35);
                    float setSize  = 1.0 + _SeaWaveGroups.y * 0.5 * cos(setPhase);

                    float waveH  = _SeaSignificantHeight * shoal * setSize;
                    float ratio  = waveH / max(depth, SEA_MIN_DEPTH);
                    float breakT = saturate((ratio - gamma * 0.7) / (gamma * 0.3));

                    // THE FOAM RIDES THE CREST. Without this the band is a function
                    // of depth alone: a clean strip parallel to the shore that never
                    // moves. Breaking happens at the crest and the trough behind it
                    // is clear water.
                    float crest = saturate((IN.positionWS.y - _SeaLevelY)
                                           / max(0.25 * waveH, 0.02));
                    breakT *= crest;

                    // 3. SHORE FOAM (spec 13.3). The run-up band makes the
                    //    water level look raised (spec 8.5).
                    //
                    // THE SWASH DOES NOT ADVANCE AS ONE STRAIGHT LINE. The
                    // run-up phase was global, so the whole coastline surged
                    // and drained together — a band that slides in and out as
                    // a single piece. A slow field shifts the phase along the
                    // shore, so one bay is filling while the next is draining.
                    float alongShore = SeaValueNoise(IN.positionWS.xz * 0.0035);
                    float phase = frac(_SeaShoreFoamPhase + alongShore * 0.6);

                    // THE SURGE IS BUILT ONCE AND USED FOR BOTH.
                    //
                    // The run-up height used to be `max * phase` — a sawtooth that
                    // climbed steadily and then snapped back, while the foam beside it
                    // followed the cosine. Two shapes for one wave. The water level and
                    // the foam now ride the SAME surge.
                    float surge = SeaSwashSurge(phase, _SeaSwashUprush);

                    // NOT EVERY SWASH REACHES THE SAME LINE.
                    //
                    // With one curve and one period the water stopped at exactly the
                    // same mark every time -- the metronome. The spectrum already
                    // carries the answer: its two peaks beat against each other and
                    // that beat is what a set of waves IS. A big set climbs the full
                    // run-up, the lull falls short. No invented randomness; the same
                    // `_SeaWaveGroups` the breaker line already breathes with.
                    float swashSet = 0.5 - 0.5 * cos(_SeaWaveGroups.x * _SeaTime
                                                     + alongShore * SEA_TWO_PI);
                    surge *= 1.0 - _SeaWaveGroups.y * 0.45 * (1.0 - swashSet);

                    float runupDepth = _SeaRunupMaxDepth * surge;
                    float effDepth = depth + runupDepth;

                    // THE EDGE IS BROKEN UP WITH NOISE. Without it the foam
                    // band becomes a straight line and the shoreline looks
                    // drawn on (spec 18 pitfall table).
                    // [SOURCE: Crest, SIGGRAPH 2017]
                    //
                    // FOUR SCALES, BECAUSE ONE SCALE READS AS A PATTERN.
                    //
                    // This was ~3 m of fine texture plus ~16 m of coarse, and the
                    // coarse one carried the edge. Measured on a 1 km stretch of
                    // shore: the edge's wander stops growing past a 32 m window
                    // (1.08 -> 1.16 -> 1.17 m). Nothing bigger than that exists, so
                    // every tooth came out the same size and the eye reads a repeat
                    // even though the autocorrelation shows no true period.
                    //
                    // A real waterline has bays, tongues and fingers at once. Two
                    // coarse octaves (~98 m and ~245 m) are ADDED on top rather than
                    // replacing the fine ones — measured, a plain 5-octave fBm spread
                    // the energy and lost the fine detail (0.29 -> 0.05 m at 1 m).
                    //
                    // After: the wander keeps growing to 1.24 m at 128 m, and the band
                    // width goes from 2.5-9.5 m to 0-11.8 m — it closes to nothing on
                    // the points and opens in the bays. The mean is 5.9 -> 6.6 m.
                    //
                    // The 16 m scale is still the smallest that matters for the OUTLINE:
                    // the waterline's own steps are the terrain heightmap's resolution
                    // (4097 texels / 30 km = 7.3 m), and noise finer than that hides
                    // nothing there.
                    float2 breakupXZ = IN.positionWS.xz * _SeaFoamBreakupTiling;

                    float breakup =
                          SeaFoamNoise(breakupXZ) * 0.55
                        + SeaValueNoise(breakupXZ * 0.18) * 0.45
                        + (SeaValueNoise(breakupXZ * 0.0292) - 0.5) * 0.6
                        + (SeaValueNoise(breakupXZ * 0.0117 + float2(51.3, 17.7)) - 0.5) * 0.5;

                    // THE NOISE MOVES THE WATERLINE, IT DOES NOT DIM THE FOAM.
                    //
                    // It used to be subtracted from the finished coverage and
                    // the result multiplied by 2.5. That multiply crushed the
                    // whole gradient into two values: the band came out as a
                    // solid white sheet with a cut edge — paper, not foam.
                    //
                    // Displacing the DEPTH the band is measured at gives the
                    // same irregular outline with the gradient intact: the
                    // edge dissolves into patches instead of ending on a line.
                    float bandDepth = effDepth / max(_SeaShoreFoamDepth, 1e-3);
                    bandDepth += (breakup - 0.5) * 0.9;

                    float band = 1.0 - smoothstep(0.15, 1.0, bandDepth);

                    // THE SWASH SWEEPS AND DRAINS — AND THE FOAM HAS A MEMORY
                    // OF IT WITHOUT A MEMORY BUFFER.
                    //
                    // The band used to be scaled by the phase, so the whole
                    // strip just brightened and dimmed in place: no bore ran
                    // up, nothing was left behind. Real shore foam does two
                    // things — the bore arrives and lays fresh foam, then the
                    // water drains and leaves a lacy residue that fades.
                    //
                    // A residue needs history, and history normally needs a
                    // persistent texture. It is not needed here: the swash is
                    // PERIODIC, so "how long ago was this point last under
                    // water" is known in closed form. For a cosine surge the
                    // covered window is symmetric about the peak, so the phase
                    // at which the water leaves a point follows from an acos.
                    float reach = saturate(depth / max(_SeaShoreFoamDepth, 1e-3));

                    // Fresh foam: where the bore stands right now.
                    float fresh = 1.0 - smoothstep(surge - 0.30, surge + 0.10, reach);

                    // Residue: time since the water drained off this point,
                    // measured in swash cycles.
                    float halfWindow = acos(clamp(1.0 - 2.0 * reach, -1.0, 1.0)) / SEA_TWO_PI;
                    float since = frac(phase - (1.0 - halfWindow));
                    float residue = exp(-since * 2.4);

                    float shoreFoam = band * max(fresh, residue * 0.55);

                    // Bubbles inside the band, at the whitecap's scale. Outside the
                    // band `shoreFoam` is already 0 and the multiply cannot revive it,
                    // so the second cellular pair is skipped there. Bit-identical.
                    if (shoreFoam > 0.0)
                        shoreFoam *= 0.55 + 0.65 * SeaFoamBubbles(IN.positionWS.xz * _SeaFoamTiling * 1.7);

                    foam = max(whitecap, max(breakT * SEA_BREAK_FOAM_GAIN, shoreFoam));

                    // RAIN ADDS FOAM, SNOW DOES NOT. The distinction comes
                    // from the bridge: `_SeaPrecipIntensity01` is only
                    // populated for rain (spec 13.5).
                    foam = saturate(foam + _SeaPrecipIntensity01 * 0.06);
                }

                // FOAM COMES AFTER FRESNEL. Foam is a SCATTERING surface; it
                // does not show the sky reflection of the water beneath it
                // (spec 12.6, 18).
                //
                // Lighting: the sun's diffuse share plus the sky. The sky
                // radiance already sits in `skyRefl`; it is taken at 0.35 as
                // the hemispherical share [CALIBRATION].
                // FOAM IS ROUGH, NOT MATTE. `_SeaFoamRoughness` was declared and
                // never read — the foam took no specular at all, so the wet sheen
                // a breaking crest has was missing and the band read as paper.
                // A broad GGX lobe at the foam's own roughness puts it back.
                float fa  = _SeaFoamRoughness * _SeaFoamRoughness;
                float fa2 = fa * fa;
                float fd  = (NoH * fa2 - NoH) * NoH + 1.0;
                float foamSpec = fa2 / (PI * fd * fd + 1e-7) * NoL;

                // FOAM IS LIT LIKE A DIFFUSE SURFACE AND IT CANNOT EXCEED WHITE.
                //
                // It used to be `foamColor * (sunColor * NoL + skyRefl * 0.35)`, and
                // `skyRefl` is the environment probe — an HDR quantity that goes well
                // above 1 on a bright sky. Multiplied by a 0.93 foam colour the result
                // came out over 1 in every channel: the foam clipped to pure white and
                // every bubble, every edge and every gradient inside it was crushed
                // flat. That is the "paper" look, and it is why more foam only ever
                // looked like more white.
                //
                // The sky now enters as irradiance (`SampleSH`), the way it does for
                // any other diffuse surface, and the sum is clamped before the albedo.
                float3 foamIrradiance = mainLight.color * NoL * mainLight.shadowAttenuation
                                      + SampleSH(N);

                float3 foamLight = min(foamIrradiance + foamSpec * 0.12, 1.0);

                // THIN FOAM IS TRANSLUCENT. A linear blend made every trace of
                // foam equally opaque, so the faint edges came out as solid
                // white paper. Squaring the coverage lets thin foam show the
                // water through it and keeps thick foam opaque.
                float foamAlpha = foam * foam * (3.0 - 2.0 * foam) * 0.80;
                color = lerp(color, _SeaFoamColor.rgb * foamLight, foamAlpha);

                // THE WATER FADES OUT INSTEAD OF BEING CUT OFF.
                //
                // Displacing the clip line with noise gave the waterline an irregular
                // SHAPE but it is still a binary cut, and the strongest thing standing on
                // it is the foam: white on one side of the line, sand on the other, with
                // nothing in between. Fading the reflection alone did not touch that.
                //
                // ONE TERM, AT THE END, FOR EVERY LAYER. `refracted` is the scene colour
                // behind this pixel — literally what the ground would look like with no
                // sea drawn over it, and at the shore the offset guard cancels the
                // refraction offset so it is EXACTLY that pixel. Blending towards it as
                // the depth goes to zero makes the two sides of the line meet at the same
                // value, so the cut has nothing left to show. Reflection, glitter, water
                // colour and foam all go through it together.
                //
                // The surface is drawn opaque on purpose (alpha blending ghosts under TAA
                // and brings sorting problems, spec 12.6). This is not alpha: it is the
                // physical statement that with no water there is no water colour.
                //
                // The terrain carries the waterline onward from here — the swash lace it
                // draws on the sand starts where this ends.
                color = lerp(refracted, color, smoothstep(0.0, SEA_SHORE_FADE_DEPTH, depth));

                // THE SEA STANDS IN THE SAME AIR AS THE TERRAIN. Every layer is fogged
                // once with ITS OWN distance — the terrain in its own shader, the cloud
                // in the compositing pass, the sky in `SkyFog`, and the sea here.
                color = ApplyHeightFog(color, _WorldSpaceCameraPos, IN.positionWS);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SeaDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex SeaDepthVertex
            #pragma fragment SeaDepthFragment
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SeaCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings SeaDepthVertex(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y = _SeaLevelY;

                // THE SAME DEFORMATION AS THE FORWARD PASS. Without it the
                // depth buffer would see a flat sea and the color buffer a
                // wavy one, and the surface would catch on its own depth test.
                SeaSurfacePoint surf = SeaDeform(posWS);
                surf.posWS = SeaFlattenFar(surf.posWS, posWS, _WorldSpaceCameraPos.xz);

                // THE SAME CURVATURE AS THE FORWARD PASS, for the same reason
                // the deformation is shared: two passes bending differently
                // would put the surface at war with its own depth test.
                float3 curvedWS = surf.posWS;
                curvedWS.y -= SeaCurvatureDrop(curvedWS.xz, _WorldSpaceCameraPos.xz);

                OUT.positionWS = surf.posWS;
                OUT.positionCS = TransformWorldToHClip(curvedWS);

                return OUT;
            }

            half4 SeaDepthFragment(Varyings IN) : SV_Target
            {
                // The SAME mask as the forward pass — otherwise the depth
                // buffer and the color buffer would see different shorelines.
                clip(SeaSampleDepth(IN.positionWS.xz));
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
