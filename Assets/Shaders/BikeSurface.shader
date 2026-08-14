Shader "ToTheSummit/BikeSurface"
{
    // PROSEDÜREL YÜZEY. Model doku dosyası olmadan geliyor ve UV'si yok (FBX'te tek bir
    // UV katmanı bile bulunmuyor — ölçüldü). Bütün desen konumdan türüyor.
    //
    // DESEN NESNE UZAYINDA, DÜNYA UZAYINDA DEĞİL. Bisiklet hareket eden bir nesne:
    // dünya uzayında örneklenseydi sürerken desen yüzeyin üstünde kayardı — boya
    // bisikletle gitmez, dünyada asılı kalırdı. Nesne uzayı bunu kökten çözüyor.
    // Ölçek de düzeltiliyor: parça dönüşümlerinde yüz kat ölçek var, ham nesne uzayında
    // bir metrelik desen bir santime düşerdi.
    //
    // AŞINMA SEBEPLİ, RASTGELE DEĞİL: toz yukarı bakan yüzeyde birikiyor, boya yukarıda
    // güneşten soluyor, kir aşağıda topluyor. Yön dünya yukarısından okunuyor çünkü
    // sebebi yerçekimi.
    //
    // GEÇİŞLER YUMUŞAK. Her karışım smoothstep ile ve genlikler düşük; sert eşik keskin
    // leke bırakıyor ve yüzey "gürültü uygulanmış plastik" gibi okunuyor.
    Properties
    {
        _BaseColor       ("Renk", Color) = (0.45, 0.12, 0.08, 1)
        _Metallic        ("Metaliklik", Range(0,1)) = 0
        _Smoothness      ("Parlaklık", Range(0,1)) = 0.45

        _Variation       ("Renk oynaması", Range(0,0.3)) = 0.06
        _Grain           ("İnce doku (parlaklıkta)", Range(0,0.5)) = 0.15
        _Brushed         ("Fırça izi", Range(0,1)) = 0

        _DustColor       ("Toz rengi", Color) = (0.62, 0.60, 0.55, 1)
        _Dust            ("Toz miktarı", Range(0,1)) = 0.25
        _DustScale       ("Toz ölçeği (metre)", Range(0.02, 1)) = 0.18

        _Fade            ("Güneş soldurması", Range(0,1)) = 0.2
        _Grime           ("Alt kir", Range(0,1)) = 0.25

        // Tekerlek tek parça geliyor: lastik, jant ve göbek aynı mesh'te. Ayrı materyal
        // atanamıyor, o yüzden ayrım YARIÇAPTAN yapılıyor.
        _WheelMode       ("Tekerlek modu", Float) = 0
        _WheelCentre     ("Göbek (nesne uzayı, metre)", Vector) = (0,0,0,0)
        _WheelAxis       ("Dönme ekseni (nesne uzayı)", Vector) = (0,1,0,0)
        _WheelRadius     ("Dış yarıçap (metre)", Float) = 0.36
        _TireColor       ("Lastik rengi", Color) = (0.07, 0.07, 0.08, 1)
        _RimColor        ("Jant rengi", Color) = (0.58, 0.59, 0.61, 1)

        // ELLE BOYANAN YÜZEY köşede duruyor, materyalde değil: renk köşe renginde,
        // örtme gücü onun alfasında, yüzeyin ışığa cevabı ikinci UV kanalında. Renk
        // materyalde tutulsaydı bir yeri boyayıp rengini değiştirmek daha önce aynı
        // yuvayla boyanmış her yeri değiştirirdi.
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _Variation;
                half _Grain;
                half _Brushed;
                half4 _DustColor;
                half _Dust;
                half _DustScale;
                half _Fade;
                half _Grime;
                float _WheelMode;
                float4 _WheelCentre;
                float4 _WheelAxis;
                float _WheelRadius;
                half4 _TireColor;
                half4 _RimColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  colour     : COLOR;
                float2 surface    : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionMS : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                half4  paint      : TEXCOORD4;
                float2 surface    : TEXCOORD5;
            };

            /// Metrik nesne uzayı: nesne uzayı, dönüşümün ölçeğiyle çarpılmış. Desen
            /// nesneye yapışık kalıyor ama ölçüsü metrede kalıyor.
            float3 MetricObject(float3 positionOS)
            {
                float scale = length(float3(unity_ObjectToWorld._m00,
                                            unity_ObjectToWorld._m10,
                                            unity_ObjectToWorld._m20));
                return positionOS * scale;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.positionMS = MetricObject(input.positionOS.xyz);
                output.paint = input.colour;
                output.surface = input.surface;
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float3 cell)
            {
                return frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            /// Değer gürültüsü, BEŞİNCİ DERECE yumuşatmayla. Üçüncü derece (3t²-2t³)
            /// hücre sınırlarında ikinci türevi kırıyor ve ışık altında ızgara gibi
            /// okunuyordu; beşinci derece o kırılmayı bırakmıyor.
            float Noise(float3 position)
            {
                float3 cell = floor(position);
                float3 f = frac(position);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float n000 = Hash(cell + float3(0,0,0)), n100 = Hash(cell + float3(1,0,0));
                float n010 = Hash(cell + float3(0,1,0)), n110 = Hash(cell + float3(1,1,0));
                float n001 = Hash(cell + float3(0,0,1)), n101 = Hash(cell + float3(1,0,1));
                float n011 = Hash(cell + float3(0,1,1)), n111 = Hash(cell + float3(1,1,1));

                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            /// Dört katman. Genlikler ikiye bölünüyor, yani en kaba katman deseni
            /// belirliyor ve incesi yalnız kıpırdatıyor — tek katman lekeli duruyor.
            float Fbm(float3 position)
            {
                return Noise(position) * 0.53
                     + Noise(position * 2.1) * 0.27
                     + Noise(position * 4.3) * 0.13
                     + Noise(position * 8.7) * 0.07;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 p = input.positionMS;

                half3 albedo = _BaseColor.rgb;
                half metallic = _Metallic;
                half smoothness = _Smoothness;

                // TEKERLEK: lastik, jant ve göbek tek mesh'te geldiği için ayrım
                // yarıçaptan. Geçişler yarıçapın yüzde biriyle yumuşatılıyor; keskin
                // olsaydı dönerken titreşen bir halka olurdu.
                if (_WheelMode > 0.5)
                {
                    // Dönme ekseni DIŞARIDAN veriliyor. Modelin kendi ekseni ile Unity'nin
                    // ekseni aynı değil: FBX Z-yukarı geliyor, Unity dönüşü transform'a
                    // koyuyor ve mesh verisi kendi düzeninde kalıyor. Eksen varsayıldığında
                    // yarıçap yerine şerit hesaplanıyordu.
                    float3 axis = normalize(_WheelAxis.xyz);
                    float3 offset = p - _WheelCentre.xyz;
                    float3 radial = offset - axis * dot(offset, axis);

                    float r = length(radial) / max(1e-4, _WheelRadius);

                    float tire = smoothstep(0.82, 0.87, r);
                    float rim  = smoothstep(0.55, 0.62, r) * (1.0 - tire);
                    float hub  = 1.0 - max(tire, rim);

                    albedo = _TireColor.rgb * tire
                           + _RimColor.rgb * rim
                           + _RimColor.rgb * 0.75 * hub;

                    metallic = (1.0 - tire) * 0.8;
                    smoothness = lerp(0.5, 0.18, tire);
                }

                // ELLE BOYANAN YÜZEY. Renk köşeden geliyor, örtme gücü alfadan: her
                // fırça darbesi kendi rengini taşıyor. Yüzeyin ışığa cevabı ikinci UV
                // kanalındaki yuva numarasından — mat, yarı mat, metalik.
                half cover = saturate(input.paint.a);

                if (cover > 0.002)
                {
                    half slot = input.surface.x;
                    half paintMetallic = slot > 1.5 ? 0.85 : 0.0;
                    half paintSmooth = slot > 1.5 ? 0.32 : (slot > 0.5 ? 0.34 : 0.22);

                    albedo = lerp(albedo, input.paint.rgb, cover);
                    metallic = lerp(metallic, paintMetallic, cover);
                    smoothness = lerp(smoothness, paintSmooth, cover);
                }

                // Renk oynaması: tek düz renk boyanmış plastik gibi duruyor. Genlik
                // küçük ve ölçek büyük — göze desen olarak değil derinlik olarak giriyor.
                float variation = (Fbm(p * 3.1) - 0.5) * _Variation;
                albedo *= 1.0 + variation;

                float up = saturate(normalWS.y);

                // Boya yukarı bakan yüzeyde güneşten soluyor. Karesi alınmıyor: solma
                // eğik yüzeyde de oluyor, yalnız yatayda değil.
                float fade = up * _Fade;
                albedo = lerp(albedo, saturate(albedo * 1.3 + 0.02), fade);

                // Kir aşağıda topluyor: çamur sıçraması ve el değmeyen yüzler.
                float grime = saturate(-normalWS.y) * _Grime
                            * (0.45 + Fbm(p * 9.0) * 0.7);
                albedo = lerp(albedo, albedo * 0.5, grime);

                // Toz yatay yüzeyde birikiyor. Eşik değil rampa: dik yüzeyde sıfır,
                // yatayda tam, arası yumuşak.
                float dust = smoothstep(0.25, 0.95, up) * _Dust
                           * (0.5 + Fbm(p / max(0.02, _DustScale)) * 0.8);
                albedo = lerp(albedo, _DustColor.rgb, saturate(dust));

                // İnce doku PARLAKLIKTA, renkte değil. Gerçek boyada ve metalde göze
                // çarpan şey rengin lekelenmesi değil, yansımanın kıpırdaması.
                float grain = (Fbm(p * 60.0) - 0.5) * _Grain;

                // Fırça izi: bir eksende uzatılmış gürültü. Krom ve alüminyumda yüzey
                // izleri hep bir yöne bakar.
                float brushed = (Fbm(float3(p.x * 90.0, p.y * 6.0, p.z * 90.0)) - 0.5)
                              * _Brushed * 0.35;

                smoothness = saturate(smoothness + grain + brushed);
                smoothness *= (1.0 - dust * 0.7) * (1.0 - grime * 0.45);

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.fogCoord = input.fogCoord;
                lighting.bakedGI = SampleSH(normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                // Yakın düzleme kırpma: URP'nin kendi gölge geçişiyle aynı. Olmadığında
                // ışığa çok yakın yüzeyler gölge haritasından düşüyor ve nesne gölgesiz
                // görünüyor.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z,
                        output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z,
                        output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
