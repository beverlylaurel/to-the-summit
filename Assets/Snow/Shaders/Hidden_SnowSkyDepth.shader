// ROL: kar yağışını engelleyen geometrinin dünya Y'sini RT_SkyVis'e yazar
// (spec §12.1).
// Çağıran: SnowSkyCamera (override materyal olarak).

Shader "Hidden/Snow/SkyDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  positionWSY : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWSY = positionWS.y;
                return OUT;
            }

            /// MUTLAK DÜNYA Y. Yakalamanın aksine burada göreli kodlama YOK:
            /// RT_SkyVis'in kapsamı 96 m ve okuyan taraf `occlY - posWS.y`
            /// farkını alıyor, yani iki değer de aynı mutlak eksende olmalı.
            /// Yarım hassasiyet 4900 m'de 4 m adım veriyor ama burada ölçülen
            /// şey santimetre değil METRE mertebesinde bir örtü yüksekliği;
            /// eşikler 0.05–0.40 m (§12.2) olduğu için bu YETMEZ.
            /// Bu yüzden RT_SkyVis RHalf DEĞİL RFloat açılıyor — gerekçe
            /// `DECISIONS.md`.
            float4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return float4(IN.positionWSY, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
