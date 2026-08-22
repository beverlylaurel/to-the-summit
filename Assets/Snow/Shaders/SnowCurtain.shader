// ROL: süspansiyon perdelerini çizer (spec §18.7).
// Çağıran: SnowCurtainController (Graphics.RenderPrimitives).

Shader "ToTheSummit/SnowCurtain"
{
    Properties
    {
        [NoScaleOffset] _CurtainNoise ("Perde gürültüsü", 2D) = "white" {}
        _CurtainTint ("Renk", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("UV kayma hızı", Float) = 0.15
        _SoftFade ("Yumuşak parçacık mesafesi (m)", Float) = 2.0
        // 4 m yetmiyordu: perde 12-25 m genis, 10 m otede bile ekranin
        // yarisini kapliyor ve duz bir levha gibi gorunuyordu ("kagit gibi
        // incecik, derinligi yok" - kullanici, ekran goruntusuyle). 18 m'de
        // yakin perdeler sonuyor, uzaktakiler duruyor.
        _NearFade ("Kameraya yakın sönüm (m)", Float) = 18.0
        _FogSuppress ("Sisin bastırma oranı", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "SnowCurtain"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            /// SnowfallSim.compute'taki `SnowFlake` ile aynı düzen. Perdede
            /// `size` YÜKSEKLİK, `frame` GENİŞLİK anlamına geliyor.
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

            TEXTURE2D(_CurtainNoise);
            SAMPLER(sampler_CurtainNoise);

            float4 _CurtainTint;
            float  _ScrollSpeed;
            float  _SoftFade;
            float  _NearFade;
            float  _FogSuppress;

            float _FogDensity01;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float  viewDepth  : TEXCOORD4;
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

                if (f.lifetime <= 0.0 || f.alpha <= 0.0005)
                {
                    OUT.positionCS = float4(0, 0, -10, 1);
                    return OUT;
                }

                float2 corner = kCorners[vertexID];

                // DÜŞEY EKSEN SABİT, yatay eksen hız yönünde. Perde bir
                // tabaka; kameraya dönmüyor.
                float3 along = normalize(float3(f.velocity.x, 0.0, f.velocity.z) + 1e-5);
                float3 up = float3(0, 1, 0);

                float3 offset = along * (corner.x * f.frame) + up * (corner.y * f.size);

                float3 positionWS = f.position + offset;

                float dist = distance(_WorldSpaceCameraPos, positionWS);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDepth = -TransformWorldToView(positionWS).z;

                // UV rüzgâr yönünde kayıyor: tabaka akıyormuş gibi görünüyor.
                OUT.uv = corner + 0.5;
                OUT.uv.x += _Time.y * _ScrollSpeed;
                OUT.uv.x += f.phase;                  // perdeler aynı fazda olmasın

                float alpha = f.alpha;

                // KAMERAYA YAKINSA SÖNÜYOR. İçinden geçerken ekran beyaza
                // boğulmasın (spec §18.7).
                alpha *= saturate(dist / max(_NearFade, 1e-3));

                // SİSİN YERİNE GEÇMİYOR, ÜSTÜNE BİNİYOR. Yoğun siste
                // görünmez katmanlar çizip fill-rate yakmamak için
                // bastırılıyor (spec §18.7).
                alpha *= 1.0 - _FogDensity01 * _FogSuppress;

                Light mainLight = GetMainLight();

                OUT.color = float4(_CurtainTint.rgb * (0.4 + mainLight.color * 0.6), alpha);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_CurtainNoise, sampler_CurtainNoise, IN.uv);

                // Kenarlarda yumuşak: dikdörtgen kenarı görünmesin.
                float2 e = abs(IN.uv - floor(IN.uv) - 0.5) * 2.0;
                float edge = (1.0 - smoothstep(0.6, 1.0, e.y));

                half alpha = tex.r * IN.color.a * edge;

                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);

                alpha *= saturate((sceneDepth - IN.viewDepth) / max(_SoftFade, 1e-3));

                clip(alpha - 0.002);

                half3 color = IN.color.rgb;
                color = MixFog(color, IN.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
