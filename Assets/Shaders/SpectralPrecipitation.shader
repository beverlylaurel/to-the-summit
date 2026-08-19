// include-rev: 1
//
// SPEKTRAL YAĞIŞ PERDESİ — `[Langer 2004]`, `snow-spec.md` §7.
//
// Seyrek particle sistemi taneleri çiziyor; bu geçiş aralarını dolduran DİNAMİK DOKUYU
// çiziyor. Yoğun karda "duvar" hissi yüz binlerce taneden gelir ve o kadar particle kare
// süresini yiyor (makale: 150 000 particle 121.9 ms, hibrit 24.6 ms).
//
// Desen `SpectralPrecipitationBaker` tarafından pişiriliyor: 64×64×30 tek kanal döngü,
// dikişli olmadığı ölçüldü (son→ilk fark / ardışık kare farkı = 1.02).
Shader "Hidden/ToTheSummit/SpectralPrecipitation"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Spectral Precipitation"

            ZWrite Off
            ZTest Always
            Cull Off

            // Langer Denklem 7: `I = I_kar·α + (1-α)·I_arka`. Standart alfa harmanı
            // bunun ta kendisi — kaynak rengi perdenin rengi, kaynak alfası opaklık.
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "HeightFog.hlsl"

            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            TEXTURE3D(_CurtainPattern);
            SAMPLER(sampler_CurtainPattern);

            float4 _CurtainParams;   // x: döşeme boyu (piksel), y: hız, z: yoğunluk, w: zaman
            float4 _CurtainDepth;    // x: yakın kesme, y: yatay görüş açısı (rad), z: karlılık, w: görüş mesafesi
            float4 _CurtainFoe;      // xy: yağışın ekran yönü (birim), zw: ekran boyu

            /// TEŞHİS. 0 kapalı · 1 bant · 2 opaklık · 3 perde yok.
            /// Göz kararı yerine AYRIK renk bandı: her renk bir SAYI aralığı, ara ton yok.
            float _CurtainProbe;

            /// Sürekli bir değeri okunabilir renge çevirir. Gradyan DEĞİL: gradyanda
            /// "biraz açık yeşil" kaç eder sorusunun cevabı yok, bantta var.
            float3 ProbeRamp(float v)
            {
                if (v < 0.02) return float3(0.05, 0.05, 0.05);   // koyu gri: yok
                if (v < 0.08) return float3(0.0, 0.0, 1.0);      // mavi:      0.02-0.08
                if (v < 0.18) return float3(0.0, 1.0, 1.0);      // camgöbeği: 0.08-0.18
                if (v < 0.32) return float3(0.0, 1.0, 0.0);      // yeşil:     0.18-0.32
                if (v < 0.50) return float3(1.0, 1.0, 0.0);      // sarı:      0.32-0.50
                if (v < 0.70) return float3(1.0, 0.5, 0.0);      // turuncu:   0.50-0.70
                return float3(1.0, 0.0, 0.0);                    // kırmızı:   0.70+
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Perde tamamen kapalı: sahne dokunulmadan geçiyor.
                if (_CurtainProbe > 2.5) return 0;

                float intensity = _CurtainParams.z;
                if (intensity <= 1e-4) return 0;

                float2 uv = input.texcoord;

                // DERİNLİK KAPISI — spec §11.3.4'ün açık noktası.
                //
                // Perdenin kendi derinliği yok; ham hâliyle tırmanış duvarının ve elin
                // ÖNÜNE biniyor. Çözüm hibrit yapının kendisinde: particle yakını çiziyor,
                // perde uzağı. Perde `yakın kesme`den önce hiç görünmüyor, oradan `uzak
                // doyum`a kadar açılıyor. Yakın nesne geometrik olarak hep önde kalıyor.
                float rawDepth = SampleSceneDepth(uv);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // KAPI ÜSTEL, DOĞRUSAL DEĞİL — perde bir HACİM.
                //
                // Doğrusal rampa yanlıştı: 400 m'de doyuyordu ve 300 m'deki yamaç
                // neredeyse tam perde alıyordu. Sonuçta desen arazinin üstüne yapışmış
                // ince taneli bir doku gibi sürünüyordu (kullanıcı bildirdi).
                //
                // Havadaki yağış katılımcı bir ortam: göze ulaşan pay `1 - exp(-d/L)`
                // ile doluyor. `L` karakteristik mesafe — o kadar yolda perdenin %63'ü
                // birikiyor. Yakın yamaçta pay küçük kalıyor, gökte tam.
                // PERDE SİSİN DOKUSUDUR — üstüne binen İKİNCİ bir örtü değil.
                //
                // Bir dönem sisin GEÇİRGENLİĞİYLE çarpılıyordu, yani "sis kalınsa kar
                // çizme" deniyordu. Tersi doğru: görüş 380 m'ye düştüğünde o sisi yapan
                // şey zaten yağan karın kendisi. Sis ortalamayı taşıyor, perde o
                // ortalamanın uzamsal DEĞİŞİMİNİ.
                //
                // Ölçüldü (bant probu): eski hâlde gökyüzü 0.70+, arazi ~0 çıkıyordu —
                // perde yalnız göğü grenliyor, asıl istenen yerde hiç yok.
                //
                // Sisin opaklığıyla çarpınca ikisi aynı yerde güçleniyor: tipide arazinin
                // önü kalın ve KÜMELİ, berrak havada perde kendiliğinden yok oluyor.
                // SİS OPAKLIĞI TEK ÜSTELDEN, 8 ÖRNEKLİ İNTEGRALDEN DEĞİL.
                //
                // `HeightFogAmount` ışın boyunca sekiz örnek alıyor; tam ekranda piksel
                // başına o kadar iş 3.5 ms tutuyordu (ölçüldü: perde kapalı 131 FPS /
                // 7.6 ms, açık 90 FPS / 11.1 ms). Görünmeyen bir katman için çok.
                //
                // Sisin kendi yasası zaten üstel. Görüş mesafesi, kontrastın %2'ye
                // düştüğü uzaklık — yani optik derinlik ~3.9. Aynı eğri tek `exp` ile
                // çıkıyor ve perdenin ihtiyacı olan doğruluk bu.
                float visibility = max(_CurtainDepth.w, 1.0);
                float fogOpacity = 1.0 - exp(-3.9 * sceneDepth / visibility);

                // YAKIN KORUMASI. Elin ve tırmanış duvarının önüne perde gelmemeli.
                float nearGuard = saturate((sceneDepth - _CurtainDepth.x)
                                           / max(_CurtainDepth.x, 1.0));

                float depthGate = fogOpacity * nearGuard;

                // BANT PROBU: deseni hiç örneklemeden, perdenin NEREDE etkili olduğunu
                // gösterir. Desen karıştığında bandın kendisi okunamıyor.
                if (_CurtainProbe > 0.5 && _CurtainProbe < 1.5)
                    return half4(ProbeRamp(depthGate), 1.0);

                if (depthGate <= 1e-4) return 0;

                // YÖN EKRAN GENELİNDE SABİT — piksel başına DEĞİL.
                //
                // Makale her döşemeye kendi yönünü veriyor (genleşme odağından türeyen
                // θ_ij) ve döşeme içinde SABİT tutuyor. Ben piksel başına sürekli
                // değiştirdim; sonuç dokunun dönmesi değil koordinat alanının BURULMASI
                // oldu — ekranda odağın çevresinde ışınsal bir girdap (kullanıcı sahne
                // görünümünde yakaladı).
                //
                // Döşeme + kenar harmanlaması yerine tek yön seçildi. Gerekçe: odak
                // makinesi kameranın ÖTELENMESİNİ modelliyor, tırmanışçı ise yavaş
                // hareket ediyor. Baskın görsel ipucu yağışın kendi yönü — düşüş artı
                // rüzgâr. Perspektif genleşmesi bilinçli olarak alınmadı.
                float2 dir = _CurtainFoe.xy;

                // DESEN DÜNYAYA BAĞLI, EKRANA DEĞİL.
                //
                // Ekran pikselinden örneklenirken desen başını çevirdiğinde ekranla
                // birlikte geliyordu — camdaki toz gibi. Kullanıcı "silüetler bir tuhaf
                // hareket ediyor" diye bildirdi; sebebi buydu.
                //
                // Bakış yönünün açısal koordinatı (azimut, yükseliş) kullanılınca desen
                // dünyada duruyor: başını çevirince içinden geçiyorsun, sürüklemiyorsun.
                // Langer bunu genleşme odağı makinesiyle çözüyor; açısal koordinat aynı
                // işi tek satırda yapıyor ve kameranın DÖNMESİ için doğru olan da bu.
                //
                // Tepe ve dip noktasında azimut tanımsızlaşıyor; tırmanışçı çoğunlukla
                // yataya bakıyor ve orada sorun yok.
                float3 viewDir = normalize(ComputeWorldSpacePosition(
                    uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP) - GetCameraPositionWS());

                float azimuth = atan2(viewDir.x, viewDir.z);
                float elevation = asin(clamp(viewDir.y, -1.0, 1.0));

                // Açısal ölçek, döşemenin EKRANDA istenen boyunu koruyacak şekilde:
                // piksel başına radyan × döşeme boyu.
                float radiansPerPixel = _CurtainParams.x > 0.0
                    ? _CurtainDepth.y / max(_CurtainFoe.z, 1.0) : 1.0;

                float2 q = float2(azimuth, elevation)
                         / max(_CurtainParams.x * radiansPerPixel, 1e-5);
                float2 rotated = float2(q.x * dir.x + q.y * dir.y,
                                       -q.x * dir.y + q.y * dir.x);

                float w = _CurtainParams.y * _CurtainParams.w;

                float alpha = SAMPLE_TEXTURE3D(_CurtainPattern, sampler_CurtainPattern,
                                               float3(rotated, w)).r;

                // KAR VE YAĞMUR AYNI DESEN, FARKLI ÇARPAN. Yağmur izleri ince ve seyrek
                // okunur; kar perdesi kalın.
                // TABAN AĞIRLIK DÜŞÜK. Perde asıl veil DEĞİL — sis o işi yapıyor; bu
                // katman yalnız tanelerin dokusunu taşıyor. Bir dönem 1.0'daydı ve
                // ekran grenli bir tüle dönüyordu.
                float weight = lerp(0.40, 0.90, _CurtainDepth.z);

                alpha = saturate(alpha * intensity * depthGate * weight);

                // PERDE KENDİ RENGİNİ SEÇMEZ — havanın rengini alıyor, tıpkı taneler gibi
                // (`SYSTEMS.md`: "tane kendi rengini seçmez"). Sabit beyaz, kapalı göğün
                // önünde patlıyor ve gece fosforlu duruyordu.
                float3 direction = normalize(ComputeWorldSpacePosition(
                    uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP) - GetCameraPositionWS());

                // OPAKLIK PROBU: son alfa. Bant doğru olsa bile katkı çok güçlü ya da
                // görünmeyecek kadar zayıf olabilir; bu ikisini ayırır.
                if (_CurtainProbe > 1.5 && _CurtainProbe < 2.5)
                    return half4(ProbeRamp(alpha), 1.0);

                float3 tint = AirColor(direction);

                return half4(tint, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
