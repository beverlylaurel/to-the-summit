// include-rev: 25  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemiyor; bu satir degistikce derleme zorlanir)
Shader "ToTheSummit/BikeSurface"
{
    // PROCEDURAL SURFACE. The model arrives with no texture file and no UVs (there is not
    // a single UV layer in the FBX — measured). The whole pattern derives from position.
    //
    // THE PATTERN IS IN OBJECT SPACE, NOT WORLD SPACE. The bike is a moving object:
    // sampled in world space the pattern would slide across the surface while riding —
    // paint does not travel with the bike, it would hang in the world. Object space fixes
    // that at the root. The scale is corrected too: the part transforms carry a factor of
    // a hundred, and in raw object space a one-metre pattern would shrink to a centimetre.
    //
    // WEAR HAS CAUSES, IT IS NOT RANDOM: dust settles on upward-facing surfaces, paint
    // fades in the sun up top, dirt collects below. The direction is read from world up
    // because the cause is gravity.
    //
    // TRANSITIONS ARE SOFT. Every mix uses smoothstep and the amplitudes are low; a hard
    // threshold leaves sharp blotches and the surface reads as "plastic with noise on it".
    Properties
    {
        _BaseColor       ("Renk", Color) = (0.45, 0.12, 0.08, 1)
        _Metallic        ("Metaliklik", Range(0,1)) = 0
        _Smoothness      ("Smoothness", Range(0,1)) = 0.45

        _Variation       ("Color variation", Range(0,0.3)) = 0.06
        _Grain           ("Fine grain (in smoothness)", Range(0,0.5)) = 0.15
        _Brushed         ("Brushed marks", Range(0,1)) = 0

        _DustColor       ("Toz rengi", Color) = (0.62, 0.60, 0.55, 1)
        _Dust            ("Dust amount", Range(0,1)) = 0.25
        _DustScale       ("Dust scale (metres)", Range(0.02, 1)) = 0.18

        _Fade            ("Sun fading", Range(0,1)) = 0.2
        _Grime           ("Alt kir", Range(0,1)) = 0.25

        // The wheel arrives as one piece: tyre, rim and hub in the same mesh. Separate
        // materials cannot be assigned, so the split is made BY RADIUS.
        _WheelMode       ("Tekerlek modu", Float) = 0
        _WheelCentre     ("Hub (object space, metres)", Vector) = (0,0,0,0)
        _WheelAxis       ("Rotation axis (object space)", Vector) = (0,1,0,0)
        _WheelRadius     ("Outer radius (metres)", Float) = 0.36
        _TireColor       ("Lastik rengi", Color) = (0.07, 0.07, 0.08, 1)
        _RimColor        ("Jant rengi", Color) = (0.58, 0.59, 0.61, 1)

        // THE HAND-PAINTED SURFACE lives in the vertices, not in the material: the color
        // is in the vertex color, the coverage in its alpha, the surface's light response
        // in the second UV channel. Held in the material, painting one spot and changing
        // its color would change every other spot painted with the same slot.
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

            // SCREEN SPACE AMBIENT OCCLUSION. Without the declaration the recesses do not
            // darken: under the mudguard, inside the basket and between the frame tubes
            // ve bisiklet fazla parlak duruyor.
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // THE SAME AIR. The bike used to call Unity's own fog (`ComputeFogFactor` /
            // `MixFog`) — with `m_Fog: 0` in the scene that call was DEAD and the bike took
            // no fog at all: in a storm the mountain went white while the bike stayed sharp.
            // Unity's fog is height independent anyway, which is why the project never uses it.
            #include "HeightFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _Variation;
                half _Grain;
                half _Brushed;
                half4 _DustColor;
                half _Dust;
                half _DustScale;
                half _Fade;
                half _Grime;
                float _WheelMode;
                float4 _WheelCentre;
                float4 _WheelAxis;
                float _WheelRadius;
                half4 _TireColor;
                half4 _RimColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  colour     : COLOR;
                float2 surface    : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionMS : TEXCOORD2;
                half4  paint      : TEXCOORD3;
                float2 surface    : TEXCOORD4;
            };

            /// Metric object space: object space multiplied by the transform's scale. The
            /// pattern stays stuck to the object while its size stays in metres.
            float3 MetricObject(float3 positionOS)
            {
                float scale = length(float3(unity_ObjectToWorld._m00,
                                            unity_ObjectToWorld._m10,
                                            unity_ObjectToWorld._m20));
                return positionOS * scale;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.positionMS = MetricObject(input.positionOS.xyz);
                output.paint = input.colour;
                output.surface = input.surface;
                return output;
            }

            float Hash(float3 cell)
            {
                return frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            /// Value noise with QUINTIC smoothing. The cubic form (3t²-2t³) breaks the
            /// second derivative at cell boundaries and read as a grid under light;
            /// the quintic form does not leave that break.
            float Noise(float3 position)
            {
                float3 cell = floor(position);
                float3 f = frac(position);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float n000 = Hash(cell + float3(0,0,0)), n100 = Hash(cell + float3(1,0,0));
                float n010 = Hash(cell + float3(0,1,0)), n110 = Hash(cell + float3(1,1,0));
                float n001 = Hash(cell + float3(0,0,1)), n101 = Hash(cell + float3(1,0,1));
                float n011 = Hash(cell + float3(0,1,1)), n111 = Hash(cell + float3(1,1,1));

                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            /// Four octaves. The amplitudes halve, so the coarsest layer sets the pattern
            /// and the finer ones only disturb it — a single layer looks blotchy.
            float Fbm(float3 position)
            {
                return Noise(position) * 0.53
                     + Noise(position * 2.1) * 0.27
                     + Noise(position * 4.3) * 0.13
                     + Noise(position * 8.7) * 0.07;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 p = input.positionMS;

                half3 albedo = _BaseColor.rgb;
                half metallic = _Metallic;
                half smoothness = _Smoothness;

                // WHEEL: because the tyre, rim and hub come in one mesh the split is made
                // by radius. The transitions are softened by one percent of the radius; sharp
                // they would be a ring that flickers as it turns.
                if (_WheelMode > 0.5)
                {
                    // The rotation axis is supplied FROM OUTSIDE. The model's own axis and
                    // Unity's are not the same: the FBX arrives Z-up, Unity puts the rotation
                    // on the transform and the mesh data stays in its own convention. With an
                    // assumed axis a stripe was computed instead of a radius.
                    float3 axis = normalize(_WheelAxis.xyz);
                    float3 offset = p - _WheelCentre.xyz;
                    float3 radial = offset - axis * dot(offset, axis);

                    float r = length(radial) / max(1e-4, _WheelRadius);

                    float tire = smoothstep(0.82, 0.87, r);
                    float rim  = smoothstep(0.55, 0.62, r) * (1.0 - tire);
                    float hub  = 1.0 - max(tire, rim);

                    albedo = _TireColor.rgb * tire
                           + _RimColor.rgb * rim
                           + _RimColor.rgb * 0.75 * hub;

                    metallic = (1.0 - tire) * 0.8;
                    smoothness = lerp(0.5, 0.18, tire);
                }

                // THE HAND-PAINTED SURFACE. The color comes from the vertex, the coverage
                // from the alpha: every brush stroke carries its own color. The surface's
                // light response comes from the slot number in the second UV channel — matte, semi-matte, metallic.
                half cover = saturate(input.paint.a);

                if (cover > 0.002)
                {
                    half slot = input.surface.x;
                    half paintMetallic = slot > 1.5 ? 0.85 : 0.0;
                    half paintSmooth = slot > 1.5 ? 0.32 : (slot > 0.5 ? 0.34 : 0.22);

                    albedo = lerp(albedo, input.paint.rgb, cover);
                    metallic = lerp(metallic, paintMetallic, cover);
                    smoothness = lerp(smoothness, paintSmooth, cover);
                }

                // Color variation: a single flat color looks like painted plastic. The
                // amplitude is small and the scale large — the eye reads it as depth, not as a pattern.
                float variation = (Fbm(p * 3.1) - 0.5) * _Variation;
                albedo *= 1.0 + variation;

                float up = saturate(normalWS.y);

                // Paint fades in the sun on upward-facing surfaces. Not squared: fading
                // happens on sloped surfaces too, not only horizontal ones.
                float fade = up * _Fade;
                albedo = lerp(albedo, saturate(albedo * 1.3 + 0.02), fade);

                // Dirt collects below: mud splash and faces nobody touches.
                float grime = saturate(-normalWS.y) * _Grime
                            * (0.45 + Fbm(p * 9.0) * 0.7);
                albedo = lerp(albedo, albedo * 0.5, grime);

                // Dust settles on horizontal surfaces. A ramp, not a threshold: zero on a
                // vertical face, full on a horizontal one, soft in between.
                float dust = smoothstep(0.25, 0.95, up) * _Dust
                           * (0.5 + Fbm(p / max(0.02, _DustScale)) * 0.8);
                albedo = lerp(albedo, _DustColor.rgb, saturate(dust));

                // The fine grain lives in SMOOTHNESS, not in color. What catches the eye on
                // real paint and metal is not the color blotching but the reflection wavering.
                float grain = (Fbm(p * 60.0) - 0.5) * _Grain;

                // Brushed marks: noise stretched along one axis. Surface marks on chrome
                // and aluminium always face one way.
                float brushed = (Fbm(float3(p.x * 90.0, p.y * 6.0, p.z * 90.0)) - 0.5)
                              * _Brushed * 0.35;

                smoothness = saturate(smoothness + grain + brushed);
                smoothness *= (1.0 - dust * 0.7) * (1.0 - grime * 0.45);

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normalWS);

                // Ambient occlusion is computed in screen space; which pixel it is read from
                // buradan veriliyor.
                lighting.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = ApplyHeightFog(color.rgb, GetCameraPositionWS(), input.positionWS);
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

                // Near plane clipping: the same as URP's own shadow pass. Without it surfaces
                // very close to the light fall out of the shadow map and the object appears
                // without a shadow.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z,
                        output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z,
                        output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

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
