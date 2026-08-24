// ROL: kar yüzeyi materyalinin per-materyal property'leri. Hepsi tek
// CBUFFER'da; global'ler dışarıda (spec §15.2 — SRP Batcher uyumu).
// Çağıran: SnowLit.shader'ın bütün pass'leri.

#ifndef SNOW_LIT_INPUT_INCLUDED
#define SNOW_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SnowCommon.hlsl"

/// SRP BATCHER İÇİN TEK BLOK. Bir property bu bloğun dışında kalırsa Frame
/// Debugger'da "SRP Batch" düşer ve dört halka dört ayrı çizim olur.
CBUFFER_START(UnityPerMaterial)
    float  _SnowBreakupScale;
    float  _SnowEdgeFadeRange;

    float  _SnowAORadius;
    float  _SnowAOStrength;
CBUFFER_END

#endif
