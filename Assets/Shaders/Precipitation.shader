// include-rev: 13  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemeyebiliyor; bu satir degisince derleme zorlanir)
Shader "ToTheSummit/Precipitation"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Precipitation"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float4 _SnowColor;
            CBUFFER_END

            // Kar kendi kutusunda sarılır: aynı tanecik bütçesi kameraya daha sıkı
            // paketlenir. Nokta biçimli tane, uzayan damla kadar ekran alanı kaplamıyor.
            float3 _BoxSize;
            float3 _SnowBoxSize;

            // Damla ve tane farklı hızlarda düşer, farklı oranda rüzgâr yer:
            // her popülasyonun kendi birikmiş kayması ve kendi yön vektörü var.
            // Yağmur ayrıca sekiz hız sınıfına bölünür: damla boyutu hem düşme hızını
            // hem rüzgâra direncini belirlediği için her sınıf başka açıda iner.
            #define RAIN_SPEED_CLASSES 8
            float4 _RainDrifts[RAIN_SPEED_CLASSES];
            float4 _RainDirections[RAIN_SPEED_CLASSES];
            float3 _SnowDrift;
            float3 _SnowDirection;

            float _Snowiness;
            float _Density;          // görsel yoğunluk, şiddetin bükülmüş hali
            float _Precipitation;    // ham şiddet, damla boyutu dağılımı için
            float _SnowDensityScale;
            float _RainSize;
            float _RainStretch;
            float _SnowSize;
            float _SnowTurbulence;  // kar tanesinin girdaba kapılma genliği, metre
            float _RainTurbulence;  // damlanın girdaba kapılma genliği, metre
            float _SnowSpin;        // dönme hızı, rüzgârla ölçeklenir
            float3 _WindSweep;      // girdap alanının rüzgârla birikmiş ötelemesi, metre

            // Arazi yüksekliği, yerdeki kar profili ve perdenin rengi burada. Yakın
            // tanecikler uzak perdeyle AYNI kaynaklardan besleniyor: ikisi ayrı kural
            // kursaydı rüzgâr eşiği bir katmanda geçip ötekinde geçmeyebilirdi.
            #include "HeightFog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;   // x: 0 yağış, 1 sürüklenen kar
                float2 corner     : TEXCOORD0;
                float2 seedXY     : TEXCOORD1;
                float2 seedZW     : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner     : TEXCOORD0;
                float  alpha      : TEXCOORD1;
                float  isSnow     : TEXCOORD2;
                float3 color      : TEXCOORD3;
                float4 lobes      : TEXCOORD4;    // tutamın iki yan lobunun kayması
                float  shape      : TEXCOORD5;    // tanenin iskelet çeşidi, 0-1
                float  isDrift    : TEXCOORD6;    // sürüklenen kar mı, yağan kar mı
            };

            // Havanın rengi. AtmosphereController global olarak yazıyor; sis, bulut,
            // gökyüzü ve şimşek de aynı değerden besleniyor. Tane kendi rengini
            // seçmiyor, çünkü kendi ışığını üretmiyor.
            // Sürüklenen kar: kutu, birikmiş kayma, tane boyu, katman kalınlığı.
            float3 _SpindriftBox;
            float3 _SpindriftParticleDrift;
            float _SpindriftSize;
            float _SpindriftLayer;

            /// Tutamın tek bir topağı: dolu bir göbek, çevresinde sönen bir hâle.
            ///
            /// Keskin kenar taneyi kâğıttan kesilmiş gibi gösteriyor, oysa kümelenmenin
            /// sınırı yok. Ama sönüm yarıçapın tamamına yayılırsa da tanenin dolu hiçbir
            /// yeri kalmıyor ve tane olduğundan hem küçük hem soluk görünüyor — göbek
            /// dolu kalır, kuyruk yarıçapın yarısından fazlasına yayılır.
            float Lobe(float2 p, float2 offset, float radius)
            {
                return smoothstep(radius, radius * 0.42, length(p - offset));
            }

            // Tanecik ızgarasını kamera etrafında kutu katları kadar kaydırır.
            // Kaydırma kutu boyutunun tam katı olduğu için tanecikler dünyada sabit görünür.
            float3 WrapAroundCamera(float3 worldPos, float3 cameraPos, float3 box)
            {
                float3 relative = worldPos - cameraPos + box * 0.5;
                relative -= box * floor(relative / box);
                return cameraPos - box * 0.5 + relative;
            }

            float Hash(float3 seed)
            {
                return frac(sin(dot(seed, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            // Quad'ın ekranda inebileceği en dar genişlik. Altına düşen tanecik
            // rasterizer tarafından yutulur veya kaynar.
            #define MinPixelWidth 1.2

            // Bir radyanlık açının kaç piksele düştüğü. Projeksiyon matrisinin [1][1]
            // öğesi 1/tan(fov/2), dikey çözünürlükle çarpılınca ölçek çıkar.
            // abs şart: D3D render hedefine çizerken y eksenini ters çevirir ve bu öğe
            // negatife düşer; işareti almazsan ölçek bozulup her şey görünmez olur.
            float PixelsPerRadian()
            {
                return abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
            }

            // Türbülans alanı: havadaki girdaplar. Tanecik konumundan örneklenir, kendi
            // tohumundan değil — aynı girdaptaki tanecikler birlikte savrulmalı. Bağımsız
            // rastgelelik kar yağışı değil karınca sürüsü görüntüsü verir.
            //
            // Alan rüzgârla birlikte akar (Taylor hipotezi: türbülans ortalama akışla
            // taşınır). Kaba girdap rüzgârı tam izler, ince girdap kısmi hızda geride
            // kırılır. Dünyaya çakılı alan, tanecikleri içinden geçerken gergin teller
            // gibi yerinde titretiyordu — hava dönmüyor, tane sallanıyor okunuyordu.
            //
            // Dikey bileşen bilerek zayıf: gerçek türbülans yatay baskındır, dikeyde
            // güçlü olursa tanecikler yükselip fiziği bozar.
            float3 Turbulence(float3 worldPos, float t)
            {
                float3 p = (worldPos - _WindSweep) * 0.15;
                float3 coarse = float3(
                    sin(p.y + t * 1.3) * cos(p.z * 0.7 + t * 0.9),
                    sin(p.z + t * 1.1) * 0.35,
                    cos(p.x + t * 1.7) * sin(p.y * 0.8 + t * 1.2));

                // İkinci oktav: küçük girdaplar, üç kat frekans, üçte bir genlik
                float3 q = (worldPos - _WindSweep * 0.55) * 0.45;
                float3 fine = float3(
                    sin(q.z + t * 2.6) * cos(q.x * 0.8 + t * 2.1),
                    cos(q.x + t * 2.3) * 0.35,
                    sin(q.y + t * 3.1) * cos(q.z * 0.9 + t * 2.4));

                return coarse + fine * 0.33;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 cameraPos = _WorldSpaceCameraPos;
                float4 seed = float4(IN.seedXY, IN.seedZW);

                float isDrift = IN.positionOS.x;
                float driftHeight = 0.0;

                // Tür seçimi yoğunluk elemesinden bağımsız olmalı: ayrı karma.
                // Yağmurun payı dördüncü kuvvetle söner: kar başlar başlamaz damla
                // neredeyse kaybolur, sulu kar kısa ve silik kalır.
                // step(y,x) "x >= y" demek; eşik sıfırken hash'i tam sıfır olan tanecikler
                // sınırı geçip kar oluyordu. Kesin küçüktür gerekiyor: karlılık sıfırsa
                // tek bir tane bile düşmemeli
                float typeRoll = Hash(seed.yzx);
                float rainShare = pow(1.0 - _Snowiness, 4.0);
                float isSnow = 1.0 - step(1.0 - rainShare, typeRoll);

                // Marshall-Palmer: küçük damla çok yaygın, iri seyrek. Ölçek parametresi
                // yağış şiddetiyle değişir (Λ = 4.1·R^-0.21), yani sağanakta dağılım iriye
                // kayar. Çiseleme ince ve yavaş süzülür, sağanak iri ve hızlı iner.
                // Şiddet zaten iki Perlin katmanıyla dalgalandığı için yağışın temposu
                // rüzgâr sıfırken bile kendiliğinden değişir — ayrı bir gürültü gerekmez
                float dropSpread = lerp(3.6, 1.3, _Precipitation);
                int dropClass = (int)min(pow(Hash(seed.yxw), dropSpread) * RAIN_SPEED_CLASSES,
                                         RAIN_SPEED_CLASSES - 1);
                float dropSize = dropClass / (RAIN_SPEED_CLASSES - 1.0);

                float3 box = lerp(_BoxSize, _SnowBoxSize, isSnow);
                float3 drift = lerp(_RainDrifts[dropClass].xyz, _SnowDrift, isSnow);
                float3 worldPos = WrapAroundCamera(seed.xyz * box + drift, cameraPos, box);

                float variation = Hash(seed.xyz);

                // --- SÜRÜKLENEN KAR ---
                // Yağıştan üç farkı var ve üçü de fiziksel:
                //   Yüksekliği YERDEN ölçülür — tane havada değil, yüzeyin üstünde akıyor.
                //   Dağılım küpsel: çoğu yere yapışık, seyrek olanı yukarıda. Gerçek
                //   profil kuvvet yasası; küp onun ucuz ve doğru yönlü yaklaşımı.
                //   Yatay gider, düşmez.
                if (isDrift > 0.5)
                {
                    box = _SpindriftBox;

                    float lift = Hash(seed.zwy);
                    float above = lift * lift * lift * _SpindriftLayer;

                    // SINIR TABAKASI. Rüzgâr yüzeyde sıfıra iner ve yükseldikçe
                    // logaritmik olarak açılır; yerde zıplayan tane serbest akışın
                    // ancak küçük bir payını yer. Serbest hızla sürülünce taneler
                    // 12 m/s'de üşüşüyor ve rüzgârdan etkilenmiyormuş gibi, tek parça
                    // akan bir perde gibi okunuyordu.
                    //
                    // Kayma CPU'da serbest akışla birikiyor; taneye özel katsayıyla
                    // çarpmak integralin kendisini ölçekler, çünkü katsayı tane boyunca
                    // sabit (yükseklik değişmiyor).
                    // Yerde %30, katmanın tepesinde %95. Önce %12-55 kurulmuştu ve
                    // yerdeki tane 12 m/s rüzgârda 1.5 m/s ile sürünüyordu — rüzgârdan
                    // etkilenmiyormuş gibi okunuyordu. Zıplayan tane serbest akışın
                    // azımsanmayacak bir payını yer; süspansiyondaki neredeyse rüzgâr
                    // hızında gider.
                    float speedFactor = lerp(0.30, 0.95, lift);

                    float3 local = WrapAroundCamera(
                        seed.xyz * box + _SpindriftParticleDrift * speedFactor,
                        cameraPos, box);

                    worldPos = float3(local.x, TerrainHeightAt(local.xz) + above, local.z);
                    isSnow = 1.0;
                    driftHeight = lift;   // 0 yerde, 1 katmanın tepesinde
                }

                // Kar tanesi hafif, girdaba tamamen kapılır. Damla ağır, direnir —
                // ve ince serpinti iri damladan çok daha fazla sapar
                float dropResponse = _RainTurbulence * lerp(1.5, 0.4, dropSize);
                float response = lerp(dropResponse, _SnowTurbulence, isSnow);

                // Sürüklenen tanenin girdap payı RÜZGÂRLA ölçekli: dingin havada
                // sürüklenme zaten yok, türbülans da yok. Sabit payla düşük rüzgârda
                // taneler yerinde titriyordu.
                response = lerp(response, response * _SpindriftWind.w * 1.6, isDrift);

                // Türbülans yamalı gelir (intermittency): enerji öbekler hâlinde geçer,
                // düzgün yayılmaz. İki farklı frekanslı dalganın çarpımı tekrar desenini
                // kırar; öbekler rüzgârla birlikte akar. Damla da tane de aynı zarfı
                // okur — aynı hava.
                float3 gustPos = worldPos - _WindSweep;
                float patch = (sin(dot(gustPos.xz, float2(0.021, 0.017)) + _Time.y * 0.31) * 0.5 + 0.5)
                            * (sin(dot(gustPos.xz, float2(-0.013, 0.024)) + _Time.y * 0.23) * 0.5 + 0.5);
                response *= 0.5 + patch * 1.5;

                worldPos += Turbulence(worldPos, _Time.y) * response;

                // Çırpıntı: düşen tanenin ardındaki girdap kopması onu yaprak gibi iki
                // yana süzdürür. Faz ve frekans taneye özel; damla çırpmaz.
                //
                // ÖLÇÜ DÜŞÜŞE GÖRE, saniyeye göre değil. Kar 1 m/s iniyor: 1-3 Hz'lik
                // salınım her 30-100 santimetrede bir tam tur demek ve ekranda dar bir
                // testere dişi okunuyordu. 0.2-0.5 Hz'de bir tur 2-5 metreye yayılıyor,
                // tane süzülüyor gibi görünüyor.
                //
                // İki oktav, oranları tam sayı DEĞİL (2.7): tek sinüs periyodik ve göz
                // tekrarı yakalıyor. Kapanmayan iki eğri yol boyunca hiç aynı şekli
                // çizmiyor. İkinci oktavın genliği küçük — düzensizlik veriyor, ayrı
                // bir titreşim değil.
                float flutterFreq = 1.4 + 2.2 * variation;
                float px = _Time.y * flutterFreq + seed.x * 12.57;
                float pz = _Time.y * flutterFreq * 0.83 + seed.w * 12.57;

                float2 glide = float2(sin(px), cos(pz)) * 0.22
                             + float2(sin(px * 2.7 + seed.z * 6.28),
                                      cos(pz * 2.7 + seed.y * 6.28)) * 0.06;

                // Çırpıntı YALNIZ DÜŞEN taneye ait: süzülen kristalin ardındaki girdap
                // kopmasından doğuyor. Yerden kalkan tane düşmüyor, çırpmıyor —
                // rüzgârla taşınıyor. Uygulanınca düşük rüzgârda 22 santimlik salınım
                // taneyi yerinde zigzag çizdiriyordu: ilerlemesi salınımından yavaştı.
                worldPos += float3(glide.x, 0.0, glide.y) * isSnow * (1.0 - isDrift);

                // Gerçek kar tanesi 1 mm ile 15 mm arasında değişir. Dar bir dağılım
                // hepsini aynı boyda gösterip misket hissi yaratıyordu.
                // Damlada kalınlık hızla aynı sınıftan gelir: iri damla hem hızlı hem kalın
                float sizeSpread = lerp(0.45 + 1.15 * dropSize, 0.4 + 1.4 * variation, isSnow);
                float size = lerp(_RainSize, _SnowSize, isSnow) * sizeSpread;

                // Yoğunluk eşiğinin üstünde kalan tanecikler sıfır boyutla elenir.
                // Damlalar ayrıca karlılıkla seyrelir: geçişte sayıları da azalsın.
                // Burada da kesin küçüktür: yağış sıfırken havada tanecik asılı kalmasın
                float densityLimit = _Density * lerp(1.0 - _Snowiness, _SnowDensityScale, isSnow);
                size *= 1.0 - step(densityLimit, seed.w);

                // Sürüklenen kar kendi kapısından geçer: rüzgâr eşiği `_SpindriftDensity`
                // içine CPU'da gömülü, yerdeki gevşek kar profilden okunuyor. Yağış
                // yoğunluğuyla hiç ilgisi yok — yağış dinmişken de savrulur.
                if (isDrift > 0.5)
                {
                    float ground = TerrainHeightAt(worldPos.xz);
                    float supply = SampleSnowProfile(ground).r;

                    // ÖBEKLENME. Sürüklenen kar tekdüze bir perde değil: yatay konvektif
                    // rulolar boyunca şeritler halinde yoğunlaşıp seyreliyor. `patch`
                    // zaten yağışın okuduğu hamle zarfı — aynı hava, ikinci bir gürültü
                    // kurmaya gerek yok.
                    // Eşik geçildikten sonra kapı GENİŞ açılır: gerçek bir ground
                    // blizzard'da ayağının dibindeki hava taneyle dolu olur. 140
                    // katsayısıyla tanelerin yarısı eleniyordu ve toz seyrek kalıyordu.
                    // Öbeklenme zarfı (`patch`) yerinde — seyrelten o olmalı, taban değil.
                    float lifted = saturate(_SpindriftDensity * 400.0) * supply
                                 * (0.35 + patch * 1.65);

                    // DAĞILIM GAMMA BENZERİ: çok sayıda küçük, az sayıda iri. Düzgün
                    // dağılımda iri tane fazla çıkıyor ve toz yerine kar yağıyor gibi
                    // okunuyordu. Kare almak kuyruğu küçüğe kaydırıyor.
                    float grain = variation * variation;

                    // BOY YÜKSEKLİKLE KÜÇÜLÜR. Ölçüm: saltasyon katmanında maksimum
                    // tane çapı yükseklikle doğrusal azalıyor — iri tane yere yakın
                    // zıplar, yukarıda yalnız ince olan asılı kalır.
                    size = _SpindriftSize * (0.35 + 1.9 * grain)
                         * lerp(1.0, 0.3, driftHeight);

                    size *= 1.0 - step(lifted, Hash(seed.wxz));

                }

                float3 viewDirection = normalize(cameraPos - worldPos);
                float3 cameraRight = normalize(UNITY_MATRIX_I_V._m00_m10_m20);
                float3 cameraUp = normalize(UNITY_MATRIX_I_V._m01_m11_m21);

                // Damla bileşke hız yönünde uzar, tane kameraya döner
                float3 fallAxis = normalize(lerp(_RainDirections[dropClass].xyz, _SnowDirection, isSnow));
                float3 streakRight = normalize(cross(fallAxis, viewDirection));

                float3 right = lerp(streakRight, cameraRight, isSnow);
                float3 up = lerp(fallAxis, cameraUp, isSnow);

                // Uzama damlaya özel. Hızlı düşen damla kare başına daha çok yol alır,
                // yani daha uzun bir iz bırakır — çizgi boyu hız sınıfını izlemeli
                float rainStretch = _RainStretch * lerp(0.45, 1.25, dropSize) * (0.85 + 0.3 * variation);
                float stretch = lerp(rainStretch, 1.0, isSnow);

                // Bir pikselden ince quad'ı rasterizer ya tek piksel çizer ya tamamen
                // atlar; kalınlık farkı ekrana ulaşmadan yok olur ve tanecikler piksel
                // ızgarasına girip çıktıkça kaynar. Genişliği tabana sabitleyip taşınan
                // ışığı alfadan düşürmek ikisini birden çözer: ince olan soluk kalır.
                float centerDistance = length(worldPos - cameraPos);
                float pixelWidth = size * PixelsPerRadian() / max(centerDistance, 0.01);
                float widen = max(1.0, MinPixelWidth / max(pixelWidth, 1e-4));

                float2 offset = IN.corner - 0.5;
                worldPos += right * offset.x * size * widen + up * offset.y * size * stretch;

                float camDistance = length(worldPos - cameraPos);
                float fade = 1.0 - smoothstep(box.x * 0.25, box.x * 0.5, camDistance);

                // Kristal düz yüzeyleri döndükçe ışığı yakalayıp bırakır. Kar yağışının
                // parıldaması silüetten değil buradan gelir.
                // _SnowSpin birikmiş açı; tane başına sabit katsayı hızı çeşitlendirir
                float spin = _SnowSpin * (0.6 + 0.8 * Hash(seed.zxy)) + variation * 6.2831853;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.corner = IN.corner;
                OUT.isSnow = isSnow;
                OUT.isDrift = isDrift;

                // Tanenin rengi havanın renginden türer ama onunla çarpılmaz. Tane
                // güçlü bir saçıcı: içinde durduğu havadan parlaktır. Düz çarpım
                // kapalı havada taneyi parlak bulutların önüne koyu gri koyuyordu —
                // kirli, yarı saydam bir leke gibi. Karekök terimi tonu koruyup
                // parlaklığı yumuşakça kaldırır: kapalı gündüzde beyaza oturur,
                // şafakta turuncu kalır, gece kısık kalır, şimşekte parlar.
                // _SnowColor bir renk değil, havanın üstüne binen ton.
                float3 fog = _HeightFogColor.rgb;
                float3 snowTint = _SnowColor.rgb * (fog * 0.9 + sqrt(fog) * 0.75);

                // Atmosfer derinliği: uzak tane havanın rengine gömülür. Renk sabit
                // kalınca yakınla uzak aynı beyazlıkta çiziliyor ve yağış tek düzleme
                // yapışmış görünüyordu — sise karışma kutu içinde bile katmanlar açar.
                snowTint = lerp(snowTint, fog,
                    smoothstep(4.0, box.x * 0.5, camDistance) * 0.55);

                OUT.color = lerp(_RainColor.rgb, snowTint, isSnow);

                // Yakın tane, uzak perdeyle AYNI rengi okur. Ayrı kaynak kurulsaydı
                // taneler perdenin içinde başka renkte yüzerdi.
                OUT.color = lerp(OUT.color, SpindriftColor(), isDrift);

                // Tutamın iki yan lobu. Yönleri taneye sabit, dönüşü spin taşıyor:
                // kümelenme savruldukça topakları da birlikte dönüyor.
                // Uzanım geniş bir aralıktan: kısa uzanan lob merkezle kaynaşıp
                // tümsekli daire verir, uzun uzanan lob taşıp topağı iki parçalı
                // gösterir — biçim çeşidi buradan doğuyor.
                // Toz taneleri için lob, dönme ve iskelet ÖLÜ: fragment tarafında
                // yumuşak disk çiziliyor, hiçbiri okunmuyor. Dallanma mekânsal olarak
                // tutarlı — toz taneleri tampon içinde bitişik duruyor.
                if (isDrift > 0.5)
                {
                    OUT.lobes = 0.0;
                    OUT.shape = 0.0;

                    // Alfa burada kapanıyor: erken çıkış aşağıdaki ortak satırı
                    // atlıyor ve tanımsız alfayla çıkmak taneyi görünmez yapardı.
                    OUT.alpha = _SnowColor.a * (0.85 + 0.15 * Hash(seed.wxy))
                              * fade / sqrt(widen);
                    return OUT;
                }

                float lobeAngle = Hash(seed.wzy) * 6.2831853 + spin;
                float lobeAngle2 = Hash(seed.zwx) * 6.2831853 + spin * 1.13;
                float lobeReach = 0.18 + 0.26 * Hash(seed.yzw);
                float lobeReach2 = 0.18 + 0.26 * Hash(seed.wyx);

                OUT.lobes = float4(cos(lobeAngle) * lobeReach, sin(lobeAngle) * lobeReach,
                                   cos(lobeAngle2) * lobeReach2, sin(lobeAngle2) * lobeReach2);

                // İskelet çeşidi: merkez lobun ağırlığını ve bükümün frekansını
                // çeşitlendirir. Tek tip merkez, her taneyi aynı silüete mahkûm ediyordu.
                OUT.shape = Hash(seed.xwy);

                // Kalan damlalar da sönükleşsin; geçişte belirgin durmasınlar.
                // İri damla daha çok ışık taşır, ince serpinti silik kalır
                float rainAlpha = _RainColor.a * (1.0 - _Snowiness) * lerp(0.7, 1.2, dropSize);

                // Taneye özel opaklık: hepsi aynı yoğunlukta olunca derinlik kayboluyordu.
                // Aralıklar dar; iki çarpan üst üste bindiği için geniş bantlar karı
                // saydamlaştırıyordu — çeşitlilik kalsın, cılızlık kalmasın.
                float snowAlpha = _SnowColor.a * (0.85 + 0.15 * Hash(seed.wxy));

                // Dönen yüzey ışığa geldiğinde parlar, kenarına döndüğünde söner
                snowAlpha *= 0.9 + 0.1 * sin(spin);

                // Genişletme yapaydı; alfa düşmezse uzaktaki tanecikler olduğundan
                // parlak görünür. Tam ışık korunumu (bölü widen) ince damlaları
                // görünmezliğe itiyor — karekök, kalınlık farkını taşırken taneciği
                // ayakta bırakan denge
                OUT.alpha = lerp(rainAlpha, snowAlpha, isSnow) * fade / sqrt(widen);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.corner - 0.5;

                // Damla: ince bir çizgi, uçlara doğru sönerek biter — çubuk gibi durmasın
                float acrossRain = saturate(1.0 - abs(centered.x) * 2.0);
                float alongRain = saturate(1.0 - abs(centered.y) * 2.0);
                float rainMask = pow(acrossRain, 0.6) * smoothstep(0.0, 0.45, alongRain);

                // Kar tanesi: üç yumuşak lobun birleşimi.
                //
                // Havada süzülen şey tek bir kristal değil, kümelenme — yüzlerce
                // kristalin birbirine yapışmış hâli, düzensiz ve tüylü. Kristalin
                // kolları bir iki milimetre; onları görmek için taneyi göze değdirmek
                // gerekiyor. Altı kollu bir silüet çizmek mikroskop görüntüsünü
                // gökyüzüne koymak oluyordu ve kar yerine yıldız yağıyordu.
                //
                // Loblar `max` ile birleşiyor: toplamak merkezi doyurup taneyi tekrar
                // diske çeviriyor, max ise topakları ayrı ayrı ayakta bırakıyor.
                float2 p = centered * 2.0;

                // Daire kırılır: örnekleme konumu iki oktav sinüs bükümüyle oynar —
                // üç temiz daire değil, düzensiz tüylü topak. Faz lob kaymasından,
                // kaba oktavın frekansı iskelet çeşidinden: iki tane aynı bükümü
                // giymez, desen taneyle birlikte döner.
                float warpFreq = 5.5 + 3.0 * IN.shape;
                p += float2(sin(p.y * warpFreq + IN.lobes.x * 23.0),
                            sin(p.x * (warpFreq + 1.3) + IN.lobes.z * 19.0)) * 0.10;
                p += float2(sin(p.y * 15.0 + IN.lobes.w * 31.0),
                            sin(p.x * 13.0 + IN.lobes.y * 27.0)) * 0.055;

                // Merkez lob taneye göre büyüyüp küçülür; yan lobların yarıçapı
                // uzanımlarıyla ters orantılı, toplam uzanım quad sınırında kalır.
                // Küçük merkez + uzun uzanım = parçalı topak, büyük merkez + kısa
                // uzanım = tüylü yumak — filo artık tek silüet giymiyor.
                float centerRadius = 0.85 - 0.30 * IN.shape;
                float snowMask = max(Lobe(p, float2(0.0, 0.0), centerRadius),
                                 max(Lobe(p, IN.lobes.xy, 0.62 - 0.35 * length(IN.lobes.xy)),
                                     Lobe(p, IN.lobes.zw, 0.54 - 0.30 * length(IN.lobes.zw))));

                // SÜRÜKLENEN KAR TOZDUR, kristal değil. Yağan tane kümelenmiş bir
                // topaktır ve lobları görünür; yerden kalkan tane o kümelenmenin
                // rüzgârla KIRILMIŞ hâli — kenarsız, biçimsiz, yumuşak. Kristal
                // maskesini giydirmek kar yağıyormuş gibi okutuyordu.
                float dustMask = saturate(1.0 - dot(centered, centered) * 4.0);
                dustMask *= dustMask;

                float mask = lerp(lerp(rainMask, snowMask, IN.isSnow), dustMask, IN.isDrift);

                // Hacim: tek renkli leke kâğıttan kesilmiş gibi düz okunur. Kapalı
                // gökte ışık yukarıdan gelir — topağın üst yarısı aydınlık, altı loş;
                // dolu göbek kenardan bir tık parlak (kalın yer çok saçar). İkisi
                // birlikte topağı küreye çevirir. Yağmur damlasına uygulanmaz.
                float ballLight = (0.84 + 0.32 * IN.corner.y) * (0.88 + 0.18 * snowMask);
                float3 color = IN.color * lerp(1.0, ballLight, IN.isSnow * (1.0 - IN.isDrift));

                return half4(color, IN.alpha * mask);
            }
            ENDHLSL
        }
    }
}
