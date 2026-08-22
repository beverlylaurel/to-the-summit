// ROL: GPU'da simüle edilen kar tanelerini ve yer savrulmasını çizer.
// Çağıran: SnowfallRenderer (Graphics.RenderPrimitives).

Shader "ToTheSummit/SnowfallParticle"
{
    Properties
    {
        [NoScaleOffset] _FlakeAtlas ("Tane atlası (4×4)", 2D) = "white" {}
        _FlakeTint ("Renk", Color) = (1, 1, 1, 1)
        _FlakeEmissive ("Işıma", Float) = 1.0

        _MinPixelSize ("Asgari ekran boyu (piksel)", Float) = 1.3
        _SoftFade ("Yumuşak parçacık mesafesi (m)", Float) = 0.4

        _StretchAlongVelocity ("Hız yönünde uzat", Float) = 0
        _StretchMin ("Asgari uzama", Float) = 1.0
        _StretchMax ("Azami uzama", Float) = 3.0
        _AlphaScale ("Alpha çarpanı", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "SnowfallParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct SnowFlake
            {
                float3 position;
                float  age;
                float3 velocity;
                float  lifetime;
                float  size;
                float  phase;
                float  frame;
                float  alpha;
            };

            StructuredBuffer<SnowFlake> _Flakes;

            TEXTURE2D(_FlakeAtlas);
            SAMPLER(sampler_FlakeAtlas);

            float4 _FlakeTint;
            float  _FlakeEmissive;
            float  _MinPixelSize;
            float  _SoftFade;
            float  _StretchAlongVelocity;
            float  _StretchMin;
            float  _StretchMax;
            float  _AlphaScale;

            /// Sis yoğunluğu MEVCUT sis sisteminden okunuyor (spec §3.7).
            float _FogDensity01;

            /// Rüzgâr hızı — uzatma bundan geliyor.
            float _WindSpeed;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float  viewDepth  : TEXCOORD4;
            };

            static const float2 kCorners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5,  0.5), float2(-0.5, 0.5)
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings OUT = (Varyings)0;

                SnowFlake f = _Flakes[instanceID];

                // Kapalı yuva: dejenere üçgen. Ayrı bir sayaç ve indirect
                // argüman tamponu tutmaktan ucuz.
                if (f.lifetime <= 0.0 || f.alpha <= 0.001)
                {
                    OUT.positionCS = float4(0, 0, -10, 1);
                    return OUT;
                }

                float2 corner = kCorners[vertexID];

                float3 toCam = _WorldSpaceCameraPos - f.position;
                float  dist = length(toCam);

                // ASGARİ EKRAN BOYU ZORUNLU (spec §17.1). Uygulanmazsa
                // uzaktaki taneler alt piksel kalıyor, kayboluyor ve TAA'da
                // titriyor.
                //
                // ÖLÇEK MEVCUT YAĞIŞ SHADER'INDAKİYLE AYNI İFADE:
                // `Precipitation.shader` → `PixelsPerRadian()`. `abs` ŞART —
                // D3D render hedefine çizerken projeksiyonun [1][1] öğesi
                // NEGATİFE düşüyor. Kendi ifademi yazınca `max(-1.73, 1e-4)`
                // 1e-4 verdi, ölçek 10000 katına çıktı ve 23 m uzaktaki tane
                // 680 m'lik bir dörtgen oldu: ekran bembeyaz, 10 FPS
                // (ölçüldü — `SYMPTOMS.md`).
                float pixelsPerRadian = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
                float minWorld = dist * _MinPixelSize / max(pixelsPerRadian, 1e-4);

                float size = max(f.size, minWorld);

                // ALT PİKSEL TANENİN IŞIĞI PİKSELE ORANLI DÜŞER.
                //
                // Asgari ekran boyu tanenin boyunu büyütüyor ama ışığını
                // artırmıyor: 5 mm'lik tane, 3 cm'lik tane kadar parlak
                // çiziliyordu ve ekranda ikisi de aynı büyüklükte tek tip
                // nokta oluyordu ("irili ufaklı değil" — ölçümde tanelerin
                // %92'si tabana dayanmıştı). Alan oranı kadar soldurmak
                // alt piksel bir yayıcının doğru integralidir; boy farkı
                // artık parlaklık farkı olarak taşınıyor.
                float subPixel = saturate((f.size * f.size) / max(size * size, 1e-12));

                // Kameraya bakan düzlem + ömür boyu dönüş.
                float3 forward = toCam / max(dist, 1e-4);
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);

                float roll = f.phase + f.age * 1.5708;      // ±90°/s
                float2 rc = float2(cos(roll), sin(roll));

                float2 rotated = float2(corner.x * rc.x - corner.y * rc.y,
                                        corner.x * rc.y + corner.y * rc.x);

                float3 offset = (right * rotated.x + up * rotated.y) * size;

                // RÜZGÂRDA HIZ YÖNÜNDE UZAMA (spec §17.1).
                if (_StretchAlongVelocity > 0.5)
                {
                    float3 velDir = normalize(f.velocity + 1e-5);
                    float3 screenVel = normalize(velDir - forward * dot(velDir, forward) + 1e-5);
                    float3 screenSide = cross(forward, screenVel);

                    // Spec §17.1: tane sisteminde 1→3×, Sistem B'de (yer
                    // savrulması) 4–8×. Alt sınır ayrı bir parametre; ikisine
                    // aynı sayıyı vermek savrulmayı tane gibi gösteriyordu.
                    float stretch = lerp(_StretchMin, _StretchMax, saturate(_WindSpeed / 12.0));

                    offset = (screenVel * rotated.y * stretch + screenSide * rotated.x) * size;
                }

                float3 positionWS = f.position + offset;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDepth = -TransformWorldToView(positionWS).z;

                // 4×4 atlas, tane başına sabit kare.
                float2 cell = float2(fmod(f.frame, 4.0), floor(f.frame / 4.0));
                OUT.uv = (corner + 0.5 + cell) * 0.25;

                // SİS FADE'İ (spec §3.7, §17.1). Uygulanmazsa sisin içinde
                // asılı beyaz noktalar kalıyor.
                float fogCut = lerp(120.0, 35.0, saturate(_FogDensity01));
                float fogFade = 1.0 - saturate(dist / max(fogCut, 1.0));

                float alpha = f.alpha * fogFade * _AlphaScale * subPixel;

                // SPEC §17.1: "Output Particle Lit Quad", Metallic 0,
                // Smoothness 0.2, `Emissive = _FlakeEmissive * mainLightColor
                // * 0.04` (gece lambaların altında görünsünler).
                //
                // AYDINLATMA QUAD'IN KENDİ NORMALİNDEN. Uydurma bir taban
                // katsayısı yok: "Lit Quad" aydınlatılmış bir yüzey demek,
                // yüzeyin normali de kameraya bakan düzlemin normali.
                Light mainLight = GetMainLight();

                half3 N = (half3)forward;
                half3 lit = SampleSH(N) + mainLight.color * saturate(dot(N, mainLight.direction));
                half3 emissive = mainLight.color * _FlakeEmissive * 0.04h;

                OUT.color = float4(_FlakeTint.rgb * lit + emissive, alpha);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_FlakeAtlas, sampler_FlakeAtlas, IN.uv);

                half alpha = tex.a * IN.color.a;

                // YUMUŞAK PARÇACIK: zemine değen tane keskin bir kenar
                // bırakmasın.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);

                alpha *= saturate((sceneDepth - IN.viewDepth) / max(_SoftFade, 1e-3));

                clip(alpha - 0.002);

                half3 color = IN.color.rgb * tex.rgb;
                color = MixFog(color, IN.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
