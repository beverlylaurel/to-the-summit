#ifndef CABIN_WEATHERED_INPUT_INCLUDED
#define CABIN_WEATHERED_INPUT_INCLUDED
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DetileOffset;
                float  _BumpScale;
                float  _RoughnessScale;
                float  _MaterialSeed;
                float  _MacroScale;
                float  _MacroStrength;
                float  _RoughnessVariation;
                float  _ThirdPhaseStrength;
                float  _TextureMipBias;
                float  _AtlasStrength;
                float  _Cutoff;
            CBUFFER_END
#endif
