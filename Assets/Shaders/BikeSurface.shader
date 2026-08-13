Shader "ToTheSummit/BikeSurface"
{
    // PROSEDÜREL YÜZEY. Model doku dosyası olmadan geliyor ve UV'sine güvenilmiyor:
    // remesh edilmemiş üretim modelinde UV ya bozuk ya hiç yok. Bu yüzden bütün desen
    // DÜNYA KONUMUNDAN türüyor — üçlü düzlem (triplanar), UV'ye hiç bakmıyor.
    //
    // Aşınma SEBEPLİ, rastgele değil: toz yukarı bakan yüzeyde birikiyor, boya yukarı
    // bakan yüzeyde güneşten soluyor, kir aşağıda topluyor. Tek tip gürültü "gürültü
    // uygulanmış plastik" olarak okunuyor.
    Properties
    {
        _BaseColor       ("Renk", Color) = (0.45, 0.12, 0.08, 1)
        _Metallic        ("Metaliklik", Range(0,1)) = 0
        _Smoothness      ("Parlaklık", Range(0,1)) = 0.45

        _DustColor       ("Toz rengi", Color) = (0.62, 0.60, 0.55, 1)
        _Dust            ("Toz miktarı", Range(0,1)) = 0.35
        _DustScale       ("Toz ölçeği (metre)", Range(0.01, 1)) = 0.12

        _Fade            ("Güneş soldurması", Range(0,1)) = 0.25
        _Grime           ("Alt kir", Range(0,1)) = 0.3
        _Variation       ("Renk oynaması", Range(0,0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half4 _DustColor;
                half _Dust;
                half _DustScale;
                half _Fade;
                half _Grime;
                half _Variation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Değer gürültüsü. Doku okuması yok: bisiklet ekranın küçük bir parçası ve
            // her piksel için doku örneklemek, üretilen desenden pahalıya geliyor.
            float Hash(float3 cell)
            {
                return frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float Noise(float3 position)
            {
                float3 cell = floor(position);
                float3 f = frac(position);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash(cell + float3(0,0,0)), n100 = Hash(cell + float3(1,0,0));
                float n010 = Hash(cell + float3(0,1,0)), n110 = Hash(cell + float3(1,1,0));
                float n001 = Hash(cell + float3(0,0,1)), n101 = Hash(cell + float3(1,0,1));
                float n011 = Hash(cell + float3(0,1,1)), n111 = Hash(cell + float3(1,1,1));

                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            float Fbm(float3 position)
            {
                return Noise(position) * 0.6
                     + Noise(position * 2.7) * 0.3
                     + Noise(position * 6.1) * 0.1;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // ÜÇ SEBEP, ÜÇ AYRI DESEN.
                //
                // Toz yukarı bakan yüzeyde birikiyor: yatay yüzeyde çok, dikeyde yok.
                float up = saturate(normalWS.y);
                float dustNoise = Fbm(input.positionWS / max(0.01, _DustScale));
                float dust = saturate(up * up * _Dust * 2.0 * (0.4 + dustNoise));

                // Boya yukarı bakan yüzeyde güneşten soluyor — aynı yön, farklı etki.
                float fade = up * _Fade;

                // Kir aşağıda topluyor: çamur sıçraması ve el değmeyen yüzler.
                float down = saturate(-normalWS.y);
                float grime = down * _Grime * (0.5 + Fbm(input.positionWS * 7.0) * 0.8);

                // Renk oynaması: tek düz renk boyanmış plastik gibi duruyor.
                float variation = (Fbm(input.positionWS * 1.7) - 0.5) * _Variation;

                half3 albedo = _BaseColor.rgb * (1.0 + variation);
                albedo = lerp(albedo, albedo * 1.35, fade);        // solma açıyor
                albedo = lerp(albedo, albedo * 0.45, grime);       // kir koyultuyor
                albedo = lerp(albedo, _DustColor.rgb, dust);

                // Tozlu ve kirli yüzey mat: parlaklık örtülüyor.
                half smoothness = _Smoothness * (1.0 - dust * 0.8) * (1.0 - grime * 0.5);

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.fogCoord = input.fogCoord;
                lighting.bakedGI = SampleSH(normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.smoothness = smoothness;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
