// Renders GPU-simulated snowflakes and blowing spindrift particles.
// Dispatched by: SnowfallRenderer (Graphics.RenderPrimitives).

Shader "ToTheSummit/SnowfallParticle"
{
    Properties
    {
        [NoScaleOffset] _FlakeAtlas ("Flake Atlas (4x4)", 2D) = "white" {}
        _FlakeTint ("Color Tint", Color) = (1, 1, 1, 1)
        _FlakeEmissive ("Ambient Lift", Float) = 0.0

        _MinPixelSize ("Minimum Pixel Size (px)", Float) = 1.3
        _SoftFade ("Soft Particle Fade Distance (m)", Float) = 0.4

        _StretchAlongVelocity ("Stretch Along Velocity", Float) = 0
        _StretchMin ("Min Stretch", Float) = 1.0
        _StretchMax ("Max Stretch", Float) = 3.0
        _AlphaScale ("Alpha Multiplier", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "SnowfallParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // THE SAME AIR AS EVERYTHING ELSE. Unity's own fog was called here and with
            // `m_Fog: 0` in the scene that call was DEAD: a falling flake stayed sharp
            // inside a storm that had swallowed the mountain behind it.
            #include "../../Shaders/HeightFog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct SnowFlake
            {
                float3 position;
                float  age;
                float3 velocity;
                float  lifetime;
                float  size;
                float  phase;
                float  frame;
                float  alpha;
            };

            StructuredBuffer<SnowFlake> _Flakes;

            TEXTURE2D(_FlakeAtlas);
            SAMPLER(sampler_FlakeAtlas);

            float4 _FlakeTint;
            float  _FlakeEmissive;
            float  _MinPixelSize;
            float  _SoftFade;
            float  _StretchAlongVelocity;
            float  _StretchMin;
            float  _StretchMax;
            float  _AlphaScale;

            float _FogDensity01;
            float _WindSpeed;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float  viewDepth  : TEXCOORD4;
                float3 directLight : TEXCOORD5;
            };

            static const float2 kCorners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5,  0.5), float2(-0.5, 0.5)
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings OUT = (Varyings)0;

                SnowFlake f = _Flakes[instanceID];

                if (f.lifetime <= 0.0 || f.alpha <= 0.001)
                {
                    OUT.positionCS = float4(0, 0, -10, 1);
                    return OUT;
                }

                float2 corner = kCorners[vertexID];

                float3 toCam = _WorldSpaceCameraPos - f.position;
                float  dist = length(toCam);

                float pixelsPerRadian = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
                float minWorld = dist * _MinPixelSize / max(pixelsPerRadian, 1e-4);
                float size = max(f.size, minWorld);
                float subPixel = saturate((f.size * f.size) / max(size * size, 1e-12));

                float3 forward = toCam / max(dist, 1e-4);
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);

                float roll = f.phase + f.age * 1.5708;
                float2 rc = float2(cos(roll), sin(roll));

                float2 rotated = float2(corner.x * rc.x - corner.y * rc.y,
                                        corner.x * rc.y + corner.y * rc.x);

                float3 offset = (right * rotated.x + up * rotated.y) * size;

                if (_StretchAlongVelocity > 0.5)
                {
                    float3 velDir = normalize(f.velocity + 1e-5);
                    float3 screenVel = normalize(velDir - forward * dot(velDir, forward) + 1e-5);
                    float3 screenSide = cross(forward, screenVel);
                    float stretch = lerp(_StretchMin, _StretchMax, saturate(_WindSpeed / 12.0));

                    offset = (screenVel * rotated.y * stretch + screenSide * rotated.x) * size;
                }

                float3 positionWS = f.position + offset;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDepth = -TransformWorldToView(positionWS).z;

                float2 cell = float2(fmod(f.frame, 4.0), floor(f.frame / 4.0));
                OUT.uv = (corner + 0.5 + cell) * 0.25;

                float fogCut = lerp(120.0, 35.0, saturate(_FogDensity01));
                float fogFade = 1.0 - saturate(dist / max(fogCut, 1.0));
                float alpha = f.alpha * fogFade * _AlphaScale * subPixel;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));

                float3 toCamera = normalize(_WorldSpaceCameraPos - positionWS);
                float cosT = dot(-toCamera, mainLight.direction);

                const float g = 0.55;
                float hg = (1.0 - g * g) / pow(max(1.0 + g * g - 2.0 * g * cosT, 1e-4), 1.5);
                half phase = (half)(0.18 + 0.42 * hg);

                // A tumbling flake is not a camera-facing Lambert plane. Average sky and ground
                // hemispheres so rotating the view cannot change its ambient illumination.
                half3 ambient = (SampleSH(half3(0, 1, 0)) + SampleSH(half3(0, -1, 0))) * 0.5h;
                ambient *= 1.0h + _FlakeEmissive * 0.04h;

                OUT.color = float4(_FlakeTint.rgb * ambient, alpha);
                OUT.directLight = _FlakeTint.rgb * mainLight.color * mainLight.shadowAttenuation * phase;
                OUT.positionWS = positionWS;

                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_FlakeAtlas, sampler_FlakeAtlas, IN.uv);
                half alpha = tex.a * IN.color.a;

                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);

                alpha *= saturate((sceneDepth - IN.viewDepth) / max(_SoftFade, 1e-3));
                clip(alpha - 0.002);

                // Cookie sampling needs fragment derivatives on D3D11, so only the already
                // shadowed direct term is carried from the vertex stage.
                half cookie = SampleMainLightCookie(IN.positionWS);
                half3 color = (IN.color.rgb + IN.directLight * cookie) * tex.rgb;
                // THE COST IS NOT MEASURED. `ApplyHeightFog` is an eight step UNROLLED
                // integral plus one 3D sample, paid per pixel on up to 250 000 quads
                // with overdraw. It was taken because a flake that ignores the air is
                // a visible contradiction and the frame had headroom (5.2 ms at the
                // time of writing). The trigger for revisiting is in `DECISIONS.md`.
                color = ApplyHeightFog(color, _WorldSpaceCameraPos, IN.positionWS);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
