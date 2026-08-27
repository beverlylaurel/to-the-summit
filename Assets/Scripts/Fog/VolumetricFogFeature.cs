using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// VOLUMETRIC FOG — the render pass driving Wronski 2014's froxel volume.
///
/// Two compute dispatches: fill the volume, integrate along the ray. The result is bound globally
/// as `_FogScatteringVolume`; `HeightFog.hlsl` samples it in every surface shader.
/// There is NO separate apply pass — the fog was already applied inside the surfaces, and opening
/// a second path would mean applying the same air twice.
///
/// THE INJECTION POINT is just before the opaque draw: the shadow maps and the light cookie are
/// ready by then, and the opaque surfaces can read the volume too.
public class VolumetricFogFeature : ScriptableRendererFeature
{
    [SerializeField] ComputeShader compute;
    [SerializeField] VolumetricFogSettings settings;

    /// FOG ON THE SKY IS A SEPARATE PASS. The sky is drawn by the PBSky package and it knows
    /// nothing of our fog; adding a call into the package would be a patch. This pass looks at the
    /// depth and fogs only the pixels that hit nothing — the hole is not specific to the sky, it
    /// exists on everything that does not call `ApplyHeightFog`.
    [SerializeField] Shader skyFogShader;

    FogPass pass;
    SkyFogPass skyPass;
    Material skyFogMaterial;

    public override void Create()
    {
        pass = new FogPass(compute, settings)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };

        if (skyFogShader != null)
        {
            skyFogMaterial = CoreUtils.CreateEngineMaterial(skyFogShader);

            // BEFORE THE CLOUDS. This pass fogs only the SKY, for the infinite path.
            //
            // At one point it was moved after the clouds: the sky was fogged and the clouds
            // painted over it immediately after, and because the sky is 83% cloud in a storm the
            // whitening was never seen. Moving the pass earlier removed the symptom but not its
            // cause — because the cloud was not fogged it was being fogged together with the sky,
            // i.e. from an INFINITE distance. The cloud stands at 2 km.
            //
            // The right rule: every layer is fogged once with ITS OWN distance. The terrain in its
            // own shader, the cloud in the compositing pass (`FogPath`), the sky here. With this
            // pass back in front of the cloud all three get a single application and no double counting is left.
            skyPass = new SkyFogPass(skyFogMaterial)
            {
                // AFTER THE PACKAGE'S OPAQUE PASS, BEFORE THE CLOUDS.
                //
                // At `AfterRenderingSkybox` this pass fell IN FRONT OF the package's `Opaque
                // Atmospheric Scattering` pass. On a silhouette pixel the two overlap: this pass
                // counts that pixel as "sky" with `ZTest Equal` and fogs it, while the package
                // counts it as "geometry" from the depth texture and lays its own aerial
                // perspective on top. The result was a double application in a one-pixel strip —
                // a dark outline in normal play, and a white one in the fog inspection because it
                // rode on top of the magenta. The mountain's body stayed flat because there is no overlap there.
                //
                // The `+2` drops it after the package's pass but still before the clouds in
                // `BeforeRenderingTransparents` — the rule that every layer is fogged once with
                // its own distance is preserved.
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 2
            };
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (compute == null || settings == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        renderer.EnqueuePass(pass);

        if (skyPass != null) renderer.EnqueuePass(skyPass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Release();
        pass = null;
        skyPass = null;

        CoreUtils.Destroy(skyFogMaterial);
        skyFogMaterial = null;
    }

    /// The full-screen pass applying the fog to the sky pixels. The blend is in the shader:
    /// `result = target × T + scattering`.
    class SkyFogPass : ScriptableRenderPass
    {
        readonly Material material;

        class PassData
        {
            public Material material;
            public TextureHandle source;
        }

        public SkyFogPass(Material fogMaterial) => material = fogMaterial;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Sky Fog", out var passData);

            passData.material = material;

            builder.SetRenderAttachment(resources.activeColorTexture, 0);

            // The depth is bound as an ATTACHMENT, not as a texture: the selection is made with
            // `ZTest Equal` and there is no read, only a test.
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
            });
        }
    }

    class FogPass : ScriptableRenderPass
    {
        static readonly int DensityVolumeId = Shader.PropertyToID("_FogDensityVolume");
        static readonly int DensityVolumeReadId = Shader.PropertyToID("_FogDensityVolumeRead");
        static readonly int ScatteringVolumeId = Shader.PropertyToID("_FogScatteringVolume");
        static readonly int VolumeDepthId = Shader.PropertyToID("_FogVolumeDepth");
        static readonly int VolumeSizeId = Shader.PropertyToID("_FogVolumeSize");
        static readonly int CornerRaysId = Shader.PropertyToID("_FogCornerRays");
        static readonly int JitterId = Shader.PropertyToID("_FogJitter");
        static readonly int CameraPosId = Shader.PropertyToID("_FogCameraPos");
        static readonly int CameraForwardId = Shader.PropertyToID("_FogCameraForward");
        static readonly int LightParamsId = Shader.PropertyToID("_FogLightParams");
        static readonly int AmbientSHId = Shader.PropertyToID("_FogAmbientSH");
        static readonly int TerrainHeightMapId = Shader.PropertyToID("_TerrainHeightMap");
        static readonly int LightDirectionId = Shader.PropertyToID("_FogMainLightDirection");
        static readonly int LightColorId = Shader.PropertyToID("_FogMainLightColor");
        static readonly int CookieTextureId = Shader.PropertyToID("_MainLightCookieTexture");
        static readonly int ShadowmapId = Shader.PropertyToID("_MainLightShadowmapTexture");
        static readonly int WorldToShadowId = Shader.PropertyToID("_MainLightWorldToShadow");
        static readonly int ShadowParamsId = Shader.PropertyToID("_MainLightShadowParams");
        static readonly int ShadowmapSizeId = Shader.PropertyToID("_MainLightShadowmapSize");
        static readonly int CascadeRadiiId = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");

        /// The cascade split spheres. Which cascade to read in a cascaded shadow follows from
        /// them; if they are not passed they are all zero and every point falls into the first cascade.
        static readonly int[] CascadeSphereIds =
        {
            Shader.PropertyToID("_CascadeShadowSplitSpheres0"),
            Shader.PropertyToID("_CascadeShadowSplitSpheres1"),
            Shader.PropertyToID("_CascadeShadowSplitSpheres2"),
            Shader.PropertyToID("_CascadeShadowSplitSpheres3"),
        };
        static readonly int CookieMatrixId = Shader.PropertyToID("_MainLightWorldToLight");

        const int GroupSize = 8;

        readonly ComputeShader compute;
        readonly VolumetricFogSettings settings;

        /// THE KERNELS ARE RESOLVED LATE. Resolved in the constructor, if the compute was not
        /// compiled at that moment `FindKernel` returns −1 and that value stays in the cache: even
        /// once the shader is fixed the pass prints "Kernel at index (0) is invalid" every frame.
        int densityKernel = -1;
        int marchKernel = -1;
        readonly Vector4[] cornerRays = new Vector4[4];
        readonly Vector4[] ambientSH = new Vector4[3];

        RenderTexture densityVolume;
        RenderTexture scatteringVolume;
        int frame;

        class PassData
        {
            public FogPass pass;
            public Camera camera;
            public Vector4 lightDirection;
            public Vector4 lightColor;
        }

        public FogPass(ComputeShader computeShader, VolumetricFogSettings fogSettings)
        {
            compute = computeShader;
            settings = fogSettings;
        }

        /// Whether the kernels are ready. If the compute did not compile the pass is silently
        /// skipped — the error already reaches the Console from the shader compiler and printing
        /// it again every frame carries no information.
        bool ResolveKernels()
        {
            if (densityKernel >= 0 && marchKernel >= 0) return true;

            if (!compute.HasKernel("FogDensityAndLighting")) return false;
            if (!compute.HasKernel("FogRayMarch")) return false;

            densityKernel = compute.FindKernel("FogDensityAndLighting");
            marchKernel = compute.FindKernel("FogRayMarch");

            return densityKernel >= 0 && marchKernel >= 0;
        }

        public void Release()
        {
            if (densityVolume != null) { densityVolume.Release(); densityVolume = null; }
            if (scatteringVolume != null) { scatteringVolume.Release(); scatteringVolume = null; }
        }

        /// The 3D volumes are PERSISTENT, not in the render graph's transient pool:
        /// `_FogScatteringVolume` is bound globally and has to live through the opaque draw.
        ///
        /// FORMAT SUPPORT IS CHECKED: random write support on `R16G16B16A16_SFloat` varies by
        /// platform. Rather than silently falling back to a wrong format it throws explicitly.
        void EnsureVolumes()
        {
            int w = settings.Width, h = settings.Height, d = settings.SliceCount;

            if (densityVolume != null && densityVolume.width == w
                && densityVolume.height == h && densityVolume.volumeDepth == d)
                return;

            Release();

            const GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat;
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore))
                throw new InvalidOperationException(
                    $"{nameof(VolumetricFogFeature)}: {format} random write desteklemiyor.");

            densityVolume = CreateVolume(w, h, d, format, "_FogDensityVolume");
            scatteringVolume = CreateVolume(w, h, d, format, "_FogScatteringVolume");
        }

        static RenderTexture CreateVolume(int w, int h, int d, GraphicsFormat format, string name)
        {
            var volume = new RenderTexture(w, h, 0, format)
            {
                name = name,
                dimension = TextureDimension.Tex3D,
                volumeDepth = d,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            volume.Create();
            return volume;
        }

        /// The frustum corner rays, scaled so their projection onto the forward axis is 1.
        /// `worldPos = cameraPos + ray · viewDepth` works directly thanks to that; normalized, the
        /// depth at the corners would stretch relative to the centre and the slices would be
        /// spherical shells instead of planes.
        void UpdateCornerRays(Camera camera)
        {
            Transform t = camera.transform;

            float tanH = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanW = tanH * camera.aspect;

            Vector3 forward = t.forward, right = t.right * tanW, up = t.up * tanH;

            cornerRays[0] = forward - right - up;
            cornerRays[1] = forward + right - up;
            cornerRays[2] = forward - right + up;
            cornerRays[3] = forward + right + up;
        }

        void Dispatch(CommandBuffer cmd, PassData data)
        {
            if (!ResolveKernels()) return;

            EnsureVolumes();
            UpdateCornerRays(data.camera);

            float near = settings.NearDistance, far = settings.FarDistance;
            int w = settings.Width, h = settings.Height, d = settings.SliceCount;

            var depth = new Vector4(near, far,
                Mathf.Log(far / near), d);
            var size = new Vector4(w, h, 1f / w, 1f / h);

            // Temporal jitter: a fixed sequence spread over eight frames. Wronski recommends the
            // jitter to trade aliasing for noise (spec §6.2); because TAA is on, the noise is
            // already dispersed in time.
            frame = (frame + 1) & 7;
            var jitter = new Vector4(Halton(frame + 1, 2) - 0.5f, 0f, 0f, 0f);

            var lightParams = new Vector4(settings.Anisotropy, settings.AmbientDimmer,
                                          settings.LightDimmer, 0f);

            cmd.SetGlobalVector(VolumeDepthId, depth);
            cmd.SetGlobalVector(VolumeSizeId, size);
            cmd.SetGlobalVectorArray(CornerRaysId, cornerRays);
            cmd.SetGlobalVector(JitterId, jitter);
            cmd.SetGlobalVector(CameraForwardId, data.camera.transform.forward);
            // THE FOG INSPECTION. IT IS WRITTEN TWICE and that is required: `cmd.SetGlobal...`
            // DOES NOT REACH the compute (this file's own lesson). Written only as a global, the
            // terrain and sky would turn magenta while the volume kept its old colour and the tool would lie.

            // The uniforms are written to the SHADER, not to a kernel; both kernels see the same
            // values, so writing them once is enough.
            cmd.SetComputeVectorParam(compute, VolumeDepthId, depth);
            cmd.SetComputeVectorParam(compute, VolumeSizeId, size);
            cmd.SetComputeVectorArrayParam(compute, CornerRaysId, cornerRays);
            cmd.SetComputeVectorParam(compute, JitterId, jitter);
            cmd.SetComputeVectorParam(compute, CameraPosId, data.camera.transform.position);
            cmd.SetComputeVectorParam(compute, LightParamsId, lightParams);

            UpdateAmbientSH();
            cmd.SetComputeVectorArrayParam(compute, AmbientSHId, ambientSH);

            // THE MAIN LIGHT IS PASSED EXPLICITLY. URP writes `_MainLightColor` and
            // `_MainLightPosition` through the COMMAND BUFFER; the compute dispatch does not see
            // that state and the direct light term was silently staying ZERO — with no light to
            // shadow, no beam was born either. Globals written with `Shader.SetGlobalX` (the fog
            // density, the fog colour) do reach it; the distinction is exactly here.
            cmd.SetComputeVectorParam(compute, LightDirectionId, data.lightDirection);
            cmd.SetComputeVectorParam(compute, LightColorId, data.lightColor);

            int groupsX = Mathf.CeilToInt(w / (float)GroupSize);
            int groupsY = Mathf.CeilToInt(h / (float)GroupSize);

            // THE TEXTURES ARE BOUND TO THE KERNEL EXPLICITLY. `Shader.SetGlobalTexture` feeds
            // materials, but compute kernels DO NOT READ the global texture table; unbound, Unity
            // prints "Property is not set" and treats the kernel as invalid.
            BindGlobalTexture(cmd, densityKernel, TerrainHeightMapId, "_TerrainHeightMap");
            BindGlobalTexture(cmd, densityKernel, CookieTextureId, "_MainLightCookieTexture");

            // THE MAIN LIGHT'S SHADOW. `MainLightRealtimeShadow` is called inside the compute
            // (the variant `_MAIN_LIGHT_SHADOWS_CASCADE` is fixed) but the shadow map WAS NOT
            // BEING BOUND to the kernel: Unity printed "Property (_MainLightShadowmapTexture) at
            // kernel index (0) is not set" and the fog never saw the light the terrain cut — there
            // were no beams in the volume, only the cloud shadow coming from the cookie.
            //
            // The texture alone is not enough: choosing the right cascade in a cascaded shadow
            // needs the split spheres, and sampling needs the matrix array and the parameters.
            // All of them are written by URP THROUGH THE COMMAND BUFFER, so the compute does not
            // see them on its own — this file's own lesson, and the same was done for the light
            // colour and the cookie matrix.
            BindGlobalTexture(cmd, densityKernel, ShadowmapId, "_MainLightShadowmapTexture");

            Matrix4x4[] worldToShadow = Shader.GetGlobalMatrixArray(WorldToShadowId);
            if (worldToShadow != null && worldToShadow.Length > 0)
                cmd.SetComputeMatrixArrayParam(compute, WorldToShadowId, worldToShadow);

            cmd.SetComputeVectorParam(compute, ShadowParamsId,
                Shader.GetGlobalVector(ShadowParamsId));
            cmd.SetComputeVectorParam(compute, ShadowmapSizeId,
                Shader.GetGlobalVector(ShadowmapSizeId));
            cmd.SetComputeVectorParam(compute, CascadeRadiiId,
                Shader.GetGlobalVector(CascadeRadiiId));

            for (int i = 0; i < CascadeSphereIds.Length; i++)
                cmd.SetComputeVectorParam(compute, CascadeSphereIds[i],
                    Shader.GetGlobalVector(CascadeSphereIds[i]));

            // The cookie matrix is written through the command buffer too; read as zero the UV
            // stays fixed and the cloud shadow falls to a single multiplier instead of structure.
            Matrix4x4 cookieMatrix = Shader.GetGlobalMatrix(CookieMatrixId);
            cmd.SetComputeMatrixParam(compute, CookieMatrixId, cookieMatrix);

            cmd.SetComputeTextureParam(compute, densityKernel, DensityVolumeId, densityVolume);
            cmd.DispatchCompute(compute, densityKernel, groupsX, groupsY, d);

            cmd.SetComputeTextureParam(compute, marchKernel, DensityVolumeReadId, densityVolume);
            cmd.SetComputeTextureParam(compute, marchKernel, ScatteringVolumeId, scatteringVolume);
            cmd.DispatchCompute(compute, marchKernel, groupsX, groupsY, 1);

            cmd.SetGlobalTexture(ScatteringVolumeId, scatteringVolume);

        }

        /// Reads from the global texture table and binds to the kernel. If the texture has not
        /// been produced yet (the first frames before the terrain is baked) a black texture is
        /// bound — not binding would make the kernel entirely invalid.
        void BindGlobalTexture(CommandBuffer cmd, int kernel, int id, string name)
        {
            Texture texture = Shader.GetGlobalTexture(name);
            cmd.SetComputeTextureParam(compute, kernel, id,
                                       texture != null ? texture : Texture2D.blackTexture);
        }

        /// THE AMBIENT SH IS PACKED BY HAND. `unity_SHAr` and its siblings live in the
        /// `UnityPerDraw` constant buffer and the compute dispatch does not bind that buffer — read
        /// there they would come out zero and shadowed fog would be pitch black.
        ///
        /// The packing is Unity's own layout: `(L1z, L1x, L1y, L0 − L2_2)`. The source is
        /// `RenderSettings.ambientProbe`, i.e. the single state baked from the sky.
        void UpdateAmbientSH()
        {
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;

            for (int c = 0; c < 3; c++)
                ambientSH[c] = new Vector4(probe[c, 3], probe[c, 1], probe[c, 2],
                                           probe[c, 0] - probe[c, 6]);
        }

        /// A Halton sequence: consecutive samples fall far from each other, they do not cluster
        /// the way random numbers do.
        static float Halton(int index, int radix)
        {
            float result = 0f, fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();

            using var builder = renderGraph.AddUnsafePass<PassData>("Volumetrik Sis", out var passData);

            passData.pass = this;
            passData.camera = cameraData.camera;

            // The main light is read from URP's own choice: the sky and the cloud use the same
            // light, so the three do not diverge.
            var lightData = frameData.Get<UniversalLightData>();
            passData.lightDirection = new Vector4(0f, 1f, 0f, 0f);
            passData.lightColor = Vector4.zero;

            if (lightData.mainLightIndex >= 0)
            {
                VisibleLight light = lightData.visibleLights[lightData.mainLightIndex];
                Vector3 direction = -light.localToWorldMatrix.GetColumn(2);

                passData.lightDirection = new Vector4(direction.x, direction.y, direction.z, 0f);
                passData.lightColor = light.finalColor;
            }

            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                data.pass.Dispatch(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd), data));
        }
    }
}
