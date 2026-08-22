// include-rev: 43  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
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

            // OCCLUSION  `[Garg 2006, §5]` son adım: "we use the user-specified depth
            // map of the scene to find the pixels for which the rain streak is not
            // occluded by the scene. The streak is rendered only over those pixels."
            //
            // Makale bunu ayrı bir adım olarak yapmak zorunda çünkü girdisi bir fotoğraf
            // ve elinde yalnız KABA bir derinlik haritası var. Bizde derinlik tamponu
            // zaten var ve piksel başına kesin: `ZTest` varsayılan `LEqual` olduğu için
            // arazinin arkasına düşen iz parçaları rasterizer'da eleniyor.
            //
            // `ZWrite Off` — izler birbirini örtmemeli, saydamlar toplanmalı.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
            CBUFFER_END

            // Kar kendi kutusunda sarılır: aynı tanecik bütçesi kameraya daha sıkı
            // paketlenir. Nokta biçimli tane, uzayan damla kadar ekran alanı kaplamıyor.
            float3 _BoxSize;

            // Damla ve tane farklı hızlarda düşer, farklı oranda rüzgâr yer:
            // her popülasyonun kendi birikmiş kayması ve kendi yön vektörü var.
            // Yağmur ayrıca sekiz hız sınıfına bölünür: damla boyutu hem düşme hızını
            // hem rüzgâra direncini belirlediği için her sınıf başka açıda iner.
            #define RAIN_SPEED_CLASSES 8

            // RÜZGÂRIN SINIR TABAKASI. Gerekçe `vert` içinde, kullanıldığı yerde.
            #define WIND_Z0          0.1      // pürüzlülük boyu, metre (kayalık arazi)
            #define WIND_REF_HEIGHT  24.0     // serbest akış kotu = görünür hacmin tepesi
            #define WIND_MIN_HEIGHT  0.1      // = z₀; burada profil tam sıfır, rüzgâr yerde durur
            #define WIND_PROFILE_L   5.4806   // ln(24/0.1)
            #define WIND_LAG_TOP     4.3791   // G(24), gecikme integralinin üst ucu

            // Girdap oktavlarının uzamsal dalga sayısı ve kendi zaman frekansı.
            // `Turbulence` içindeki katsayıların aynası; atalet süzgeci bunları okuyor.
            #define TURB_COARSE_K    0.60
            #define TURB_COARSE_W    1.30
            #define TURB_FINE_K      1.80
            #define TURB_FINE_W      2.60
            #define TURB_COARSE_LAMBDA 10.472   // 2*pi/0.60, kaba oktavın dalga boyu
            #define KARMAN           0.4        // von Karman; yüzey tabakasında l = KARMAN*z

            float4 _RainDrifts[RAIN_SPEED_CLASSES];
            float4 _RainDriftsNear[RAIN_SPEED_CLASSES];   // iç kutunun kendi kayması
            float3 _NearBoxSize;
            float4 _RainDirections[RAIN_SPEED_CLASSES];
            float _Density;          // görsel yoğunluk, şiddetin bükülmüş hali
            float _Precipitation;    // ham şiddet, damla boyutu dağılımı için
            float _RainTurbulence;  // damlanın girdaba kapılma genliği, metre
            float3 _WindSweep;      // girdap alanının rüzgârla birikmiş ötelemesi, metre

            // ---- GARG-NAYAR İZ VERİTABANI  `[Garg 2006, §5]`, `rain-spec.md` §6 ----
            //
            // Damlanın görüntü izi sabit parlaklıkta bir çubuk değil: salınan damla
            // ışığı benekler, yayılmış highlight'lar ve eğri konturlar hâlinde kırıyor.
            // Desen ray-tracing gerektirdiği için offline pişirilmiş, burada aranıyor.
            TEXTURE2D_ARRAY(_StreakPoint);      SAMPLER(sampler_StreakPoint);
            TEXTURE2D_ARRAY(_StreakAmbient);    SAMPLER(sampler_StreakAmbient);

            // Çalışma kümesindeki dilim düzeni: ((köşe * 5) + dcam) * 10 + osc.
            // Köşe sırası (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
            float2 _StreakCellBlend;      // (v, h) hücresi içindeki pay
            float4 _StreakCornerPresent;  // köşe veritabanında var mı (0/1)
            float  _StreakMirror;         // azimut 180° üstündeyse doku yatay çevrilir
            float  _StreakDcamFraction[5];// dizi en uzun `dcam`'e göre dolduruldu
            float  _StreakExposure;       // kameranın pozlama süresi, saniye
            float  _StreakDbPeriod;       // veritabanının pişirildiği salınım periyodu
            float  _StreakSourceScale;    // veritabanı kaynağının bizim güneşimize oranı

            /// TEŞHİS KİPİ. 0 kapalı, 1 büyüt, 2 ham desen, 3 alfa.
            ///
            /// Fiziksel ölçekte iz 24 m'de 0.4 × 12 piksel ve α ≈ 0.02 — gözle
            /// "var mı yok mu" ayrılamıyor. Üç kip üç ayrı soruyu ayırıyor:
            /// boyut mu küçük, desen mi boş, alfa mı düşük.

            /// GÜNEŞ DİSKİNİN RADYANSI — yönlü kanalın kaynağı.
            ///
            /// `_HeightFogSunColor` KULLANILAMIYOR, adı yanıltıcı: kendi yorumu
            /// "gök, güneş yönünde, ufkun 2° üstü" diyor, yani GÖK rengi. Yönlü kanal
            /// güneşin kendisini istiyor ve disk, gökten mertebelerce parlak.
            /// Ölçüldü: o globalle radyans 0.08-0.32 bandında kalıyor ve damlalar
            /// gökten koyu düşüyordu.
            ///
            /// Ambient kanal `_HeightFogColor`'ı kullanmaya devam ediyor — o gerçekten
            /// gök rengi, damlanın kubbeden aldığı aydınlatma o.
            float3 _StreakSunRadiance;

            /// Bir taneciğin temsil ettiği damla kümesinin ekran payı. Gerekçesi
            /// kullanıldığı yerde: yoğunluğumuz gerçeğin binde biri.
            /// (dış kutu yoğunluğu, iç kutu yoğunluğu) damla/m³. Temsil payı buradan
            /// KONUMA göre türetiliyor; sabit değil.
            float4 _RainDensity;

            #define STREAK_DCAM_COUNT 5
            #define STREAK_OSC_COUNT 10

            /// Damlanın gerçek yarıçapı (metre). Sınıf oranından Marshall-Palmer
            /// aralığına eşleniyor: 0.25 mm ince serpinti, 2.5 mm iri damla.
            ///
            /// Quad'ın genişliği de buradan geliyor (çap = 2r₀) ve şeffaflık formülü
            /// de bunu istiyor. Tek kaynak: ikisi ayrı sayılardan gelseydi alfa ile
            /// ekrandaki kalınlık birbirinden bağımsız kayabilirdi.
            /// MARSHALL-PALMER'DAN SÜREKLİ ÖRNEKLEME.
            ///
            /// `N(D) = N₀·exp(−ΛD)`,  `Λ = 4.1·R^(−0.21)` mm⁻¹, `R` yağış oranı (mm/sa).
            /// Üstel dağılımdan örnek: `D = −ln(u)/Λ`.
            ///
            /// ESKİDEN 8 AYRIK DEĞERDİ. Yarıçap hız sınıfı indeksinden türüyordu
            /// (`dropClass/7`), yani boy ve kalınlık sekiz kademeye kilitliydi ve
            /// damlalar gözle "hepsi aynı" okunuyordu (kullanıcı bildirdi). Hız sınıfı
            /// ayrık KALMALI — rüzgâr tepkisi sınıf başına dizide tutuluyor — ama
            /// yarıçapın ayrık olması için sebep yok; makale de sürekli dağılım istiyor
            /// (`[Garg 2006, §5]`, dipnot 11).
            ///
            /// Çap 0.5-5 mm'ye kırpılıyor: altı sisin işi, üstü düşerken parçalanır.
            float DropRadius(float3 u, float intensity)
            {
                float rate = lerp(0.5, 50.0, intensity);          // mm/sa
                float lambda = 4.1 * pow(rate, -0.21);            // mm⁻¹

                // KAPLAMAYA GÖRE ÖRNEKLEME — sayıya göre değil.
                //
                // Marshall-Palmer sayı dağılımı: damlaların ezici çoğunluğu minik.
                // Ölçüldü: R = 50 mm/sa'te Λ = 1.82, ORTANCA ÇAP 0.38 mm. Sayıya göre
                // örneklenince tanecik bütçesinin neredeyse tamamı ekranda görünmeyen
                // damlalara gidiyor ve yağmur kayboluyor (kullanıcı bildirdi).
                //
                // Gerçek yağmurda o minik damlalar da var, ama görüntüyü iri olanlar
                // taşıyor ve bizim damla sayımız gerçeğin binde biri — bütçe görünür
                // olana harcanmalı.
                //
                // Ekran payı ≈ çap × hız ≈ D². Üstel dağılımın D²-ağırlıklı hâli
                // Gamma(3): üç üstel örneğin toplamı. Ortalama çap 3/Λ = 1.65 mm.
                //
                // Makale de bu esnekliği kendi dipnotunda veriyor (`[Garg 2006]`,
                // dipnot 11): "The size distribution can also be customized to include
                // larger drop sizes to create more dramatic rain effects."
                float sum = -(log(max(u.x, 1e-4)) + log(max(u.y, 1e-4)) + log(max(u.z, 1e-4)));
                float diameter = sum / lambda;                    // mm

                // 0.5-5 mm: altı sisin işi, üstü düşerken parçalanır.
                return clamp(diameter, 0.5, 5.0) * 0.0005;        // yarıçap, metre
            }

            /// Terminal hız (m/s), Gunn & Kinzer ölçümlerinin Atlas bağıntısı:
            ///   v(D) = 9.65 − 10.3·exp(−0.6·D),  D = çap (mm)
            ///
            /// MAKALEDE YOK. `[Garg 2006]` `α = 2r₀/(vT_exp)` formülünde `v`'yi
            /// kullanıyor ama modelini vermiyor; `rain-spec.md` §11.2-2 bu boşluğu
            /// işaretleyip Gunn & Kinzer'e yönlendiriyor.
            ///
            /// TANECİKLERİN GÖRSEL DÜŞÜŞ HIZI BU DEĞİL. `_RainDirections` 16 m/s
            /// taşıyor çünkü tanecikler 16-24 m uzakta ve açısal hız gerçek 9 m/s ile
            /// fazla yavaş okunuyordu. O bilinçli sapma yerinde duruyor; şeffaflık ise
            /// fiziksel hızı istiyor, yoksa alfa görsel bir ayara bağlanmış olur.
            float TerminalVelocity(float radius)
            {
                float diameterMm = radius * 2000.0;
                return 9.65 - 10.3 * exp(-0.6 * diameterMm);
            }

            // Arazi yüksekliği, yerdeki kar profili ve perdenin rengi burada. Yakın
            // tanecikler uzak perdeyle AYNI kaynaklardan besleniyor: ikisi ayrı kural
            // kursaydı rüzgâr eşiği bir katmanda geçip ötekinde geçmeyebilirdi.
            #include "HeightFog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;   // x: 0 yağış / 1 sürüklenen kar, y: iç kutu
                float2 corner     : TEXCOORD0;
                float2 seedXY     : TEXCOORD1;
                float2 seedZW     : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner     : TEXCOORD0;
                float  alpha      : TEXCOORD1;
                float3 color      : TEXCOORD3;
                float3 streak     : TEXCOORD7;    // (osc, dcam alt indeks, dcam payı)
                float2 streakCrop : TEXCOORD8;    // (v ölçeği, birleştirme yapıldı mı)
                float3 airColor   : TEXCOORD9;    // damlanın ARDINDAKİ göğün radyansı
            };

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
            // rastgelelik yağış değil karınca sürüsü görüntüsü verir.
            //
            // Alan rüzgârla birlikte akar (Taylor hipotezi: türbülans ortalama akışla
            // taşınır). Kaba girdap rüzgârı tam izler, ince girdap kısmi hızda geride
            // kırılır. Dünyaya çakılı alan, tanecikleri içinden geçerken gergin teller
            // gibi yerinde titretiyordu — hava dönmüyor, tane sallanıyor okunuyordu.
            //
            // Dikey bileşen bilerek zayıf: gerçek türbülans yatay baskındır, dikeyde
            // güçlü olursa tanecikler yükselip fiziği bozar.
            /// `gainCoarse` / `gainFine`: taneciğin ATALET SÜZGECİ, oktav başına.
            /// Gerekçe ve sayılar çağrıldığı yerde.
            float3 Turbulence(float3 worldPos, float t, float gainCoarse, float gainFine)
            {
                // ÖLÇEK GÖRÜNÜR HACİMDEN KÜÇÜK OLMAK ZORUNDA.
                //
                // Eskiden 0.15 idi, yani dalga boyu 42 m. Görünür yağmur hacmi 32 m;
                // alan tüm hacim boyunca bir periyodunu bile tamamlamıyordu ve 1 metre
                // arayla iki damla arasında yalnız 9 derece faz farkı kalıyordu. Sonuç
                // girdap değil, hacmin TOPLUCA salınmasıydı — kullanıcı "girdaplar çok
                // tek standart, çalışıyor mu emin değilim" dedi. Çalışıyordu, ama
                // komşu damlalar arasında fark üretmiyordu.
                //
                // 0.60 → dalga boyu 10.5 m, kutuda 3.1 periyot, 1 metrede 34 derece.
                float3 p = (worldPos - _WindSweep) * 0.60;
                float3 coarse = float3(
                    sin(p.y + t * 1.3) * cos(p.z * 0.7 + t * 0.9),
                    sin(p.z + t * 1.1) * 0.35,
                    cos(p.x + t * 1.7) * sin(p.y * 0.8 + t * 1.2));

                // İkinci oktav: küçük girdaplar, üç kat frekans, üçte bir genlik.
                // 1.80 → dalga boyu 3.5 m, kutuda 9.2 periyot, 1 metrede 103 derece —
                // komşu damlalar farklı girdabın içinde.
                float3 q = (worldPos - _WindSweep * 0.55) * 1.80;
                float3 fine = float3(
                    sin(q.z + t * 2.6) * cos(q.x * 0.8 + t * 2.1),
                    cos(q.x + t * 2.3) * 0.35,
                    sin(q.y + t * 3.1) * cos(q.z * 0.9 + t * 2.4));

                return coarse * gainCoarse + fine * (0.33 * gainFine);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 cameraPos = _WorldSpaceCameraPos;
                float4 seed = float4(IN.seedXY, IN.seedZW);

                float isNear = IN.positionOS.y;   // iç kutuya mı ait


                // Marshall-Palmer: küçük damla çok yaygın, iri seyrek. Ölçek parametresi
                // yağış şiddetiyle değişir (Λ = 4.1·R^-0.21), yani sağanakta dağılım iriye
                // kayar. Çiseleme ince ve yavaş süzülür, sağanak iri ve hızlı iner.
                // Şiddet zaten iki Perlin katmanıyla dalgalandığı için yağışın temposu
                // rüzgâr sıfırken bile kendiliğinden değişir — ayrı bir gürültü gerekmez
                // TEK KAYNAK: ÖNCE YARIÇAP, SINIF ONDAN TÜRER.
                //
                // Bir dönem tersiydi: sınıf bağımsız bir hash'ten çıkıyor, yarıçap da
                // ayrı bir hash'ten. Sonuç tutarsızdı — damla en yavaş sınıfta olup en
                // uzun izi bırakabiliyordu, çünkü HAREKET sınıftan, İZ BOYU yarıçaptan
                // geliyordu. Rüzgâr ve girdap tepkisi de sınıfa bağlı olduğu için aynı
                // kopukluk oradaydı.
                //
                // Şimdi yarıçap sürekli dağılımdan örnekleniyor, sınıf onun kovası.
                // Sınıf AYRIK KALMAK ZORUNDA: rüzgâr sürüklenmesi CPU'da sınıf başına
                // integre ediliyor (`_RainDrifts`), damla başına yapılamıyor.
                float dropRadius = DropRadius(
                    float3(Hash(seed.yxw), Hash(seed.wxy), Hash(seed.xwz)), _Precipitation);

                // 0.25-2.5 mm yarıçap aralığını sınıf ekseni olarak kullan.
                float dropSize = saturate((dropRadius - 0.00025) / 0.00225);
                int dropClass = (int)min(dropSize * RAIN_SPEED_CLASSES,
                                         RAIN_SPEED_CLASSES - 1);

                float physicalSpeed = TerminalVelocity(dropRadius);
                // ATALET SÜZGECİNİN GEVŞEME SÜRESİ ÇÖKME HIZINDAN: `τ = v_t/g`. Üç
                // popülasyonun üçü de ayrı: damla 2-9 m/s, kar tanesi 1.4, yerden
                // kalkan kırık tanecik ~0.5 (küçük ve düzensiz, havaya anında oturur).
                float fallSpeed = physicalSpeed;

                // İÇ KUTU. Ayrı bir tanecik popülasyonu; kendi kutusuna, kendi
                // kaymasıyla sarıyor. Kar da iç kutuya girebiliyor — orada temsil payı
                // yok, tanecikler bire bir çiziliyor, yani sıklaşma kendiliğinden
                // doğru sonucu veriyor.
                float3 box = isNear > 0.5
                           ? _NearBoxSize
                           : _BoxSize;
                float3 drift = isNear > 0.5
                             ? _RainDriftsNear[dropClass].xyz
                             : _RainDrifts[dropClass].xyz;

                // ---- RÜZGÂRIN SINIR TABAKASI ----
                //
                // Rüzgâr yerde sıfıra iner ve yükseldikçe logaritmik açılır:
                // `f(z) = ln(z/z₀)/ln(z_ref/z₀)`, `z₀ = 0.1 m` (kayalık). Sürüklenen
                // karda bu vardı, yağan yağışta yoktu — damla serbest akışı her kotta
                // tam yiyordu, yani yere yakın rüzgâr olması gerekenin iki katıydı.
                //
                // BANT DENENDİ VE ÖLÇÜMLE ELENDİ. Sınıf başına dört yükseklik bandına
                // ayrı kayma integre edilmişti. Bantların kaymaları zamanla SINIRSIZ
                // ayrışıyor (30 sn'de 101 m) ve kutuya sarılınca aradaki fark rastgele
                // bir sayıya dönüşüyor (±24 m). Damla düşerken bantlar arasında geçtiği
                // için o rastgele fark ona 21 m/s'ye kadar SAHTE YATAY HIZ olarak
                // biniyordu — rüzgârın kendisinden büyük. Belirti: "yağmur havada kar
                // gibi sürükleniyor".
                //
                // DOĞRUSU KAPALI BİÇİM. Damla yavaş havada geçirdiği süre boyunca
                // serbest akışın gerisinde kalır; bu GECİKME sınırlı bir integraldir:
                //
                //     Λ(z) = (U/v_t) · ∫_z^{z_ref} (1 − f(z')) dz'
                //
                // İntegralin analitik hâli `G(z) = z − z·(ln(z/z₀) − 1)/L`,
                // `L = ln(z_ref/z₀)`. Λ tek değişkenli, düzgün ve MONOTON — türevi
                // `dΛ/dt = U(1 − f(z))`, yani damlanın yatay hızı tam olarak `U·f(z)`.
                // Ne rastgele sıçrama var ne de sınırsız birikim.
                float3 probe = WrapAroundCamera(seed.xyz * box + drift, cameraPos, box);
                float aboveGround = clamp(probe.y - TerrainHeightAt(probe.xz),
                                          WIND_MIN_HEIGHT, WIND_REF_HEIGHT);

                float profile = log(aboveGround / WIND_Z0) / WIND_PROFILE_L;

                // `G(z_ref)` sabit: 24 − 24·(ln240 − 1)/ln240 = 4.3789
                float integral = WIND_LAG_TOP
                               - (aboveGround - aboveGround
                                  * (log(aboveGround / WIND_Z0) - 1.0) / WIND_PROFILE_L);

                // Yatay rüzgâr yönü ve büyüklüğü sınıf vektöründen; dikey bileşen
                // terminal hız olduğu için `.xz` rüzgârın kendisi.
                // BÜYÜKLÜK ŞART, birim yön yetmez: hem gecikme hem atalet süzgeci
                // ORAN hesaplıyor. Normalize edilmiş vektörle kar 1.4 m/s gidiyormuş
                // gibi okunur, rüzgâr payı kaybolur ve tane sınır tabakasını hiç
                // görmez.
                float3 classVelocity = _RainDirections[dropClass].xyz * _RainDirections[dropClass].w;
                float2 windFlat = classVelocity.xz;
                float windSpeed = length(windFlat);
                float2 windUnit = windSpeed > 1e-4 ? windFlat / windSpeed : float2(0.0, 0.0);

                float lag = (windSpeed / max(fallSpeed, 0.1)) * integral;
                drift.xz -= windUnit * lag;
                float3 worldPos = WrapAroundCamera(seed.xyz * box + drift, cameraPos, box);

                float variation = Hash(seed.xyz);


                // ---- TANECİĞİN GİRDABA TEPKİSİ: ATALET SÜZGECİ ----
                //
                // Tanecik havanın her kıvrımını takip edemez. Sürüklenme denklemi
                // birinci mertebedendir, yani tanecik alçak geçiren bir süzgeçtir:
                // gevşeme süresi `τ = v_t/g`, `ω` frekanslı bir zorlamaya genlik oranı
                // `1/√(1+(ωτ)²)` ile cevap verir. Hızlı kıvrımları ORTALAR, yemez.
                //
                // Damla alanın içinden GEÇTİĞİ için gördüğü frekans uzamsal ölçekten
                // doğuyor: `ω ≈ k·|V| + ω_zaman`. Girdap ölçeği bir adım önce dört kat
                // sıklaştırılmıştı ve ince oktav 13.85 m/s'de 27.5 rad/s ≈ 4 Hz'e
                // çıkmıştı — damlanın τ'su 0.21 sn, 4 Hz'i takip edemez. Model tam
                // genliği uyguladığı için damla yaprak gibi çırpıyordu: "yağmur havada
                // kar gibi sürükleniyor".
                //
                // ÖLÇÜLDÜ (rüzgâr 13.7 m/s):
                //
                //   0.5 mm damla  τ 0.206  kaba 0.451  ince 0.174
                //   1.1 mm damla  τ 0.455  kaba 0.223  ince 0.080
                //   5.0 mm damla  τ 0.932  kaba 0.111  ince 0.039
                //   kar tanesi    τ 0.102  kaba 0.714  ince 0.336
                //
                // YAĞMURU KARDAN AYIRAN ŞEY BU. Kar en ince damladan 1.6 kat, iri
                // damladan 9 kat fazla takip ediyor — kar süzülür, damla iner. Eskiden
                // ikisi de aynı alanı aynı genlikte yiyordu; fark elle konmuş bir
                // `lerp(1.5, 0.4, dropSize)` katsayısıyla taklit ediliyordu. O TELAFİ
                // TERİMİ SİLİNDİ; farkı artık fizik veriyor.
                float3 meanVelocity = classVelocity;
                meanVelocity.xz *= profile;

                float3 dropVelocity = float3(meanVelocity.x, -fallSpeed, meanVelocity.z);
                float dropSpeed = length(dropVelocity);

                float tau = fallSpeed / 9.81;
                float wCoarse = TURB_COARSE_K * dropSpeed + TURB_COARSE_W;
                float wFine   = TURB_FINE_K   * dropSpeed + TURB_FINE_W;
                float gainCoarse = rsqrt(1.0 + wCoarse * tau * wCoarse * tau);
                float gainFine   = rsqrt(1.0 + wFine   * tau * wFine   * tau);

                // ---- GİRDAP ÖLÇEĞİ KOTLA KÜÇÜLÜR: ENERJİ OKTAVLAR ARASINDA KAYAR ----
                //
                // Yüzey tabakasında girdabın BOYU yükseklikle büyür (`l ≈ κz`), hız
                // değişintisi ise yaklaşık sabit kalır. Yere yakın 10.5 m'lik girdap
                // fiziksel olarak SIĞMAZ — zemin onu keser.
                //
                // ÖNCE `min(1, κz/λ)` İLE KESİLDİ, ÖLÇÜMLE ELENDİ: sapmayı 18 kat
                // düşürüyordu (3.6 cm → 0.2 cm), yağmur bıçak gibi düzleşiyor ve kar
                // yerde savrulmayı tamamen bırakıyordu (40.8 cm → 3.5 cm, yani yer
                // blizzard'ı yok oluyordu). Hata, o formülün ENERJİYİ YOK ETMESİ:
                // oysa sığmayan enerji kaybolmaz, küçük ölçeklere geçer.
                //
                // Doğrusu payı kaydırmak. Alanın dalga boyu sabit olduğu için ölçeği
                // değiştiremiyoruz; yapılabilecek olan kaba oktavın ancak sığdığı kadar
                // enerji tutması, kalanın ince oktava geçmesi. Toplam hız değişintisi
                // korunur, yer değiştirme düşer — çünkü küçük girdabın yer değiştirmesi
                // `1/k` ile küçüktür.
                //
                // Taban 50/50: mevcut alanın oktav ağırlıkları (0.5 / 0.165) zaten
                // `k_ince/k_kaba = 3` oranında, yani hız değişintisi iki oktavda eşit.
                //
                // ÖLÇÜLDÜ — uç %10'daki sapma/iz oranı:
                //   orta hava   1.55 → 0.75
                //   fırtına     1.58 → 0.42
                // Kar 2 m kotta 40.8 → 15.0 cm (iz boyu 11 cm), yani savrulmaya devam.
                float coarseShare = 0.5 * saturate(KARMAN * aboveGround / TURB_COARSE_LAMBDA);
                gainCoarse *= sqrt(coarseShare / 0.5);
                gainFine   *= sqrt((1.0 - coarseShare) / 0.5);

                float response = _RainTurbulence;

                // Sürüklenen tanenin girdap payı RÜZGÂRLA ölçekli: dingin havada
                // sürüklenme zaten yok, türbülans da yok. Sabit payla düşük rüzgârda
                // taneler yerinde titriyordu.
                response = response;

                // Türbülans yamalı gelir (intermittency): enerji öbekler hâlinde geçer,
                // düzgün yayılmaz. İki farklı frekanslı dalganın çarpımı tekrar desenini
                // kırar; öbekler rüzgârla birlikte akar. Damla da tane de aynı zarfı
                // okur — aynı hava.
                float3 gustPos = worldPos - _WindSweep;
                float patch = (sin(dot(gustPos.xz, float2(0.021, 0.017)) + _Time.y * 0.31) * 0.5 + 0.5)
                            * (sin(dot(gustPos.xz, float2(-0.013, 0.024)) + _Time.y * 0.23) * 0.5 + 0.5);
                response *= 0.5 + patch * 1.5;

                // DAMLA BAŞINA YÖN SAPMASI — girdap alanının kendi türevinden.
                //
                // Sınıf ayrık kalmak zorunda (rüzgâr sürüklenmesi CPU'da sınıf başına
                // integre ediliyor), yani bir sınıftaki bütün damlalar birebir aynı
                // yönde iniyordu: ekranda yalnız sekiz iz açısı vardı.
                //
                // SAPMA UYDURULMUYOR, ZATEN VAR OLANDAN TÜRETİLİYOR. Damlanın çizilen
                // konumu `x + response·T(x,t)`; o konumun gerçek hızı bileşke hızın TAM
                // TÜREVİ, yani `V + response·(∂T/∂t + (V·∇)T)`. Tam türev tek ek örnekle
                // alınıyor: damlanın `dt` sonra bulunacağı yerde alan yeniden
                // örnekleniyor. Adım fırtınada 0.28 m — ince oktavın 3.5 m'lik dalga
                // boyunun çok altında (kh = 0.45 rad, sonlu fark hatası %0.8).
                //
                // Süzgeç buraya da giriyor: takip edilmeyen kıvrım yön de saptıramaz.
                float3 turbHere = Turbulence(worldPos, _Time.y, gainCoarse, gainFine);

                const float dt = 0.02;   // saniye
                float3 turbNext = Turbulence(worldPos + meanVelocity * dt, _Time.y + dt,
                                             gainCoarse, gainFine);
                float3 velocityFluctuation = (turbNext - turbHere) * (response / dt);

                worldPos += turbHere * response;

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

                // Gerçek kar tanesi 1 mm ile 15 mm arasında değişir. Dar bir dağılım
                // hepsini aynı boyda gösterip misket hissi yaratıyordu.
                // Damlada kalınlık hızla aynı sınıftan gelir: iri damla hem hızlı hem kalın
                // ---- YAĞMUR QUAD'I FİZİKSEL  `[Garg 2006, §5]` ----
                //
                // "Based on the drop's distance from the camera and the angle that
                // drop's velocity vector makes with the camera's optical axis, we scale
                // the final streak texture to its projected size in the image."
                //
                // İzdüşüm boyutu ayrı hesaplanmıyor: quad DÜNYA ölçüsünde kuruluyor ve
                // perspektif izdüşümü ölçeklemeyi kendisi yapıyor. Boy pozlama süresinde
                // kat edilen yol, genişlik damlanın çapı.
                //
                // ESKİ HÂLİ `_RainSize` × `_RainStretch` idi, yani görsel ayardan
                // geliyordu. O ayar iz görünümünü veritabanından almayan bir modele
                // aitti; doku artık gerçek bir damlanın izini taşıyor ve ölçeği de
                // gerçek olmalı, yoksa desenin frekansı ekranda yanlış boyda çıkar.
                float radius = dropRadius;

                float rainWidth = 2.0 * radius;
                float rainLength = dropSpeed * _StreakExposure;

                float sizeSpread = 0.4 + 1.4 * variation;
                float size = rainWidth;

                // Yoğunluk eşiğinin üstünde kalan tanecikler sıfır boyutla elenir.
                // Damlalar ayrıca karlılıkla seyrelir: geçişte sayıları da azalsın.
                // Burada da kesin küçüktür: yağış sıfırken havada tanecik asılı kalmasın
                float densityLimit = _Density;
                size *= 1.0 - step(densityLimit, seed.w);


                float3 viewDirection = normalize(cameraPos - worldPos);
                float3 cameraRight = normalize(UNITY_MATRIX_I_V._m00_m10_m20);
                float3 cameraUp = normalize(UNITY_MATRIX_I_V._m01_m11_m21);

                // Damla bileşke hız + türbülans dalgalanması yönünde uzar (yukarıda
                // türetildi); tane kameraya döner, yön okumaz.
                float3 rainAxis = normalize(dropVelocity + velocityFluctuation);
                float3 fallAxis = normalize(rainAxis);
                float3 streakRight = normalize(cross(fallAxis, viewDirection));

                float3 right = streakRight;
                float3 up = fallAxis;

                // Uzama artık serbest bir ayar değil: boy/genişlik oranı damlanın
                // pozlama süresince kat ettiği yolun çapına oranı. Hızlı düşen iri damla
                // kendiliğinden daha uzun iz bırakıyor.
                float stretch = rainLength / max(rainWidth, 1e-6);

                // Bir pikselden ince quad'ı rasterizer ya tek piksel çizer ya tamamen
                // atlar; kalınlık farkı ekrana ulaşmadan yok olur ve tanecikler piksel
                // ızgarasına girip çıktıkça kaynar. Genişliği tabana sabitleyip taşınan
                // ışığı alfadan düşürmek ikisini birden çözer: ince olan soluk kalır.
                // ---- İZ VERİTABANI İNDEKSLERİ (yalnız yağmur) ----
                //
                // `osc` damla başına RASTGELE. Makale `§5`: "Each drop is also randomly
                // assigned oscillation parameters Osc from the set of parameters used
                // to create our streak database." Hangi indeksin hangi genlik çiftine
                // karşılık geldiği ne makalede ne arşivde yazıyor (`rain-spec.md`
                // §11.2-7); rastgele seçim bunu gerektirmiyor.
                float oscIndex = min(floor(Hash(seed.zyw) * STREAK_OSC_COUNT),
                                     STREAK_OSC_COUNT - 1.0);

                // `θ_v` kameranın bakış yönüyle damlanın DÜŞÜŞ yönü arasındaki açı.
                // Veritabanı klasörü diklikten sapmayı tutuyor: `dcam = |90° − θ_v|`.
                // Ölçüldü: iz boyu oranı `cos(dcam)` (makale dipnot 10 — "the lengths
                // of the streaks for θ_v ≠ 90° are smaller since the viewing direction
                // is not orthogonal to the fall direction").
                float thetaV = degrees(acos(clamp(dot(viewDirection, fallAxis), -1.0, 1.0)));
                float dcamPos = clamp(abs(90.0 - thetaV) / 20.0, 0.0,
                                      STREAK_DCAM_COUNT - 1.0);

                // ---- DAMLA BOYUTU: KIRPMA / BİRLEŞTİRME  `[Garg 2006, §5]` ----
                //
                // Denklem 2'ye göre damla boyutu yalnız salınım FREKANSINI değiştiriyor,
                // deseni değil. Yani farklı boyuttaki damla aynı desenden geçiyor, ama
                // periyodu farklı: `ω_n ∝ r₀^{-3/2}` → `T_new = 2π/ω₂ ∝ r₀^{3/2}`.
                //
                // Pozlama süresi içinde dokunun ancak `T_exp/T_new` kadarı görünüyor.
                // Oran 1'in altındaysa doku KIRPILIYOR, üstündeyse kopyaları
                // BİRLEŞTİRİLİP kırpılıyor — makale dipnot 13: "For long exposure times,
                // the streak texture repeats itself with the time period of oscillation."
                float newPeriod = _StreakDbPeriod * pow(radius / 0.0016, 1.5);
                float vScale = _StreakExposure / max(newPeriod, 1e-6);

                float centerDistance = length(worldPos - cameraPos);
                float pixelWidth = size * PixelsPerRadian() / max(centerDistance, 0.01);
                float widen = max(1.0, MinPixelWidth / max(pixelWidth, 1e-4));

                float2 offset = IN.corner - 0.5;
                worldPos += right * offset.x * size * widen + up * offset.y * size * stretch;

                float camDistance = length(worldPos - cameraPos);
                // SÖNÜM KUTU YÜZEYİNE SIKIŞIK.
                //
                // Sönümün tek işi sarma sınırında patlamayı gizlemek: tanecikler
                // kameranın etrafındaki küpte sarılıyor ve yüzeyde (0.5·kutu) alfa
                // sıfır olmalı, yoksa damla birden belirip kayboluyor.
                //
                // Eskiden 0.25'te başlıyordu ve bedeli ölçüldü: küresel kabuk dağılımına
                // göre taneciklerin %87.5'i sönüm bölgesinde kalıyor, yani bütçenin
                // ancak sekizde biri tam güçte çiziliyordu. Kullanıcı "yağış 1'de
                // istediğim yoğunluğu hissedemiyorum" dedi.
                //
                // 0.45'te başlayınca tam güçlü pay %12.5'ten %73'e çıkıyor. Sönüm bandı
                // hâlâ 0.95 m kalınlığında ve sarma yüzeyinde alfa sıfır — patlama yok.
                float fade = 1.0 - smoothstep(box.x * 0.45, box.x * 0.5, camDistance);

                // Kristal düz yüzeyleri döndükçe ışığı yakalayıp bırakır. Kar yağışının
                // parıldaması silüetten değil buradan gelir.

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.corner = IN.corner;
                OUT.streak = float3(oscIndex, floor(dcamPos), frac(dcamPos));
                // Yataydan eğim: damlanın GERÇEK yörünge açısı, girdap sapması dahil.
                // `fallAxis` yağmurda `dropVelocity + velocityFluctuation`'ın birimi.
                OUT.streakCrop = float2(vScale, vScale > 1.0 ? 1.0 : 0.0);

                // DAMLANIN ARDINDAKİ GÖK. Damla ışık üretmiyor, arkadan geleni kırıyor;
                // ambient kanalın kaynağı bu yüzden göğün O YÖNDEKİ radyansı.
                //
                // `_HeightFogColor` KULLANILAMIYOR: o sisin rengi, gökten sönük.
                // Ölçüldü — onunla radyans 0.08-0.32 bandında kalıyor ve damlalar
                // gökten koyu düşüp SİYAH LEKE gibi okunuyordu. Göğü çizen fonksiyon
                // `AirColor` (`Sky.shader` da onu çağırıyor), tek kaynak o.
                // TON İLE PARLAKLIK AYRI.
                //
                // Damla dalga boyu seçmiyor; rengi üstüne düşen ışıktan. Ama gündüz tek
                // bir yönün rengini alamaz: onu aydınlatan gök kubbenin TAMAMI ve sonuç
                // nötre yakın. Ham `AirColor` olduğu gibi geçirilince damlalar mavi
                // okunuyordu — projede aynı hata savrulan karda da yapılmış ve kuralı
                // `HeightFog.hlsl`'e yazılmış ("ufuk mavisini taşımak yamacı fosforlu
                // maviye çeviriyordu").
                //
                // Ayrım güneşin yüksekliğinden: alçakken ışık yönlü ve renkli, şafakta
                // yağmur kızıl olmalı; yükseldikçe dağınık ve nötr.
                //
                // LUMİNANS KORUNUYOR — yalnız ton nötre çekiliyor. Parlaklık ayrı
                // ölçülmüş bir büyüklük (`SourceScale`), ona dokunulmuyor.
                float3 sky = AirColor(-viewDirection);
                float skyLuma = dot(sky, float3(0.2126, 0.7152, 0.0722));
                float3 skyHue = sky / max(1e-4, skyLuma);
                float lowSun = 1.0 - smoothstep(0.02, 0.28, _SunHeight);
                OUT.airColor = lerp(1.0, skyHue, lowSun * 0.9) * skyLuma;

                OUT.color = _RainColor.rgb;

                // ---- ŞEFFAFLIK  `[Garg 2006, §5]`, `[Garg & Nayar 2005]` ----
                //
                //   I_r = (1−α)·I_b + α·I_streak,     α = 2r₀ / (v·T_exp)
                //
                // Damla pozlama süresince yol alıyor; tek pikselde geçirdiği süre
                // çapının kat ettiği yola oranı kadar. Kısa pozlamada iz DAHA OPAK
                // olur — makalenin kendi vurgusu.
                //
                // Hız FİZİKSEL terminal hız, taneciğin görsel düşüş hızı değil
                // (gerekçe `TerminalVelocity` başında). Yarıçap ve hız quad kurulurken
                // zaten hesaplandı.
                // TEMSİL PAYI GEOMETRİYE DEĞİL KAPLAMAYA GİRİYOR.
                //
                // Bir dönem quad'ın enini ve boyunu çarpıyordu. Kâğıtta çıktı: iz boyu
                // 40-183 cm oluyordu, oysa gerçek damla 1/60 s'de 3.4-15 cm yol alır.
                // 1.8 metrelik "damla" yağmur değil çubuk.
                //
                // Tanecik N damlayı temsil ediyorsa N kat BÜYÜK değil N kat OPAK olmalı:
                // üst üste binen N damlanın kapaması `1 − (1−α)^N`. Boyut fiziksel
                // kalıyor, iz görünür oluyor. α 0.02 → 0.21.
                // Hız `dropSpeed`, terminal hız DEĞİL: `[Garg 2006]`'nın α'sı damlanın
                // pozlama boyunca süpürdüğü yolun kaçta kaçını kapattığı. Boy da aynı
                // yoldan çıkıyor; ikisi ayrı hız okursa iz uzayıp saydamlığı sabit
                // kalır, yani enerji yoktan var olur.
                float singleDrop = saturate(2.0 * radius
                                            / max(dropSpeed * _StreakExposure, 1e-6));
                // TEMSİL PAYI KONUMDAN TÜRÜYOR, kutudan değil.
                //
                // Aynı noktadaki iki tanecik hangi kutudan geldiğine bakılmaksızın aynı
                // sayıda gerçek damlayı temsil etmeli; kutuya bağlansaydı aynı yerde iki
                // farklı opaklık çıkardı.
                //
                // İç kutunun payı KENDİ SÖNÜMÜYLE aynı eğriyle giriyor: iç tanecikler
                // 0.45·12 = 5.4 m'de sönmeye başlayıp 6 m'de bitiyor, yani ötesinde
                // yoğunluğa katkıları da yok. İkisi ayrışsaydı sınırda opaklık sıçrardı.
                float nearShare = 1.0 - smoothstep(_NearBoxSize.x * 0.45,
                                                   _NearBoxSize.x * 0.5, centerDistance);
                float localDensity = _RainDensity.x + _RainDensity.y * nearShare;
                float representation = 1000.0 / max(localDensity, 1e-4);

                float rainAlpha = 1.0 - pow(1.0 - singleDrop, representation);

                // Taneye özel opaklık: hepsi aynı yoğunlukta olunca derinlik kayboluyordu.
                // Aralıklar dar; iki çarpan üst üste bindiği için geniş bantlar karı
                // saydamlaştırıyordu — çeşitlilik kalsın, cılızlık kalmasın.

                // Dönen yüzey ışığa geldiğinde parlar, kenarına döndüğünde söner

                // Genişletme yapaydı; alfa düşmezse uzaktaki tanecikler olduğundan
                // parlak görünür. Tam ışık korunumu (bölü widen) ince damlaları
                // görünmezliğe itiyor — karekök, kalınlık farkını taşırken taneciği
                // ayakta bırakan denge
                // YARIM KORUNUM — ÖLÇÜLEREK GERİ ALINDI.
                //
                // Bir dönem tam korunum (`÷widen`) yazıldı, gerekçesi "kalınlık farkı
                // parlaklığa geçsin"di. Ölçüldü ve gerekçe çürüdü: kalınlık farkı ZATEN
                // ekrana ulaşamıyor, damlaların eni 0.1-1.0 piksel ve hepsi 1.2 piksel
                // raster tabanına oturuyor. Tam korunum hiçbir şey kazandırmadı, yalnız
                // her damlayı `widen` kadar böldü — 5 m'deki tipik damlanın alfası 0.45'ten
                // 0.17'ye düştü ve kullanıcı "çok şeffaflar" dedi.
                //
                // Yarım korunumda kademelenme de DAHA İYİ: `widen` damla boyuyla ters
                // orantılı olduğu için bölen de damlayla değişiyor. İnce damla ÷3.46,
                // kalın damla ÷1.1 — son alfa 0.17 ile 0.79 arası, 4.6 kat fark.
                // Tam korunumda bu fark 2.4 kattı.
                // ÜS BİLİNÇLİ OLARAK 0.35 — tam korunum 0.5 olurdu.
                //
                // Bağlayıcı kısıt temsil payı değil, `widen` bölmesi: ortanca `widen` 11,
                // yani tam korunumda alfa 3.3'e bölünüyor ve damlalar şeffaf kalıyor.
                // Üs taraması (kutu 32 m, şiddet 0.4, 20 000 örnek):
                //
                //   üs 0.50 → ortanca alfa 0.262, ince/kalın farkı 2.49x
                //   üs 0.35 → ortanca alfa 0.377, farkı 1.94x
                //   üs 0.25 → ortanca alfa 0.479, farkı 1.64x
                //   üs 0.00 → ortanca alfa 0.873, farkı 1.09x   (fark yok olur)
                //
                // 0.35: alfa 1.44 kat artıyor, kademelenmenin dörtte üçü duruyor.
                //
                // Bu makalenin de yaptığı sapma. `[Tatarchuk 2006, §3.6.1]`: "Realistic
                // rain is very faint in bright regions... While this may be physically
                // accurate, it doesn't create a perception of strong rainfall."
                float rainThin = pow(widen, -0.35);
                OUT.alpha = rainAlpha * fade * rainThin;
                return OUT;
            }

            /// Dört `(v,h)` köşesinin ağırlığı. Sıra pişiricideki dilim sırasıyla
            /// aynı: (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
            float4 StreakCornerWeights()
            {
                float vT = _StreakCellBlend.x, hT = _StreakCellBlend.y;
                float4 w = float4((1.0 - vT) * (1.0 - hT), (1.0 - vT) * hT,
                                  vT * (1.0 - hT), vT * hT);

                // EKSİK KOMBİNASYON: veritabanında yok (uç dikey açıda iz dejenere,
                // `rain-spec.md` §5.4.5 — ölçüldü, yalnız `v = ±90` kutuplarında ve
                // orada da `h170` dışındakiler). Ağırlığı sıfırlanıp kalanlar yeniden
                // normalize ediliyor, yoksa iz o hücrede sönüyor.
                return w * _StreakCornerPresent;
            }

            /// Tek `dcam` seviyesinde dört köşenin harmanı.
            float SampleStreakAtDcam(float2 uv, float osc, int dcam, float4 weights)
            {
                float sum = 0.0, total = 0.0;

                // Dizi en uzun `dcam`'e göre dolduruldu; kısa olanların altı boş.
                float2 st = float2(uv.x, uv.y * _StreakDcamFraction[dcam]);

                [unroll]
                for (int c = 0; c < 4; c++)
                {
                    float w = weights[c];
                    if (w <= 0.0) continue;
                    float slice = (c * STREAK_DCAM_COUNT + dcam) * STREAK_OSC_COUNT + osc;
                    sum += w * SAMPLE_TEXTURE2D_ARRAY(_StreakPoint, sampler_StreakPoint,
                                                      st, slice).r;
                    total += w;
                }

                return total > 0.0 ? sum / total : 0.0;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.corner - 0.5;

                // ---- YAĞMUR İZİ: VERİTABANINDAN  `[Garg 2006, §5]` ----
                //
                // Prosedürel çizgi SİLİNDİ. Makalenin kendi ölçümü (`§3`): "a spherical
                // drop model is simply not adequate when rendering close-up rain
                // streaks" — sabit parlaklıklı çubuk benekleri, yayılmış highlight'ları
                // ve eğri konturları üretemiyor.
                //
                // Sekiz komşu, üç açısal boyutta ikişer: `(θ_l, φ_l)` dört köşe ×
                // `θ_v` iki komşu. Makale buna "bilinear" diyor ama üç boyutta iki
                // komşu, yani trilineer.
                float streakU = _StreakMirror > 0.5 ? 1.0 - IN.corner.x : IN.corner.x;

                // Kırpma / birleştirme: oran 1'in üstündeyse doku kendini tekrar ediyor.
                float streakV = IN.corner.y * IN.streakCrop.x;
                streakV = IN.streakCrop.y > 0.5 ? frac(streakV) : streakV;
                float2 streakUV = float2(streakU, streakV);

                float4 cornerWeights = StreakCornerWeights();
                int dcamLow = (int)IN.streak.y;
                int dcamHigh = min(dcamLow + 1, STREAK_DCAM_COUNT - 1);

                float pointStreak = lerp(
                    SampleStreakAtDcam(streakUV, IN.streak.x, dcamLow, cornerWeights),
                    SampleStreakAtDcam(streakUV, IN.streak.x, dcamHigh, cornerWeights),
                    IN.streak.z);

                // AMBIENT AYRI ÖRNEKLENİYOR ve toplanıyor (`§5`: "we scale each of these
                // textures individually with the corresponding source intensity and
                // color. These scaled textures are added"). Işık yönü olmadığı için
                // yalnız `(θ_v, Osc)` ile indeksleniyor.
                float ambientStreak = lerp(
                    SAMPLE_TEXTURE2D_ARRAY(_StreakAmbient, sampler_StreakAmbient,
                        float2(streakU, streakV * _StreakDcamFraction[dcamLow]),
                        dcamLow * STREAK_OSC_COUNT + IN.streak.x).r,
                    SAMPLE_TEXTURE2D_ARRAY(_StreakAmbient, sampler_StreakAmbient,
                        float2(streakU, streakV * _StreakDcamFraction[dcamHigh]),
                        dcamHigh * STREAK_OSC_COUNT + IN.streak.x).r,
                    IN.streak.z);

                // KIRPMA UÇLARI YUMUŞATILIYOR (`§5`: "The streaks ends are then blurred
                // to smooth out the sharp edges due to cropping"). Yarıçap makalede yok
                // (`rain-spec.md` §11.2-5); dokunun kendi çözünürlüğünde bir bant
                // seçildi — iki teksel, `size16`'da 1/262.
                float endFade = smoothstep(0.0, 0.008, IN.corner.y)
                              * smoothstep(0.0, 0.008, 1.0 - IN.corner.y);

                // Her kaynak KENDİ rengiyle ölçeklenip toplanıyor (`§5` sonu).
                // HALE MASKESİ UYGULANMIYOR — gerekçesi geometri, kolaylık değil.
                // `§5`: "we use a mask whose intensity at a pixel i is equal to 1/d_i²,
                // where d_i is the distance in 3D of the falling drop from the light
                // source". Güneş sonsuzda; `d_i` her damla için aynı, yani maske sabit
                // bir çarpana iniyor ve zaten kaynağın şiddetinde taşınıyor. Hale de
                // ışık konisi de SONLU mesafedeki kaynağın işi. Sahneye lamba, fener ya
                // da şimşek eklendiğinde bu maske gerekecek — `DECISIONS.md`.
                //
                // Anizotropik maske de aynı sebeple yok: güneş izotrop.
                float3 rainRadiance = (pointStreak * _StreakSunRadiance
                                     + ambientStreak * IN.airColor)
                                    * _StreakSourceScale;

                // DAMLANIN KAPLADIĞI ALAN. Alfa quad'ın TAMAMINDA sabit olamaz.
                //
                // Makalede `α` sabit çünkü doku damlanın görüntüsünün TA KENDİSİ —
                // quad ile iz aynı şey. Bizde quad temsil payıyla büyütülmüş bir
                // dikdörtgen; damla onun tamamını kaplamıyor. Sabit alfa bırakılınca
                // her damla ince bir iz yerine DOLU BİR DİKDÖRTGEN basıyordu ve gökten
                // sönük olduğu için siyah leke gibi okunuyordu (kullanıcı bildirdi).
                //
                // Kaplama dokunun kendisinden geliyor: ambient kanal damlanın görüntü
                // izini tüm genişliğinde taşıyor, yönlü kanal onun üstündeki parlak
                // filament. İkisinin büyüğü, damlanın o pikseli kaplayıp kaplamadığını
                // söylüyor.
                float coverage = saturate(max(ambientStreak, pointStreak));

                float rainMask = coverage * endFade;

                // Kar tanesi: üç yumuşak lobun birleşimi.
                //
                return half4(rainRadiance, IN.alpha * rainMask);
            }
            ENDHLSL
        }
    }
}
