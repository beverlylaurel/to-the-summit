// ROL: kamera lensine yapışan kar (§10.2, opsiyonel). Ekran uzayında yavaş eriyen
// tane lekeleri.
// Çağıran: SnowLensFeature.

Shader "Hidden/Snow/Lens"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SnowLens"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "SnowSparkle.hlsl"   // SnowHash33

            float _LensSnowAmount;
            float _LensTime;
            float _LensCellDensity;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (_LensSnowAmount <= 0.001) return source;

                // Ekran en-boy oranı düzeltmesi: lekeler yuvarlak kalmalı.
                float2 p = uv * float2(_ScreenParams.x / _ScreenParams.y, 1.0) * _LensCellDensity;

                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;

                float3 rnd = SnowHash33(float3(cell, 11.0));

                // Her hücrede bir leke; tohum ne kadar büyükse o kadar geç beliriyor.
                float appear = step(rnd.x, _LensSnowAmount);

                // ERİME: leke belirdikten sonra yarıçapı yavaşça küçülüyor ve
                // saydamlaşıyor. Zaman tohumla kaydırılıyor, hepsi aynı anda erimesin.
                float phase = frac(_LensTime * 0.06 + rnd.y);
                float melt = 1.0 - phase;

                float2 offset = (rnd.yz - 0.5) * 0.6;
                float radius = (0.10 + rnd.z * 0.16) * melt;

                float d = length(f - offset);
                float blob = 1.0 - smoothstep(radius * 0.55, radius, d);

                half mask = (half)(blob * appear * melt * saturate(_LensSnowAmount));

                // Leke ışığı dağıtıyor: altındaki görüntüyü beyaza doğru çekiyor ve
                // hafifçe parlatıyor. Opak beyaz boyanmıyor — lens kirli, kör değil.
                half3 scattered = lerp(source.rgb, half3(0.86, 0.89, 0.94), mask * 0.75h);

                return half4(scattered, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

