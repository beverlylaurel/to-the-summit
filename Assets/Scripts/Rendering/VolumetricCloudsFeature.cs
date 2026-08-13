using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// Bulutları düşük çözünürlükte çizip kareler arasında biriktirir, sonra sahneye bindirir.
///
/// Tam çözünürlükte, her karede baştan hesaplamak piksel başına yüzlerce hacim örneği
/// demek; ölçtüğümüzde tek başına yirmi milisaniyenin üstündeydi. İş hem mekâna hem zamana
/// yayılıyor: çözünürlük düşürülüyor ve her kare piksel içinde başka bir noktadan
/// örnekleniyor. Kamera durduğunda birikmiş görüntü tam çözünürlüğün örnek yoğunluğuna
/// yakınsıyor — yani durağan hâlde kayıp yok. Hareket hâlinde geçmiş kameranın hareketine
/// göre yeniden konumlandırılıyor.
public class VolumetricCloudsFeature : ScriptableRendererFeature
{
    [SerializeField] Shader shader;
    [Tooltip("1 = tam çözünürlük, 2 = yarım, 4 = çeyrek. Maliyeti kadrat olarak düşürür. " +
             "Kaybolan ayrıntıyı kareler arası biriktirme geri getiriyor, o yüzden düşük " +
             "çözünürlük tek başına değerlendirilmemeli.")]
    [SerializeField, Range(1, 4)] int downsample = 4;

    Material material;
    CloudPass pass;

    public override void Create()
    {
        if (shader == null) return;

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new CloudPass(material, downsample)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || material == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Release();
        CoreUtils.Destroy(material);
    }

    class CloudPass : ScriptableRenderPass
    {
        static readonly int CloudTextureId = Shader.PropertyToID("_CloudTexture");
        static readonly int HistoryId = Shader.PropertyToID("_CloudHistory");
        static readonly int TargetSizeId = Shader.PropertyToID("_CloudTargetSize");
        static readonly int CameraForwardId = Shader.PropertyToID("_CloudCameraForward");
        static readonly int RayBottomLeftId = Shader.PropertyToID("_CloudRayBottomLeft");
        static readonly int RayBottomRightId = Shader.PropertyToID("_CloudRayBottomRight");
        static readonly int RayTopLeftId = Shader.PropertyToID("_CloudRayTopLeft");
        static readonly int RayTopRightId = Shader.PropertyToID("_CloudRayTopRight");
        static readonly int JitterId = Shader.PropertyToID("_CloudJitter");
        static readonly int PreviousViewProjectionId = Shader.PropertyToID("_CloudPreviousViewProjection");
        static readonly int HistoryValidId = Shader.PropertyToID("_CloudHistoryValid");
        static readonly int FullSizeId = Shader.PropertyToID("_CloudFullSize");
        static readonly int BlockIndexId = Shader.PropertyToID("_CloudBlockIndex");

        static readonly Vector3[] frustumCorners = new Vector3[4];

        readonly Material material;
        readonly int downsample;

        /// Blokta hangi hücrenin hangi karede hesaplanacağı. Blok kenarı `downsample`
        /// kadar: ışın yürüyüşü kaç kat küçükse tam çözünürlüğü doldurmak o kadar kare
        /// sürüyor. Sıra ardışık kareleri komşu hücrelere düşürmüyor, birikme böyle
        /// daha çabuk düzgünleşiyor.
        readonly int[] blockOrder;

        RTHandle[] history = new RTHandle[2];
        int historyIndex;
        int historyWidth, historyHeight;
        bool historyValid;

        Matrix4x4 previousViewProjection = Matrix4x4.identity;
        int frame;

        public CloudPass(Material material, int downsample)
        {
            this.material = material;
            this.downsample = Mathf.Max(1, downsample);
            blockOrder = BuildBlockOrder(this.downsample);
        }

        public void Release()
        {
            history[0]?.Release();
            history[1]?.Release();
            history[0] = history[1] = null;
            historyValid = false;
        }

        static int[] BuildBlockOrder(int size)
        {
            var order = new int[size * size];

            // Dört tabanlı ters bit sırası yalnızca 4×4'te tanımlı; küçük bloklarda
            // hücre sayısı zaten az olduğundan sıranın dağılımı fark ettirmiyor.
            for (int i = 0; i < order.Length; i++)
                order[i] = size == 4
                    ? ((i & 1) << 3) | ((i & 2) << 1) | ((i & 4) >> 1) | ((i & 8) >> 3)
                    : i;

            return order;
        }

        /// Geçmiş tamponu kareler arasında yaşamak zorunda, dolayısıyla Render Graph'ın
        /// kendi geçici dokularıyla olmuyor: dışarıda tutulup içeri aktarılıyor.
        void EnsureHistory(int width, int height)
        {
            if (history[0] != null && historyWidth == width && historyHeight == height) return;

            Release();

            historyWidth = width;
            historyHeight = height;

            for (int i = 0; i < 2; i++)
                history[i] = RTHandles.Alloc(width, height,
                    colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite: false, filterMode: FilterMode.Bilinear,
                    wrapMode: TextureWrapMode.Clamp, name: "CloudHistory" + i);
        }

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle history;
            public int shaderPass;

            public bool setCameraData;
            public Vector4 cameraForward;
            public Vector4 targetSize;
            public Vector4 rayBottomLeft, rayBottomRight, rayTopLeft, rayTopRight;
            public Vector4 jitter;

            public bool setResolveData;
            public Matrix4x4 previousViewProjection;
            public Vector4 fullSize;
            public float blockIndex;
            public float valid;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();

            if (resources.isActiveTargetBackBuffer) return;

            var sceneColor = resources.activeColorTexture;
            if (!sceneColor.IsValid()) return;

            var descriptor = camera.cameraTargetDescriptor;
            int width = Mathf.Max(1, descriptor.width / downsample);
            int height = Mathf.Max(1, descriptor.height / downsample);

            // Geçmiş tam çözünürlükte. Düşük çözünürlükte tutulunca her karenin farklı
            // alt-piksel örnekleri aynı hücreye biriktiriliyor, yani ayrıntı çözülmüyor
            // ortalanıyordu: sonuç daha yumuşak bir düşük çözünürlük oluyordu, daha keskin
            // bir tam çözünürlük değil. Tam çözünürlükte her örnek kendi pikseline yazılıyor.
            EnsureHistory(descriptor.width, descriptor.height);

            var cloudDescriptor = new RenderTextureDescriptor(width, height,
                RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            var raw = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, cloudDescriptor, "CloudsRaw", false, FilterMode.Bilinear);

            var previous = renderGraph.ImportTexture(history[historyIndex]);
            var accumulated = renderGraph.ImportTexture(history[1 - historyIndex]);

            var cam = camera.camera;
            cam.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), cam.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

            var transform = cam.transform;
            Vector3 bottomLeft = transform.TransformVector(frustumCorners[0]);
            Vector3 topLeft = transform.TransformVector(frustumCorners[1]);
            Vector3 topRight = transform.TransformVector(frustumCorners[2]);
            Vector3 bottomRight = transform.TransformVector(frustumCorners[3]);

            var targetSize = new Vector4(width, height, 1f / width, 1f / height);
            var fullSize = new Vector4(descriptor.width, descriptor.height,
                1f / descriptor.width, 1f / descriptor.height);

            // Bu karede hesaplanacak hücre ve onun düşük çözünürlüklü doku içindeki kayması.
            // Kayma hücrenin tam çözünürlük ızgarasındaki merkezine denk geliyor: örnek
            // yazılacağı pikselin tam ortasından alınıyor.
            int block = blockOrder[frame % blockOrder.Length];
            var jitter = new Vector2(block % downsample + 0.5f, block / downsample + 0.5f)
                         / downsample - new Vector2(0.5f, 0.5f);

            // 1 — ışın yürüyüşü, düşük çözünürlükte ve kareye göre kaydırılmış
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Bulut ışın yürütme", out var data))
            {
                data.material = material;
                data.source = sceneColor;
                data.shaderPass = 0;
                data.setCameraData = true;
                data.cameraForward = transform.forward;
                data.targetSize = targetSize;
                data.rayBottomLeft = bottomLeft;
                data.rayBottomRight = bottomRight;
                data.rayTopLeft = topLeft;
                data.rayTopRight = topRight;
                data.jitter = jitter;

                builder.UseTexture(sceneColor);
                builder.SetRenderAttachment(raw, 0);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(CameraForwardId, pass.cameraForward);
                    context.cmd.SetGlobalVector(TargetSizeId, pass.targetSize);
                    context.cmd.SetGlobalVector(RayBottomLeftId, pass.rayBottomLeft);
                    context.cmd.SetGlobalVector(RayBottomRightId, pass.rayBottomRight);
                    context.cmd.SetGlobalVector(RayTopLeftId, pass.rayTopLeft);
                    context.cmd.SetGlobalVector(RayTopRightId, pass.rayTopRight);
                    context.cmd.SetGlobalVector(JitterId, pass.jitter);

                    Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0),
                        pass.material, pass.shaderPass);
                });
            }

            // 2 — geçmişle harmanla; sonuç bir sonraki karenin geçmişi olur
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Bulut biriktirme", out var data))
            {
                data.material = material;
                data.source = raw;
                data.history = previous;
                data.shaderPass = 1;
                data.setResolveData = true;
                data.previousViewProjection = previousViewProjection;
                data.fullSize = fullSize;
                data.blockIndex = block;
                data.valid = historyValid ? 1f : 0f;

                builder.UseTexture(raw);
                builder.UseTexture(previous);
                builder.SetRenderAttachment(accumulated, 0);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(HistoryId, pass.history);
                    context.cmd.SetGlobalMatrix(PreviousViewProjectionId, pass.previousViewProjection);
                    context.cmd.SetGlobalVector(FullSizeId, pass.fullSize);
                    context.cmd.SetGlobalFloat(BlockIndexId, pass.blockIndex);
                    context.cmd.SetGlobalFloat(HistoryValidId, pass.valid);

                    Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0),
                        pass.material, pass.shaderPass);
                });
            }

            // 3 — sahne rengi kopyalanır: aynı dokuyu hem okuyup hem yazamayız
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            var sceneCopy = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "SceneBeforeClouds", false);

            renderGraph.AddCopyPass(sceneColor, sceneCopy, passName: "Sahne kopyası");

            // 4 — birikmiş sonuç sahneye bindirilir
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Bulut bindirme", out var data))
            {
                data.material = material;
                data.source = sceneCopy;
                data.history = accumulated;
                data.shaderPass = 2;

                builder.UseTexture(sceneCopy);
                builder.UseTexture(accumulated);
                builder.SetRenderAttachment(sceneColor, 0);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(CloudTextureId, pass.history);

                    Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0),
                        pass.material, pass.shaderPass);
                });
            }

            // Sahne kamerası ile oyun kamerası aynı geçmişi paylaşamaz: matrisleri farklı,
            // biri diğerinin birikimini bozar. Yalnızca oyun kamerası biriktiriyor.
            if (camera.cameraType == CameraType.Game)
            {
                previousViewProjection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true)
                                         * cam.worldToCameraMatrix;
                historyIndex = 1 - historyIndex;
                historyValid = true;
                frame++;
            }
        }
    }
}
