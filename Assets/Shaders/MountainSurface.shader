// include-rev: 112  (Unity, .hlsl degisince .shader'i yeniden
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
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

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

            /// TEŞHİS ANAHTARI — TERS MANTIK, BİLEREK.
            ///
            /// Eskiden `_TerrainShadowReceive` idi (1 = açık) ve yalnız
            /// `DebugMenu.Update()` yazıyordu. Panel sahnede yoksa ya da
            /// kapalıysa global hiç yazılmıyor, Unity globalleri SIFIR
            /// başlıyor ve arazi gölgesiz çiziliyordu — build'de oynanışın
            /// tamamı gölgesiz.
            ///
            /// Ters mantıkla varsayılan doğru tarafa düşüyor: kimse yazmazsa
            /// 0 kalır, 0 da "kapatma" demek.
            float _TerrainShadowOff;
            /// GEÇİCİ. Aydınlık-gölge sınırının etrafına ince renk şeritleri basıyor.
            /// Soru "zikzak var mı" ve cevabı parlaklıkla değil BİÇİMLE veriliyor:
            /// normal alanı düzgünse şeritler ince ve akıcı, doku ızgarasına oturmuşsa
            /// dikdörtgen bloklar. Blok mu şerit mi -- tek bakışta ayrılıyor.
            /// GEÇİCİ CETVEL. Dünya koordinatında ızgara çizgileri basıyor: 10 m
            /// camgöbeği, 100 m kırmızı, 1000 m sarı. Testerenin KAÇ METRE olduğunu
            /// tahminle değil sayarak bulmak için — beş tur katman tahmin edildi ve
            /// hepsi yanlış çıktı, çünkü eksik olan tek sayı diş boyuydu.

            #include "MountainSurface.hlsl"

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

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // ARAZİ DE KAR KALINLIĞI KADAR YÜKSELİYOR.
                //
                // Kar mesh'i yerel kar sütunu kadar yükseliyordu, arazi ise
                // yerinde kalıyordu: bölge sınırında kar derinliğiyle
                // ÖLÇEKLENEN bir basamak. Kenar rampası 2 m; 1 cm karda %0.5
                // eğim (görünmez), 20 cm'de %10, 50 cm'de %25. Belirti tam
                // olarak böyle bildirildi — ince karda yok, kalın karda var.
                //
                // Yükseltme kar örtüsü maskesiyle ağırlıklanmıyor: maske
                // fragman'da, burada yalnız konum var. Kar çizgisinin altında
                // `_FallbackSWE` zaten sıfır, dolayısıyla yükseltme de sıfır.
                positionWS.y += SnowWorldCoverHeight();

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
                float3 shadingNormal = surface.normalWS;
                inputData.normalWS = shadingNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(shadingNormal);
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
                // TEŞHİS ANAHTARI: `_TerrainShadowOff` birken gölge haritası hiç
                // okunmuyor. Arazi ekranın çoğunu kaplıyor ve bu okuma piksel başına
                // yapılıyor — kare süresindeki payı ancak kapatıp ölçerek bilinir.
                if (_TerrainShadowOff < 0.5)
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

                // ARAZİNİN KARI DA KARIN KENDİ IŞIKLANDIRMASINI KULLANIYOR.
                //
                // Eskiden kar mesh'i sarmal NdotL + arkadan sızma + `_ShadowTint`'li
                // ortam kullanıyordu, arazinin kar katmanı ise URP'nin standart
                // PBR'ını. Aynı kar, iki model. Ölçüldü: bölge sınırının iki
                // yakası arasında %2.3 parlaklık farkı (iç 0.8318, dış 0.8132) —
                // düz beyaz bir alanda bu fark KESKİN ÇİZGİ olarak okunuyor ve
                // oyuncuyu takip eden 24 m'lik kareyi çiziyordu.
                //
                // Kar nerede olursa olsun aynı maddedir; ışıklandırması da tek
                // yerden gelir. Kaya tarafı standart PBR'da kalıyor, iki sonuç
                // `snowMask` ile harmanlanıyor.
                if (surface.snowMask > 0.001)
                {
                    // Arazi tarafında iz yok, kabuk yok; yoğunluk dünyanın
                    // genel değeri. Derinlik örtü kalınlığı — mesh'te ölçülen
                    // sütun, burada sabit.
                    // DERİNLİK DÜNYANIN KAR SÜTUNU. `_SnowCoverThickness` (4 cm)
                    // NESNELERİN üstündeki ince örtü için (spec §16); arazi
                    // onunla ışıklandırılınca kar mesh'inin gördüğü ~50 cm'lik
                    // sütundan farklı çıkıyordu. Derinlik `SnowAmbient`'ın sızma
                    // terimini `exp(-derinlik·7)` ile sürüyor.
                    //
                    // AO da mesh tarafındaki gibi bağlanıyor: orada
                    // `SnowHeightAO`, burada yüzeyin kendi örtülmesi. Sabit 1.0
                    // arazinin oyuklarını yok sayıyordu.
                    //
                    // İkisi birlikte ölçüldü: mesh/arazi parlaklık oranı
                    // 1.61 kattan 1.16 kata indi (24 m'lik kare belirtisi).
                    SnowSurface ks = SnowBuildSurface(_FallbackRhoN, _SurfaceWetness, 0.0, 0.0,
                                                      _WorldSnowDepth, IN.positionWS,
                                                      length(fwidth(IN.positionWS.xz)));

                    half3 karIsik = SnowDirectLight(mainLight, inputData.normalWS,
                                                    inputData.viewDirectionWS, ks)
                                  + SnowAmbient(inputData.normalWS, ks,
                                                mainLight.shadowAttenuation,
                                                (half)surface.occlusion);

                    lit = lerp(lit, karIsik, (half)surface.snowMask);
                }

                // KARDAN YANSIYAN GÜNEŞ. Gölgedeki bir noktanın çevresini güneş vuran
                // kar sarıyor ve o ışık hiç sayılmıyordu: sahnede GI yok, ortam yalnız
                // gökyüzü probundan geliyor. Kar albedosu 0.8 olduğu için eksik olan
                // terim gökyüzünden BÜYÜK.
                //
                // Ölçüm (renk probu 2, 15:00): güneş-gölge farkı 3.5-5 diyafram; açık
                // havada kar için gerçek değer 2-3.5. Fark 1-1.5 stop.
                //
                // GÖRÜŞ FAKTÖRÜ DERS KİTABI, UYDURMA KATSAYI YOK. Eğimli bir yüzeyin
                // gökyüzünü görme oranı (1+cosβ)/2, kalanı zemin: (1-cosβ)/2. cosβ
                // normalin Y'si. Düz zeminde sıfır — düz zemin başka zemin görmez,
                // doğrusu da bu.
                //
                // GÖLGE ÇARPANI UYGULANMIYOR ve bu bilinçli: yansıyan ışık ÇEVREDEN
                // geliyor, çevre bu nokta gölgedeyken de güneş alıyor olabilir. Zaten
                // meselenin tamamı bu.
                //
                // Gece kendiliğinden sönüyor: `direction.y` sıfıra iniyor ve güneşin
                // şiddeti artık hava kütlesi sönümünü taşıyor.
                float groundView = (1.0 - saturate(shadingNormal.y)) * 0.5;
                float3 horizontalIrradiance = mainLight.color * saturate(mainLight.direction.y);
                lit += surface.albedo * horizontalIrradiance * groundView
                     * brdfData.diffuse * aoFactor.indirectAmbientOcclusion;

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

        // Gölge ve derinlik geçişleri ELLE YAZILDI: URP'nin hazır dosyaları gölge sapması
        // gibi tuzaklarını da beraberinde getiriyor.
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
            #pragma vertex Vertex
            #pragma fragment ShadowFragment
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "MountainSurfaceInput.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                // Kar örtüsünün geometrisi: ileri geçişle aynı ofset (bkz. Vertex).
                positionWS.y += SnowWorldCoverHeight();

                // Sapma normale göre uygulanıyor; yer değiştirmiş yüzeyin normali
                // arazininkinden farklı, o yüzden düz yukarı değil gerçek normal.
                float2 uv = (positionWS.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
                float2 packed = SAMPLE_TEXTURE2D_LOD(_GroundNormals, sampler_GroundNormals,
                                                     uv, 0).rg * 2.0 - 1.0;
                float3 baseNormal = normalize(float3(packed.x,
                    sqrt(saturate(1.0 - dot(packed, packed))), packed.y));
                float3 normalWS = baseNormal;

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
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                // Kar örtüsünün geometrisi: ileri geçişle aynı ofset (bkz. Vertex).
                positionWS.y += SnowWorldCoverHeight();
                OUT.positionCS = TransformWorldToHClip(positionWS);
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
            #pragma vertex Vertex
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                // Kar örtüsünün geometrisi: ileri geçişle aynı ofset (bkz. Vertex).
                OUT.positionWS.y += SnowWorldCoverHeight();
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
                return half4(baseNormal, 0.0);
            }
            ENDHLSL
        }
    }
}
