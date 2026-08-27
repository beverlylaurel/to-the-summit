// ROLE: makes the state textures' channels visible in the diagnostic window.
// CALLED BY: SnowDebugWindow (Graphics.Blit).

Shader "Hidden/Snow/Debug"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            /// 0=R 1=G 2=B 3=A 4=derived height 5=raw (SkyVis/WindShadow)
            float  _DebugMode;
            float  _DebugRange;

            /// The value subtracted before display. The capture texture's channels are
            /// zero-centred (height relative to the observer, velocity signed); without the
            /// bias the negative half stays entirely black and half of it is unreadable.
            float  _DebugBias;

            /// A world grid: it proves the content DOES NOT SLIDE as the player walks.
            /// If the snap is right the lines stand still in the world (spec Phase 1 acceptance criterion).
            float  _DebugGridSize;
            float2 _DebugWorldCenter;
            float  _DebugWorldSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);
                return OUT;
            }

            float SnowDensityDbg(float rhoN) { return lerp(50.0, 550.0, saturate(rhoN)); }

            half4 Frag(Varyings IN) : SV_Target
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float v;
                if      (_DebugMode < 0.5) v = s.r;
                else if (_DebugMode < 1.5) v = s.g;
                else if (_DebugMode < 2.5) v = s.b;
                else if (_DebugMode < 3.5) v = s.a;
                else if (_DebugMode < 4.5) v = s.r * 1000.0 / max(SnowDensityDbg(s.g), 1.0);
                else                       v = s.r;

                float shown = saturate((v - _DebugBias) / max(_DebugRange, 1e-5));
                half3 color = half3(shown, shown, shown);

                // World grid
                float2 world = (IN.uv - 0.5) * _DebugWorldSize + _DebugWorldCenter;
                float2 g = abs(frac(world / max(_DebugGridSize, 1e-3)) - 0.5);
                float  gridLine = 1.0 - smoothstep(0.0, 0.02, min(g.x, g.y));
                color = lerp(color, half3(0.2, 0.9, 0.3), gridLine * 0.45);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
