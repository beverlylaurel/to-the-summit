// include-rev: 80  (Unity, .hlsl degisince .shader'i yeniden
// derlemeyebiliyor; bu satir degisince derleme zorlanir)
Shader "ToTheSummit/MountainSurface"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"

            // Unity terrain'i özel bir yoldan çiziyor ve nesne başına ışık verisini
            // yalnızca kendini terrain uyumlu ilan eden shader'lara veriyor. Etiket
            // olmadan unity_LightData sıfır kalıyor, doğrudan güneş tamamen kesiliyor
            // ve geriye yalnızca ambient kalıyor.
            "TerrainCompatible" = "True"
        }

        // Terrain kendi materyalini instancing ile çizebiliyor; kapalı olsa bile
        // varyantın bulunması gerekiyor.
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SnowTessVertexStage
            #pragma hull SnowHull
            #pragma domain ForwardDomain
            #pragma fragment Fragment
            #pragma target 4.6

            // ARAZİNİN GÖLGESİ İKİ KAYNAKTAN. Dağın kendi sırtı yükseklik alanından
            // yürüyerek bulunuyor (bkz. TerrainSunShadow) — gölge haritası o mesafeyi
            // taşımıyor, elli metrede bitiyor. Ama HAREKETLİ NESNELER haritada: bisiklet,
            // oyuncu, ileride kaya ve çadır. Harita okunmadığı sürece bunların hiçbiri
            // yere gölge düşürmüyordu.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            // Renderer Forward+ modunda: ışıklar kümelerle dağıtılıyor ve nesne başına
            // ışık verisi doldurulmuyor. Bu keyword bildirilmezse GetMainLight() eski
            // dala düşüp doldurulmamış unity_LightData'yı okuyor, güneş tamamen kesiliyor.
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // Bulut gölgesi bu anahtarla geliyor: bulut sistemi gölgeyi ana ışığın cookie
            // dokusuna yazıyor, URP de onu burada uyguluyor.
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // EKRAN UZAYI ÖRTÜŞME GÖLGESİ ARAZİDE OKUNMUYOR. Derinlik tamponundan
            // çalışıyor ve arazi örgüsünün üçgen yüzeylerini yüzey kıvrımı sanıp zemine
            // yumuşak kafes çizgileri çiziyor (bkz. `DECISIONS.md` — SSAO kapalı).
            // Büyük ölçekli oyuk gölgesini pişmiş maruziyet kanalı zaten veriyor.
            //
            // Özellik boru hattında AÇIK: yakın plan nesneler (bisiklet, ekipman, çadır)
            // onu okuyor ve kuytuları kararıyor. Anahtar burada bildirilmediği için
            // arazi etkilenmiyor.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _TerrainShadowReceive;
            #include "MountainSurface.hlsl"
            #include "SnowTessellation.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float  fogFactor   : TEXCOORD2;
            };

            // Köşe aşaması yalnız konumu taşıyor: asıl iş bölünmeden SONRA, domain'de.
            // Yer değiştirme burada yapılsaydı 4.28 metrelik ızgarada uygulanır ve
            // birikintinin şekli hiç çözülemezdi.
            TessellationControlPoint SnowTessVertexStage(Attributes IN)
            {
                return SnowTessVertex(IN.positionOS);
            }

            [domain("tri")]
            Varyings ForwardDomain(TessellationFactors factors,
                                   const OutputPatch<TessellationControlPoint, 3> patch,
                                   float3 barycentric : SV_DomainLocation)
            {
                Varyings OUT;

                float3 positionWS = SnowDomainPositionWS(patch, barycentric);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            /// UniversalFragmentPBR'ın açık hali — çünkü ana ışığın gölgesini bizim
            /// yürüyüşümüz veriyor ve hazır fonksiyonun içine dışarıdan gölge
            /// enjekte etmenin bir yolu yok. Parçalar yine URP'nin kendi fonksiyonları:
            /// BRDF, ışık başına katkı, SSAO birleşimi. Kaybedilen tek şey yansıma
            /// küresi örneklemesi — sahnede yansıma küresi yok, yüzey de mat.
            half4 Fragment(Varyings IN) : SV_Target
            {
                MountainSurface surface = BuildMountainSurface(IN.positionWS);

                // Forward+ ışık döngüsü makroları bu değişkeni adıyla okuyor
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = surface.normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(surface.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half alpha = 1.0;
                BRDFData brdfData;
                InitializeBRDFData(surface.albedo, 0.0, half3(0.0, 0.0, 0.0),
                    surface.smoothness, alpha, brdfData);

                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(
                    float2(0.0, 0.0), surface.occlusion);

                // Arazinin kendi gölgesi yükseklik alanından. Işığa sırtı dönük piksel
                // yürümüyor: katkısı zaten sıfır, kırk adım boşa giderdi.
                Light mainLight = GetMainLight();
                mainLight.shadowAttenuation =
                    dot(inputData.normalWS, mainLight.direction) > 0.0
                        ? TerrainSunShadow(IN.positionWS, mainLight.direction)
                        : 1.0;

                // Hareketli nesnelerin gölgesi haritadan ve arazininkiyle ÇARPILIYOR:
                // ikisi ayrı olay — biri sırtın arkasında kalmak, öteki üstünde bir cisim
                // durmak. Aynı kanaldan gidiyorlar çünkü ikisi de doğrudan güneşi kesiyor.
                // TEŞHİS ANAHTARI: `_TerrainShadowReceive` sıfırken gölge haritası hiç
                // okunmuyor. Arazi ekranın çoğunu kaplıyor ve bu okuma piksel başına
                // yapılıyor — kare süresindeki payı ancak kapatıp ölçerek bilinir.
                if (_TerrainShadowReceive > 0.5)
                    mainLight.shadowAttenuation *=
                        MainLightRealtimeShadow(TransformWorldToShadowCoord(IN.positionWS));

                // BULUT GÖLGESİ bulut sisteminin kendi cookie dokusundan geliyor; gökyüzünü
                // çizen yoğunluk alanının ta kendisi. Doğrudan güneşi kesiyor, gökten gelen
                // dolaylı ışığa dokunmuyor — arazi gölgesiyle aynı kanaldan.
            #ifdef _LIGHT_COOKIES
                mainLight.color *= SampleMainLightCookie(IN.positionWS);
            #endif

                half3 lit = inputData.bakedGI * aoFactor.indirectAmbientOcclusion * brdfData.diffuse;
                lit += LightingPhysicallyBased(brdfData, mainLight,
                    inputData.normalWS, inputData.viewDirectionWS) * aoFactor.directAmbientOcclusion;

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                    lit += LightingPhysicallyBased(brdfData, light,
                        inputData.normalWS, inputData.viewDirectionWS);
                LIGHT_LOOP_END
                #endif

                lit += surface.emission;

                half4 color = half4(lit, 1.0);

                // Unity'nin sisi yerine yükseklik sisi: yoğunluk alçakta toplanır,
                // yükseldikçe seyrelir. İkisi birlikte uygulanırsa sönüm iki kez sayılır.
                color.rgb = ApplyHeightFog(color.rgb, _WorldSpaceCameraPos, IN.positionWS);

                return color;
            }
            ENDHLSL
        }

        // Gölge ve derinlik geçişleri ELLE YAZILDI. Önceden URP'nin hazır dosyalarından
        // geliyordu ve gerekçesi doğruydu (gölge sapması gibi tuzakları bize taşımasın).
        // Kar yer değiştirmesi geldiğinde zorunlu oldu: o dosyalar kendi vertex
        // fonksiyonlarını getiriyor, yer değiştirme oraya giremiyor. Girmezse gölge
        // birikintinin ALTINDA kalır ve bulut derinliği yanlış okur.
        //
        // Sapma yine URP'nin kendi fonksiyonuyla (`ApplyShadowBias`) uygulanıyor —
        // elle yazılan yalnız köşe akışı.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex SnowTessVertexStage
            #pragma hull SnowHull
            #pragma domain ShadowDomain
            #pragma fragment ShadowFragment
            #pragma target 4.6
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "SnowTessellation.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            TessellationControlPoint SnowTessVertexStage(Attributes IN)
            {
                return SnowTessVertex(IN.positionOS);
            }

            [domain("tri")]
            Varyings ShadowDomain(TessellationFactors factors,
                                  const OutputPatch<TessellationControlPoint, 3> patch,
                                  float3 barycentric : SV_DomainLocation)
            {
                Varyings OUT;

                float3 positionWS = SnowDomainPositionWS(patch, barycentric);

                // Sapma normale göre uygulanıyor; yer değiştirmiş yüzeyin normali
                // arazininkinden farklı, o yüzden düz yukarı değil gerçek normal.
                float2 uv = (positionWS.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
                float2 packed = SAMPLE_TEXTURE2D_LOD(_GroundNormals, sampler_GroundNormals,
                                                     uv, 0).rg * 2.0 - 1.0;
                float3 baseNormal = normalize(float3(packed.x,
                    sqrt(saturate(1.0 - dot(packed, packed))), packed.y));
                float3 normalWS = SnowDisplacedNormal(positionWS, baseNormal);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex SnowTessVertexStage
            #pragma hull SnowHull
            #pragma domain DepthDomain
            #pragma fragment DepthFragment
            #pragma target 4.6

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "SnowTessellation.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            TessellationControlPoint SnowTessVertexStage(Attributes IN)
            {
                return SnowTessVertex(IN.positionOS);
            }

            [domain("tri")]
            Varyings DepthDomain(TessellationFactors factors,
                                 const OutputPatch<TessellationControlPoint, 3> patch,
                                 float3 barycentric : SV_DomainLocation)
            {
                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(
                    SnowDomainPositionWS(patch, barycentric));
                return OUT;
            }

            half4 DepthFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // SSAO Source: DepthNormals okuyor; bu geçiş olmadan normal tamponu boş kalır
        // ve ambient occlusion terrain üzerinde çöp okur.
        //
        // Standart geçiş KÖŞE normalini yazar; SSAO onu okuyunca arazi örgüsünün
        // üçgen kırıklarını "yüzey kıvrımı" sanıp gölgeliyor ve zeminde, örgüyü
        // andıran yumuşak kafes çizgileri beliriyordu — 30 metrelik falloff'uyla
        // yalnız yakında, dünyaya çakılı, saatten bağımsız. Uzun bir eleme avının
        // sonunda bulundu. Işıklandırmanın kullandığı pürüzsüz pişmiş normal yazılır:
        // iki tüketici aynı yüzeyi görür, çelişemezler.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex SnowTessVertexStage
            #pragma hull SnowHull
            #pragma domain NormalsDomain
            #pragma fragment frag
            #pragma target 4.6

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "SnowTessellation.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            TessellationControlPoint SnowTessVertexStage(Attributes IN)
            {
                return SnowTessVertex(IN.positionOS);
            }

            [domain("tri")]
            Varyings NormalsDomain(TessellationFactors factors,
                                   const OutputPatch<TessellationControlPoint, 3> patch,
                                   float3 barycentric : SV_DomainLocation)
            {
                Varyings OUT;
                OUT.positionWS = SnowDomainPositionWS(patch, barycentric);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = (IN.positionWS.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
                float2 packed = SAMPLE_TEXTURE2D(_GroundNormals, sampler_GroundNormals, uv).rg
                                * 2.0 - 1.0;
                float3 baseNormal = normalize(float3(packed.x,
                    sqrt(saturate(1.0 - dot(packed, packed))), packed.y));

                // SSAO bu tamponu okuyor: kar birikintisinin eğimi burada da olmalı,
                // yoksa kabartının dibinde olması gereken gölge hiç oluşmaz.
                return half4(SnowDisplacedNormal(IN.positionWS, baseNormal), 0.0);
            }
            ENDHLSL
        }
    }
}
