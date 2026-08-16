// include-rev: 58  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemiyor; bu satir degistikce derleme zorlanir)
Shader "ToTheSummit/Sky"
{
    Properties
    {
        _SunColor ("Güneş", Color) = (1, 0.95, 0.85, 1)
        _MoonColor ("Ay", Color) = (0.75, 0.8, 0.95, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Gradyan, hava rengi ve sis buradan gelir: gökyüzü kendi rengini
            // hesaplamaz, sisle AYNI AirColor fonksiyonunu çağırır. İki formül
            // tutulduğu sürece her hava köşesinde yeniden ayrışıyorlardı.
            // _SunDirection ve _LightningFlash bildirimleri de oradan geliyor.
            #include "HeightFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunColor;
                float4 _MoonColor;
            CBUFFER_END

            // Bulut geçişiyle ortak: AtmosphereController global olarak yazar
            float3 _MoonDirection;
            float _StarStrength;

            /// Bulut sisteminin ortam sondası geçişi 1 yazar, geçiş bitince 0'a döner.
            /// Sondanın küpüne güneş/ay diski girmesin diye (`VolumetricCloudsURP`).
            float _DisableSunDisk;

            float4 _LightningPosition;   // xyz çakmanın dünya konumu, w leke yarıçapı

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.direction = IN.positionOS.xyz;
                return OUT;
            }

            /// Yön uzayı üç boyutlu bölünmeli: iki boyuta indirgemek farklı yönleri aynı
            /// hücreye düşürüyor ve kamera dönünce yıldızlar yeniden dağılıyor.
            float Hash3(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            /// Kadran: keskin disk, dar iç hâle, geniş dış saçılma
            float3 Disk(float3 direction, float3 target, float3 color, float size, float glow, float brightness)
            {
                float d = saturate(dot(direction, target));

                float disk = smoothstep(1.0 - size, 1.0 - size * 0.25, d);

                float3 core = color * (disk * 7.0);

                // Kademeli hâle halkaları: her katman bir öncekinden geniş, daha sönük
                // ve daha doygun. Işık dışa doğru daha uzun bir atmosfer yolundan geçer,
                // bu yüzden merkez beyaza doyarken dış halkalar sarıya, turuncuya ve
                // kızıla iner. Tek bir sürekli düşüşle çizmek kadranı düz bir leke
                // bırakıyordu — kademelenme gerçek bir hâlenin okunmasını sağlıyor.
                // Üstel düşüş katman üretmiyor: üs küçüldükçe fonksiyon düzleşip gökyüzüne
                // yayılan genel bir parlaklığa dönüşüyor. Her halkanın kendi sınırı olmalı.
                float3 tint = color;
                float3 halo = 0.0;
                float radius = size * 7.0;
                float weight = 2.2;

                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    tint *= color;                  // her katmanda renk derinleşir

                    // Bant içte geniş, dışta dar. Sabit bir oran iki uçtan birini bozuyor:
                    // küçük yarıçapta dar bant kenarı keskinleştirip yapay çember bırakıyor,
                    // büyük yarıçapta geniş bant halkayı doyuramayıp söndürüyor.
                    float band = radius * lerp(2.4, 0.6, i / 4.0);

                    float edge = 1.0 - radius;
                    float ring = smoothstep(edge, edge + band, d);

                    halo += tint * (ring * weight);
                    radius *= 2.6;                  // sonraki halka belirgin şekilde geniş
                    weight *= 0.5;                  // ve sönük
                }

                return (core + halo) * brightness;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 direction = normalize(IN.direction);
                float height = direction.y;

                // Gradyan sisle ortak: hava neyse gökyüzü o. Yıldız, kadran ve şimşek
                // "hava"nın arkasındaki cisimlerdir — üstlerine sis biner, gökyüzü
                // sislenmeyince çorbanın içinde bile yıldız görünüyordu.
                float3 sky = AirColor(direction);
                float3 extras = 0.0;

                // Yıldızlar yön uzayındaki hücrelere oturur; her hücre kendi boyutunu,
                // parlaklığını ve renk sıcaklığını taşır. Konum yönden türediği için
                // kamera dönse de yıldızlar yerinde kalır.
                // Izgara ekranda birkaç pikselden geniş olmalı. Dar ızgarada yıldız yarım
                // pikselin altında kalıyor; kamera döndükçe piksel ızgarası kayıyor ve
                // yıldız komşu piksele atlayıp yer değiştiriyormuş gibi görünüyor.
                float3 grid = direction * 140.0;
                float3 cell = floor(grid);
                float3 local = frac(grid) - 0.5;

                float present = step(0.986, Hash3(cell));

                // Boyut dar bir bantta: gerçek yıldız neredeyse noktasaldır, ama ekranda
                // en az bir iki piksel kaplamazsa kararlı çizilemez
                float radius = lerp(0.17, 0.36, Hash3(cell + 5.1));
                float shape = smoothstep(radius, radius * 0.25, length(local));

                // Parlaklık karesel dağılır: gökyüzünde sönük yıldız çok, parlak az
                float magnitude = Hash3(cell + 17.3);
                float brightness = lerp(0.3, 1.0, magnitude * magnitude);

                // Renk sıcaklığı: sıcak yıldız mavi-beyaz, soğuk olan sarımsı-turuncu.
                // Sapma küçük tutulur, çoğu yıldız gerçekte de beyaza yakın görünür.
                float temperature = Hash3(cell + 91.7) * 2.0 - 1.0;
                float3 tint = float3(1.0 + temperature * 0.16, 1.0, 1.0 - temperature * 0.20);

                // Atmosfer türbülansı yıldızı titretir ama hepsini değil: yalnızca birkaçı
                // parıldar. Tümü titrerse gökyüzü kaynıyor gibi olur.
                float twinkleRoll = Hash3(cell + 41.7);
                float twinkles = step(0.38, twinkleRoll);

                // Türbülans birkaç ölçekte birden çalışır, tek frekans metronom gibi
                // vurur. İki hız üst üste binince ritim düzensizleşir ve her yıldız
                // kendine has bir örüntü kazanır. Yavaş katman alt sınırı belirliyor:
                // en hızlı yıldız bile birkaç saniyeden kısa sürede tekrarlamıyor.
                float slowRate = lerp(0.7, 1.2, Hash3(cell + 63.1));
                float fastRate = lerp(1.5, 2.2, Hash3(cell + 77.9));
                float phase = twinkleRoll * 6.2831853;

                float flicker = sin(_Time.y * slowRate + phase) * 0.6
                              + sin(_Time.y * fastRate + phase * 2.3) * 0.4;

                // Parıldayan yıldız tamamen sönüp geri yanar. Küçük bir salınım sönük bir
                // yıldızda gözle seçilmiyordu; atmosfer türbülansı gerçekte de yıldızı
                // görünürlük sınırına kadar kısar.
                float twinkle = lerp(1.0, saturate(flicker * 0.5 + 0.5), twinkles);

                extras += present * shape * brightness * twinkle * tint
                          * _StarStrength * saturate(height);

                // Görünürlük kadranın kendi yüksekliğine bağlı: şafakta sönmesin.
                // Ufka yakınken çekirdek BEYAZLAŞIR, parlamaz: parlaklık çarpanı hâleyi
                // de büyütüp diskin çevresindeki duvarı dolduruyordu ve ton eşlemede
                // ikisi aynı turuncuya yapışıyordu. Gerçekte batan güneş çevresindeki
                // turuncudan daha beyaz-sarı okunur — ayrışma renkten gelir.
                float lowDisk = 1.0 - saturate(abs(_SunDirection.y) / 0.3);
                float3 sunDiskColor = lerp(_SunColor.rgb, float3(1.0, 0.92, 0.78), lowDisk * 0.5);
                float sunVisible = smoothstep(-0.10, 0.04, _SunDirection.y);
                float moonVisible = smoothstep(-0.10, 0.04, _MoonDirection.y);

                // Kadranlar yıldızlarla aynı sepete konmaz: yıldız sisin ilk
                // kalınlığında söner ama güneş astronomik parlaklıkta — berrak havada
                // ufukta da görünür, batımı izleyebilmemizin sebebi bu. Sonsuz gök
                // yolu yerine sınırlı bir yolla sönür: berrakta loş kızıl disk kalır
                // (rengi zaten süzülmüş güneş), yağışta ve çorbada kaybolur.
                // BULUT ORTAM SONDASI ÇİZİLİRKEN KADRANLAR KAPANIR. Bulut sistemi göğü
                // 16×16'lık bir küpe çizip ortalamasını ortam ışığı olarak kullanıyor;
                // güneş diski oraya girerse (parlaklık 1400) ortalama diskin rengine
                // kayıyor ve bulutlar kahverengiye çalıyor. Kaynak bunu şart koşuyor:
                // "capture the sky environment without sun disk" (`sky brief.md`).
                // Global'i bulut sisteminin ortam geçişi kuruyor.
                float3 disks = (1.0 - _DisableSunDisk) * (
                    Disk(direction, _SunDirection, sunDiskColor, 0.0016, 1400.0, sunVisible)
                  + Disk(direction, _MoonDirection, _MoonColor.rgb, 0.0011, 3000.0, moonVisible * 0.5));

                // Şimşek boşluklardan görünen gökyüzünü de aydınlatır, ama asıl parlayan
                // bulut kütlesinin kendisi — o bindirme geçişinde ekleniyor. Burada pay
                // küçük. Konum ve yarıçap bulutla birebir aynı değerden geliyor: ikisi
                // ayrı hesaplasaydı gökyüzü bir yerde, bulut başka bir yerde parlardı.
                //
                // Gökyüzü sonsuzda olduğu için mesafe doğrudan kullanılamıyor; lekenin
                // **açısal** boyutu hesaplanıyor. Yakın çakma geniş bir alanı kaplar,
                // uzak olan aynı yarıçapta ama dar bir leke bırakır — perspektif budur.
                float3 toStrike = _LightningPosition.xyz - _WorldSpaceCameraPos;
                float reach = max(1.0, length(toStrike));

                float cosine = dot(direction, toStrike / reach);
                float angle = sqrt(max(0.0, 2.0 - 2.0 * cosine));
                float spread = angle / max(0.001, _LightningPosition.w / reach);

                extras += _LightningFlash.rgb * 0.35
                          * lerp(0.08, 1.0, 1.0 / (1.0 + spread * spread));

                // Hava ile arkasındaki cisimler ayrı sislenir: yıldız ve şimşek lekesi
                // sisin ardında kalır, sisin kendisi şimşeği arazi sisiyle aynı payla
                // saçar. Yoğun çorbada yukarı bakınca süt görünür — yıldız değil.
                float fogAmount = SkyFogAmount(_WorldSpaceCameraPos, direction);
                sky += _LightningFlash.rgb * (LightningFogScatter * fogAmount);
                sky += extras * (1.0 - fogAmount);

                float diskFade = exp(-SkyFogDepth(_WorldSpaceCameraPos, direction, 8000.0));
                sky += disks * diskFade;

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
