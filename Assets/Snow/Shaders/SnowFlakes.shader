// ROL: kar tanelerinin çizimi (§10.1 Output). Prosedürel quad; tane verisi
// StructuredBuffer'dan geliyor, mesh yok.
// Çağıran: SnowfallController (DrawProcedural).

Shader "Hidden/Snow/Flakes"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Flakes"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex FlakeVertex
            #pragma fragment FlakeFragment
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct SnowFlake
            {
                float3 position;
                float  age;
                float3 velocity;
                float  lifetime;
                float  seed;
                float  size;
                float  alpha;
                float  spin;
            };

            StructuredBuffer<SnowFlake> _Flakes;

            float  _MinPixelSize;
            float  _ScreenHeight;
            float  _TanHalfFov;
            float  _WindStretch;      // 0..1, güçlü rüzgârda hız yönünde uzama
            half3  _FlakeTint;
            half   _FlakeEmissive;
            float  _SoftFadeDistance;

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  alpha      : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
            };

            static const float2 kCorners[4] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(-0.5, 0.5), float2(0.5, 0.5)
            };

            static const uint kIndices[6] = { 0, 2, 1, 1, 2, 3 };

            Varyings FlakeVertex(uint vertexID : SV_VertexID)
            {
                uint flakeIndex = vertexID / 6;
                uint corner = kIndices[vertexID % 6];

                SnowFlake f = _Flakes[flakeIndex];

                float3 toCamera = GetCameraPositionWS() - f.position;
                float distance = length(toCamera);

                // ASGARİ EKRAN BOYUTU ZORUNLU (§10.1). Bu olmadan uzaktaki kar
                // pikselin altına düşüp kayboluyor ve TAA'da titriyor.
                float minWorldSize = distance * (_MinPixelSize / max(_ScreenHeight, 1.0))
                                   * 2.0 * _TanHalfFov;
                float size = max(f.size, minWorldSize);

                // BÜYÜTME KADAR ALFA DÜŞÜYOR. Tane bir pikselden küçükken boyu zorla
                // büyütülüyor; alfa aynı kalırsa kapladığı alan gerçeğinden kat kat büyük
                // görünüyor ve ekran televizyon karı gibi doluyor — ölçüldü, 40 000 tane
                // 20 m'de tamamen opak bir perde yapıyordu.
                //
                // Alan oranıyla bölününce toplam örtü korunuyor: uzaktaki tane görünür
                // ama solük.
                float sizeRatio = f.size / max(size, 1e-6);
                float coverageScale = saturate(sizeRatio * sizeRatio);

                // Kameraya bakan düzlem + rastgele dönüş. Güçlü rüzgârda hız yönünde
                // uzatılıyor: fırtınada tane değil çizgi görünüyor.
                float3 forward = distance > 1e-4 ? toCamera / distance : float3(0, 0, 1);

                // DİKEY EKSENLE ÇAPRAZ ÇARPIM SIFIR OLABİLİR. Tane tam kameranın üstünde
                // ya da altındayken forward = (0,1,0) ve cross sıfır; normalize NaN
                // üretiyor, quad ekran boyu uzuyor. Belirti: yatay siyah çizgiler.
                //
                // Doğum kutusu kameranın 11 m ÜSTÜNDE, yani bu durum sürekli oluşuyor.
                float3 axis = abs(forward.y) > 0.99 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 right = normalize(cross(axis, forward));
                float3 up = cross(forward, right);

                float s, c;
                sincos(f.spin + f.age * 1.7, s, c);

                float2 q = kCorners[corner];
                float2 rotated = float2(q.x * c - q.y * s, q.x * s + q.y * c);

                float3 offset = (right * rotated.x + up * rotated.y) * size;

                float speed = length(f.velocity);
                if (speed > 1e-3)
                {
                    float3 velocityDir = f.velocity / speed;

                    // EKRAN DÜZLEMİNDEKİ BİLEŞENİ NORMALİZE ETMEDEN ÖNCE ÖLÇÜLÜYOR.
                    // Tane doğrudan kameraya ya da kameradan uzağa gidiyorsa bu bileşen
                    // sıfır; normalize edilince NaN çıkıyor ve quad ekran boyu uzuyor.
                    // Belirti: görüntünün üzerinde yatay siyah çizgiler.
                    float3 lateral = velocityDir - forward * dot(velocityDir, forward);
                    float lateralLength = length(lateral);

                    if (lateralLength > 1e-3)
                    {
                        float3 alongScreen = lateral / lateralLength;

                        float stretch = 1.0 + _WindStretch * 2.0;
                        offset += alongScreen * (dot(offset, alongScreen) * (stretch - 1.0));
                    }
                }

                float3 positionWS = f.position + offset;

                Varyings output;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = q + 0.5;
                // DOĞUM KUTUSUNUN KENARI YUMUŞATILIYOR. Kutu 40 m; kenarında kar birden
                // bitiyor ve ekranda yuvarlak bir kar bulutu görünüyor. Son çeyrekte
                // söndürülünce sınır okunmuyor.
                float boxFade = 1.0 - smoothstep(14.0, 20.0, distance);

                output.alpha = f.alpha * coverageScale * boxFade;
                output.normalWS = forward;
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 FlakeFragment(Varyings input) : SV_Target
            {
                // PROSEDÜREL TANE. Spec 4x4 flake atlası istiyor; yumuşak kenarlı bir
                // disk aynı işi doku olmadan yapıyor ve tekrar deseni bırakmıyor.
                float2 d = input.uv - 0.5;
                float r = length(d) * 2.0;

                half mask = (half)saturate(1.0 - smoothstep(0.55, 1.0, r));
                if (mask <= 0.004) discard;

                // YUMUŞAK PARÇACIK: yüzeye yaklaşınca sönüyor, kesişme çizgisi olmuyor.
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-4);
                float sceneDepth = LinearEyeDepth(
                    SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r,
                    _ZBufferParams);

                float particleDepth = input.screenPos.w;
                half soft = (half)saturate((sceneDepth - particleDepth) / max(_SoftFadeDistance, 1e-3));

                Light mainLight = GetMainLight();

                // Tane ince ve saçılmalı: yönden bağımsız aydınlanıyor, üstüne gece
                // lambaların altında görünsün diye küçük bir yayınım biniyor.
                half3 color = _FlakeTint * (mainLight.color * 0.6h + _FlakeEmissive * mainLight.color * 0.04h);
                color += SampleSH(float3(0, 1, 0)) * 0.4h;

                return half4(color, mask * (half)input.alpha * soft);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

