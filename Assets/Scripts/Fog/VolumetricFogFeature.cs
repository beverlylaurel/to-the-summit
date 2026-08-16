using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// VOLUMETRİK SİS — Wronski 2014 froxel hacmini süren render geçişi.
///
/// İki compute dispatch: hacmi doldur, ışın boyunca birik. Sonuç `_FogScatteringVolume`
/// olarak global bağlanıyor; `HeightFog.hlsl` onu her yüzey shader'ında örnekliyor.
/// Ayrı bir uygulama geçişi YOK — sis zaten yüzeylerin içinde uygulanıyordu, ikinci bir
/// yol açmak aynı havayı iki kez uygulamak olurdu.
///
/// GEÇİŞ NOKTASI opak çizimden hemen önce: gölge haritaları ve ışık cookie'si o an hazır,
/// opak yüzeyler de hacmi okuyabiliyor.
public class VolumetricFogFeature : ScriptableRendererFeature
{
    [SerializeField] ComputeShader compute;
    [SerializeField] VolumetricFogSettings settings;

    FogPass pass;

    /// TEŞHİS — GEÇİCİ. Hacim gerçekten dolduruluyor mu, gölge kodu derlendi mi.
    /// Sis doğrulanınca bu alanlar ve F1'deki bölüm silinir.
    public static int DispatchCount;
    public static bool ShadowKeywordOn;
    public static bool CookieBound;
    public static Vector4 VolumeDepth;

    /// TEŞHİS — GEÇİCİ. Kapatınca hacim dağıtılmaya devam ediyor ama `_FogVolumeDepth.z`
    /// sıfırlanıyor: `HeightFog.hlsl` hacmi atlayıp kuyruğu kameradan başlatıyor, yani
    /// görüntü hacim ÖNCESİ hâline dönüyor. A/B doğrulamasının tamamı bu.
    public static bool VolumeDisabled;

    /// TEŞHİS — GEÇİCİ. Hacmin ortam kaynağı ile analitik yolun sis rengi aynı olguyu
    /// tarif ediyor; ölçekleri ayrışırsa hacim sönümü uygulayıp ışımayı koyamıyor.
    public static Vector3 AmbientDC;
    public static Vector4 FogColor;
    public static bool CookieMatrixValid;

    public override void Create()
    {
        pass = new FogPass(compute, settings)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (compute == null || settings == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Release();
        pass = null;
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
        static readonly int SnowProfileId = Shader.PropertyToID("_SnowProfile");
        static readonly int LightDirectionId = Shader.PropertyToID("_FogMainLightDirection");
        static readonly int LightColorId = Shader.PropertyToID("_FogMainLightColor");
        static readonly int CookieTextureId = Shader.PropertyToID("_MainLightCookieTexture");
        static readonly int CookieMatrixId = Shader.PropertyToID("_MainLightWorldToLight");

        const int GroupSize = 8;

        readonly ComputeShader compute;
        readonly VolumetricFogSettings settings;

        /// KERNEL'LER GEÇ ÇÖZÜLÜYOR. Kurucuda çözülünce, compute o an derlenmemişse
        /// `FindKernel` −1 döndürüyor ve o değer önbellekte kalıyor: shader sonradan
        /// düzelse bile geçiş her karede "Kernel at index (0) is invalid" basıyor.
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

        /// Kernel'ler hazır mı. Compute derlenmemişse geçiş sessizce atlanıyor —
        /// hata zaten shader derleyicisinden Console'a düşüyor, her karede tekrar
        /// basmanın bilgi değeri yok.
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

        /// 3B hacimler KALICI, geçiş grafiğinin geçici havuzunda değil: `_FogScatteringVolume`
        /// global olarak bağlanıyor ve opak çizim boyunca yaşaması gerekiyor.
        ///
        /// FORMAT DESTEĞİ KONTROL EDİLİYOR: `R16G16B16A16_SFloat` üzerinde random write
        /// desteği platforma göre değişiyor. Sessizce yanlış formata düşmek yerine açıkça
        /// fırlatılıyor.
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

        /// Frustum köşe ışınları, ileri eksene izdüşümü 1 olacak şekilde.
        /// `worldPos = cameraPos + ray · viewDepth` bu sayede doğrudan çalışıyor;
        /// normalize edilseydi köşelerde derinlik merkeze göre uzar, dilimler düzlem
        /// yerine küresel kabuk olurdu.
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
                VolumetricFogFeature.VolumeDisabled ? 0f : Mathf.Log(far / near), d);
            var size = new Vector4(w, h, 1f / w, 1f / h);

            // Zamansal kayma: sekiz kareye yayılan sabit dizi. Wronski jitter'ı
            // aliasing'i gürültüye takas etmek için öneriyor (spec §6.2); TAA açık
            // olduğu için gürültü zaten zamanda dağılıyor.
            frame = (frame + 1) & 7;
            var jitter = new Vector4(Halton(frame + 1, 2) - 0.5f, 0f, 0f, 0f);

            var lightParams = new Vector4(settings.Anisotropy, settings.AmbientDimmer,
                                          settings.LightDimmer, 0f);

            cmd.SetGlobalVector(VolumeDepthId, depth);
            cmd.SetGlobalVector(VolumeSizeId, size);
            cmd.SetGlobalVectorArray(CornerRaysId, cornerRays);
            cmd.SetGlobalVector(JitterId, jitter);
            cmd.SetGlobalVector(CameraForwardId, data.camera.transform.forward);

            // Uniform'lar KERNEL'E DEĞİL shader'a yazılıyor; iki kernel de aynı değerleri
            // görüyor, tek yazım yeter.
            cmd.SetComputeVectorParam(compute, VolumeDepthId, depth);
            cmd.SetComputeVectorParam(compute, VolumeSizeId, size);
            cmd.SetComputeVectorArrayParam(compute, CornerRaysId, cornerRays);
            cmd.SetComputeVectorParam(compute, JitterId, jitter);
            cmd.SetComputeVectorParam(compute, CameraPosId, data.camera.transform.position);
            cmd.SetComputeVectorParam(compute, LightParamsId, lightParams);

            UpdateAmbientSH();
            cmd.SetComputeVectorArrayParam(compute, AmbientSHId, ambientSH);

            // ANA IŞIK AÇIKÇA GEÇİYOR. `_MainLightColor` ve `_MainLightPosition`'ı URP
            // KOMUT TAMPONU üzerinden yazıyor; compute dispatch'i o durumu görmüyor ve
            // doğrudan ışık terimi sessizce SIFIR kalıyordu — gölgelenecek ışık olmayınca
            // huzme de doğmuyordu. `Shader.SetGlobalX` ile yazılan globaller (sis
            // yoğunluğu, sis rengi) ulaşıyor; ayrım tam olarak burada.
            cmd.SetComputeVectorParam(compute, LightDirectionId, data.lightDirection);
            cmd.SetComputeVectorParam(compute, LightColorId, data.lightColor);

            int groupsX = Mathf.CeilToInt(w / (float)GroupSize);
            int groupsY = Mathf.CeilToInt(h / (float)GroupSize);

            // DOKULAR KERNEL'E AÇIKÇA BAĞLANIYOR. `Shader.SetGlobalTexture` materyalleri
            // besliyor ama compute kernel'leri global doku tablosunu OKUMUYOR; bağlanmazsa
            // Unity "Property is not set" basıp kernel'i geçersiz sayıyor.
            //
            // Yoğunluk modeli ikisini de okuyor: arazi yüksekliği spindrift'in yere
            // yapışması için, kar profili de rüzgârın kaldıracak kar bulup bulmadığı için.
            BindGlobalTexture(cmd, densityKernel, TerrainHeightMapId, "_TerrainHeightMap");
            BindGlobalTexture(cmd, densityKernel, SnowProfileId, "_SnowProfile");
            BindGlobalTexture(cmd, densityKernel, CookieTextureId, "_MainLightCookieTexture");

            // Cookie matrisi de komut tamponuyla yazılıyor; sıfır okunursa UV sabit
            // kalır ve bulut gölgesi yapı yerine tek bir çarpana düşer.
            Matrix4x4 cookieMatrix = Shader.GetGlobalMatrix(CookieMatrixId);
            VolumetricFogFeature.CookieMatrixValid = cookieMatrix != Matrix4x4.zero;
            cmd.SetComputeMatrixParam(compute, CookieMatrixId, cookieMatrix);

            cmd.SetComputeTextureParam(compute, densityKernel, DensityVolumeId, densityVolume);
            cmd.DispatchCompute(compute, densityKernel, groupsX, groupsY, d);

            cmd.SetComputeTextureParam(compute, marchKernel, DensityVolumeReadId, densityVolume);
            cmd.SetComputeTextureParam(compute, marchKernel, ScatteringVolumeId, scatteringVolume);
            cmd.DispatchCompute(compute, marchKernel, groupsX, groupsY, 1);

            cmd.SetGlobalTexture(ScatteringVolumeId, scatteringVolume);

            VolumetricFogFeature.DispatchCount++;
            VolumetricFogFeature.ShadowKeywordOn = true;   // varyant sabit, gölge yolu hep derleniyor
            VolumetricFogFeature.VolumeDepth = depth;
            VolumetricFogFeature.CookieBound = Shader.GetGlobalTexture("_MainLightCookieTexture") != null;
            VolumetricFogFeature.AmbientDC = new Vector3(ambientSH[0].w, ambientSH[1].w, ambientSH[2].w);
            VolumetricFogFeature.FogColor = Shader.GetGlobalVector("_HeightFogColor");
        }

        /// Global doku tablosundan okuyup kernel'e bağlar. Doku henüz üretilmemişse
        /// (arazi pişmeden ilk kareler) siyah doku bağlanıyor — bağlamamak kernel'i
        /// tamamen geçersiz kılardı.
        void BindGlobalTexture(CommandBuffer cmd, int kernel, int id, string name)
        {
            Texture texture = Shader.GetGlobalTexture(name);
            cmd.SetComputeTextureParam(compute, kernel, id,
                                       texture != null ? texture : Texture2D.blackTexture);
        }

        /// ORTAM SH'si ELLE PAKETLENİYOR. `unity_SHAr` ve kardeşleri `UnityPerDraw`
        /// sabit tamponunda duruyor ve compute dispatch'i o tamponu bağlamıyor — orada
        /// okunsalardı sıfır gelir, gölgeli sis simsiyah çıkardı.
        ///
        /// Paketleme Unity'nin kendi düzeni: `(L1z, L1x, L1y, L0 − L2_2)`. Kaynak
        /// `RenderSettings.ambientProbe`, yani gökyüzünden pişen tek durum.
        void UpdateAmbientSH()
        {
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;

            for (int c = 0; c < 3; c++)
                ambientSH[c] = new Vector4(probe[c, 3], probe[c, 1], probe[c, 2],
                                           probe[c, 0] - probe[c, 6]);
        }

        /// Halton dizisi: ardışık örnekler birbirinden uzak düşüyor, rastgele sayıda
        /// olduğu gibi kümelenmiyor.
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

            // Ana ışık URP'nin kendi seçiminden okunuyor: gökyüzü ve bulut da aynı ışığı
            // kullanıyor, üçü ayrışmasın.
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
