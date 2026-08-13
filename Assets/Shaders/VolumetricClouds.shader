// include-rev: 59  (CloudCommon.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemiyor; bu satir degistikce derleme zorlanir)
Shader "ToTheSummit/VolumetricClouds"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _CloudTargetSize;      // xy: hedef çözünürlük, zw: 1/çözünürlük
        float3 _CloudCameraForward;
        float3 _CloudRayBottomLeft;
        float3 _CloudRayBottomRight;
        float3 _CloudRayTopLeft;
        float3 _CloudRayTopRight;

        /// Işın yönü frustumun dört köşe ışınının bilineer karışımı.
        /// Matris tersine göre konvansiyondan bağımsız ve daha ucuz.
        float3 CloudRayDirection(float2 uv)
        {
            float3 bottom = lerp(_CloudRayBottomLeft, _CloudRayBottomRight, uv.x);
            float3 top = lerp(_CloudRayTopLeft, _CloudRayTopRight, uv.x);
            return normalize(lerp(bottom, top, uv.y));
        }
        ENDHLSL

        // 0 — bulutları düşük çözünürlükte, kareye göre kaydırılmış örnek konumundan çiz
        Pass
        {
            Name "CloudRaymarch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "CloudCommon.hlsl"

            float2 _CloudJitter;   // piksel içindeki kayma, -0.5..0.5

            float4 frag(Varyings IN) : SV_Target
            {
                // Her kare piksel içinde başka bir noktadan örnekleniyor. On altı kare
                // bir tam tur atıyor ve biriken sonuç, tam çözünürlükte tek karede
                // alınacak örnek yoğunluğuna yakınsıyor.
                float2 uv = IN.texcoord + _CloudJitter * _CloudTargetSize.zw;

                float3 direction = CloudRayDirection(uv);

                // Sahnedeki geometri ışını keser: bulut dağın önüne geçmez.
                // Derinlik kamera ekseni boyuncadır; ışın boyunca uzunluğa çevrilir.
                float rawDepth = SampleSceneDepth(uv);
                float sceneDistance = 1e9;

                if (rawDepth > 0.0)
                {
                    float axial = dot(direction, normalize(_CloudCameraForward));
                    sceneDistance = LinearEyeDepth(rawDepth, _ZBufferParams) / max(0.05, axial);
                }

                // Bayer fazı kareye göre de kayar: jitter bileşenleri ±k/4 olduğundan
                // ×8 tek tam sayılar verir ve 16 karelik döngü 16 ayrı permütasyon
                // gezer. Faz düşük çözünürlük pikseline sabit kalınca serpme deseni
                // birikimde aynen donuyor ve bulut 4 piksellik damalı bir doku
                // giyiyordu; kare başına permütasyon onu piksel boyu grene kırar.
                float2 pixel = uv * _CloudTargetSize.xy + _CloudJitter * 8.0;
                return RaymarchClouds(_WorldSpaceCameraPos, direction, pixel, sceneDistance);
            }
            ENDHLSL
        }

        // 1 — bu karenin örneklerini tam çözünürlüklü geçmişe yerleştir
        Pass
        {
            Name "CloudTemporalResolve"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            #include "CloudCommon.hlsl"

            TEXTURE2D(_CloudHistory);
            float4x4 _CloudPreviousViewProjection;
            float4 _CloudFullSize;      // xy: tam çözünürlük, zw: 1/çözünürlük
            float _CloudBlockIndex;     // bu karenin blok içindeki sırası
            float _CloudHistoryValid;


            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Bu kare, tam çözünürlük ızgarasının bloklarından yalnızca bir hücresini
                // hesapladı. O hücreye denk gelen piksel taze değeri alır, kalanlar
                // geçmişten gelir. Blok bir kez dolaştığında her piksel gerçekten
                // hesaplanmış olur — ortalama değil, kendi örneği.
                // Blok kenarı iki çözünürlüğün oranı; ışın yürüyüşü kaç kat küçükse o.
                float2 grid = max(1.0, round(_CloudFullSize.xy * _CloudTargetSize.zw));
                float2 cell = fmod(floor(uv * _CloudFullSize.xy), grid);
                bool fresh = abs(cell.y * grid.x + cell.x - _CloudBlockIndex) < 0.5;

                // Taze örnek nokta örneklemeyle alınır: bilineer olsaydı komşu hücrelerin
                // değerleri karışır ve çözmeye çalıştığımız ayrıntı yeniden bulanırdı.
                float4 current = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uv);

                if (fresh || _CloudHistoryValid < 0.5) return current;

                float4 clip = mul(_CloudPreviousViewProjection,
                                  float4(CloudAnchor(CloudRayDirection(uv)), 1.0));
                if (clip.w <= 0.0) return current;

                float2 previous = clip.xy / clip.w * 0.5 + 0.5;

                #if UNITY_UV_STARTS_AT_TOP
                previous.y = 1.0 - previous.y;
                #endif

                // Ekran dışına düşen geçmiş yok: o piksel geçen kare görünmüyordu.
                if (any(previous < 0.0) || any(previous > 1.0)) return current;

                float4 history = SAMPLE_TEXTURE2D(_CloudHistory, sampler_LinearClamp, previous);

                // Komşuluk kelepçesi: geçmiş, bu karenin çevresindeki değer aralığının
                // dışına çıkamaz. Örtüşen bir şey açıldığında ya da bulut kenarı hızla
                // kaydığında eski değer aralığın dışında kalıyor ve kırpılarak atılıyor —
                // iz bırakmayı önleyen kısım bu.
                float4 lo = current;
                float4 hi = current;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    const float2 offsets[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
                    float4 neighbour = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
                                                        uv + offsets[i] * _CloudTargetSize.zw);
                    lo = min(lo, neighbour);
                    hi = max(hi, neighbour);
                }

                // Harman yok. Taze olmayan pikselin `current`'ı kendi örneği değil,
                // bloğun tek ışın yürüyüşü — 16 piksel için aynı değer. Ona doğru
                // çekmek çözmeye çalıştığımız alt-piksel ayrıntısını geri siler ve
                // blok sınırlarında basamak bırakır. Piksel kendi örneğini sırası
                // gelince alıyor; o zamana kadar geçmişini olduğu gibi taşıyor.
                return clamp(history, lo, hi);
            }
            ENDHLSL
        }

        // 2 — birikmiş sonucu tam çözünürlükte sahneye bindir
        Pass
        {
            Name "CloudComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            #include "CloudCommon.hlsl"

            TEXTURE2D(_CloudTexture);
            float4 _CloudFullSize;      // xy: tam çözünürlük, zw: 1/çözünürlük

            // Şimşek: LightningFlash yazar, gökyüzü de aynı değerleri okur.
            // _LightningFlash bildirimi CloudCommon → HeightFog'dan geliyor.
            float4 _LightningPosition;   // xyz çakmanın dünya konumu, w leke yarıçapı

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float4 clouds = SAMPLE_TEXTURE2D(_CloudTexture, sampler_LinearClamp, uv);

                float3 viewRay = CloudRayDirection(uv);
                float3 deckPoint = CloudAnchor(viewRay);

                // UZAK KENAR BENEKLENMESİ: yürüyüş yarım çözünürlükte, birikim
                // harmansız ve geçmiş yeniden yansıtması katman-ortası çapasına
                // dayanıyor — uzakta çapa hatası büyür, komşuluk kelepçesi gevşer ve
                // komşu pikseller farklı karelerden değer taşır. Kenar birkaç piksele
                // sıkıştığı için bu doğrudan benek olarak okunur. Çare bu katmanda:
                // mesafeyle açılan çadır filtresi. Yakın bulut hiç dokunulmadan
                // keskin kalır (filtre 9 km'ye kadar kapalı).
                // Merdiven kenar: yürüyüş yarım çözünürlükte, geçmiş yansıtması
                // hareket hâlinde yakınsamıyor — siluet 2 pikselli bloklara
                // kuantalanıyor. Çadır filtresi MESAFE KAPISIZ: kapı (6 km) yüzünden
                // yakın-orta bulutlarda hiç çalışmıyordu, merdiven orada da var.
                // Yarıçap 0.9 tam texel: bilineer dört tap blok sınırını tam ortalar,
                // gövde detayı korunur. Uzakta pay tam açılır (kenar bir pikselden
                // ince, orada yumuşaklık bedava).
                {
                    float deckDistance = distance(deckPoint, _WorldSpaceCameraPos);
                    // Yarıçap BLOK BOYUNA göre ölçeklenir: yürüyüş çözünürlüğü
                    // düşürüldüğünde (downsample 2 → 3 → 4) merdiven basamağı da
                    // büyür; sabit yarıçap o zaman blok sınırını ortalayamıyor.
                    // Pay ve yarıçap ölçülü: 1/3 çözünürlükte blok 3 piksel ve tam güçlü
                    // geniş çadır gövdeyi de bulanıklaştırıyordu. Merdiven basamağı
                    // kenarda yaşıyor, gövde zaten düz — yakında hafif, uzakta tam.
                    float block = max(1.0, _CloudFullSize.x * _CloudTargetSize.z);
                    // ÇADIR AZ AMA AÇIK. Tamamen kapatıldığında blok deseni geri geldi:
                    // TAA tek başına yetmiyor, çünkü yürüyüş çözünürlüğü 1/16 ve blok
                    // kenarı 4 piksel — grade'in kontrastı da kenarları belirginleştiriyor.
                    // Eski değerler (yakında 0.35, uzakta 0.90) gövdeyi de bulanıklaştırıyordu;
                    // bu aralık yalnız kenarı eritiyor.
                    float amount = lerp(0.18, 0.5, smoothstep(4000.0, 18000.0, deckDistance));
                    float2 o = _CloudFullSize.zw * (block * 0.32);
                    float4 tent =
                          SAMPLE_TEXTURE2D(_CloudTexture, sampler_LinearClamp, uv + float2( o.x,  o.y))
                        + SAMPLE_TEXTURE2D(_CloudTexture, sampler_LinearClamp, uv + float2(-o.x,  o.y))
                        + SAMPLE_TEXTURE2D(_CloudTexture, sampler_LinearClamp, uv + float2( o.x, -o.y))
                        + SAMPLE_TEXTURE2D(_CloudTexture, sampler_LinearClamp, uv + float2(-o.x, -o.y));
                    clouds = lerp(clouds, tent * 0.25, amount);
                }

                // Şimşek bulutun içinde çakar: görünen şey kütlenin içeriden aydınlanması.
                // Işın yürüyüşünün içine konamaz — o on altı kareye yayıldığı için
                // piksellerin bir kısmı çakmayı görür, kalanı görmez ve parlama blok blok
                // titrer. Burası tam çözünürlükte ve her kare çalışıyor.
                //
                // Parlama çakmanın bulunduğu **yerde** toplanır, bir yönde değil. Işın
                // katmanla kesiştiriliyor ve bulunan dünya noktasının çakmaya uzaklığına
                // göre sönüyor. Yön kullanmak yeterli değildi: yön mesafe taşımıyor, bu
                // yüzden yaklaştıkça büyümesi gereken leke sabit açıda kalıyordu.
                //
                // Uzağa da küçük bir pay düşüyor: ışık kütlenin içinde saçılıp dağılıyor,
                // çakmadan uzak bulut da bir miktar aydınlanıyor.
                float3 direction = viewRay;
                float spread = distance(deckPoint, _LightningPosition.xyz) / _LightningPosition.w;
                float local = lerp(0.08, 1.0, 1.0 / (1.0 + spread * spread));

                // Kalınlıkla ölçekleniyor: ince kenar hafif, kütle güçlü parlar
                float3 lit = clouds.rgb + _LightningFlash.rgb * clouds.a * local;

                // Kapalı gökte güneşin YERİ kaybolmaz: ışık katmandan çoklu saçılmayla
                // süzülür, güneşin arkasında olduğu yerde bulutta parlak sıcak bir yama
                // görünür. Diski gökyüzü çizer (boşluklardan); bu yama örtülü kısmın
                // gerçeğidir. İnce bulutta gök zaten görünür, kalında ışık boğulur —
                // pay a(1-a) çanıyla orta kalınlıkta tepe yapar. Işın yürüyüşü bunu
                // veremiyor: ışık sondası yatay güneşte sıfıra iniyor, çoklu saçılma
                // bütçede yok; yama tam çözünürlükte, şimşek lekesiyle aynı katta.
                float sunAmount = pow(saturate(dot(direction, _SunDirection)), 24.0);
                float glowUp = smoothstep(-0.08, 0.1, _SunDirection.y);
                float bell = clouds.a * (1.0 - clouds.a) * 4.0;
                lit += _CloudSunColor.rgb * (sunAmount * glowUp * bell * 0.8);

                // Kamera önü sis peçesi artık ışın yürüyüşünün içinde, GERÇEK ilk bulut
                // mesafesiyle uygulanıyor. Buradaki çapa-tabanlı peçe, ufka yakın
                // ışınlarda çapayı (katman ortası küresi) 100+ km'ye düşürüp sis
                // integraline absürt bir yol sardırıyor ve katman ortasının altındaki
                // kameraya bulutları haksız yere siliyordu; ortayı aşınca çapa 1 m'ye
                // dejenere olup bulutlar bir anda beliriyordu.
                return float4(scene.rgb * (1.0 - clouds.a) + lit, scene.a);
            }
            ENDHLSL
        }
    }
}
