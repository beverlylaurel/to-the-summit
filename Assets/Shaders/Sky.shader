// include-rev: 85  (HeightFog.hlsl degisince Unity bu dosyaya dokunulmadikca
// yeniden derlemiyor; bu satir degistikce derleme zorlanir)
Shader "ToTheSummit/Sky"
{
    Properties
    {
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _MoonColor ("Ay", Color) = (0.75, 0.8, 0.95, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The gradient, the air color and the fog come from here: the sky does not
            // compute its own color, it calls the SAME AirColor function as the fog. As
            // long as two formulas were kept they drifted apart at every weather corner.
            // The _SunDirection and _LightningFlash declarations come from there too.
            #include "HeightFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunColor;
                float4 _MoonColor;
            CBUFFER_END

            // Shared with the cloud pass: AtmosphereController writes it globally
            float3 _MoonDirection;
            float _StarStrength;

            /// The cloud system's ambient probe pass writes 1 and returns to 0 when done.
            /// So the sun/moon disc does not enter the probe cube (`VolumetricCloudsURP`).
            float _DisableSunDisk;


            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.direction = IN.positionOS.xyz;
                return OUT;
            }

            /// Direction space must be divided in three dimensions: reducing it to two drops
            /// different directions into the same cell and the stars redistribute as the camera turns.
            float Hash3(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            /// Disc: a sharp core, a narrow inner halo, a broad outer scattering
            float3 Disk(float3 direction, float3 target, float3 color, float size, float glow, float brightness)
            {
                float d = saturate(dot(direction, target));

                float disk = smoothstep(1.0 - size, 1.0 - size * 0.25, d);

                float3 core = color * (disk * 7.0);

                // Stepped halo rings: each layer is wider than the previous one, fainter
                // and more saturated. Light travelling outward passes through a longer path
                // of atmosphere, so the centre saturates to white while the outer rings fall
                // to yellow, orange and red. Drawing it with one continuous falloff left the
                // disc a flat blob — the stepping is what makes a real halo readable.
                // An exponential falloff produces no layers: as the exponent shrinks the
                // function flattens into a general glow spread over the sky. Each ring needs its own boundary.
                float3 tint = color;
                float3 halo = 0.0;
                float radius = size * 7.0;
                float weight = 2.2;

                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    tint *= color;                  // the color deepens with each layer

                    // The band is wide inside and narrow outside. A fixed ratio breaks one
                    // end or the other: at a small radius a narrow band sharpens the edge and
                    // leaves an artificial circle, at a large radius a wide band cannot saturate the ring and puts it out.
                    float band = radius * lerp(2.4, 0.6, i / 4.0);

                    float edge = 1.0 - radius;
                    float ring = smoothstep(edge, edge + band, d);

                    halo += tint * (ring * weight);
                    radius *= 2.6;                  // the next ring is markedly wider
                    weight *= 0.5;                  // and fainter
                }

                return (core + halo) * brightness;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 direction = normalize(IN.direction);
                float height = direction.y;

                // The gradient is shared with the fog: whatever the air is, the sky is. Stars,
                // discs and lightning are objects BEHIND the "air" — fog sits on top of them,
                // and while the sky was not fogged the stars showed even inside the soup.
                float3 sky = AirColor(direction);
                float3 extras = 0.0;

                // Stars sit in cells of direction space; each cell carries its own size,
                // brightness and color temperature. Because the position derives from the
                // direction, the stars stay put even as the camera turns.
                // The grid has to be wider than a few pixels on screen. On a narrow grid a star
                // falls below half a pixel; as the camera turns the pixel grid shifts and the
                // star jumps to a neighbouring pixel, looking as if it moved.
                float3 grid = direction * 140.0;
                float3 cell = floor(grid);
                float3 local = frac(grid) - 0.5;

                float present = step(0.986, Hash3(cell));

                // The size sits in a narrow band: a real star is almost a point, but on screen
                // it cannot be drawn stably unless it covers at least a pixel or two
                float radius = lerp(0.17, 0.36, Hash3(cell + 5.1));
                float shape = smoothstep(radius, radius * 0.25, length(local));

                // Brightness follows a square distribution: the sky has many faint stars and few bright ones
                float magnitude = Hash3(cell + 17.3);
                float brightness = lerp(0.3, 1.0, magnitude * magnitude);

                // Color temperature: a hot star is blue-white, a cool one yellowish-orange.
                // The spread is kept small; most stars really do look close to white.
                float temperature = Hash3(cell + 91.7) * 2.0 - 1.0;
                float3 tint = float3(1.0 + temperature * 0.16, 1.0, 1.0 - temperature * 0.20);

                // Atmospheric turbulence makes stars twinkle, but not all of them: only a few
                // sparkle. If they all did, the sky would look like it is boiling.
                float twinkleRoll = Hash3(cell + 41.7);
                float twinkles = step(0.38, twinkleRoll);

                // Turbulence works at several scales at once; a single frequency beats like a
                // metronome. Two rates on top of each other make the rhythm irregular and give
                // every star its own pattern. The slow layer sets the lower bound:
                // even the fastest star does not repeat in less than a few seconds.
                float slowRate = lerp(0.7, 1.2, Hash3(cell + 63.1));
                float fastRate = lerp(1.5, 2.2, Hash3(cell + 77.9));
                float phase = twinkleRoll * 6.2831853;

                float flicker = sin(_Time.y * slowRate + phase) * 0.6
                              + sin(_Time.y * fastRate + phase * 2.3) * 0.4;

                // A sparkling star goes out completely and comes back. A small oscillation was
                // invisible on a faint star; atmospheric turbulence really does dim a star all
                // the way down to the visibility limit.
                float twinkle = lerp(1.0, saturate(flicker * 0.5 + 0.5), twinkles);

                extras += present * shape * brightness * twinkle * tint
                          * _StarStrength * saturate(height);

                // Visibility depends on the disc's own elevation: it must not go out at dawn.
                // Near the horizon the core TURNS WHITE, it does not brighten: a brightness
                // multiplier also grew the halo and filled the wall around the disc, and under
                // tonemapping both stuck to the same orange. A real setting sun reads whiter
                // and more yellow than the orange around it — the separation comes from color.
                float lowDisk = 1.0 - saturate(abs(_SunDirection.y) / 0.3);
                float3 sunDiskColor = lerp(_SunColor.rgb, float3(1.0, 0.92, 0.78), lowDisk * 0.5);
                float sunVisible = smoothstep(-0.10, 0.04, _SunDirection.y);
                float moonVisible = smoothstep(-0.10, 0.04, _MoonDirection.y);

                // Discs are not put in the same basket as stars: a star goes out in the first
                // thickness of fog, but the sun is at astronomical brightness — in clear air it
                // is visible on the horizon too, which is why we can watch it set. It fades
                // along a bounded path rather than the infinite sky path: in clear air a dim
                // red disc remains (its color already filtered), and in rain and soup it disappears.
                // THE DISCS ARE TURNED OFF WHILE THE CLOUD AMBIENT PROBE IS DRAWN. The cloud
                // system draws the sky into a 16x16 cube and uses its average as ambient light;
                // if the sun disc entered it (brightness 1400) the average would shift toward
                // the disc's color and the clouds would go brown. The source requires this:
                // "capture the sky environment without sun disk" (`sky brief.md`).
                // The global is set up by the cloud system's ambient pass.
                float3 disks = (1.0 - _DisableSunDisk) * (
                    Disk(direction, _SunDirection, sunDiskColor, 0.0016, 1400.0, sunVisible)
                  + Disk(direction, _MoonDirection, _MoonColor.rgb, 0.0011, 3000.0, moonVisible * 0.5));

                // Lightning also lights the sky visible through the gaps, but what really glows
                // is the cloud mass itself — that is added in the compositing pass. The share
                // here is small. The position and radius come from exactly the same value as the
                // cloud: computed separately, the sky would flash in one place and the cloud in another.
                //
                // Because the sky is at infinity the distance cannot be used directly; the blob's
                // **angular** size is computed instead. A near strike covers a wide area, a
                // distant one leaves a narrow blob at the same radius — that is perspective.
                // THE FLASH GLOW COMES FROM THE TABLE. There were two heuristic terms here for a
                // while: an angular blob (`0.35 * 1/(1+spread²)`) and a flat contribution weighted
                // by the fog share. Neither knew the distance or the phase angle; that was exactly
                // the glow Dobashi criticised as "different from the real physical phenomenon".
                //
                // Now it is `HeightFog.hlsl -> LightningScatter` — the SAME source as the fog and
                // the terrain. Computed separately, the sky would flash in one place and the fog in another.
                float fogAmount = SkyFogAmount(_WorldSpaceCameraPos, direction);
                sky += extras * (1.0 - fogAmount);
                sky += LightningScatter(_WorldSpaceCameraPos,
                                        _WorldSpaceCameraPos + direction * 100000.0);

                float diskFade = exp(-SkyFogDepth(_WorldSpaceCameraPos, direction, 8000.0));
                sky += disks * diskFade;

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
