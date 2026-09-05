// include-rev: 113  (Unity, .hlsl degisince .shader'i yeniden
// derlemeyebiliyor; bu satir degisince derleme zorlanir)
Shader "ToTheSummit/MountainSurface"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"

            // Unity draws terrain through a special path and only gives per-object light
            // data to shaders that declare themselves terrain compatible. Without the tag
            // unity_LightData stays zero, direct sunlight is cut entirely and only ambient
            // remains.
            "TerrainCompatible" = "True"
        }

        // Terrain can draw its own material with instancing; even when it is off the
        // variant has to exist.
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment Fragment
            #pragma target 5.0

            // THE TERRAIN'S SHADOW COMES FROM TWO SOURCES. The mountain's own ridges are
            // found by marching the height field (see TerrainSunShadow) — the shadow map
            // does not carry that distance, it ends at 150 metres. But MOVING OBJECTS are
            // in the map: the bike, the player, later rocks and tents. As long as the map
            // was not read, none of them cast a shadow on the ground.
            // SNOW QUALITY TIER. `SnowManager.ApplyQualityKeyword` enabled the global
            // keyword but no shader had the pragma — the variant was never compiled so
            // `#if defined(_SNOW_QUALITY_HIGH)` was always false and the three layers of
            // `SnowDetailNormals` (meso, micro, crushed) never ran.
            #pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            // The renderer is in Forward+ mode: lights are distributed in clusters and the
            // per-object light data is not filled in. Without this keyword GetMainLight()
            // falls to the old branch, reads the unfilled unity_LightData and the sun is cut entirely.
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // The cloud shadow arrives through this keyword: the cloud system writes the
            // shadow into the main light's cookie texture and URP applies it here.
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // SCREEN SPACE AMBIENT OCCLUSION IS NOT READ ON THE TERRAIN. It works from the
            // depth buffer and mistakes the triangle faces of the terrain mesh for surface
            // curvature, drawing soft lattice lines on the ground (see `DECISIONS.md` — SSAO off).
            // The baked exposure channel already provides large-scale cavity shading.
            //
            // It is ON in the feature pipeline: near objects (the bike, equipment, tents)
            // read it and their recesses darken. Because the keyword is not declared here
            // the terrain is unaffected.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "MountainSurface.hlsl"
            #include "../Snow/Shaders/SnowTessellation.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;

                // THE TERRAIN DOES NOT RISE BY THE SNOW COLUMN.
                //
                // It used to, and there was no counterpart on the physics side:
                // the `CharacterController` stands on the terrain collider, i.e.
                // on the ROCK. Measured: foot 205.539, rock 205.489, drawn
                // surface 205.98 — the character started half a metre buried and
                // the eye sat below the snow surface (user: "the character
                // spawns inside the ground", "the trail is in the air").
                //
                // Snow height is now carried ONLY by the snow mesh; the mesh
                // descends to the terrain elevation at the region edge with
                // `SnowEdgeFade`. Half a metre of elevation difference is not
                // discernible at distance anyway.

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            /// Hull oncesi gecis: yalniz dunya konumuna cevirir.
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessPosition(patch, bary));
            }

            /// UniversalFragmentPBR written out — because our own march provides the main
            /// light's shadow and there is no way to inject a shadow from outside into the
            /// ready-made function. The parts are still URP's own functions: BRDF,
            /// per-light contribution, SSAO combination. Wet terrain also evaluates the
            /// shared analytic sky along the reflected ray: an impact ring is a water-film
            /// normal and is visible primarily by reflection, especially when clouds
            /// suppress the direct sun.
            half4 Fragment(Varyings IN) : SV_Target
            {
                MountainSurface surface = BuildMountainSurface(IN.positionWS);

                // The Forward+ light loop macros read this variable by name
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                float3 shadingNormal = surface.normalWS;
                inputData.normalWS = shadingNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                // No `fogCoord`: only `MixFog` reads it and the scene has Unity's own fog
                // switched off (`m_Fog: 0`). The terrain applies `ApplyHeightFog` itself.
                inputData.bakedGI = SampleSH(shadingNormal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half alpha = 1.0;
                BRDFData brdfData;
                // F0 IS BLENDED BETWEEN SNOW AND ROCK.
                //
                // Rock is a dielectric and URP's 0.04 (n = 1.5) fits it. Ice is
                // n = 1.31 and F0 = 0.018 — on the terrain the two materials sit
                // in the same pass, so F0 has to cross with the same mask. Using
                // a single 0.04 makes the snow return 2.2 times too much specular.
                half f0 = lerp(0.04h, (half)SNOW_ICE_F0, surface.snowMask);
                SnowInitBRDF(surface.albedo, surface.smoothness, f0, alpha, brdfData);

                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(
                    float2(0.0, 0.0), surface.occlusion);

                // The terrain's own shadow from the height field. A pixel with its back to
                // the light does not march: its contribution is already zero and forty steps would be wasted.
                Light mainLight = GetMainLight();
                mainLight.shadowAttenuation =
                    dot(inputData.normalWS, mainLight.direction) > 0.0
                        ? TerrainSunShadow(IN.positionWS, mainLight.direction)
                        : 1.0;

                // The shadow of moving objects comes from the map and is MULTIPLIED with the
                // terrain's: two separate phenomena — one is being behind a ridge, the other
                // is having an object above you. They go through the same channel because
                // both cut the direct sun.
                mainLight.shadowAttenuation *=
                    MainLightRealtimeShadow(TransformWorldToShadowCoord(IN.positionWS));

                // THE CLOUD SHADOW comes from the cloud system's own cookie texture; the very
                // density field that draws the sky. It cuts the direct sun and does not touch
                // the indirect light from the sky — through the same channel as the terrain shadow.
            #ifdef _LIGHT_COOKIES
                mainLight.color *= SampleMainLightCookie(IN.positionWS);
            #endif

                half3 lit = inputData.bakedGI * aoFactor.indirectAmbientOcclusion * brdfData.diffuse;
                lit += LightingPhysicallyBased(brdfData, mainLight,
                    inputData.normalWS, inputData.viewDirectionWS) * aoFactor.directAmbientOcclusion;

                // RAIN IMPACTS ARE READ IN REFLECTION, NOT AS PAINTED CIRCLES.
                //
                // The ring slope already reached `surface.normalWS`, but this hand-written
                // lighting path deliberately omitted URP's environment specular. During rain
                // the sun is cloud-attenuated and SampleSH is almost directionless, so changing
                // the normal changed virtually no light: the ground got wet, yet the rings were
                // invisible. The sea did not have the bug because it evaluates a sky
                // reflection for every water normal.
                //
                // Only the near, resolvable rain-film region evaluates the reflection. Terrain
                // renderers do not receive a valid per-object reflection probe in this scene,
                // so sampling `unity_SpecCube0` here returns the error colour. `AirColor` is
                // the shared sky/fog source and remains valid for every terrain draw. The top
                // interface uses water's F0 (n = 1.333 -> 0.0204); no albedo circle or
                // lighting-independent glow is introduced.
                if (surface.rainFilm > 0.001h)
                {
                    half3 filmNormal = normalize((half3)surface.rainFilmNormalWS);
                    half3 filmReflectionVector = reflect(-inputData.viewDirectionWS, filmNormal);
                    half3 filmSky = (half3)AirColor(filmReflectionVector)
                                  * (half)surface.occlusion;

                    half NoV = saturate(dot(filmNormal, inputData.viewDirectionWS));
                    const half WaterF0 = 0.0204h;
                    half oneMinusNoV = 1.0h - NoV;
                    half filmFresnel = WaterF0 + (1.0h - WaterF0)
                                     * Pow4(oneMinusNoV) * oneMinusNoV;
                    lit += filmSky * filmFresnel * surface.rainFilm
                         * (1.0h - surface.snowMask);
                }

                // THE TERRAIN'S SNOW ALSO USES THE SNOW'S OWN LIGHTING.
                //
                // The snow mesh used to use wrapped NdotL + back translucency + ambient with
                // `_ShadowTint`, while the terrain's snow layer used URP's standard PBR. The
                // same snow, two models. Measured: a 2.3% brightness difference across the
                // two sides of the region boundary (inside 0.8318, outside 0.8132) — over a
                // flat white field that difference reads as a HARD LINE and drew the 24 m
                // square that followed the player.
                //
                // Snow is the same material wherever it is; its lighting comes from one place
                // too. The rock side stays on standard PBR and the two results are blended
                // with `snowMask`.
                if (surface.snowMask > 0.001)
                {
                    // On the terrain side there is no trail and no crust; the density is the
                    // world's general value. The depth is the cover thickness — a column
                    // measured on the mesh, constant here.
                    // THE DEPTH IS THE WORLD'S SNOW COLUMN. `_SnowCoverThickness` (4 cm) is
                    // for the thin cover ON OBJECTS (spec §16); lighting the terrain with it
                    // gave a different result from the ~50 cm column the snow mesh sees. The
                    // depth drives `SnowAmbient`'s translucency term through `exp(-depth·7)`.
                    //
                    // AO is wired the same way as on the mesh side: `SnowHeightAO` there,
                    // the surface's own occlusion here. A constant 1.0 ignored the terrain's
                    // hollows.
                    //
                    // The two were measured together: the mesh/terrain brightness ratio fell
                    // from 1.61x to 1.16x (the 24 m square symptom).
                    // The texture blend was read ONCE in `BuildSurface` and carried through
                    // `surface.snowBlend`; it is not resampled here.
                    // Density, wetness and disturbance are LOCAL: compacted snow inside the
                    // trail, virgin snow outside. `BuildSurface` read both and carried them
                    // in the struct.
                    SnowSurface ks = SnowBuildSurfaceFrom(surface.snowBlend,
                                                          surface.snowRhoN, surface.snowWet,
                                                          surface.snowDisturb, 0.0,
                                                          _WorldSnowDepth, IN.positionWS,
                                                          length(fwidth(IN.positionWS.xz)));

                    // THE NORMAL IS NOT ADDED AGAIN HERE. The surface texture's slope already
                    // enters `surface.normalWS` inside `MountainSurface.hlsl` and
                    // `inputData.normalWS` comes from there; added a second time the bump
                    // would come out double.
                    float3 karN = inputData.normalWS;

                    // AMBIENT LIGHT IS SCALED BY SKY VISIBILITY.
                    //
                    // `SampleSH` is DIRECTIONLESS in this scene: measured, up and down give
                    // the same value (0.223, 0.293, 0.420) because PBSky has no ground term
                    // and the sky is drawn below the horizon too. Directionless ambient gives
                    // snow no shape at all: with the sun off, the screen's deviation falls to
                    // 0.00232, i.e. the snow is flat white paper.
                    //
                    // The sky radiance reaching a point is as much as it CAN SEE of the sky.
                    // The mountain's own horizon already measures that (`SampleSkyVisibility`,
                    // which the snow mask uses as well). Tied to the ambient, hollows and
                    // slopes separate.
                    half skyVisibility = (half)SampleSkyVisibility(IN.positionWS);

                    // THE CAVITY'S OWN SHADOW is applied to the direct light. It is not
                    // applied to the ambient: the sky comes from every direction and the
                    // cavity wall does not cut it — that is the `occlusion` term's job.
                    // THE SKY SHARE: diffuse irradiance / (diffuse + direct). That is the
                    // physical meaning of the shadow ceiling — a surface in shadow does not
                    // get the sun, it gets the sky. In overcast it goes to 1 and the shadow
                    // erases itself.
                    half gokLum   = Luminance(SampleSH(half3(0, 1, 0)));
                    half gunesLum = Luminance(mainLight.color)
                                  * saturate(mainLight.direction.y);
                    half skyShare   = gokLum / max(gokLum + gunesLum, 1e-4h);

                    Light trailLight = mainLight;
                    trailLight.shadowAttenuation *= SnowReliefShadow(mainLight.direction,
                                                                  surface.snowDentDepth,
                                                                  skyShare);

                    half3 karIsik = SnowDirectLight(trailLight, karN,
                                                    inputData.viewDirectionWS, ks)
                                  + SnowAmbient(karN, ks,
                                                mainLight.shadowAttenuation,
                                                (half)surface.occlusion * skyVisibility,
                                                mainLight.color,
                                                mainLight.direction);

                    lit = lerp(lit, karIsik, (half)surface.snowMask);
                }

                // SUN REFLECTED OFF THE SNOW. A point in shadow is surrounded by snow the
                // sun is hitting, and that light was never counted: there is no GI in the
                // scene and the ambient comes only from the sky probe. With a snow albedo of
                // 0.8 the missing term is LARGER than the sky's.
                //
                // Measurement (color probe 2, 15:00): the sun-shadow difference was 3.5-5
                // stops; the real value for snow in clear weather is 2-3.5. A gap of 1-1.5 stops.
                //
                // THE VIEW FACTOR IS TEXTBOOK, NO INVENTED COEFFICIENT. The fraction of the
                // sky a sloped surface sees is (1+cosβ)/2, the rest is ground: (1-cosβ)/2.
                // cosβ is the normal's Y. Zero on flat ground — flat ground sees no other
                // ground, which is correct.
                //
                // THE SHADOW MULTIPLIER IS NOT APPLIED and that is deliberate: the reflected
                // light comes FROM THE SURROUNDINGS, and the surroundings may well be in sun
                // while this point is in shadow. That is the entire point.
                //
                // It fades out at night by itself: `direction.y` drops to zero and the sun's
                // intensity already carries the air mass attenuation.
                float groundView = (1.0 - saturate(shadingNormal.y)) * 0.5;
                float3 horizontalIrradiance = mainLight.color * saturate(mainLight.direction.y);
                lit += surface.albedo * horizontalIrradiance * groundView
                     * brdfData.diffuse * aoFactor.indirectAmbientOcclusion;

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                    lit += LightingPhysicallyBased(brdfData, light,
                        inputData.normalWS, inputData.viewDirectionWS);
                LIGHT_LOOP_END
                #endif

                // FOAM AT A VERTICAL BANK STILL SEES THE SKY. The terrain BRDF uses the
                // bank normal, so a shadowed face can turn even off-white contact foam
                // nearly black and reveal the polygon silhouette again. Use the upward sky
                // irradiance for the aerated residue only; it follows the time and weather,
                // and therefore does not become a lighting-independent glow at night.
                half shoreContact = surface.shoreContact * (1.0h - surface.snowMask);
                half3 shoreSky = SampleSH(half3(0.0h, 1.0h, 0.0h))
                               * half3(0.78h, 0.80h, 0.82h);
                lit = lerp(lit, max(lit, shoreSky), shoreContact * 0.72h);

                lit += surface.emission;

                half4 color = half4(lit, 1.0);

                // Height fog instead of Unity's: density collects low and thins with
                // altitude. Applied together the attenuation would be counted twice.
                color.rgb = ApplyHeightFog(color.rgb, _WorldSpaceCameraPos, IN.positionWS);

                return color;
            }
            ENDHLSL
        }

        // The shadow and depth passes are WRITTEN BY HAND: URP's ready-made files bring
        // their traps, such as shadow bias, along with them.
        //
        // The bias is still applied with URP's own function (`ApplyShadowBias`) — only the
        // vertex flow is written by hand.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment ShadowFragment
            #pragma target 5.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "../Snow/Shaders/SnowTessellation.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;


                // The bias is applied along the normal; the displaced surface's normal differs
                // from the terrain's, so it is the real normal rather than straight up.
                float2 uv = (positionWS.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
                float2 packed = SAMPLE_TEXTURE2D_LOD(_GroundNormals, sampler_GroundNormals,
                                                     uv, 0).rg * 2.0 - 1.0;
                float3 baseNormal = normalize(float3(packed.x,
                    sqrt(saturate(1.0 - dot(packed, packed))), packed.y));
                float3 normalWS = baseNormal;

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            /// Hull oncesi gecis: yalniz dunya konumuna cevirir.
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessPosition(patch, bary));
            }

            half4 ShadowFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment DepthFragment
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "../Snow/Shaders/SnowTessellation.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            /// Hull oncesi gecis: yalniz dunya konumuna cevirir.
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessPosition(patch, bary));
            }

            half4 DepthFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // SSAO Source: it reads DepthNormals; without this pass the normal buffer stays empty
        // and ambient occlusion reads garbage over the terrain.
        //
        // The standard pass writes the VERTEX normal; reading it, SSAO mistakes the triangle
        // breaks of the terrain mesh for "surface curvature" and shades them, and soft
        // lattice lines resembling the mesh appeared on the ground — with its 30 metre
        // falloff, only up close, nailed to the world, independent of the hour. It was found
        // at the end of a long elimination hunt. The smooth baked normal that lighting uses
        // is written instead: two consumers see the same surface and cannot contradict.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment frag
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MountainSurfaceInput.hlsl"
            #include "../Snow/Shaders/SnowTessellation.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            /// Hull oncesi gecis: yalniz dunya konumuna cevirir.
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessPosition(patch, bary));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = (IN.positionWS.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
                float2 packed = SAMPLE_TEXTURE2D(_GroundNormals, sampler_GroundNormals, uv).rg
                                * 2.0 - 1.0;
                float3 baseNormal = normalize(float3(packed.x,
                    sqrt(saturate(1.0 - dot(packed, packed))), packed.y));

                // SSAO reads this buffer: the slope of the snow deposition has to be here
                // too, otherwise the shadow that belongs at the foot of a bump never forms.
                return half4(baseNormal, 0.0);
            }
            ENDHLSL
        }
    }
}
