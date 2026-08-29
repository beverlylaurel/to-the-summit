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

                OUT.positionWS = surf.posWS;
                OUT.positionCS = TransformWorldToHClip(surf.posWS);
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
                clip(depth);

                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float  dist = length(_WorldSpaceCameraPos - IN.positionWS);

                // --- NORMAL, FROM THE FFT SLOPE TEXTURE (spec 10.5) ---
                //
                // NO central difference: the slope already comes from the FFT
                // and that is more accurate (spec 6.7).
                float2 slopeSum = _SeaDbgNoWaves > 0.5 ? 0.0
                                : SeaSampleSlope(IN.positionWS.xz);

                float3 N = normalize(float3(-slopeSum.x, 1.0, -slopeSum.y));

                // NORMAL DETAIL FADES WITH DISTANCE. Without the fade, waves
                // smaller than a texel get sampled and TAA turns the surface
                // into a boiling mess (spec 10.5).
                float normalFade = saturate(1.0 - (dist - 120.0) / 400.0);
                N = normalize(lerp(float3(0, 1, 0), N, normalFade));

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
                if (_SeaDbgNoRefraction <= 0.5)
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
                float3 skyRefl = GlossyEnvironmentReflection(R, IN.positionWS,
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

                // --- FOAM (spec 13) — THREE SOURCES ---
                float foam = 0.0;

                if (_SeaDbgNoFoam <= 0.5)
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
                    float bubbles = SeaFoamBubbles(foamUV);
                    whitecap = saturate(whitecap - (1.0 - bubbles) * 0.55);

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
                    float waveH  = _SeaSignificantHeight * shoal;
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

                    float runupDepth = _SeaRunupMaxDepth * phase;
                    float effDepth = depth + runupDepth;

                    // THE EDGE IS BROKEN UP WITH NOISE. Without it the foam
                    // band becomes a straight line and the shoreline looks
                    // drawn on (spec 18 pitfall table).
                    // [SOURCE: Crest, SIGGRAPH 2017]
                    //
                    // TWO SCALES. The fine noise (~3 m) is the foam's own
                    // texture; the COARSE noise (~16 m) breaks up the
                    // straightness of the waterline.
                    //
                    // The coarse scale came FROM A MEASUREMENT: the steps in
                    // the waterline are the terrain heightmap's own
                    // resolution (4097 texels / 30 km = 7.3 m) and they DO NOT
                    // CHANGE when the sea mesh is refined. Noise finer than
                    // that scale hides nothing there.
                    float breakup =
                          SeaFoamNoise(IN.positionWS.xz * _SeaFoamBreakupTiling) * 0.55
                        + SeaValueNoise(IN.positionWS.xz * (_SeaFoamBreakupTiling * 0.18)) * 0.45;

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
                    float surge = 0.5 - 0.5 * cos(SEA_TWO_PI * phase);

                    // Fresh foam: where the bore stands right now.
                    float fresh = 1.0 - smoothstep(surge - 0.30, surge + 0.10, reach);

                    // Residue: time since the water drained off this point,
                    // measured in swash cycles.
                    float halfWindow = acos(clamp(1.0 - 2.0 * reach, -1.0, 1.0)) / SEA_TWO_PI;
                    float since = frac(phase - (1.0 - halfWindow));
                    float residue = exp(-since * 2.4);

                    float shoreFoam = band * max(fresh, residue * 0.55);

                    // Bubbles inside the band, at the whitecap's scale.
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

                OUT.positionWS = surf.posWS;
                OUT.positionCS = TransformWorldToHClip(surf.posWS);

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
