// ROLE: the material for objects that collect snow on top. It is a NEW shader;
// none of the existing object shaders were changed (spec §16, §1.4).
// Which objects move to it is the user's decision.

Shader "ToTheSummit/SnowCoverObject"
{
    Properties
    {
        _BaseMap ("Taban doku", 2D) = "white" {}
        _BaseColor ("Taban rengi", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3

        [NoScaleOffset] _SnowBreakup ("Snow edge noise", 2D) = "gray" {}

        _SnowSlopeThreshold ("Slope threshold", Range(0, 1)) = 0.25
        _SnowSlopeSharpness ("Slope sharpness", Float) = 1.6
        _SnowBreakupScale ("Noise scale (1/m)", Float) = 1.8
        _SnowBreakupStrength ("Noise strength", Range(0, 1)) = 0.55
        _SnowEdgeSharpness ("Edge sharpness", Float) = 4.0

        _SnowThickness ("Snow thickness (m)", Float) = 0.03
        _SnowEdgeBulge ("Edge bulge", Float) = 0.6

        _SnowAlbedo ("Kar rengi", Color) = (0.90, 0.92, 0.95, 1)
        _SnowSmoothness ("Snow smoothness", Range(0, 1)) = 0.45

        [Toggle(_SNOW_DISPLACEMENT_ON)] _SnowDisplacement ("Vertex displacement", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SnowCoverForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature_local_vertex _SNOW_DISPLACEMENT_ON
            // The reasoning is in `MountainSurface.shader`. The object cover uses the same
            // snow lighting; the sparkle gate comes from there.
            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SnowCover.hlsl"
            #include "SnowLighting.hlsl"

            /// The terrain wetness is also published as a GLOBAL (`TerrainSurface`);
            /// the cover material reads it so the same snow looks the same on both surfaces.
            float _SurfaceWetness;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowAlbedo;
                float  _Smoothness;
                float  _SnowSmoothness;
                float  _SnowSlopeThreshold;
                float  _SnowSlopeSharpness;
                float  _SnowBreakupScale;
                float  _SnowBreakupStrength;
                float  _SnowEdgeSharpness;
                float  _SnowThickness;
                float  _SnowEdgeBulge;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

#ifdef _SNOW_DISPLACEMENT_ON
                // VERTEX DISPLACEMENT IS OPTIONAL (spec §16). It is turned on for dense
                // meshes; on a low-resolution rock the vertex count is not enough, so the
                // thickness is read from the normal.
                float mask = SnowCoverMask(positionWS, normalWS, 1.0,
                                           _SnowSlopeThreshold, _SnowSlopeSharpness,
                                           _SnowBreakupScale, _SnowBreakupStrength,
                                           _SnowEdgeSharpness);

                positionWS += _SnowUpDirection * _SnowThickness * mask;
#endif

                OUT.positionWS = positionWS;
                OUT.normalWS = normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.shadowCoord = TransformWorldToShadowCoord(positionWS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);

                // The AO input is 1: if the object has its own AO it comes from there.
                // Without the cavity factor the snow fills the recesses too.
                float mask = SnowCoverMask(IN.positionWS, N, 1.0,
                                           _SnowSlopeThreshold, _SnowSlopeSharpness,
                                           _SnowBreakupScale, _SnowBreakupStrength,
                                           _SnowEdgeSharpness);

                N = SnowCoverNormal(N, mask, _SnowEdgeBulge);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                half3 albedo = lerp(baseTex.rgb * _BaseColor.rgb, (half3)_SnowAlbedo.rgb, mask);
                half smoothness = lerp(_Smoothness, _SnowSmoothness, mask);

                BRDFData brdfData;
                half alpha = 1.0h;
                // The snow's F0 is ice's (0.018) and the object's own surface is a dielectric
                // (0.04). The same mask passes both; `MountainSurface` does exactly the same
                // on the terrain.
                half f0 = lerp(0.04h, (half)SNOW_ICE_F0, (half)mask);
                SnowInitBRDF(albedo, smoothness, f0, alpha, brdfData);

                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                Light mainLight = GetMainLight(IN.shadowCoord);

                half3 color = LightingPhysicallyBased(brdfData, mainLight, N, V);
                color += SampleSH(N) * albedo;

                // AN OBJECT'S SNOW IS THE SAME SUBSTANCE AS THE TERRAIN'S.
                //
                // The cover was drawn with standard URP PBR for a while: no wrapped
                // diffuse, no transmission, no sparkle, roughness by another route.
                // The same snow shone differently on top of a rock. Snow is the same
                // substance wherever it is; the light model has to be single too.
                //
                // The cover surface is built from the world's general state: there is no
                // deformation texture on the object and no local density to measure.
                // The thickness is `_SnowCoverThickness` (spec §16).
                if (mask > 0.001h)
                {
                    SnowSurface ks = SnowBuildSurface(_FallbackRhoN, _SurfaceWetness, 0.0, 0.0,
                                                      _SnowCoverThickness, IN.positionWS,
                                                      length(fwidth(IN.positionWS.xz)));

                    float3 karN = N;
                    {
                        float2 e = float2(karN.x, karN.z) / max(karN.y, 1e-3)
                                 + (float2)ks.surfSlope;
                        karN = normalize(float3(e.x, 1.0, e.y));
                    }

                    half3 karIsik = SnowDirectLight(mainLight, karN, V, ks)
                                  + SnowAmbient(karN, ks, mainLight.shadowAttenuation, 1.0h,
                                                mainLight.color, mainLight.direction);

                    color = lerp(color, karIsik, mask);
                }

#if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                    color += LightingPhysicallyBased(brdfData, light, N, V);
                LIGHT_LOOP_END
#endif

                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowAlbedo;
                float  _Smoothness;
                float  _SnowSmoothness;
                float  _SnowSlopeThreshold;
                float  _SnowSlopeSharpness;
                float  _SnowBreakupScale;
                float  _SnowBreakupStrength;
                float  _SnowEdgeSharpness;
                float  _SnowThickness;
                float  _SnowEdgeBulge;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
