// ROL: kar yüzeyinin materyali. Faz 4'te geometri + normal + kenar kesme;
// ışıklandırma Faz 6'da tamamlanıyor.
// Çağıran: SnowClipmap'in halka renderer'ları.

Shader "ToTheSummit/SnowLit"
{
    Properties
    {
        [NoScaleOffset] _SnowBreakup ("Kenar gürültüsü", 2D) = "gray" {}
        _SnowBreakupScale ("Gürültü ölçeği (1/m)", Float) = 3.0
        _SnowEdgeFadeRange ("Kenar geçiş aralığı (m)", Float) = 0.02
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
            #pragma multi_compile_fog

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

            #include "SnowLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
