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

        [Header(Yuzey dokulari)]
        [NoScaleOffset] _SnowSurfTazeColor ("Taze - renk", 2D) = "white" {}
        [NoScaleOffset][Normal] _SnowSurfTazeNormal ("Taze - normal", 2D) = "bump" {}
        [NoScaleOffset] _SnowSurfTazeRough ("Taze - pürüzlülük", 2D) = "gray" {}
        [NoScaleOffset] _SnowSurfTozColor ("Toz - renk", 2D) = "white" {}
        [NoScaleOffset][Normal] _SnowSurfTozNormal ("Toz - normal", 2D) = "bump" {}
        [NoScaleOffset] _SnowSurfTozRough ("Toz - pürüzlülük", 2D) = "gray" {}
        [NoScaleOffset] _SnowSurfYerlesmisColor ("Yerleşmiş - renk", 2D) = "white" {}
        [NoScaleOffset][Normal] _SnowSurfYerlesmisNormal ("Yerleşmiş - normal", 2D) = "bump" {}
        [NoScaleOffset] _SnowSurfYerlesmisRough ("Yerleşmiş - pürüzlülük", 2D) = "gray" {}
        [NoScaleOffset] _SnowSurfRuzgarColor ("Rüzgâr - renk", 2D) = "white" {}
        [NoScaleOffset][Normal] _SnowSurfRuzgarNormal ("Rüzgâr - normal", 2D) = "bump" {}
        [NoScaleOffset] _SnowSurfRuzgarRough ("Rüzgâr - pürüzlülük", 2D) = "gray" {}
        _SnowSurfTileMeters ("Yüzey dokusu döşeme boyu (m)", Float) = 2.5
        _SnowSurfStrength ("Yüzey dokusu gücü", Range(0,1)) = 0.65

        _SnowBreakupScale ("Gürültü ölçeği (1/m)", Float) = 3.0
        _SnowEdgeFadeRange ("Kenar geçiş aralığı (m)", Float) = 0.006


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

            // BULUT GÖLGESİ. Arazi bunu okuyor, kar mesh'i okumuyordu: bulutun
            // altında arazi kararırken oyuncunun çevresindeki kar aynı
            // parlaklıkta kalıyor ve ekranda takip eden bir KARE oluşuyordu.
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

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
