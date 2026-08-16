// include-rev: 68
Shader "ToTheSummit/LightningBolt"
{
    // Kanal katkısal çizilir: şimşek ışık yayan bir plazma, arkasındaki bulutu ya da
    // gökyüzünü karartmaz, üstüne ekler. Renk ve sönüm LineRenderer'ın köşe renginden
    // geliyor — malzemeyi her karede değiştirmek yerine tek malzeme paylaşılıyor.
    SubShader
    {
        // Buluttan sonra çizilir, ama bulutu kendisi hesaba katar.
        //
        // Opak kuyruğa alıp bindirmeye bırakmak denendi ve geri alındı: bindirmedeki
        // `α`, o pikselin **tüm ışını** boyunca biriken bulut. Kolun arkasında kalan,
        // on kilometre ötedeki deniz de o sayının içinde — yani kol, kendisinin arkasında
        // duran bulutla karartılıyor ve fırtınada tamamen kayboluyordu.
        //
        // Doğrusu yalnızca **önündeki** bulutla sönmek: ışının katmana girdiği uzaklık
        // kolun uzaklığıyla karşılaştırılıyor. Kol katmanın berisindeyse hiç sönmüyor —
        // bulut tabanının altında asılı bir kanala bakarken arada bulut yok, ki gerçekte
        // de öyle.
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // Derinliğe yazmıyor: kanal bir yüzey değil, ışık. Yazsaydı arkasındaki bulut
        // ışın yürüyüşünde kesilir, kanalın çevresinde bulutsuz bir oyuk açılırdı.
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Bolt"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HeightFog.hlsl"

            TEXTURE2D(_CloudTexture);
            SAMPLER(sampler_CloudTexture);

            // Katmanın kotları AtmosphereController tarafından global yazılıyor; burada
            // yalnızca "önümde bulut var mı" sorusu için okunuyorlar. CloudCommon.hlsl'i
            // dahil etmek bütün hacim yürütücüsünü, üç boyutlu dokularını ve onlarca
            // globalini bir çizgi shader'ına taşımak olurdu.
            //
            // `_CloudBottom` burada BİLDİRİLMİYOR: bulut gölgesi eklendiğinden beri
            // HeightFog.hlsl'de duruyor ve bu dosya onu zaten dahil ediyor.
            float _CloudTop;

            // Bulut küresinin yarıçapı, AtmosphereController'ın yazdığı global. Sabit
            // kopya tutulamaz: kürenin yarıçapı sahne ölçeğini belirliyor ve ayrışırsa
            // şimşek bulutun içinde başlaması gerekirken önünde ya da arkasında kalır.
            float _PlanetRadius;
            #define BoltPlanetRadius _PlanetRadius

            /// Işının verilen yarıçaplı küreye girdiği uzaklık. Girmiyorsa negatif.
            float BoltSphereEntry(float3 origin, float3 direction, float radius)
            {
                float b = dot(origin, direction);
                float c = dot(origin, origin) - radius * radius;
                float d = b * b - c;
                if (d < 0.0) return -1.0;

                float root = sqrt(d);
                float near = -b - root;

                return near > 0.0 ? near : -b + root;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Şerit boyunca enine profil: ortası beyaz bir çekirdek, kenarları hâle.
                // Tek düze bir şerit kâğıt gibi duruyor; gerçek kanal ince ve çok parlak
                // bir eksenle onu saran daha soluk bir parıltıdan oluşuyor.
                float across = abs(IN.uv.y * 2.0 - 1.0);

                float core = saturate(1.0 - across * 4.0);
                float halo = 1.0 - across;
                halo *= halo;

                float3 light = IN.color.rgb * (core * 3.0 + halo * 0.6);

                // Üst uç buluttan çıkar, orada başlamaz. Sert bir uçla başlayınca kanal
                // bulutun önüne asılmış gibi duruyor; kısa bir açılma onu kütlenin
                // içinden çıkıyor gibi gösteriyor.
                light *= smoothstep(0.0, 0.18, IN.uv.x);

                // Havanın kendisi kanalı da yutar. Katkısal çizdiğimiz için sis rengine
                // karışmıyor, sönüyor: iki kilometre uzaktaki kol dipteki kadar parlak
                // kalırsa mesafesi okunmuyor ve gökyüzüne çizilmiş gibi duruyor.
                // Sis modeli yüzeylere göre ayarlı: görüş mesafesinde arazi tamamen
                // kayboluyor. Kanal o yüzeylerden kat kat parlak, dolayısıyla aynı
                // sönümü uygulamak onu fırtınada — yani çaktığı tek havada — tümden
                // siliyor. Karekök, parlak bir kaynağın sisin içinde daha uzağa
                // gitmesini karşılıyor.
                light *= sqrt(1.0 - HeightFogAmount(_WorldSpaceCameraPos, IN.positionWS));

                // Önünde duran bulut kadar söner, arkasındaki kadar değil. Işının katmana
                // girdiği uzaklık kolunkiyle karşılaştırılıyor; kol katmanın berisindeyse
                // pay sıfır.
                float3 toBolt = IN.positionWS - _WorldSpaceCameraPos;
                float boltDistance = length(toBolt);

                float3 fromCentre = _WorldSpaceCameraPos - float3(0.0, -BoltPlanetRadius, 0.0);
                float3 toward = toBolt / boltDistance;

                float entry = BoltSphereEntry(fromCentre, toward, BoltPlanetRadius + _CloudBottom);
                float exit = BoltSphereEntry(fromCentre, toward, BoltPlanetRadius + _CloudTop);

                if (entry >= 0.0 && exit > entry)
                {
                    float share = saturate((boltDistance - entry) / (exit - entry));
                    float2 screen = IN.positionCS.xy / _ScaledScreenParams.xy;
                    float opacity = SAMPLE_TEXTURE2D(_CloudTexture, sampler_CloudTexture, screen).a;

                    light *= 1.0 - opacity * share;
                }

                return half4(light, 1.0);
            }
            ENDHLSL
        }
    }
}
