// ROLE: writes the world Y of the geometry blocking snowfall into RT_SkyVis
// (spec §12.1).
// CALLED BY: SnowSkyCamera (as an override material).

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

            /// ABSOLUTE WORLD Y. Unlike the capture there is NO relative encoding here:
            /// RT_SkyVis covers 96 m and the reading side takes the difference
            /// `occlY - posWS.y`, so both values have to be on the same absolute axis.
            /// Half precision gives a 4 m step at 4900 m, but what is measured here is a
            /// cover height on the order of METRES, not centimetres;
            /// because the thresholds are 0.05–0.40 m (§12.2) that IS NOT ENOUGH.
            /// That is why RT_SkyVis is opened as RFloat and NOT RHalf — the reasoning is in
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
