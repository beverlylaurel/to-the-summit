// DERİN KAR YÜZEYİ. Arazi mesh'inden ayrı, oyuncuyu izleyen yoğun yama.
//
// Gerekçe `SnowPatch.cs` başında: arazi üçgeni 7.32 m ve tessellation tavanı 64, yani
// iz için en iyi ihtimalle 11.4 cm. Burada dörtgen 9.4 cm ve bölünme gerekmiyor.
//
// Yüzey = arazi yüksekliği + kar derinliği − iz.
Shader "ToTheSummit/SnowPatch"
{
    // Properties YOK: kar rengi `_SnowColor` global olarak `TerrainSurface`'tan
    // geliyor. Yamaya ayrı bir renk verilseydi arazi karıyla iki farklı beyaz olurdu.

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"   // araziden SONRA: aynı kotta z-kavgası olmasın
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MountainSurface.hlsl"
            #include "SnowTessellation.hlsl"

            TEXTURE2D(_TerrainHeightmap);
            SAMPLER(sampler_TerrainHeightmap);

            // `_PatchCenter` ve `_PatchHalf` GLOBAL: arazi shader'ı da okuyor, çünkü
            // yamanın kapsadığı yerde arazi çizilmemeli.
            float  _PatchCell;     // dörtgen kenarı, metre
            float  _RingIsOuter;   // 1 = dış halka, iç halkanın alanını keser
            float4 _RingCenter;

            float4 _TerrainHeightScale;   // x kot ölçeği, y taban kotu
            float4 _TerrainHeightUv;      // x ölçek, y yarım texel kaydırması

            /// ARAZİ YÜKSEKLİĞİ. Ölçek ve UV düzeltmesi C#'tan geliyor (`SnowPatch`),
            /// shader'a gömülü değil: Unity'nin heightmap sözleşmesi değişirse sayı tek
            /// yerde durmalı.
            float TerrainHeight(float2 worldXZ)
            {
                float2 uv = (worldXZ - _TerrainOrigin.xz) / max(1.0, _TerrainSize.x);
                uv = uv * _TerrainHeightUv.x + _TerrainHeightUv.y;

                float raw = SAMPLE_TEXTURE2D_LOD(_TerrainHeightmap, sampler_TerrainHeightmap,
                                                 saturate(uv), 0).r;
                return _TerrainHeightScale.y + raw * _TerrainHeightScale.x;
            }

            /// Karın yüzeyi: arazi + kar + mikro kabartı − iz.
            ///
            /// MAKRO DERİNLİK DIŞARIDAN VERİLİYOR, köşe başına BİR kez hesaplanıyor.
            ///
            /// Normal üç komşu örnekle kuruluyor ve `SnowMacroDepth` her birinde
            /// yeniden çağrılsaydı köşe başına üç kez koşardı — içinde çok oktavlı
            /// bükümlü birikinti gürültüsü var ve iki halkada 1.6 milyon çağrı ediyordu
            /// (gölge geçişiyle iki katı). Ölçüldü: kare süresinin baskın kalemi.
            ///
            /// Sabit almanın bedeli yok: makro derinlik ~2.6 metrelik birikinti
            /// gövdesiyle değişiyor, hücre ise 4.7 cm. O ölçekteki eğim katkısı yüzde
            /// ikinin altında ve normale girmiyor. Normale giren şey zaten mikro
            /// kabartı ve iz — ikisi de burada, hücre ölçeğinde.
            float SnowTop(float2 worldXZ, float ground, float depth)
            {
                float3 at = float3(worldXZ.x, ground, worldXZ.y);

                // MİKRO KABARTI YALNIZ İÇ HALKADA. Dış halkanın hücresi 37.5 cm ve
                // mesafesi 12-48 m: 35 santimlik sastrugi sırtı orada bir pikselin
                // altında kalıyor, hesaplanması boşa.
                float micro = _RingIsOuter > 0.5 ? 0.0 : SnowMicroRelief(worldXZ, depth);

                // DIŞ HALKA İZİ BULANIK OKUYOR: hücresi 37.5 cm, doku texel'i 4.7 cm.
                // Tek örnekle kenar hücrelere basamaklanıyor ve ekranda düzenli merdiven
                // çıkıyor.
                float trail = _RingIsOuter > 0.5
                            ? SnowFootprintWide(at, _PatchCell * 0.5)
                            : SnowFootprint(at);

                return ground + depth + micro - trail;
            }

            struct Attributes { float3 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 terrainNormalWS : TEXCOORD4;
                float  depth      : TEXCOORD2;   // oradaki kar kalınlığı, metre
                float  fogCoord   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Yama matrisle taşınıyor; xz dünyaya buradan geçiyor.
                float2 worldXZ = TransformObjectToWorld(IN.positionOS).xz;

                // NORMAL KOMŞU ÖRNEKLEMEDEN. Yüzey tamamen prosedürel; köşe normali
                // yok. Adım hücrenin kendisi kadar — daha küçüğü aynı texel'in içinde
                // kalıp sıfır eğim verir.
                //
                // ARAZİ YÜKSEKLİĞİ ÜÇ NOKTADA BİR KEZ okunuyor. Yama normali ve arazi
                // normali aynı üç örneği paylaşıyor; ayrı ayrı okunduğunda köşe başına
                // altı doku çağrısı ediyordu, 263 bin köşede iki katı boşa iş.
                float e = _PatchCell;
                float2 atX = worldXZ + float2(e, 0.0);
                float2 atZ = worldXZ + float2(0.0, e);

                float g = TerrainHeight(worldXZ);
                float gx = TerrainHeight(atX);
                float gz = TerrainHeight(atZ);

                // Makro derinlik BİR kez; gerekçe `SnowTop` başında.
                float depth = SnowMacroDepth(float3(worldXZ.x, g, worldXZ.y));

                float y = SnowTop(worldXZ, g, depth);
                float hx = SnowTop(atX, gx, depth);
                float hz = SnowTop(atZ, gz, depth);

                OUT.normalWS = normalize(float3(y - hx, e, y - hz));

                // Arazi gölgelendirmesinin döndürdüğü normal mikro detayı (sastrugi,
                // tane, rüzgâr kabuğu) ARAZİNİN normali üstüne bindirilmiş; yamada o
                // detayı kullanmak için önce hangi tabana bindiğini bilmek gerekiyor.
                OUT.terrainNormalWS = normalize(float3(g - gx, e, g - gz));

                float3 positionWS = float3(worldXZ.x, y, worldXZ.y);
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.depth = depth;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // KAR YOKSA YAMA DA YOK — ve kesme PİKSEL BAŞINA. Arazi tarafındaki
                // kesme de aynı fonksiyonu piksel başına çağırıyor; köşede hesaplanıp
                // aradeğerlenseydi sınırda ya delik ya çakışma kalırdı.
                clip(SnowMacroDepth(IN.positionWS) - _SnowDisplaceStart);

                // DIŞ HALKA İÇİN ALANINI KESİYOR. İkisi aynı kotta çizilirse z-kavgası
                // olur ve iç halkanın ince izi kaba yüzeyle titreşir. Pay bırakılıyor:
                // hücre boyları farklı (4.7 cm / 18.75 cm) ve sınırda aradeğerleme
                // farkıyla ayrışıyorlar.
                if (_RingIsOuter > 0.5 && _PatchHalf > 0.0)
                {
                    float2 toInner = abs(IN.positionWS.xz - _PatchCenter.xz);
                    if (max(toInner.x, toInner.y) < _PatchHalf - 0.5) discard;
                }

                // GÖLGELENDİRME ARAZİNİN KENDİ FONKSİYONU. Yamaya ayrı bir kar
                // gölgelendirmesi yazılsaydı sınırda iki farklı beyaz görünürdü —
                // mikro doku, sastrugi, parıltı ve alpenglow hepsi orada.
                MountainSurface surface = BuildMountainSurface(IN.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;

                // NORMAL: yamanın geometrisi + arazinin MİKRO detayı.
                //
                // Yüzey burada arazininkinden farklı (iz kazılmış), o yüzden taban
                // normal yamanın kendi geometrisinden geliyor. Ama karın taneli
                // görünümü arazinin mikro detayında; o detay `surface.normalWS` ile
                // arazi normalinin FARKI olarak alınıp yamanın normaline bindiriliyor.
                //
                // Fark almak şart: `surface.normalWS`'i doğrudan kullanmak izi
                // düzleştirir, hiç kullanmamak da karı cam gibi bırakır — üstelik
                // derleyici o yolu eleyip doku örneklemesini siliyor.
                float3 micro = surface.normalWS - normalize(IN.terrainNormalWS);
                inputData.normalWS = normalize(normalize(IN.normalWS) + micro);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surface.albedo;
                surfaceData.emission = surface.emission;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.occlusion = surface.occlusion;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "SnowDisplacement.hlsl"

            TEXTURE2D(_TerrainHeightmap);
            SAMPLER(sampler_TerrainHeightmap);
            float  _PatchCell;

            float4 _TerrainHeightScale;
            float4 _TerrainHeightUv;

            float TerrainHeightShadow(float2 worldXZ)
            {
                float2 uv = (worldXZ - _TerrainOrigin.xz) / max(1.0, _TerrainSize.x);
                uv = uv * _TerrainHeightUv.x + _TerrainHeightUv.y;

                float raw = SAMPLE_TEXTURE2D_LOD(_TerrainHeightmap, sampler_TerrainHeightmap,
                                                 saturate(uv), 0).r;
                return _TerrainHeightScale.y + raw * _TerrainHeightScale.x;
            }

            /// GÖLGE GEÇİŞİNDE MİKRO KABARTI YOK. Gölge haritasının texel'i metre
            /// ölçeğinde; santimetrelik sırtlar oraya hiç ulaşmıyor ama hesabı iki kat
            /// köşe işi ediyordu.
            float SnowTopShadow(float3 at, float ground)
            {
                return ground + SnowMacroDepth(at) - SnowFootprint(at);
            }

            struct ShadowIn { float3 positionOS : POSITION; };
            struct ShadowOut { float4 positionCS : SV_POSITION; float depth : TEXCOORD0; };

            ShadowOut shadowVert(ShadowIn IN)
            {
                ShadowOut OUT;
                float2 worldXZ = TransformObjectToWorld(IN.positionOS).xz;
                float ground = TerrainHeightShadow(worldXZ);
                float3 at = float3(worldXZ.x, ground, worldXZ.y);
                float y = SnowTopShadow(at, ground);

                OUT.positionCS = TransformWorldToHClip(float3(worldXZ.x, y, worldXZ.y));
                OUT.depth = SnowMacroDepth(at);
                return OUT;
            }

            half4 shadowFrag(ShadowOut IN) : SV_Target
            {
                clip(IN.depth - _SnowDisplaceStart);
                return 0;
            }
            ENDHLSL
        }
    }
}
