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

            float4 _CurtainParams;   // xy: ekran boyu (px), z: yoğunluk, w: zaman
            float4 _CurtainFlow;     // xy: akışın ekran yönü, z: hız, w: desen ölçeği (px)
            float4 _CurtainDepth;    // x: yakın kesme (m), y: karlılık, z: boş, w: görüş (m)


            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

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
                // PERDE ORTA BANTTA — ÜÇ KATMANIN ORTASI.
                //
                // `rain-spec.md` §10.3/§10.4 bunu açıkça söylüyor: yakını Garg-Nayar
                // taneleri çizer, uzağı Langer perdesi doldurur, ikisi çakışmaz.
                //
                // ALT SINIR tanecik kutusunun kenarı: orada damlalar tek tek çiziliyor,
                // perde üstlerine binerse aynı yağış iki kez sayılır.
                //
                // ÜST SINIR görüşten: sis oranın ötesini zaten siliyor ve orada perde
                // sisin işini ikinci kez yapar. Perdenin bir dönem kapatılma sebebi tam
                // buydu (`DECISIONS.md`) — tüm ekrana tül sürüyordu. Bant, sisin hâlâ
                // içinden görülebildiği aralık.
                float nearEdge = max(_CurtainDepth.x, 1.0);
                float far = max(_CurtainDepth.w, 1.0);

                float depthGate = smoothstep(nearEdge, nearEdge * 2.5, sceneDepth)
                                * (1.0 - smoothstep(far * 0.25, far * 0.8, sceneDepth));

                if (depthGate <= 1e-4) return 0;

                // TEK YÖN, TEK ÖRNEK — MAKALENİN KENDİ İLK YAPILANDIRMASI (`§6.2`).
                //
                // Perde bir dönem ekranı döşemelere bölüyor, her döşemeye genleşme
                // odağından türeyen kendi θ'sını veriyordu (`§7`). Bu SÖKÜLDÜ; sebebi
                // ölçülmüş bir belirti ve makalenin kendi sınırı:
                //
                //   YÖNTEM DÖNDÜRMEYE UYGUN DEĞİL. Makale θ'yı faza kare kare ARTIMLI
                //   işliyor (`§5.2`): `φ(t+1) := C(t)·(cosθ(t)ωx + sinθ(t)ωy)/|ω| · φ(t)`.
                //   Genlik alanı `|α̂|` sabit kalıyor, yani θ değişince alan yerinde
                //   durur, yalnız TAŞINMA yönü döner. Biz θ=0 pişirip UV döndürüyoruz;
                //   cebri açınca bu `α̂(R₋θ ω)` demek — zamansal kısım aynı ama rastgele
                //   faz alanı da dönüyor, yani desen KATI CİSİM gibi dönüyor. Kullanıcı
                //   bunu "sağ sol yaptıkça bazıları saat yönünde, bazıları tersine tam
                //   tur atıyor" diye bildirdi: odağın iki yanındaki döşemeler ters
                //   yönlerde dönüyordu.
                //
                //   MAKALE ZATEN θ'YI ZAMANLA DEĞİŞTİRMİYOR. `§7.2`, birebir: "the
                //   parameters C and θ varied from one image tile to the next, but did
                //   not vary over time." Serbest bakan birinci şahıs kamera makalenin
                //   doğruladığı alanın dışında.
                //
                // Geriye makalenin ilk örneğinin yapılandırması kalıyor: tek doku, tüm
                // ekrana dikişsiz döşenmiş, tek yön. Dikiş yok çünkü opaklık fonksiyonu
                // `(x,y)`'de toroidal. Örnek sayısı 4'ten 1'e indi.
                //
                // KALAN KUSUR, BİLİNÇLİ: θ değişince ekranın TAMAMI rijit döner. Yönü
                // ekran geneli olduğu için dönme yavaş ve sınırlı — döşeme başına tam
                // tur değil. Tam çözümü θ'yı da pişirmek (16 yön, M=64, 3.9 MB) ve iki
                // yön arasında harmanlamak; kalan dönme görünür olursa oraya bakılır.
                float2 pixel = uv * _CurtainParams.xy;
                float patternScale = max(_CurtainFlow.w, 1.0);
                float2 dir = _CurtainFlow.xy;

                float2 local = pixel / patternScale;
                float2 rot = float2(local.x * dir.x + local.y * dir.y,
                                   -local.x * dir.y + local.y * dir.x);

                float w = _CurtainFlow.z * _CurtainParams.w;

                // R KAR, G YAĞMUR. İki desen ayrı pişiyor: yağmurun halkası bir oktav
                // yukarıda (damla taneden küçük) ve zamansal frekansı ~2× (daha hızlı,
                // dolayısıyla daha bulanık). Karlılık ikisini harmanlıyor — sulu kar
                // ikisinin bir arada bulunması, tıpkı tanelerde olduğu gibi.
                float2 rg = SAMPLE_TEXTURE3D(_CurtainPattern, sampler_CurtainPattern,
                                             float3(rot, w)).rg;
                float alpha = lerp(rg.g, rg.r, _CurtainDepth.y);

                // BURADA BAŞKA EĞRİ YOK. Opaklık eğrisi PİŞİRİCİDE, makalenin koyduğu
                // yerde (`§5.6`, kare alma). Bir dönem burada ayrıca ortalama çıkarılıyor
                // ve `lerp(0.40, 0.90, karlılık)` ağırlığıyla çarpılıyordu; üç eğri üst
                // üste binince çıktının makaleyle ilgisi kalmamıştı.
                //
                // Makalenin bileşimi (`§5.7`, Denklem 7):
                //   I = I_snow·α + (1−α)·I_bg
                // `Blend SrcAlpha OneMinusSrcAlpha` ile birebir aynı. Shader'ın işi
                // yalnız α'yı ve I_snow'u vermek.
                alpha = saturate(alpha * intensity * depthGate);

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
