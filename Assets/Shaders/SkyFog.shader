// include-rev: 5
//
// GÖKYÜZÜNE SİS. Sis katılımcı bir ortam: kameraya ulaşan her ışın onun içinden geçer.
// Arazide biten ışınlar `MountainSurface` içinde sönümleniyordu, ama SONSUZA giden
// ışınlar — gökyüzü — hiç sönümlenmiyordu.
//
// Belirti: tam fırtınada (görüş 140 m) arazi tamamen sise dönüşüyor, gökyüzü ham
// kalıyor. İkisi farklı renkte olduğu için arada keskin bir sınır kalıyor ve göz onu
// "dağın silueti" diye okuyor. Gerçek beyazlamada gökyüzü de sistir, sınır olmaz.
//
// Eskiden gökyüzünü bizim `Sky.shader`'ımız çiziyordu ve `SkyFogAmount`'u kendi
// uyguluyordu; gökyüzü PBSky paketine devredilince o adım kayboldu. Pakete çağrı eklemek
// yama olurdu — delik gökyüzüne özel değil, `ApplyHeightFog` çağırmayan her şeyde var.
//
// Bu geçiş DERİNLİĞE bakıyor: yalnız hiçbir şeye çarpmamış pikselleri sisliyor, opak
// yüzeyler kendi shader'larında zaten sislendiği için onlara dokunmuyor.
Shader "Hidden/ToTheSummit/SkyFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Sky Fog"
            ZWrite Off
            Cull Off

            // GÖKYÜZÜ SEÇİMİ DERİNLİK TESTİNDEN, doku okumasından değil.
            // `_CameraDepthTexture` bu noktada (skybox'tan hemen sonra) henüz
            // kopyalanmamış olabiliyor; okunan değer uzak düzlem çıkıyor ve her piksel
            // atılıyordu — geçiş çalışıyor ama hiçbir şey çizmiyordu (ölçüldü: çizim
            // sayacı artıyor, ekranda etki yok).
            //
            // Üçgen UZAK DÜZLEMDE çiziliyor; `Equal` yalnız derinliğe hiçbir şeyin
            // yazmadığı pikselleri geçiriyor. Opak yüzeyler kendi shader'larında zaten
            // sislendi, buradan ikinci kez uygulanmıyor.
            ZTest Equal

            // `sonuç = hedef × T + saçılım`. Kaynak alfası geçirgenliği taşıyor:
            // `One SrcAlpha` tam olarak bu formülü veriyor, ayrı bir kopyalama gerekmiyor.
            Blend One SrcAlpha

            HLSLPROGRAM
            #pragma vertex VertSkyFog
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "HeightFog.hlsl"

            /// Tam ekran üçgeni UZAK DÜZLEMDE. `Blit.hlsl`'in kendi vertex'i üçgeni yakın
            /// düzleme koyuyor; `ZTest Equal` ile gökyüzünü seçebilmek için derinlik
            /// tamponunun temizlenmiş değerinde olmalı.
            Varyings VertSkyFog(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                float3 cameraPos = GetCameraPositionWS();
                float3 far = ComputeWorldSpacePosition(uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 direction = normalize(far - cameraPos);

                float3 air = AirColor(direction) + _LightningFlash.rgb * LightningFogScatter;

                // HACİM ÖNCE. Gökyüzü ışını hacmi baştan sona geçiyor, yani hacmin SON
                // dilimi: orada geçirgenlik birikmiş, in-scattering de öyle.
                float3 volumeScatter = 0.0;
                float volumeTransmittance = 1.0;
                float3 tailStart = cameraPos;

                if (_FogVolumeDepth.z > 0.0)
                {
                    float4 volume = SAMPLE_TEXTURE3D_LOD(_FogScatteringVolume,
                                                         sampler_FogScatteringVolume,
                                                         float3(uv, 1.0), 0);

                    volumeScatter = volume.rgb;
                    volumeTransmittance = volume.a;

                    // Kuyruk hacmin bittiği yerden başlıyor; yön ileri eksene izdüşümü 1
                    // olacak şekilde ölçeklenmiyor çünkü `SkyFogDepth` birim yön istiyor.
                    float forward = max(dot(direction, _FogCameraForward.xyz), 1e-4);
                    tailStart = cameraPos + direction * (_FogVolumeDepth.y / forward);
                }

                // KUYRUK SONSUZ YOL. Arazi yolu sonluydu ve örnekle integre ediliyordu;
                // gök yolunun sonu yok, her katmanın üstel profili kapalı biçimde
                // integre ediliyor. `SkyFogAmount` zaten bunun için duruyordu.
                float tailAmount = SkyFogAmount(tailStart, direction);

                float transmittance = volumeTransmittance * (1.0 - tailAmount);
                float3 scattering = volumeScatter + volumeTransmittance * air * tailAmount;

                if (_SkyFogDebug > 0.5)
                    return half4(volumeTransmittance, tailAmount, 0.0, 0.0);

                return half4(scattering, transmittance);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
