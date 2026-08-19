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

            float4 _CurtainParams;   // x: döşeme boyu (px), y: desen ölçeği (px), z: yoğunluk, w: zaman
            float4 _CurtainDepth;    // x: yakın kesme (m), y: akış hızı, z: karlılık, w: ışınsallık
            float4 _CurtainFlow;     // xy: tek yönlü akışın ekran yönü, z: boş, w: boş
            float4 _CurtainFoe;

            /// DÖŞEME KARIŞTIRICISI. Makale her döşemeyi AYRI sentezliyor (`§7`), yani
            /// komşu döşemelerin gürültüsü bağımsız. Biz tek doku pişirip tekrar
            /// kullanıyoruz — kaydırma küçük olursa komşular desenin neredeyse aynı
            /// yerini okur ve ekran tek lekenin ızgarasına döner. Kullanıcı bunu
            /// "niye bu kadar düzenliler" diye bildirdi.
            ///
            /// Hash döşeme başına `[0,1)²` kaydırma üretiyor; doku Repeat olduğu için
            /// her kaydırma geçerli. Bağımsız sentezin ucuz karşılığı: aynı gürültünün
            /// ilişkisiz bölgeleri.
            float2 TileHash(float2 t)
            {
                float3 h = frac(t.xyx * float3(0.1031, 0.1030, 0.0973));
                h += dot(h, h.yzx + 33.33);
                return frac((h.xx + h.yz) * h.zy);
            }      // xy: yağışın ekran yönü (birim), zw: ekran boyu

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
                // SAHNE DERİNLİĞİNE GÖRE KAPI YOK — DESEN ZATEN DERİNLİĞİ TAŞIYOR.
                //
                // Dispersiyon bağıntısının bütün amacı bu: farklı uzamsal frekanslar
                // farklı hızda akıyor çünkü farklı DERİNLİKTEKİ taneleri temsil ediyorlar
                // (yakın tane büyük ve hızlı, uzak tane küçük ve yavaş, hepsi üst üste).
                // Üstüne bir de sahne derinliğine göre kapı koymak aynı bilgiyi iki kez
                // uygulamaktı.
                //
                // Ölçüldü: mesafe kapısıyla perde ufka yapışık İNCE BİR ŞERİDE düşüyordu.
                // Geometrik olarak doğru — düz ovada 50-600 m aralığı ekranda dar bir
                // kuşağa sıkışır, perspektif mesafeyi ezer — ama işe yaramaz. Yağan kar
                // her yöne dolu, arkasında ne olduğuna bakmaz.
                //
                // Geriye tek koşul kalıyor: çok yakındaki cismin ÖNÜNE geçmemek. El ve
                // tırmanış duvarı perdenin berisinde durmalı.
                float depthGate = saturate((sceneDepth - _CurtainDepth.x)
                                           / max(_CurtainDepth.x, 1.0));

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
                // DÖŞEMELİ KURULUM — `[Langer 2004, §7]`'nin gerçek hâli.
                //
                // Perdenin içindeki her şey aynı derinlikteymiş gibi hareket ediyordu:
                // ekrana bağlarsan cama yapışıyor, dünyaya bağlarsan dağa. Doğrusu
                // arada — yağan kar farklı derinliklerde, farklı hızlarda.
                //
                // Parallax'ı veren şey GENLEŞME ODAĞI: kamera ilerlerken görüntü hareketi
                // odaktan dışa açılıyor, hız odağa uzaklıkla lineer artıyor. Bunu PİKSEL
                // başına uygulamak koordinat alanını buruyor ve ışınsal girdap bırakıyor
                // (ölçüldü). Makalenin çözümü: yön ve hız DÖŞEME içinde SABİT, komşu
                // döşemeler kenarda harmanlanıyor.
                //
                // İki ölçek AYRI: döşeme boyu açısal çözünürlüğü belirliyor (küçük olsun
                // ki odak çevresinde yön yumuşak değişsin), desen ölçeği ekrandaki
                // özellik boyunu (büyük olsun ki 4-32 piksel aralığı korunsun). Tek
                // parametreye bağlanınca ikisi çelişiyor.
                float2 pixel = uv * _CurtainFoe.zw;
                float2 foe = _CurtainFoe.xy;
                float tileSize = max(_CurtainParams.x, 1.0);
                float patternScale = max(_CurtainParams.y, 1.0);
                float halfDiagonal = 0.5 * length(_CurtainFoe.zw);

                float2 tileF = pixel / tileSize;
                float2 baseTile = floor(tileF - 0.5);
                float2 blend = tileF - 0.5 - baseTile;

                float alphaSum = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int ty = 0; ty < 2; ty++)
                [unroll]
                for (int tx = 0; tx < 2; tx++)
                {
                    float2 tileIndex = baseTile + float2(tx, ty);
                    float2 center = (tileIndex + 0.5) * tileSize;

                    // IŞINSAL VE TEK YÖNLÜ AKIŞ ARASINDA SÜREKLİ GEÇİŞ.
                    //
                    // Genleşme odağı yalnız akış görüş eksenine YAKINSA ekranda bir
                    // noktadır. Akış eksene dikleşince odak sonsuza gider ve akış
                    // paralelleşir — o sınırda odağın yeri anlamsızdır.
                    //
                    // Eski kod bunu görmüyordu: odağı 1000 m öteki bir dünya noktasının
                    // izdüşümünden buluyor, nokta kameranın arkasına düşünce ekran
                    // MERKEZİNE sıçrıyordu. Kar dik düştüğü için odak başucundadır ve
                    // yaw'da tam o kararsız bölgede gezer; sonuç, kamera çevrilince
                    // desenin 360° dönmesiydi (kullanıcı bildirdi).
                    //
                    // `_CurtainDepth.w` ışınsallık: 1 tam ışınsal, 0 tam paralel.
                    float radial = _CurtainDepth.w;

                    float2 fromFoe = center - foe;
                    float radius = length(fromFoe);
                    float2 radialDir = radius > 1e-3 ? fromFoe / radius : _CurtainFlow.xy;

                    float2 mixed = lerp(_CurtainFlow.xy, radialDir, radial);
                    float mixedLen = length(mixed);
                    float2 dir = mixedLen > 1e-3 ? mixed / mixedLen : float2(1.0, 0.0);

                    // HIZ ODAĞA UZAKLIKLA LİNEER (C_ij = C₀·|p_ij − FOE|). Odağın
                    // dibindeki döşemeler neredeyse durgun — makalenin kendi gözlemi.
                    // Paralel sınırda böyle bir odak yok, hız ekran boyunca sabit.
                    float speed = _CurtainDepth.y
                                * lerp(1.0, saturate(radius / halfDiagonal), radial);

                    // Dönme DÖŞEME MERKEZİ etrafında; döşeme içinde sabit olduğu için
                    // burulma yok, katı dönme var.
                    float2 local = (pixel - center) / patternScale;
                    float2 rot = float2(local.x * dir.x + local.y * dir.y,
                                       -local.x * dir.y + local.y * dir.x);

                    float2 q = rot + TileHash(tileIndex);

                    float w = (0.35 + speed) * _CurtainParams.w;

                    // R KAR, G YAĞMUR. İki desen ayrı pişiyor: yağmurun halkası bir
                    // oktav yukarıda (damla taneden küçük) ve zamansal frekansı 2.5×
                    // (daha hızlı, dolayısıyla daha bulanık). Karlılık ikisini
                    // harmanlıyor — sulu kar ikisinin bir arada bulunması, tıpkı
                    // tanelerde olduğu gibi (`SYSTEMS.md`).
                    float2 rg = SAMPLE_TEXTURE3D(_CurtainPattern, sampler_CurtainPattern,
                                                 float3(q, w)).rg;
                    float a = lerp(rg.g, rg.r, _CurtainDepth.z);

                    // Bilineer harmanlama: kenarda iki komşunun payı eşitleniyor, dikiş
                    // görünmüyor. Makale 10 piksellik örtüşmede lineer harmanlıyor.
                    float weight = (tx == 0 ? 1.0 - blend.x : blend.x)
                                 * (ty == 0 ? 1.0 - blend.y : blend.y);

                    alphaSum += a * weight;
                    weightSum += weight;
                }

                float alpha = weightSum > 1e-5 ? alphaSum / weightSum : 0.0;

                // ORTALAMA HAVADIR, TEPELER TANEDİR.
                //
                // Pişirici deseni ortalaması 0.5 olacak şekilde [0,1]'e eşliyor
                // (`SpectralPrecipitationBaker`, `[Langer 2004, §7.7]`). Bu ortalama
                // doğrudan opaklık olarak kullanılınca ekranın TAMAMINA sabit bir gri
                // sürülüyordu — ölçüldü: tam karda ~0.45. Kullanıcının "gren" dediği şey
                // desenin DC bileşeniydi, taneleri değil.
                //
                // Tane seyrek ve ayrıktır: aradaki hava saydam. Ortalamanın altı sıfıra
                // iniyor, üstü [0,1]'e geriliyor. Sihirli katsayı yok — 0.5 pişiricinin
                // yazdığı ortalama, bölen de aralığın üst yarısı.
                alpha = saturate((alpha - 0.5) / 0.5);

                // KAR VE YAĞMUR AYNI DESEN, FARKLI ÇARPAN. Yağmur izleri ince ve seyrek
                // okunur; kar perdesi kalın.
                // TABAN AĞIRLIK DÜŞÜK. Perde asıl veil DEĞİL — sis o işi yapıyor; bu
                // katman yalnız tanelerin dokusunu taşıyor. Bir dönem 1.0'daydı ve
                // ekran grenli bir tüle dönüyordu.
                float weight = lerp(0.40, 0.90, _CurtainDepth.z);

                alpha = saturate(alpha * intensity * depthGate * weight);

                // OPAKLIK PROBU: son alfa. Bant doğru olsa bile katkı çok güçlü ya da
                // görünmeyecek kadar zayıf olabilir; bu ikisini ayırır.
                if (_CurtainProbe > 1.5 && _CurtainProbe < 2.5)
                    return half4(ProbeRamp(alpha), 1.0);

                // PERDE KENDİ RENGİNİ SEÇMEZ — ama gökyüzünden de boyanmaz.
                //
                // `AirColor` bakış yönüne bağlı ve gök gradyanını taşıyor. Perdeye onu
                // bağlayınca benekler gökten KOYU düştü (ölçüldü: 235 m görüşte gök sis
                // rengindeyken tanecikler kirli koyu okunuyordu).
                //
                // Aynı ayrım `HeightFog.hlsl`'de iki kez yazılmış: satır 248 (savrulan
                // karın rengi) ve satır 611 (perde gök rengine boyanmaz). Havada asılı
                // tane yukarıdan güneşle ve altındaki karın yansımasıyla aydınlanır;
                // hangi yöne bakıldığı onu karartmaz. `SpindriftColor` tam bu büyüklük —
                // ayrı bir kaynak kurulmuyor.
                float3 tint = SpindriftColor();

                return half4(tint, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
