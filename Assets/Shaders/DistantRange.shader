// The unreachable backdrop terrain that surrounds the playable square. One draw, no
// textures, no shadows: at 20 km and beyond only the silhouette and the aerial perspective
// read, and both are cheaper than anything else this could have been.
//
// The mesh comes from `DistantRangeBuilder`; its vertex colour already carries the snow line
// and the rock, so this shader only has to light it and put it inside the air.
Shader "ToTheSummit/DistantRange"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // THE SAME AIR AS THE TERRAIN. Two fog paths would show up as a seam exactly where
            // the backdrop meets the playable ground, which is the one place it must not.
            #include "HeightFog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);

                // NO SHADOW LOOKUP. The cascades end around 2 km and this geometry starts at
                // 15 km, so a shadow coordinate out here samples past the last cascade and
                // returns garbage or a constant. The lit/shadowed split that actually reads at
                // this distance is the slope's own facing, which is the N.L below.
                Light mainLight = GetMainLight();

                float NoL = saturate(dot(N, mainLight.direction));

                // Wrapped diffuse. A hard N.L cuts the shaded faces to black at this scale,
                // where in reality they are filled by the sky; the wrap is the cheap stand-in
                // for that fill and keeps the ridge line readable.
                float wrapped = saturate((dot(N, mainLight.direction) + 0.35) / 1.8225);

                float3 albedo = IN.color.rgb;
                float3 ambient = SampleSH(N);

                float3 color = albedo * (ambient + mainLight.color * lerp(wrapped, NoL, 0.55));

                color = ApplyHeightFog(color, _WorldSpaceCameraPos, IN.positionWS);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
