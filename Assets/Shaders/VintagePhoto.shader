Shader "Hidden/ToTheSummit/VintagePhoto"
{
    Properties
    {
        // Graphics.Blit only binds its source when the material exposes _MainTex as an
        // actual shader property; an HLSL texture declaration alone is not enough.
        [HideInInspector] _MainTex("Source", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;
        float _Exposure;
        float _IsoScale;
        float _FrameSeed;
        float _VignetteStrength;
        float _ChromaticAberration;
        float _Distortion;
        float _Contrast;
        float _Sharpen;
        float _GrainStrength;
        float _PurpleFringe;
        float3 _WhiteBalance;
        float2 _FocusStep;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            // Graphics.Blit submits a quad with POSITION/TEXCOORD attributes. A fullscreen
            // triangle generated from SV_VertexID only works with DrawProcedural; mixing the
            // two left most of the destination untouched (solid black JPEGs).
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        float Hash12(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float PhotoLuminance(float3 color)
        {
            return dot(color, float3(0.2126, 0.7152, 0.0722));
        }

        float3 SampleOptics(float2 uv)
        {
            float2 p = uv * 2.0 - 1.0;
            float r2 = dot(p, p);
            float2 warped = 0.5 + 0.5 * p * (1.0 + _Distortion * r2);
            float2 radial = p * _ChromaticAberration * r2;
            float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped + radial).r;
            float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped).g;
            float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped - radial).b;
            return float3(r, g, b);
        }

        float3 Tone(float3 linearColor)
        {
            // Mid-2000s neutral camera matrix: modest red separation and restrained blues.
            float3 c;
            c.r = dot(linearColor, float3(1.052, -0.040, -0.012));
            c.g = dot(linearColor, float3(-0.018, 1.035, -0.017));
            c.b = dot(linearColor, float3(-0.010, -0.055, 1.065));
            c = max(c, 0.0);

            const float white = 1.10;
            c = c * (1.0 + c / (white * white)) / (1.0 + c);
            c = saturate((c - 0.18) * (1.0 + _Contrast) + 0.18);
            return c;
        }
        ENDHLSL

        // Lens + 12-bit sensor approximation + neutral ISP. JPEG chroma subsampling and DCT
        // quantisation are supplied by Unity's real JPEG encoder at the selected quality.
        Pass
        {
            Name "Process"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0) uv.y = 1.0 - uv.y;
                #endif

                float3 raw = max(SampleOptics(uv) * _Exposure * _WhiteBalance, 0.0);
                float2 sensorPixel = floor(uv / abs(_MainTex_TexelSize.xy));
                float fixedPattern = Hash12(sensorPixel * 0.019 + 7.1) - 0.5;
                float temporal = Hash12(sensorPixel + _FrameSeed) - 0.5;

                // Shot noise grows with sqrt(signal); read noise grows with ISO. PRNU is fixed
                // per sensel and survives between photographs, unlike temporal shot noise.
                float shotSigma = 0.0060 * sqrt(max(PhotoLuminance(raw), 0.0001))
                                * sqrt(max(_IsoScale, 0.5));
                float readSigma = 0.0020 * pow(max(_IsoScale, 0.5), 0.68);
                raw += temporal * 3.464 * (shotSigma + readSigma);
                raw *= 1.0 + fixedPattern * 0.008;

                float hot = step(0.99994, Hash12(sensorPixel * 1.713 + 91.7));
                raw += hot * (0.18 + 0.22 * Hash12(sensorPixel + 41.0)) * _IsoScale;
                raw = floor(saturate(raw) * 4095.0 + 0.5) / 4095.0;

                float3 color = Tone(raw);

                // Tonal unsharp mask after the curve, as a camera ISP would do it.
                float2 dx = float2(abs(_MainTex_TexelSize.x), 0.0);
                float2 dy = float2(0.0, abs(_MainTex_TexelSize.y));
                float3 neighbour = (Tone(max(SampleOptics(uv + dx) * _Exposure * _WhiteBalance, 0.0))
                                  + Tone(max(SampleOptics(uv - dx) * _Exposure * _WhiteBalance, 0.0))
                                  + Tone(max(SampleOptics(uv + dy) * _Exposure * _WhiteBalance, 0.0))
                                  + Tone(max(SampleOptics(uv - dy) * _Exposure * _WhiteBalance, 0.0))) * 0.25;
                color = saturate(color + (color - neighbour) * _Sharpen);

                float2 p = uv * 2.0 - 1.0;
                float radius2 = dot(p, p);
                float cos4 = rcp((1.0 + 0.55 * radius2) * (1.0 + 0.55 * radius2));
                float mechanical = 1.0 - smoothstep(0.58, 1.45, radius2) * 0.28;
                color *= lerp(1.0, cos4 * mechanical, _VignetteStrength);

                float highlight = smoothstep(0.72, 1.0, max(raw.r, max(raw.g, raw.b)));
                float edge = saturate(radius2 * 0.7);
                color += float3(0.32, 0.03, 0.38) * highlight * edge * _PurpleFringe * 0.12;

                float grain = (Hash12(sensorPixel * 0.51 + _FrameSeed * 2.3) - 0.5)
                            * _GrainStrength * 0.022 * sqrt(max(_IsoScale, 0.5));
                color = saturate(color + grain);
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MeterLogLuminance"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMeter

            float4 FragMeter(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0) uv.y = 1.0 - uv.y;
                #endif
                float2 d = abs(_MainTex_TexelSize.xy) * 2.0;
                float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(d.x, d.y)).rgb;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d.x, d.y)).rgb;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(d.x, -d.y)).rgb;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - d).rgb;
                float logLuminance = log2(max(PhotoLuminance(c * 0.2), 0.00001));
                return logLuminance.xxxx;
            }
            ENDHLSL
        }

        // A separable focus transition applied only to the live view, after camera colour.
        Pass
        {
            Name "LiveFocus"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFocus
            float4 FragFocus(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0) uv.y = 1.0 - uv.y;
                #endif
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 0.227027;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + _FocusStep * 0.346154) * 0.316216;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - _FocusStep * 0.346154) * 0.316216;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + _FocusStep * 0.807692) * 0.070270;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - _FocusStep * 0.807692) * 0.070270;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
