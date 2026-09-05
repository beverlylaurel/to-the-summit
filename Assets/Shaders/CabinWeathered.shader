// Kabin kaplamasi: yuksek frekansli ahsap dokusu UV0 uzerinden doseme olarak,
// dusuk frekansli yipranma (Blender'da pisirildi) UV1 atlasindan carpan olarak gelir.
// Yakindan keskin, uzaktan Blender ile ayni gorunur.
Shader "Cabin/WeatheredLit"
{
    Properties
    {
        _BaseMap("Doseme albedo (UV0)", 2D) = "white" {}
        _BaseColor("Taban rengi", Color) = (1,1,1,1)
        _BumpMap("Doseme normal (UV0)", 2D) = "bump" {}
        _BumpScale("Normal siddeti", Float) = 1.0
        _TintMap("Yipranma tinti (UV1, x2)", 2D) = "grey" {}
        _RoughMetalMap("Puruz R / Metaliklik G / Detile maskesi B (UV1)", 2D) = "grey" {}
        _RoughnessScale("Puruz carpani", Range(0,2)) = 1.0
        _DetileOffset("Detile ikinci ornek kaymasi (UV)", Vector) = (0,0,0,0)
        _Cutoff("Kirpma esigi", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DetileOffset;
                float  _BumpScale;
                float  _RoughnessScale;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_TintMap);        SAMPLER(sampler_TintMap);
            TEXTURE2D(_RoughMetalMap);  SAMPLER(sampler_RoughMetalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uvLM       : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                half4  tangentWS  : TEXCOORD4;
                float4 shadowCoord: TEXCOORD5;
                half   fogFactor  : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.tangentWS   = half4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv0         = TRANSFORM_TEX(IN.uv0, _BaseMap);
                OUT.uv1         = IN.uv1;
                OUT.shadowCoord = GetShadowCoord(p);
                OUT.fogFactor   = ComputeFogFactor(p.positionCS.z);
                OUTPUT_LIGHTMAP_UV(IN.uvLM, unity_LightmapST, OUT.staticLightmapUV);
                OUTPUT_SH(n.normalWS, OUT.vertexSH);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half3 rmd       = SAMPLE_TEXTURE2D(_RoughMetalMap, sampler_RoughMetalMap, IN.uv1).rgb;
                half  roughness = saturate(rmd.r * _RoughnessScale);
                half  metallic  = rmd.g;
                half  detileFac = rmd.b;

                // Doseme albedosu Blender'daki gibi iki ornegin karisimi: ayni olcek,
                // damar boyunca kaydirilmis ikinci ornek, karisim maskesi UV1'den pismis.
                half3 sampA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv0).rgb;
                half3 sampB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv0 + _DetileOffset.xy).rgb;
                half3 baseTex = lerp(sampA, sampB, detileFac);

                // Tint atlasi 1/4 olcekle pisirildi: kaplama x tint carpimi 1'i asabildigi
                // icin depolamada bolundu, burada geri acilir.
                half3 tint    = SAMPLE_TEXTURE2D(_TintMap, sampler_TintMap, IN.uv1).rgb * 4.0h;
                half3 albedo  = baseTex * _BaseColor.rgb * tint;

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv0), _BumpScale);

                float  sgn       = IN.tangentWS.w;
                float3 bitangent = sgn * cross(IN.normalWS.xyz, IN.tangentWS.xyz);
                half3x3 tbn      = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS.xyz);
                half3 normalWS   = NormalizeNormalPerPixel(TransformTangentToWorld(nTS, tbn));

                SurfaceData s = (SurfaceData)0;
                s.albedo     = albedo;
                s.metallic   = metallic;
                s.smoothness = 1.0h - roughness;
                s.normalTS   = nTS;
                s.occlusion  = 1.0h;
                s.alpha      = 1.0h;

                InputData d = (InputData)0;
                d.positionWS      = IN.positionWS;
                d.normalWS        = normalWS;
                d.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                d.shadowCoord     = IN.shadowCoord;
                d.fogCoord        = IN.fogFactor;
                d.bakedGI         = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                d.shadowMask      = half4(1,1,1,1);

                half4 color = UniversalFragmentPBR(d, s);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DetileOffset;
                float  _BumpScale;
                float  _RoughnessScale;
                float  _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DetileOffset;
                float  _BumpScale;
                float  _RoughnessScale;
                float  _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DetileOffset;
                float  _BumpScale;
                float  _RoughnessScale;
                float  _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
