// ROL: kar yüzeyinin materyali — geometri, kenar kesme, detay normalleri,
// ışıklandırma ve parıltı.
// Çağıran: SnowSurface'in mesh renderer'ı.

Shader "ToTheSummit/SnowLit"
{
    Properties
    {
        [NoScaleOffset] _SnowBreakup ("Kenar gürültüsü", 2D) = "gray" {}
        [NoScaleOffset][Normal] _SnowDetailNormal ("Detay normali", 2D) = "bump" {}
        [NoScaleOffset] _SastrugiNoise ("Sastrugi gürültüsü", 2D) = "gray" {}

        _SnowBreakupScale ("Gürültü ölçeği (1/m)", Float) = 3.0
        _SnowEdgeFadeRange ("Kenar geçiş aralığı (m)", Float) = 0.02

        _ShadowTint ("Gölge rengi", Color) = (0.66, 0.76, 0.95, 1.0)
        _TranslucencyStrength ("Yarı saydamlık", Float) = 1.0

        _SparkleCellSize ("Parıltı hücre boyu (m)", Float) = 0.004
        _SparkleDensity ("Parıltı yoğunluğu", Float) = 0.06
        _SparkleSharpness ("Parıltı keskinliği", Float) = 8.0
        _SparkleIntensity ("Parıltı şiddeti", Float) = 12.0

        _SnowAORadius ("İz içi AO yarıçapı (m)", Float) = 0.10
        _SnowAOStrength ("İz içi AO şiddeti", Range(0, 1)) = 1.0
    }

    SubShader
    {
        // ARAZİ ÖNCE, KAR SONRA (spec §8.3). Böylece karın `clip()` ettiği
        // yerlerde arazinin derinliği zaten yazılmış oluyor.
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+50"
        }

        Pass
        {
            Name "SnowForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SnowLitVertex
            #pragma fragment SnowLitFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            // Kalite kademeleri (spec §15.3): detay normal katmanı sayısı ve
            // parıltı bu keyword'lerle açılıp kapanıyor.
            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH

            #include "SnowLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SnowShadowVertex
            #pragma fragment SnowShadowFragment

            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH

            #include "SnowLitShadowPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SnowDepthNormalsVertex
            #pragma fragment SnowDepthNormalsFragment

            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH

            #include "SnowLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
