// include-rev: 32
//
// FOG ON THE SKY. Fog is a participating medium: every ray reaching the camera passes through it.
// Rays ending on the terrain were attenuated inside `MountainSurface`, but rays going to
// INFINITY — the sky — were not attenuated at all.
//
// The symptom: in a full storm (140 m visibility) the terrain turned entirely to fog while the sky
// stayed raw. Being different colors, a hard boundary remained between them and the eye read it
// as "the silhouette of the mountain". In a real whiteout the sky is fog too and there is no boundary.
//
// The sky used to be drawn by our own `Sky.shader`, which applied `SkyFogAmount` itself;
// when the sky moved to the PBSky package that step was lost. Adding a call into the package
// would be a patch — the hole is not specific to the sky, it exists for anything that does not call `ApplyHeightFog`.
//
// This pass looks at DEPTH: it only fogs pixels that hit nothing, and leaves opaque
// surfaces alone because they already fogged themselves in their own shaders.
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

            // THE SKY IS SELECTED BY THE DEPTH TEST, not by a texture read.
            // `_CameraDepthTexture` may not be copied yet at this point (right after
            // the skybox); the value read comes out as the far plane and every pixel
            // was discarded — the pass ran but drew nothing (measured: the draw
            // counter went up, no effect on screen).
            //
            // The triangle is drawn ON THE FAR PLANE; `Equal` only lets through pixels
            // nothing has written depth to. Opaque surfaces already fogged in their own
            // shaders, so it is not applied a second time here.
            ZTest Equal

            // `result = destination x T + scattering`. The source alpha carries the
            // transmittance: `One SrcAlpha` gives exactly that formula, no separate copy needed.
            Blend One SrcAlpha

            HLSLPROGRAM
            #pragma vertex VertSkyFog
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #include "HeightFog.hlsl"

            /// Full-screen triangle ON THE FAR PLANE. `Blit.hlsl`'s own vertex puts the
            /// triangle on the near plane; to select the sky with `ZTest Equal` it has to
            /// sit at the cleared value of the depth buffer.
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

                // BOTH SOURCES MUST SAY FAR PLANE. `ZTest Equal` reads the depth
                // BUFFER; the package's aerial perspective reads the depth TEXTURE.
                // The two can disagree on a silhouette pixel, and that pixel counted as
                // both "sky" and "geometry" and got processed twice — a one-pixel
                // outline, dark in normal play and white in the fog diagnostic. With no
                // overlap in the body, the trace only showed at the edge.
                //
                // The pass order was fixed as well, but on its own it would stay ORDER
                // DEPENDENT. This gate is order independent: no pixel the package counts
                // as geometry can get through. If the texture is not ready the far plane
                // is read and the gate is transparent — the behaviour falls back to what
                // `ZTest Equal` alone would do, so it is never worse.
                float sceneDepth = SampleSceneDepth(uv);
                if (abs(sceneDepth - UNITY_RAW_FAR_CLIP_VALUE) > 1e-6) discard;

                float3 cameraPos = GetCameraPositionWS();
                float3 far = ComputeWorldSpacePosition(uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 direction = normalize(far - cameraPos);

                // The flash was TAKEN OUT of here: it was multiplied by the fog amount,
                // so the flare vanished in a clear sky. It is added below with `LightningScatter`.
                float3 air = AirColor(direction);

                // THE VOLUME FIRST. A sky ray passes through the volume end to end, i.e.
                // the LAST slice of the volume: transmittance has accumulated there and so has the in-scattering.
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

                    // The tail starts where the volume ends; the direction is not scaled
                    // so its forward-axis projection is 1, because `SkyFogDepth` wants a unit direction.
                    float forward = max(dot(direction, _FogCameraForward.xyz), 1e-4);
                    tailStart = cameraPos + direction * (_FogVolumeDepth.y / forward);
                }

                // THE TAIL IS AN INFINITE PATH. The terrain path was finite and integrated
                // by sampling; the sky path has no end, so each layer's exponential profile
                // is integrated in closed form. `SkyFogAmount` was there for exactly this.
                float tailAmount = SkyFogAmount(tailStart, direction);

                float transmittance = volumeTransmittance * (1.0 - tailAmount);
                float3 scattering = volumeScatter + volumeTransmittance * air * tailAmount
                                  + LightningScatter(cameraPos, far);

                return half4(scattering, transmittance);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
