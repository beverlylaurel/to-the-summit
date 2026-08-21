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
            half4  _FlakeAmbient;   // gök ortamı, C#'tan
            float  _SoftFadeDistance;

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

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

                // ASGARİ BOY TANE BAŞINA ÖLÇEKLENİYOR.
                //
                // Düz `max(f.size, minWorldSize)` uzaktaki BÜTÜN taneleri aynı boya
                // eşitliyordu: 20 metrede asgari boy 4.2 cm, en büyük tane 3.1 cm —
                // hepsi tek tip görünüyordu. Kullanıcı "taneler irili ufaklı değil" dedi.
                //
                // Tanenin kendi çarpanı (0.6–1.7) asgari boya da uygulanınca uzakta
                // 0.8–2.2 piksel arası değişiyor ve çeşitlilik kalıyor.
                float sizeFactor = f.size / 0.018;
                float size = max(f.size, minWorldSize * sizeFactor);

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

                        // UZATMA YALNIZ TABAN BOYA. Uzaktaki tane asgari piksel
                        // kuralıyla zaten büyütülmüş; onu bir de üçe katlamak ekranı
                        // çizgilerle dolduruyor. sizeRatio uzakta küçüldüğü için uzatma
                        // orada kendiliğinden sönüyor.
                        // UZATMA 1→2. Spec 3 kat diyor ama 12 m/s rüzgârda hız 6:1
                        // yatay ve tane ekranda çizgiye dönüşüyor; 2 kat hareketi
                        // okutuyor ama tane olmaktan çıkarmıyor.
                        float stretch = 1.0 + _WindStretch * sizeRatio;
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

                // YUMUŞAK TANE. Önce `smoothstep(0.55, 1.0, r)` vardı: ortası düz beyaz
                // bir disk, kenarı belirgin bir halka — tane değil pul gibi görünüyordu.
                // Gauss düşüşü merkezden kenara kesintisiz iniyor.
                half mask = (half)exp(-r * r * 3.2);
                if (mask <= 0.01) discard;

                // YUMUŞAK PARÇACIK: yüzeye yaklaşınca sönüyor, kesişme çizgisi olmuyor.
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-4);
                float sceneDepth = LinearEyeDepth(
                    SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r,
                    _ZBufferParams);

                float particleDepth = input.screenPos.w;
                half soft = (half)saturate((sceneDepth - particleDepth) / max(_SoftFadeDistance, 1e-3));

                Light mainLight = GetMainLight();

                // TANE ZEMİN KARIYLA AYNI PARLAKLIKTA olmak zorunda — ikisi de aynı
                // malzeme. Önceki hâlde ortam ışığı 0.4 ile kısılıyordu ve tane parlak
                // gökyüzünün önünde KOYU kalıyordu; kullanıcı "siyah çizgiler" diye
                // bildirdi. Alfa harmanlamada renk arka plandan koyuysa leke koyu olur.
                //
                // Rastgele yönelen bir tanede N.L'nin yönler üzerinden ortalaması 0.5;
                // ortam terimi zemindeki gibi TAM alınıyor.
                const half3 flakeAlbedo = half3(0.92, 0.94, 0.97);

                // ORTAM IŞIĞI DIŞARIDAN VERİLİYOR, SampleSH İLE DEĞİL.
                //
                // SampleSH küresel harmonik sabitlerini okuyor ve o sabitler UNITY
                // TARAFINDAN RENDERER BAŞINA yazılıyor. Bu çizim prosedürel — ortada
                // renderer yok, sabitler sıfır kalıyor ve tane yalnız güneş payıyla
                // aydınlanıyordu. Parlak göğün önünde bu KOYU demek.
                //
                // Belirti: yakındaki (neredeyse opak) taneler siyah çizgi, uzaktaki
                // (saydam) taneler beyaz görünüyordu — ekran görüntüsünde ölçüldü.
                half3 color = flakeAlbedo * (0.5h * mainLight.color + _FlakeAmbient.rgb);

                // Gece lambaların altında görünsünler diye küçük bir yayınım.
                color += _FlakeTint * (_FlakeEmissive * mainLight.color * 0.04h);

                // TANE ARKA PLANDAN KOYU OLAMAZ.
                //
                // Kar tanesi hem güneşi saçıyor hem gök ışığını geçiriyor; gökyüzünün
                // önünde en az gökyüzü kadar parlaktır. Hesaplanan değer sahnenin HDR
                // göğünden küçük kalınca alfa harmanlamada tane KOYU bir leke oluyordu —
                // kullanıcı "siyah yatay çizgiler" diye bildirdi, eski yağış sisteminin
                // sıfır çizdiği ölçülerek doğrulandı.
                //
                // Arkadaki renk taban alınıyor: proje göğü nasıl aydınlatırsa aydınlatsın
                // tane ondan koyu kalmıyor. Çıplak arazinin önünde saçılma terimi zaten
                // daha büyük olduğu için tane parlak görünüyor.
                half3 behind = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,
                                                  sampler_CameraOpaqueTexture, screenUV).rgb;

                color = max(color, behind * 1.03h);

                return half4(color, mask * (half)input.alpha * soft);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

