// ROL: dağın kendi gölgesini kar mesh'i için hesaplar.
// Çağıran: SnowLitForwardPass.hlsl.

#ifndef SNOW_TERRAIN_SHADOW_INCLUDED
#define SNOW_TERRAIN_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

/// DAĞ GÖLGESİ İKİ YÜZEYDE DE AYNI OLMAK ZORUNDA.
///
/// Arazi bu gölgeyi `MountainSurface.hlsl` içindeki `TerrainSunShadow` ile
/// hesaplıyor ama oradaki veriler arazi materyalinin `UnityPerMaterial`
/// bloğunda; kar mesh'i başka bir materyal kullandığı için onlara
/// erişemiyordu ve gölgeyi HİÇ uygulamıyordu.
///
/// Belirti ölçüldü: güneş ufka yakınken (06:25) arazi kendi gölgesinde
/// koyulup gölge tonuyla maviye çalıyor, kar mesh'i gölgesiz ve nötr
/// kalıyordu — bölge sınırı oyuncuyu izleyen 24 m'lik parlak bir kare olarak
/// görünüyordu. Parlaklık oranı 1.61 kata kadar çıktı.
///
/// `TerrainSurface` aynı üç veriyi global adlarla da yayınlıyor; buradaki
/// hesap arazidekinin birebir aynısı. İkisi ayrışırsa sınır yine kendini
/// gösterir.
TEXTURE2D_ARRAY(_TerrainShadowHorizon);
SAMPLER(sampler_TerrainShadowHorizon);

float4 _TerrainShadowOrigin;
float4 _TerrainShadowSize;

float SnowTerrainSunShadow(float3 worldPos, float3 sunDir)
{
    // Ufuk haritasının anlamsız açı okumasını engelleyen kapı; sınır ufkun
    // biraz ALTINDA ki alpenglow boyunca gölge sertçe açılıp kapanmasın.
    if (sunDir.y < -0.035) return 1.0;

    float horizonFade = saturate(sunDir.y / 0.035 + 1.0);

    float2 uv = (worldPos.xz - _TerrainShadowOrigin.xz) / max(_TerrainShadowSize.xz, 1e-3);

    // Pişirilmiş alanın dışında engel yok; ufuk sıfır sayılır.
    if (any(uv != saturate(uv))) return 1.0;

    const float TwoPi = 6.2831853;
    float sector = atan2(sunDir.z, sunDir.x) / TwoPi * 16.0;
    sector += sector < 0.0 ? 16.0 : 0.0;

    float lower = floor(sector);
    float blend = sector - lower;

    float a0 = SAMPLE_TEXTURE2D_ARRAY_LOD(_TerrainShadowHorizon, sampler_TerrainShadowHorizon,
                   uv, fmod(lower, 16.0), 0).r;
    float a1 = SAMPLE_TEXTURE2D_ARRAY_LOD(_TerrainShadowHorizon, sampler_TerrainShadowHorizon,
                   uv, fmod(lower + 1.0, 16.0), 0).r;

    float horizon = lerp(a0, a1, blend) * 1.5707963;
    float elevation = FastASin(saturate(sunDir.y));

    return lerp(1.0, smoothstep(horizon - 0.02, horizon + 0.10, elevation), horizonFade);
}

#endif
