// ROL: üstünde kar tutan nesnelerin materyali (§9). Kaya, çatı, dal.
// Kar maskesi eğim + gökyüzü görünürlüğü + girinti + kırılma gürültüsünden çıkıyor.

Shader "To The Summit/Snow Cover Object"
{
    Properties
    {
        [Header(Taban)]
        _BaseMap ("Taban dokusu", 2D) = "white" {}
        _BaseColor ("Taban rengi", Color) = (0.5, 0.5, 0.5, 1.0)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal siddeti", Float) = 1.0
        _OcclusionMap ("AO", 2D) = "white" {}
        _BaseSmoothness ("Taban puruzsuzlugu", Range(0.0, 1.0)) = 0.3

        [Header(Kar)]
        _SnowAlbedo ("Kar rengi", Color) = (0.90, 0.92, 0.95, 1.0)
        _SnowSmoothness ("Kar puruzsuzlugu", Range(0.0, 1.0)) = 0.65
        _SnowSlopeThreshold ("Egim esigi", Range(0.0, 1.0)) = 0.25
        _SnowSlopeSharpness ("Egim keskinligi", Range(0.5, 6.0)) = 1.6
        _SnowBreakupScale ("Kirilma olcegi", Range(0.1, 8.0)) = 1.8
        _SnowBreakupStrength ("Kirilma siddeti", Range(0.0, 1.0)) = 0.55
        _SnowEdgeSharpness ("Kenar keskinligi", Range(1.0, 12.0)) = 4.0
        _SnowEdgeBulge ("Kenar yuvarlakligi", Range(0.0, 4.0)) = 1.0
        _SnowThickness ("Kalinlik (m)", Range(0.0, 0.5)) = 0.06

        [Toggle(_SNOW_DISPLACEMENT_ON)] _SnowDisplacement ("Geometri kabarsin", Float) = 0
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex CoverVertex
            #pragma fragment CoverFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // GEOMETRİ KABARMASI OPSİYONEL. Yeterli köşesi olmayan mesh'te kabartma
            // yüzeyi yırtıyor; anahtar kapalıyken yalnız gölgelendirme değişiyor.
            #pragma shader_feature_local _SNOW_DISPLACEMENT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SnowCover.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SnowAlbedo;
                half _BumpScale;
                half _BaseSmoothness;
                half _SnowSmoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float4 shadowCoord: TEXCOORD4;
            };

            Varyings CoverVertex(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                float3 positionWS = positions.positionWS;

            #if defined(_SNOW_DISPLACEMENT_ON)
                // AO vertex'te bilinmiyor; kabarma için 1 veriliyor ve girinti terimi
                // yalnız fragment'ta uygulanıyor.
                float mask = SnowCoverMask(positionWS, normals.normalWS, 1.0);
                positionWS += _SnowUpDirection * (_SnowThickness * mask);
            #endif

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normals.normalWS;
                output.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);

                return output;
            }

            half4 CoverFragment(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);

                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);

                float3 normalWS = normalize(mul(normalTS, tbn));

                // AO baked dokudan. Yoksa doku beyaz ve girinti terimi devre dışı kalıyor;
                // SSAO fragment'ta güvenilir değil ve yanlış sonuç verir (§9).
                half ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g;

                float mask = SnowCoverMask(input.positionWS, normalWS, ao);

                normalWS = SnowCoverNormal(normalWS, mask);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(baseSample.rgb, _SnowAlbedo.rgb, mask);
                surface.smoothness = lerp(_BaseSmoothness, _SnowSmoothness, mask);
                surface.occlusion = ao;
                surface.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                return UniversalFragmentPBR(inputData, surface);
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

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local _SNOW_DISPLACEMENT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "SnowCover.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SnowAlbedo;
                half _BumpScale;
                half _BaseSmoothness;
                half _SnowSmoothness;
            CBUFFER_END

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 ShadowVertex(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if defined(_SNOW_DISPLACEMENT_ON)
                // GÖLGE GEÇİŞİ AYNI KABARMAYI UYGULAMAK ZORUNDA, yoksa gölge yüzeyin
                // altında kalır.
                positionWS += _SnowUpDirection * (_SnowThickness * SnowCoverMask(positionWS, normalWS, 1.0));
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return positionCS;
            }

            half4 ShadowFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

