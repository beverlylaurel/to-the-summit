// ROL: kar durum dokusunun kanal görselleştirmesi. Editör penceresi bu materyalle
// blit yapar; her kanal ayrı ayrı ve türetilmiş derinlik h da gösterilebilir.
// Çağıran: Editor/SnowDebugWindow.cs.

Shader "Hidden/Snow/Debug"
{
    Properties
    {
        _MainTex ("Durum dokusu", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SnowCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 0 = swe, 1 = rhoN, 2 = wet, 3 = disturb, 4 = h, 5 = engel haritası
            float _DebugMode;

            // Görüntülenen değerin tavanı. Pencere moda göre yazar.
            float _DebugRange;

            // Dünya ızgarasının aralığı, metre. 0 = kapalı.
            //
            // SNAP TESTİ BUNUNLA YAPILIYOR: ızgara DÜNYA koordinatından çiziliyor.
            // Bölge merkezi tam teksele snap'liyse çizgiler önizlemede tam teksel
            // adımlarla sıçrar ve asla kaymaz. Snap bozuksa yürürken sürekli kayar.
            float _DebugGridSize;

            // Önizlenen dokunun kendi dünya eşlemesi. Durum dokusu ile engel haritasının
            // merkezi ve boyu farklı; ızgara hangisi gösteriliyorsa onunkini kullanmalı.
            float2 _DebugWorldCenter;
            float  _DebugWorldSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float3 color;

                if (_DebugMode > 4.5)
                {
                    // ENGEL HARİTASI. Temizlik değeri -9999; ondan yükseği bir engelin
                    // kotu demek. Açık gökyüzü açık gri, engelli bölge koyu.
                    float occluded = step(-9000.0, s.r);
                    color = lerp(float3(0.80, 0.80, 0.80), float3(0.10, 0.10, 0.10), occluded);
                }
                else
                {
                    float value;
                    if (_DebugMode < 0.5)      value = s.r / max(_DebugRange, 1e-5);
                    else if (_DebugMode < 1.5) value = s.g;
                    else if (_DebugMode < 2.5) value = s.b;
                    else if (_DebugMode < 3.5) value = s.a;
                    else                       value = SnowHeight(s.r, s.g) / max(_DebugRange, 1e-5);

                    color = saturate(value).xxx;
                }

                if (_DebugGridSize > 0.0)
                {
                    float2 world = (input.uv - 0.5) * _DebugWorldSize + _DebugWorldCenter;
                    float2 dist  = abs(frac(world / _DebugGridSize + 0.5) - 0.5) * _DebugGridSize;
                    float2 width = max(fwidth(world), 1e-6) * 1.5;

                    float gridLine = 1.0 - min(smoothstep(0.0, width.x, dist.x),
                                               smoothstep(0.0, width.y, dist.y));

                    color = lerp(color, float3(1.0, 0.25, 0.10), gridLine);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

