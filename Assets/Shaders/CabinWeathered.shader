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
        _MaterialSeed("Yapi yuzey tohumu", Float) = 0.0
        _MacroScale("Buyuk ton parcasi (metre)", Range(1,16)) = 6.0
        _MacroStrength("Buyuk ton siddeti", Range(0,0.15)) = 0.0
        _RoughnessVariation("Puruz mikro degisimi", Range(0,0.2)) = 0.0
        _ThirdPhaseStrength("Ucuncu tekrar kirma", Range(0,0.5)) = 0.0
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
                float  _MaterialSeed;
                float  _MacroScale;
                float  _MacroStrength;
                float  _RoughnessVariation;
                float  _ThirdPhaseStrength;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_TintMap);        SAMPLER(sampler_TintMap);
            TEXTURE2D(_RoughMetalMap);  SAMPLER(sampler_RoughMetalMap);

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Three-dimensional value noise keeps the metre-scale change continuous across
            // separately authored boards and stones. It supplies only a few percent of tone;
            // the authored UV1 atlas remains responsible for damp, sun and material identity.
            float MaterialMacro(float3 positionWS)
            {
                float3 p = positionWS / max(_MacroScale, 0.5) + _MaterialSeed;
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

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
                float macro     = MaterialMacro(IN.positionWS);
                half  roughness = saturate(rmd.r * _RoughnessScale
                                           + (macro - 0.5) * _RoughnessVariation);
                half  metallic  = rmd.g;
                half  detileFac = smoothstep(0.12h, 0.88h, rmd.b);

                // Doseme albedosu Blender'daki gibi iki ornegin karisimi: ayni olcek,
                // damar boyunca kaydirilmis ikinci ornek, karisim maskesi UV1'den pismis.
                half3 sampA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv0).rgb;
                half3 sampB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv0 + _DetileOffset.xy).rgb;
                half3 baseTex = lerp(sampA, sampB, detileFac);

                // A restrained third phase breaks the remaining long repeat without rotating
                // directional grain or corrugation. The same blend is used for the normal map
                // below, so visible colour detail and perceived relief cannot drift apart.
                half thirdFac = smoothstep(0.62h, 0.90h, (half)macro) * (half)_ThirdPhaseStrength;
                float2 uvC = IN.uv0 - _DetileOffset.xy * 0.613;
                half3 sampC = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvC).rgb;
                baseTex = lerp(baseTex, sampC, thirdFac);

                // Tint atlasi 1/4 olcekle pisirildi: kaplama x tint carpimi 1'i asabildigi
                // icin depolamada bolundu, burada geri acilir.
                half3 tint    = SAMPLE_TEXTURE2D(_TintMap, sampler_TintMap, IN.uv1).rgb * 4.0h;
                half macroTone = 1.0h + ((half)macro - 0.5h) * (2.0h * (half)_MacroStrength);
                half3 albedo  = baseTex * _BaseColor.rgb * tint * macroTone;

                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv0), _BumpScale);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap,
                                                               IN.uv0 + _DetileOffset.xy), _BumpScale);
                half3 nC = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvC), _BumpScale);
                half3 nTS = normalize(lerp(normalize(lerp(nA, nB, detileFac)), nC, thirdFac));

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
                float  _MaterialSeed;
                float  _MacroScale;
                float  _MacroStrength;
                float  _RoughnessVariation;
                float  _ThirdPhaseStrength;
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
                float  _MaterialSeed;
                float  _MacroScale;
                float  _MacroStrength;
                float  _RoughnessVariation;
                float  _ThirdPhaseStrength;
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
                float  _MaterialSeed;
                float  _MacroScale;
                float  _MacroStrength;
                float  _RoughnessVariation;
                float  _ThirdPhaseStrength;
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
