// ROL: deformer'ların alt yüzeyinin dünya Y'sini ve yatay hızını RT_Capture'a
// yazar (spec §9.2).
// Çağıran: SnowCaptureCamera (override materyal olarak).

Shader "Hidden/Snow/CaptureDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            // Cull Off ZORUNLU. Nesnenin alt yüzeyi kameraya bakan yüzdür ve
            // çoğu mesh'te back-face'tir. Bu satır olmazsa yakalama BOŞ çıkar
            // (spec §9.2 ve §22).
            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            /// CBUFFER DIŞINDA. MaterialPropertyBlock ile nesne başına
            /// yazılıyor; `UnityPerMaterial` içine konursa SRP Batcher yolu
            /// property block'u yok sayar.
            float4 _DeformerVelocity;

            /// YÜKSEKLİK MUTLAK DEĞİL GÖRELİ YAZILIYOR — ölçülmüş sebeple.
            /// RT_Capture yarım hassasiyet. Bu projenin arazisi ~4900 m'de ve
            /// yarım kayan noktanın 4096–8192 aralığındaki adımı 4 METRE; mutlak
            /// dünya Y saklamak batma derinliğini tamamen yok eder. Gözlemciye
            /// göre kodlanınca aralık ±3 m'ye iner ve adım ayak civarında
            /// 0.06 mm, hacmin ucunda 1.95 mm olur.
            float _SnowCaptureOriginY;

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

            float4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // R = alt yüzeyin dünya Y'si, GB = yatay hız, A = maske.
                return float4(IN.positionWSY - _SnowCaptureOriginY,
                              _DeformerVelocity.x, _DeformerVelocity.y, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
