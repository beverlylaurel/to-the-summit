// ROL: kar yüzeyinin materyali. Clipmap halkaları bununla çiziliyor.
// Gölgelendirme §8: sarmalanmış diffüz, geçirgenlik, kenar yumuşatmalı parıltı,
// dört katman detay normal.

Shader "To The Summit/Snow Lit"
{
    Properties
    {
        [Header(Yuzey)]
        _AlbedoFresh ("Taze kar albedosu", Color) = (0.90, 0.92, 0.95, 1.0)
        _AlbedoPacked ("Sikismis kar albedosu", Color) = (0.72, 0.75, 0.81, 1.0)
        _TintWet ("Islak kar tonu", Color) = (0.84, 0.86, 0.89, 1.0)
        _ShadowTint ("Golge tonu", Color) = (0.66, 0.76, 0.95, 1.0)

        [Header(Isik)]
        _TranslucencyStrength ("Gecirgenlik", Range(0.0, 4.0)) = 1.0

        [Header(Parilti)]
        _SparkleIntensity ("Siddet", Range(0.0, 40.0)) = 12.0
        _SparkleCellSize ("Hucre boyu (m)", Range(0.0005, 0.02)) = 0.004
        _SparkleDensity ("Yogunluk", Range(0.0, 0.5)) = 0.06
        _SparkleSharpness ("Keskinlik", Range(1.0, 32.0)) = 8.0

        [Header(Detay)]
        _WindDetailStrength ("Ruzgar dalgasi", Range(0.0, 2.0)) = 1.0
    }

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
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex SnowVertex
            #pragma fragment SnowFragment

            // FORWARD+ ANAHTARLARININ TAMAMI. Eksik bırakılan bir tanesi GetMainLight'ı
            // ölü dala düşürüyor ve yüzey simsiyah çıkıyor; bu belirti bu projede bir
            // kez ölçüldü.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // Kalite seviyesi (§8.5). Global keyword; SnowQuality.ApplyKeywords açıyor.
            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH

            #include "SnowLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex SnowDepthVertex
            #pragma fragment SnowDepthFragment
            #include "SnowLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex SnowDepthNormalsVertex
            #pragma fragment SnowDepthNormalsFragment
            #include "SnowLitForwardPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}

