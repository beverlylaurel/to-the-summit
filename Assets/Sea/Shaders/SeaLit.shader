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
            #pragma multi_compile_fog

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

            #include "SeaCommon.hlsl"

            // Optics globals
            float3 _SeaExtinctionRGB;
            float4 _SeaUpwellingColor;
            float  _SeaRefractionStrength;
            float  _SeaRoughnessCalm;
            float  _SeaRoughnessRough;

            float4 _SeaSkyColor;
            float4 _SeaHorizonColor;
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
                float  fogCoord   : TEXCOORD2;
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
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);

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

                float3 volume = SeaVolumeColor(thickness);
                float3 belowSurface = lerp(_SeaUpwellingColor.rgb, refracted * volume, volume);

                // --- SKY REFLECTION (spec 12.4) ---
                //
                // The sea DOES NOT BUILD ITS OWN SKY MODEL. The game already
                // has an atmosphere and two sources would contradict.
                float3 R = reflect(-V, N);
                float3 skyRefl = lerp(_SeaHorizonColor.rgb, _SeaSkyColor.rgb, saturate(R.y));
                skyRefl = lerp(skyRefl, skyRefl * 0.62, _SeaCloudCover01);

                // --- SUN GLITTER (spec 12.5) ---
                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;
                float3 H = normalize(V + L);

                float roughness = lerp(_SeaRoughnessCalm, _SeaRoughnessRough,
                                       saturate(length(_SeaWindWS) / 20.0));

                // Rain roughens the surface (spec 13.5).
                roughness = lerp(roughness, 0.22, _SeaPrecipIntensity01 * 0.7);

                // DISTANT GLITTER IS DIFFUSE. Far waves live below what the
                // camera can resolve, so the glitter spreads out
                // [SOURCE: Tessendorf 2004 6 introduction].
                roughness = lerp(roughness, 0.35, saturate((dist - 200.0) / 1500.0));

                float spec = pow(saturate(dot(N, H)), max(2.0 / (roughness * roughness), 2.0));

                // NO GLITTER AT NIGHT (spec 12.5, 18 pitfall).
                spec *= saturate(_SeaSunElevation01 * 20.0);

                float3 glitter = mainLight.color * spec;

                // --- COMBINE (spec 12.6) ---
                float F = SeaFresnel(N, V);
                float3 color = lerp(belowSurface, skyRefl, F) + glitter;

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
                    float angle = atan2(foldDir.y, foldDir.x);
                    float sn, cs; sincos(angle, sn, cs);
                    float2x2 rot = float2x2(cs, -sn, sn, cs);

                    float2 foamUV = mul(rot, IN.positionWS.xz * _SeaFoamTiling);
                    foamUV.x *= 0.35;
                #endif

                    whitecap = saturate(whitecap * (0.55 + 0.75 * SeaFoamNoise(foamUV)));

                    // 2. BREAKING FOAM (spec 8.3). When the ratio of wave
                    //    height to water depth exceeds the breaker index the
                    //    wave breaks.
                    float slope = SeaSampleBottomSlope(IN.positionWS.xz);
                    float gamma = SeaBreakerIndex(slope);
                    float waveH = 2.0 * abs(IN.positionWS.y - _SeaLevelY);
                    float ratio = waveH / max(depth, SEA_MIN_DEPTH);
                    float breakT = saturate((ratio - gamma * 0.7) / (gamma * 0.3));

                    // 3. SHORE FOAM (spec 13.3). The run-up band makes the
                    //    water level look raised (spec 8.5).
                    float runupDepth = _SeaRunupMaxDepth * _SeaShoreFoamPhase;
                    float effDepth = depth + runupDepth;

                    float shoreFoam = 1.0 - smoothstep(0.0, _SeaShoreFoamDepth, effDepth);
                    shoreFoam *= 0.4 + 0.6 * _SeaShoreFoamPhase;

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
                    shoreFoam = saturate((shoreFoam - breakup * 0.45) * 2.5);

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
                float3 foamLight = mainLight.color * saturate(dot(N, L)) + skyRefl * 0.35;
                color = lerp(color, _SeaFoamColor.rgb * foamLight, foam * 0.9);

                // FOG THROUGH URP'S OWN FUNCTION (spec 3.5). No fog
                // computation of our own IS WRITTEN.
                color = MixFog(color, IN.fogCoord);

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
