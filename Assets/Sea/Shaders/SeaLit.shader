// ROL: deniz yuzeyinin cizimi. Sig su donusumu (vertex) + optik (fragment).
// Cagiran: SeaSurface (materyal olarak).

Shader "ToTheSummit/SeaLit"
{
    Properties
    {
        // Bos — butun degerler global uniform. Materyal basina property yok,
        // yani SRP Batcher uyumu icin CBUFFER da bos (spec 15.2).
    }

    SubShader
    {
        // OPAK CIZILIYOR. Seffaflik hissi refraksiyon ve sogurmadan geliyor,
        // alpha'dan degil. Alpha blend TAA'da hayalet birakiyor ve siralama
        // sorunu cikariyor (spec 12.6, 18).
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite On
        Cull Back
        Blend Off

        Pass
        {
            Name "SeaForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // KALITE KEYWORD'U `multi_compile` OLMAK ZORUNDA.
            //
            // `Shader.EnableKeyword` ile acilan bir keyword burada tanimli
            // degilse varyant HIC derlenmiyor ve `#if defined(...)`
            // sessizce false kaliyor. Kar sisteminde uc detay katmani tam
            // bu yuzden hic calismamisti.
            #pragma multi_compile _SEA_QUALITY_LOW _SEA_QUALITY_MEDIUM _SEA_QUALITY_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #include "SeaCommon.hlsl"

            // Optik globalleri
            float3 _SeaExtinctionRGB;
            float4 _SeaUpwellingColor;
            float  _SeaRefractionStrength;
            float  _SeaRoughnessCalm;
            float  _SeaRoughnessRough;

            float4 _SeaSkyColor;
            float4 _SeaHorizonColor;
            float  _SeaCloudCover01;
            float  _SeaSunElevation01;
            float  _SeaPrecipIntensity01;

            float  _SeaRunupMaxDepth;
            float  _SeaShoreFoamPhase;
            float  _SeaShoreFoamDepth;
            float4 _SeaFoamColor;
            float  _SeaFoamRoughness;
            float  _SeaFoamTiling;
            float  _SeaFoamBreakupTiling;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings SeaVertex(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y = _SeaLevelY;

                // DALGA ALANI VE SIG SU DONUSUMU `SeaCommon`'DA.
                //
                // Ileri gecis ile derinlik gecisi AYNI fonksiyonu cagiriyor;
                // ayri yazilsalardi iki tampon farkli bir yuzey gorurdu.
                SeaSurfaceNokta nokta = SeaDeform(posWS);

                OUT.positionWS = nokta.posWS;
                OUT.positionCS = TransformWorldToHClip(nokta.posWS);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            /// TAM FRESNEL — SCHLICK DEGIL.
            ///
            /// Schlick siyirma acilarda belirgin sapiyor ve deniz
            /// goruntusunde asil karakter tam orada (spec 12.1, Tessendorf
            /// 6.2 Sekil 24). Iki dalli yapi dogrudan Tessendorf'un ornek
            /// shader'indan.
            float SeaFresnel(float3 N, float3 V)
            {
                float cosThetaI = abs(dot(V, N));
                float thetaI    = acos(saturate(cosThetaI));
                float sinThetaT = sin(thetaI) / SEA_WATER_IOR;

                if (sinThetaT >= 1.0) return 1.0;      // tam ic yansima

                float thetaT = asin(sinThetaT);

                if (thetaI < 1e-4)
                {
                    float r = (SEA_WATER_IOR - 1.0) / (SEA_WATER_IOR + 1.0);
                    return r * r;
                }

                float fs = sin(thetaT - thetaI) / sin(thetaT + thetaI);
                float ts = tan(thetaT - thetaI) / tan(thetaT + thetaI);

                return 0.5 * (fs * fs + ts * ts);
            }

            /// SU HACMI SOGURMASI.
            ///
            /// Kirmizi en hizli, mavi en yavas sonumleniyor — suyun mavi
            /// gorunmesinin sebebi. [KAYNAK: Tessendorf 2004 7.1]
            float3 SeaVolumeColor(float pathLength)
            {
                return exp(-_SeaExtinctionRGB * pathLength);
            }

            half4 SeaFragment(Varyings IN) : SV_Target
            {
                // KARA USTU ATILIYOR — her piksel kendi derinligini okuyor,
                // boylece kiyi cizgisi quad sinirina takilmiyor.
                float depth = SeaSampleDepth(IN.positionWS.xz);
                clip(depth);

                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float  dist = length(_WorldSpaceCameraPos - IN.positionWS);

                // --- NORMAL, FFT EGIM DOKUSUNDAN (spec 10.5) ---
                //
                // Merkezi fark KULLANILMIYOR: egim zaten FFT ile uretiliyor
                // ve o daha dogru (spec 6.7).
                float2 egim = _SeaDbgNoWaves > 0.5 ? 0.0
                            : SeaSampleSlope(IN.positionWS.xz);

                float3 N = normalize(float3(-egim.x, 1.0, -egim.y));

                // UZAKTA NORMAL DETAYI SONUYOR. Sonmezse bir tekselden kucuk
                // dalgalar orneklenip TAA ile kaynayan bir yuzey olusuyor
                // (spec 10.5).
                float normalFade = saturate(1.0 - (dist - 120.0) / 400.0);
                N = normalize(lerp(float3(0, 1, 0), N, normalFade));

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // --- SU KALINLIGI (spec 12.3) ---
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(sceneEyeDepth - IN.screenPos.w, 0.0);

                // --- REFRAKSIYON ---
                //
                // LOW'DA KAPALI, DUZ RENK (spec 15.3). Refraksiyon opak
                // dokuyu ve derinlik dokusunu okuyor; iki tam ekran
                // ornekleme.
                float3 refracted = _SeaUpwellingColor.rgb;

            #if !defined(_SEA_QUALITY_LOW)
                if (_SeaDbgNoRefraction <= 0.5)
                {
                    float2 refrOffset = N.xz * _SeaRefractionStrength / max(dist, 1.0);
                    float2 refrUV = screenUV + refrOffset;

                    // SAPTIRMA KONTROLU. Sapmis ornegin derinligi yuzeyden
                    // sigsa saptirmayi iptal et; yoksa kiyida su, onundeki
                    // kayanin rengini "icine ceker" (spec 12.3).
                    float sapmisDerinlik = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                    if (sapmisDerinlik < IN.screenPos.w) refrUV = screenUV;

                    refracted = SampleSceneColor(refrUV);
                }
            #endif

                float3 volume = SeaVolumeColor(thickness);
                float3 belowSurface = lerp(_SeaUpwellingColor.rgb, refracted * volume, volume);

                // --- GOK YANSIMASI (spec 12.4) ---
                //
                // Deniz KENDI GOKYUZU MODELINI KURMUYOR. Oyunun zaten bir
                // atmosferi var ve iki kaynak celisirdi.
                float3 R = reflect(-V, N);
                float3 skyRefl = lerp(_SeaHorizonColor.rgb, _SeaSkyColor.rgb, saturate(R.y));
                skyRefl = lerp(skyRefl, skyRefl * 0.62, _SeaCloudCover01);

                // --- GUNES PARILTISI (spec 12.5) ---
                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;
                float3 H = normalize(V + L);

                float roughness = lerp(_SeaRoughnessCalm, _SeaRoughnessRough,
                                       saturate(length(_SeaWindWS) / 20.0));

                // Yagmurda yuzey puruzlenir (spec 13.5).
                roughness = lerp(roughness, 0.22, _SeaPrecipIntensity01 * 0.7);

                // UZAKTAKI PARILTI YAYINIK. Uzaktaki dalgalar kameranin
                // cozemedigi olcekte oldugu icin parilti yayiliyor
                // [KAYNAK: Tessendorf 2004 6 giris].
                roughness = lerp(roughness, 0.35, saturate((dist - 200.0) / 1500.0));

                float spec = pow(saturate(dot(N, H)), max(2.0 / (roughness * roughness), 2.0));

                // GECE PARILTI YOK (spec 12.5, 18 tuzak).
                spec *= saturate(_SeaSunElevation01 * 20.0);

                float3 glitter = mainLight.color * spec;

                // --- BIRLESTIRME (spec 12.6) ---
                float F = SeaFresnel(N, V);
                float3 color = lerp(belowSurface, skyRefl, F) + glitter;

                // --- KOPUK (spec 13) — UC KAYNAK ---
                float foam = 0.0;

                if (_SeaDbgNoFoam <= 0.5)
                {
                    // 1. TEPE KOPUGU, KATLANMA YONUNDE UZATILIYOR.
                    //
                    // `e-` ozvektoru yuzeyin hangi yatay yonde katlandigini
                    // gosteriyor (spec 13.2, Tessendorf denklem 48). Desen o
                    // yonde uzatilmazsa kopuk her yonde ayni ve dalgayla
                    // ilgisiz gorunuyor.
                    float2 foldDir;
                    float whitecap = SeaSampleFoam(IN.positionWS.xz, foldDir);

                    // LOW'DA YON UZATMA KAPALI (spec 15.3): desen dondurulmuyor,
                    // duz dunya koordinatindan okunuyor.
                #if defined(_SEA_QUALITY_LOW)
                    float2 foamUV = IN.positionWS.xz * _SeaFoamTiling;
                #else
                    float angle = atan2(foldDir.y, foldDir.x);
                    float sn, cs; sincos(angle, sn, cs);
                    float2x2 rot = float2x2(cs, -sn, sn, cs);

                    float2 foamUV = mul(rot, IN.positionWS.xz * _SeaFoamTiling);
                    foamUV.x *= 0.35;
                #endif

                    whitecap = saturate(whitecap * (0.55 + 0.75 * SeaFoamNoise(foamUV)));

                    // 2. KIRILMA KOPUGU (spec 8.3). Dalga yuksekliginin su
                    //    derinligine orani kirilma indeksini asiyorsa dalga
                    //    kiriliyor.
                    float slope = SeaSampleBottomSlope(IN.positionWS.xz);
                    float gamma = SeaBreakerIndex(slope);
                    float H     = 2.0 * abs(IN.positionWS.y - _SeaLevelY);
                    float oran  = H / max(depth, SEA_MIN_DEPTH);
                    float breakT = saturate((oran - gamma * 0.7) / (gamma * 0.3));

                    // 3. KIYI KOPUGU (spec 13.3). Kabarma bandi su seviyesini
                    //    yukselmis gibi gosteriyor (spec 8.5).
                    float runupDepth = _SeaRunupMaxDepth * _SeaShoreFoamPhase;
                    float effDepth = depth + runupDepth;

                    float shoreFoam = 1.0 - smoothstep(0.0, _SeaShoreFoamDepth, effDepth);
                    shoreFoam *= 0.4 + 0.6 * _SeaShoreFoamPhase;

                    // KENAR GURULTUYLE KIRILIYOR. Kirilmazsa kopuk bandi duz
                    // bir cizgi olur ve kiyi cizilmis gibi durur (spec 18
                    // tuzak tablosu). [KAYNAK: Crest, SIGGRAPH 2017]
                    //
                    // IKI OLCEK. Ince gurultu (~3 m) kopugun kendi dokusu;
                    // KABA gurultu (~16 m) su cizgisinin duz gorunmesini
                    // kiriyor.
                    //
                    // Kaba olcek OLCUMDEN geldi: su cizgisindeki basamaklar
                    // arazi heightmap'inin kendi cozunurlugu (4097 teksel /
                    // 30 km = 7.3 m) ve deniz mesh'i inceltilince
                    // DEGISMIYOR. O olcekten kucuk bir gurultu orada hicbir
                    // sey ortmuyor.
                    float breakup =
                          SeaFoamNoise(IN.positionWS.xz * _SeaFoamBreakupTiling) * 0.55
                        + SeaValueNoise(IN.positionWS.xz * (_SeaFoamBreakupTiling * 0.18)) * 0.45;
                    shoreFoam = saturate((shoreFoam - breakup * 0.45) * 2.5);

                    foam = max(whitecap, max(breakT * SEA_BREAK_FOAM_GAIN, shoreFoam));

                    // YAGMUR KOPUK EKLIYOR, KAR EKLEMIYOR. Ayrim kopruden
                    // geliyor: `_SeaPrecipIntensity01` yalniz yagmurda dolu
                    // (spec 13.5).
                    foam = saturate(foam + _SeaPrecipIntensity01 * 0.06);
                }

                // KOPUK FRESNEL'DEN SONRA. Kopuk SACAN bir yuzey; altindaki
                // suyun gok yansimasini gostermiyor (spec 12.6, 18).
                //
                // Isik: gunes yayinik payi + gok. Gok radyansi zaten
                // `skyRefl`'de duruyor; yarim kure payi olarak 0.35 ile
                // aliniyor [KALIBRASYON].
                float3 foamLight = mainLight.color * saturate(dot(N, L)) + skyRefl * 0.35;
                color = lerp(color, _SeaFoamColor.rgb * foamLight, foam * 0.9);

                // SIS URP'NIN KENDI FONKSIYONUYLA (spec 3.5). Kendi sis
                // hesabi YAZILMIYOR.
                color = MixFog(color, IN.fogCoord);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SeaDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex SeaDepthVertex
            #pragma fragment SeaDepthFragment
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SeaCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings SeaDepthVertex(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y = _SeaLevelY;

                // ILERI GECISLE AYNI DEFORMASYON. Uygulanmazsa derinlik
                // tamponu duz bir denizi, renk tamponu dalgali bir denizi
                // gorur; yuzey kendi derinlik testine takilir.
                SeaSurfaceNokta nokta = SeaDeform(posWS);

                OUT.positionWS = nokta.posWS;
                OUT.positionCS = TransformWorldToHClip(nokta.posWS);

                return OUT;
            }

            half4 SeaDepthFragment(Varyings IN) : SV_Target
            {
                // Ileri gecisle AYNI maske — yoksa derinlik tamponu ile renk
                // tamponu farkli kiyi cizgisi gorur.
                clip(SeaSampleDepth(IN.positionWS.xz));
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
