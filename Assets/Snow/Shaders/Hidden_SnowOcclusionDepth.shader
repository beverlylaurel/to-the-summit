// ROL: gökyüzü görünürlüğü haritasının çizim shader'ı. Tek iş yapar: pikselin
// DÜNYA Y'sini yazar. Kameraya en yakın (en yüksek) yüzey derinlik testini kazanır,
// yani dokuda o noktanın üstündeki en yüksek engelin kotu kalır.
// Çağıran: SnowOcclusionCapture'ın render geçişi, override materyal olarak.

Shader "Hidden/Snow/OcclusionDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SnowOcclusionDepth"
            Tags { "LightMode" = "SnowOcclusion" }

            // ZWrite açık ve LEqual: aynı geometri URP'nin opak geçişinde zaten
            // çizildiği için derinlik eşit gelir ve test geçer.
            ZTest LEqual
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Frag(Varyings input) : SV_Target
            {
                return input.positionWS.y;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

