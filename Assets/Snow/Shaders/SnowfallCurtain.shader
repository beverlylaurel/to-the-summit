// ROL: uzak yağış katmanının tek perdesini çizer (spec §17.2).
// Çağıran: SnowfallCurtains, quad başına bir çizim.

Shader "Snow/SnowfallCurtain"
{
    Properties
    {
        _MainTex ("Kar tanesi gürültüsü", 2D) = "black" {}
        _Tiling ("Döşeme", Float) = 4
        _Alpha ("Katman alpha", Float) = 0.1
        _ScrollSpeed ("Dikey kayma hızı", Float) = 0.25
        _WindUV ("Rüzgâr UV kayması", Vector) = (0,0,0,0)
        _Tint ("Renk", Color) = (0.78, 0.84, 0.95, 1)
    }

    SubShader
    {
        // SAHNE GEOMETRİSİNİN ARKASINDA KALIYOR (spec §17.2):
        // `Depth Write = Off`, `ZTest LEqual`. Perde kameraya kilitli ama
        // dağın önüne geçmiyor — 55 m'deki perde 30 m'deki sırtın arkasında.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SnowfallCurtain"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float  _Tiling;
                float  _Alpha;
                float  _ScrollSpeed;
                float4 _WindUV;
                float4 _Tint;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // UV KAYDIRMA C# TARAFINDA BİRİKMİYOR, BURADA (spec §17.2):
                // `uv += (_WindWS.xz * 0.12 + float2(0, -_ScrollSpeed)) * time`.
                // Zaman shader'dan geliyor; C#'ta biriktirilseydi kare hızına
                // bağlı sürüklenme olurdu.
                float2 scroll = _WindUV.xy + float2(0.0, -_ScrollSpeed);

                o.uv = input.uv * _Tiling + scroll * _Time.y;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;

                // PERDE GECE KARARIYOR. Sabit renk bırakılırsa uzaktaki kar
                // gece de gündüzki parlaklıkta duruyor. Taban terim ambient'in
                // yerine geçiyor: perde tamamen sönmesin, ama güneşle birlikte
                // parlasın.
                Light mainLight = GetMainLight();
                half3 lit = _Tint.rgb * (0.25h + mainLight.color * 0.75h);

                return half4(lit, a * _Alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
