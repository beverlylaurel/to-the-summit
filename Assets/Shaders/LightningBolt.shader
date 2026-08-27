// include-rev: 68
Shader "ToTheSummit/LightningBolt"
{
    // The channel is drawn additively: lightning is a light-emitting plasma, it does not
    // darken the cloud or sky behind it, it adds on top. Color and fade come from the
    // LineRenderer's vertex color — a single material is shared instead of changing it every frame.
    SubShader
    {
        // Drawn after the clouds, but it accounts for the clouds itself.
        //
        // Putting it in the opaque queue and leaving it to the compositing was tried and
        // reverted: the `alpha` there is the cloud accumulated along the pixel's **whole ray**.
        // The sea ten kilometres away, behind the bolt, is inside that number too — so the
        // bolt was darkened by cloud standing behind it and vanished entirely in a storm.
        //
        // The correct behaviour is to be attenuated only by the cloud **in front**: the distance
        // at which the ray enters the layer is compared with the bolt's distance. If the bolt is
        // nearer than the layer it is not attenuated at all — looking at a channel hanging below
        // the cloud base there is no cloud in between, which is how it really is.
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // It does not write depth: the channel is light, not a surface. If it did, the cloud
        // behind it would be cut in the ray march and a cloudless hole would open around the channel.
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Bolt"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HeightFog.hlsl"

            TEXTURE2D(_CloudTexture);
            SAMPLER(sampler_CloudTexture);

            // The layer's elevations are written globally by AtmosphereController; here they
            // are only read for the question "is there cloud in front of me". Including
            // CloudCommon.hlsl would drag the whole volume marcher, its 3D textures and
            // dozens of globals into a line shader.
            //
            // `_CloudBottom` is NOT DECLARED here: ever since cloud shadows were added
            // HeightFog.hlsl'de duruyor ve bu dosya onu zaten dahil ediyor.
            float _CloudTop;

            // Radius of the cloud sphere, a global written by AtmosphereController. A local
            // copy cannot be kept: the sphere's radius sets the scene scale, and if the two
            // drift the lightning ends up in front of or behind the cloud instead of inside it.
            float _PlanetRadius;
            #define BoltPlanetRadius _PlanetRadius

            /// Distance at which the ray enters a sphere of the given radius. Negative if it misses.
            float BoltSphereEntry(float3 origin, float3 direction, float radius)
            {
                float b = dot(origin, direction);
                float c = dot(origin, origin) - radius * radius;
                float d = b * b - c;
                if (d < 0.0) return -1.0;

                float root = sqrt(d);
                float near = -b - root;

                return near > 0.0 ? near : -b + root;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Cross profile along the strip: a white core in the middle, a halo at the edges.
                // A uniform strip looks like paper; a real channel is a thin, very bright axis
                // wrapped in a fainter glow.
                float across = abs(IN.uv.y * 2.0 - 1.0);

                float core = saturate(1.0 - across * 4.0);
                float halo = 1.0 - across;
                halo *= halo;

                float3 light = IN.color.rgb * (core * 3.0 + halo * 0.6);

                // The upper end comes out of the cloud, it does not start there. Starting with a
                // hard end makes the channel look pinned in front of the cloud; a short fade-in
                // makes it look like it emerges from inside the mass.
                light *= smoothstep(0.0, 0.18, IN.uv.x);

                // The air itself swallows the channel too. Because we draw additively it does not
                // blend into the fog color, it fades: a bolt two kilometres away that stays as
                // bright as one at the base reads with no distance and looks painted on the sky.
                // The fog model is tuned for surfaces: at the visibility range the terrain
                // disappears completely. The channel is many times brighter than those surfaces,
                // so applying the same attenuation would erase it entirely in a storm — the only
                // weather it strikes in. The square root accounts for a bright source reaching
                // further through fog.
                light *= sqrt(1.0 - HeightFogAmount(_WorldSpaceCameraPos, IN.positionWS));

                // It fades by the cloud in front of it, not the cloud behind. The distance at
                // which the ray enters the layer is compared with the bolt's; if the bolt is
                // nearer than the layer the share is zero.
                float3 toBolt = IN.positionWS - _WorldSpaceCameraPos;
                float boltDistance = length(toBolt);

                float3 fromCentre = _WorldSpaceCameraPos - float3(0.0, -BoltPlanetRadius, 0.0);
                float3 toward = toBolt / boltDistance;

                float entry = BoltSphereEntry(fromCentre, toward, BoltPlanetRadius + _CloudBottom);
                float exit = BoltSphereEntry(fromCentre, toward, BoltPlanetRadius + _CloudTop);

                if (entry >= 0.0 && exit > entry)
                {
                    float share = saturate((boltDistance - entry) / (exit - entry));
                    float2 screen = IN.positionCS.xy / _ScaledScreenParams.xy;
                    float opacity = SAMPLE_TEXTURE2D(_CloudTexture, sampler_CloudTexture, screen).a;

                    light *= 1.0 - opacity * share;
                }

                return half4(light, 1.0);
            }
            ENDHLSL
        }
    }
}
