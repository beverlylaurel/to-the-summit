using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Equips a physical camera, presents an exposure-simulated live view and records a separate,
/// pre-post-process HDR exposure. The game's eye adaptation and cinematic grade never become
/// part of the JPEG; the camera's own lens, sensor and ISP response is applied exactly once.
[DisallowMultipleComponent]
public sealed class VintagePhotoMode : MonoBehaviour
{
    enum Mode { Hidden, Equipped, Viewfinder, Review, Gallery }

    [Serializable]
    sealed class PhotoMetadata
    {
        public string fileName;
        public string capturedLocal;
        public int iso;
        public float aperture;
        public float shutterSeconds;
        public float exposureCompensation;
        public float meteredSceneLuminance;
        public string metering;
        public string whiteBalance;
        public int width;
        public int height;
    }

    [SerializeField] VintageDslrProfile profile;
    [SerializeField] Shader processingShader;
    [SerializeField] VintagePhotoPreviewFeature previewFeature;
    [SerializeField] Camera viewCamera;
    [SerializeField] Camera captureCamera;
    [SerializeField] Renderer placeholder;
    [SerializeField] MouseLook mouseLook;
    [SerializeField] FirstPersonController movement;

    Mode mode;
    Mode galleryReturnMode = Mode.Equipped;
    Material processingMaterial;
    VintagePhotoLibrary library;
    Texture2D displayedPhoto;
    int galleryIndex;
    float reviewUntil;
    float exposureCompensation;
    float baseFieldOfView;
    float zoom = 1f;
    float targetZoom = 1f;
    float zoomVelocity;
    bool ownsFieldOfView;
    VintageZoomFocus zoomFocus;
    VintagePhotoPreview preview;
    bool capturing;
    bool captureCameraRendered;
    string captureStage = "Idle";
    string notice;
    float noticeUntil;

    [NonSerialized] GUIStyle hudStyle;
    [NonSerialized] GUIStyle smallStyle;
    [NonSerialized] GUIStyle centerStyle;
    [NonSerialized] GUIStyle galleryStyle;

    public bool IsEquipped => mode != Mode.Hidden;
    public bool IsCapturing => capturing;
    public string CaptureStage => captureStage;
    public string PhotoDirectory => library?.DirectoryPath ?? string.Empty;
    public int PhotoCount => library?.Count ?? 0;

#if UNITY_EDITOR
    public bool EditorPreviewReady => preview?.Ready ?? false;
    public float EditorPreviewExposure => preview?.Exposure ?? 0f;
    public float EditorPreviewLuminance => preview?.Luminance ?? 0f;
    public Texture EditorPreviewImage => preview?.Image;
    public float EditorFocusAmount => zoomFocus?.Amount ?? 0f;
    public void EditorViewfinderForTest(bool active) => SetMode(active ? Mode.Viewfinder : Mode.Hidden);
    public void EditorZoomForTest(float steps) => ChangeZoom(steps);

    /// Integration-test entry point. It is absent from player builds and uses the same capture
    /// coroutine as the real left-click path.
    public void EditorCaptureForTest()
    {
        if (capturing) return;
        SetMode(Mode.Viewfinder);
        StartCoroutine(Capture());
    }
#endif

    public void Bind(VintageDslrProfile cameraProfile, Shader photoShader, VintagePhotoPreviewFeature liveFeature, Camera playerCamera,
                     Camera hdrCaptureCamera, Renderer cameraPlaceholder, MouseLook look,
                     FirstPersonController controller)
    {
        previewFeature = liveFeature;
        profile = cameraProfile;
        processingShader = photoShader;
        viewCamera = playerCamera;
        captureCamera = hdrCaptureCamera;
        placeholder = cameraPlaceholder;
        mouseLook = look;
        movement = controller;

        if (Application.isPlaying) Initialize();
    }

    void OnEnable() => Initialize();

    void Initialize()
    {
        if (profile == null || processingShader == null || previewFeature == null || viewCamera == null
            || captureCamera == null || placeholder == null || mouseLook == null || movement == null)
            throw new InvalidOperationException($"{nameof(VintagePhotoMode)}: dependencies are not assigned.");

        // Unity objects retain a managed wrapper after native destruction when domain
        // reload is disabled. Use Unity's null check, not the C# null-coalescing operator.
        if (processingMaterial == null) processingMaterial = new Material(processingShader)
        {
            name = "Vintage DSLR Processing (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };

        library ??= new VintagePhotoLibrary();
        preview?.Dispose();
        preview = new VintagePhotoPreview(profile, processingShader);
        previewFeature.Register(viewCamera, preview);
        RenderPipelineManager.endCameraRendering -= OnViewCameraRendered;
        RenderPipelineManager.endCameraRendering += OnViewCameraRendered;
        zoomFocus = new VintageZoomFocus(profile);
        exposureCompensation = profile.exposureCompensation;
        capturing = false;
        captureStage = "Idle";
        captureCamera.enabled = false;
        SetMode(Mode.Hidden);
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnViewCameraRendered;
        if (preview != null) previewFeature.Unregister(preview);
        preview?.Dispose();
        preview = null;
        zoomFocus = null;
        RenderPipelineManager.endCameraRendering -= OnCaptureCameraRendered;
        if (captureCamera != null) captureCamera.enabled = false;
        if (placeholder != null) placeholder.enabled = false;
        RestorePlayerInput();
        RestoreFieldOfView();
        DestroyDisplayedPhoto();
    }

    void OnDestroy()
    {
        if (processingMaterial != null) Destroy(processingMaterial);
    }

    void Update()
    {
        if (mode == Mode.Review && Time.unscaledTime >= reviewUntil)
        {
            DestroyDisplayedPhoto();
            SetMode(Mode.Viewfinder);
        }

        if (noticeUntil > 0f && Time.unscaledTime >= noticeUntil)
            notice = string.Empty;

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.digit4Key.wasPressedThisFrame)
        {
            if (capturing)
            {
                ShowNotice("Fotoğraf kaydediliyor");
                return;
            }
            SetMode(mode == Mode.Hidden ? Mode.Equipped : Mode.Hidden);
            return;
        }

        if (mode == Mode.Hidden) return;

        if (keyboard.gKey.wasPressedThisFrame)
        {
            if (mode == Mode.Gallery)
            {
                CloseGallery();
            }
            else if (!capturing)
            {
                galleryReturnMode = mode == Mode.Viewfinder ? Mode.Viewfinder : Mode.Equipped;
                OpenGallery();
            }
            return;
        }

        if (mode == Mode.Gallery)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                StepGallery(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                StepGallery(1);
            if (mouse.rightButton.wasPressedThisFrame) CloseGallery();
            return;
        }

        if (mode == Mode.Review)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                DestroyDisplayedPhoto();
                SetMode(Mode.Equipped);
            }
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (capturing)
            {
                ShowNotice("Fotoğraf kaydediliyor");
                return;
            }
            SetMode(mode == Mode.Viewfinder ? Mode.Equipped : Mode.Viewfinder);
            return;
        }

        if (mode != Mode.Viewfinder) return;

        if (!capturing)
        {
            // Uniform scroll deltas already express wheel steps. Only native Windows
            // deltas use 120 units per step; dividing uniform input again hides the zoom.
            float wheel = mouse.scroll.ReadValue().y;
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                && (Application.platform == RuntimePlatform.WindowsEditor
                    || Application.platform == RuntimePlatform.WindowsPlayer))
                wheel /= 120f;
            ChangeZoom(wheel);
        }

        if (!capturing && keyboard.qKey.wasPressedThisFrame) ChangeCompensation(-1f / 3f);
        if (!capturing && keyboard.eKey.wasPressedThisFrame) ChangeCompensation(1f / 3f);

        if (mouse.leftButton.wasPressedThisFrame && !capturing)
        {
            if (library.Count >= profile.cardCapacity)
                ShowNotice("Hafıza kartı dolu");
            else
                StartCoroutine(Capture());
        }
    }

    void ChangeCompensation(float delta)
    {
        exposureCompensation = Mathf.Clamp(
            Mathf.Round((exposureCompensation + delta) * 3f) / 3f, -3f, 3f);
    }

    void ChangeZoom(float steps)
    {
        targetZoom = Mathf.Clamp(targetZoom * Mathf.Exp(steps * profile.zoomStep),
            1f, profile.maximumZoom);
    }

    void SetMode(Mode next)
    {
        if (next == Mode.Viewfinder && !ownsFieldOfView)
        {
            baseFieldOfView = viewCamera.fieldOfView;
            ownsFieldOfView = true;
        }
        if (next == Mode.Hidden || next == Mode.Equipped) RestoreFieldOfView();
        if (mode != next && next != Mode.Viewfinder) preview?.Reset();
        mode = next;
        if (preview != null) preview.Enabled = next == Mode.Viewfinder;
        if (next != Mode.Viewfinder) zoomFocus?.Reset();
        if (placeholder != null)
            placeholder.enabled = next == Mode.Equipped;

        bool frozen = next == Mode.Review || next == Mode.Gallery;
        if (mouseLook != null) mouseLook.InputEnabled = !frozen;
        if (movement != null) movement.InputEnabled = !frozen;

        if (next == Mode.Hidden || next == Mode.Equipped || next == Mode.Viewfinder)
            DestroyDisplayedPhoto();
    }

    void RestorePlayerInput()
    {
        if (mouseLook != null) mouseLook.InputEnabled = true;
        if (movement != null) movement.InputEnabled = true;
    }

    void LateUpdate()
    {
        if (preview != null)
        {
            preview.Enabled = mode == Mode.Viewfinder && (!capturing || !preview.Ready);
            Rect frame = FitRect(3f / 2f, 0.82f);
            preview.Crop = new Vector4(frame.width / Screen.width, frame.height / Screen.height,
                frame.x / Screen.width, frame.y / Screen.height);
        }
        if (!ownsFieldOfView || mode != Mode.Viewfinder || capturing)
        {
            zoomFocus?.Reset();
            return;
        }
        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity,
            profile.zoomSmoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        viewCamera.fieldOfView = 2f * Mathf.Atan(
            Mathf.Tan(baseFieldOfView * Mathf.Deg2Rad * 0.5f) / zoom) * Mathf.Rad2Deg;
        zoomFocus?.Tick(Mathf.Abs(zoomVelocity) / Mathf.Max(zoom, 1f), Time.unscaledDeltaTime);
    }

    void RestoreFieldOfView()
    {
        if (ownsFieldOfView && viewCamera != null) viewCamera.fieldOfView = baseFieldOfView;
        ownsFieldOfView = false;
        zoom = targetZoom = 1f;
        zoomVelocity = 0f;
    }

    IEnumerator Capture()
    {
        capturing = true;
        captureStage = "WaitingForPreview";
        int previewFrames = 0;
        while ((preview == null || !preview.Ready) && previewFrames++ < 60) yield return null;
        if (preview == null || !preview.Ready)
        {
            capturing = false;
            captureStage = "PreviewUnavailable";
            ShowNotice("Vizör hazırlanıyor; tekrar dene");
            yield break;
        }
        float exposure = preview.Exposure;
        float sceneLuminance = preview.Luminance;
        float seed = preview.Seed;
        captureStage = "WaitingForCamera";
        if (placeholder != null) placeholder.enabled = false;

        RenderTexture scene = CreateTarget(profile.captureWidth, profile.captureHeight,
            GraphicsFormat.R16G16B16A16_SFloat, 24, "Vintage DSLR Scene HDR");

        ConfigureCaptureCamera(scene);

        // The camera is enabled for one render only. The SRP callback is the proof that URP
        // actually completed this camera; WaitForEndOfFrame can stall forever when Unity's
        // Game view is not focused, which also made automated/headless captures unreliable.
        captureCameraRendered = false;
        RenderPipelineManager.endCameraRendering -= OnCaptureCameraRendered;
        RenderPipelineManager.endCameraRendering += OnCaptureCameraRendered;
        captureCamera.enabled = true;
        int waitedFrames = 0;
        while (!captureCameraRendered && waitedFrames++ < 8)
            yield return null;

        RenderPipelineManager.endCameraRendering -= OnCaptureCameraRendered;
        captureCamera.enabled = false;

        if (!captureCameraRendered)
        {
            ReleaseTarget(scene);
            capturing = false;
            captureStage = "CameraDidNotRender";
            ShowNotice("Çekim kamerası görüntü üretemedi");
            yield break;
        }

        // Lock the exposure/colour response that was visible at shutter time. Do not
        // re-meter the high-resolution capture and surprise the player with a new exposure.
        captureStage = "Processing";
        VintagePhotoProcessing.Configure(processingMaterial, profile, exposure, seed);
        RenderTexture output = CreateTarget(profile.outputWidth, profile.outputHeight,
            GraphicsFormat.R8G8B8A8_SRGB, 0, "Vintage DSLR JPEG Source");
        Graphics.Blit(scene, output, processingMaterial, 0);
        ReleaseTarget(scene);

        // Async readback avoids a 10 MP GPU fence. JPEG compression itself remains on the
        // main thread because Unity's encoder owns native engine memory.
        bool complete = false;
        bool failed = false;
        Texture2D photo = null;
        AsyncGPUReadback.Request(output, 0, TextureFormat.RGBA32, request =>
        {
            if (request.hasError)
            {
                failed = true;
                complete = true;
                return;
            }

            photo = new Texture2D(profile.outputWidth, profile.outputHeight,
                TextureFormat.RGBA32, false, false)
            {
                name = "Vintage DSLR Exposure",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            photo.LoadRawTextureData(request.GetData<byte>());
            photo.Apply(false, false);
            complete = true;
        });

        while (!complete) yield return null;
        captureStage = "Encoding";
        ReleaseTarget(output);

        if (failed || photo == null)
        {
            capturing = false;
            captureStage = "ReadbackFailed";
            if (mode == Mode.Viewfinder) ShowNotice("Fotoğraf kaydedilemedi");
            yield break;
        }

        yield return null;

        string path = library.NewJpegPath();
        try
        {
            byte[] jpeg = ImageConversion.EncodeToJPG(photo, profile.jpegQuality);
            File.WriteAllBytes(path, jpeg);
            WriteMetadata(path, sceneLuminance, exposure);
            library.Register(path);
        }
        catch (Exception exception)
        {
            Destroy(photo);
            capturing = false;
            captureStage = "SaveFailed";
            ShowNotice("Fotoğraf kaydedilemedi");
            Debug.LogException(exception, this);
            yield break;
        }

        capturing = false;
        captureStage = "Saved";
        displayedPhoto = photo;
        reviewUntil = Time.unscaledTime + profile.reviewSeconds;
        SetMode(Mode.Review);
        ShowNotice("Fotoğraf kaydedildi");
    }

    void ConfigureCaptureCamera(RenderTexture target)
    {
        captureCamera.CopyFrom(viewCamera);
        captureCamera.transform.SetPositionAndRotation(
            viewCamera.transform.position, viewCamera.transform.rotation);
        captureCamera.targetTexture = target;
        captureCamera.aspect = profile.captureWidth / (float)profile.captureHeight;

        float halfFov = viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
        captureCamera.fieldOfView = 2f * Mathf.Atan(
            Mathf.Tan(halfFov) * FitRect(3f / 2f, 0.82f).height
            / Screen.height) * Mathf.Rad2Deg;
        captureCamera.depth = viewCamera.depth + 1f;
        captureCamera.allowHDR = true;
        captureCamera.allowMSAA = false;

        UniversalAdditionalCameraData data = captureCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.dithering = false;
        data.renderShadows = true;
        data.stopNaN = true;
        data.requiresDepthOption = CameraOverrideOption.On;
        data.requiresColorOption = CameraOverrideOption.On;
    }

    void OnCaptureCameraRendered(ScriptableRenderContext context, Camera renderedCamera)
    {
        if (renderedCamera == captureCamera)
            captureCameraRendered = true;
    }

    void OnViewCameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera != viewCamera || preview == null || mode != Mode.Viewfinder) return;
        if (capturing && preview.Ready) return;
        preview.Render(exposureCompensation, zoomFocus?.Amount ?? 0f);
    }

    static RenderTexture CreateTarget(int width, int height, GraphicsFormat format,
                                      int depthBits, string name)
    {
        var descriptor = new RenderTextureDescriptor(width, height)
        {
            graphicsFormat = format,
            depthBufferBits = depthBits,
            msaaSamples = 1,
            sRGB = GraphicsFormatUtility.IsSRGBFormat(format),
            useMipMap = false,
            autoGenerateMips = false
        };
        var texture = new RenderTexture(descriptor)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();
        return texture;
    }

    static void ReleaseTarget(RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        Destroy(texture);
    }

    void WriteMetadata(string jpegPath, float sceneLuminance, float exposure)
    {
        float shutter = Mathf.Clamp(profile.referenceShutterSeconds * exposure,
            1f / 4000f, 30f);
        var metadata = new PhotoMetadata
        {
            fileName = Path.GetFileName(jpegPath),
            capturedLocal = DateTime.Now.ToString("O"),
            iso = profile.iso,
            aperture = profile.aperture,
            shutterSeconds = shutter,
            exposureCompensation = exposureCompensation,
            meteredSceneLuminance = sceneLuminance,
            metering = profile.metering.ToString(),
            whiteBalance = profile.whiteBalance.ToString(),
            width = profile.outputWidth,
            height = profile.outputHeight
        };
        File.WriteAllText(Path.ChangeExtension(jpegPath, ".json"),
            JsonUtility.ToJson(metadata, true));
    }

    void OpenGallery()
    {
        library.Refresh();
        if (library.Count == 0)
        {
            ShowNotice("Henüz fotoğraf yok");
            return;
        }

        galleryIndex = library.Count - 1;
        SetMode(Mode.Gallery);
        LoadGalleryPhoto();
    }

    void CloseGallery()
    {
        DestroyDisplayedPhoto();
        SetMode(galleryReturnMode == Mode.Hidden ? Mode.Equipped : galleryReturnMode);
    }

    void StepGallery(int step)
    {
        if (library.Count == 0) return;
        galleryIndex = (galleryIndex + step + library.Count) % library.Count;
        LoadGalleryPhoto();
    }

    void LoadGalleryPhoto()
    {
        DestroyDisplayedPhoto();
        displayedPhoto = library.Load(galleryIndex);
        if (displayedPhoto == null) ShowNotice("Fotoğraf okunamadı");
    }

    void DestroyDisplayedPhoto()
    {
        if (displayedPhoto == null) return;
        Destroy(displayedPhoto);
        displayedPhoto = null;
    }

    void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    void OnGUI()
    {
        if (mode == Mode.Hidden || profile == null) return;
        GUI.depth = -100;
        EnsureStyles();

        if (mode == Mode.Viewfinder) DrawViewfinder();
        else if (mode == Mode.Review) DrawPhoto("ÖN İZLEME  ·  2 sn");
        else if (mode == Mode.Gallery) DrawGallery();
        else GUI.Label(new Rect(0f, Screen.height - 55f, Screen.width, 40f),
            "KAMERA  ·  Sağ tık: vizör  ·  G: fotoğraflar  ·  4: kaldır", centerStyle);

        if (!string.IsNullOrEmpty(notice))
            GUI.Label(new Rect(0f, 24f, Screen.width, 40f), notice, centerStyle);
    }

    void DrawViewfinder()
    {
        Rect frame = FitRect(3f / 2f, 0.82f);
        DrawOutsideMask(frame, 1f);
        GUI.DrawTexture(frame, preview != null && preview.Ready ? preview.Image : Texture2D.blackTexture,
            ScaleMode.StretchToFill, false);
        Color etched = new Color(0.91f, 0.86f, 0.72f, 0.48f);
        DrawOutline(frame, new Color(0.19f, 0.17f, 0.13f), 2f);

        // A quiet etched focusing screen: concentric arcs and a split-image centre.
        float radius = Mathf.Clamp(frame.height * 0.055f, 20f, 42f);
        DrawFocusRing(frame.center, radius, etched);
        DrawFocusRing(frame.center, radius * 0.82f,
            new Color(0.91f, 0.86f, 0.72f, 0.2f));
        DrawLine(frame.center + Vector2.left * radius * 0.72f,
            frame.center + Vector2.right * radius * 0.72f, etched);

        float footerHeight = Screen.height - frame.yMax;
        float meterY = frame.yMax + footerHeight * 0.12f;
        hudStyle.fontSize = Mathf.Max(9, Mathf.FloorToInt(Mathf.Min(18f, footerHeight * 0.23f, frame.width / 44f)));
        smallStyle.fontSize = Mathf.Max(8, Mathf.FloorToInt(Mathf.Min(14f, footerHeight * 0.19f, Screen.width / 65f)));
        float tickSpacing = Mathf.Min(18f, frame.width / 30f);
        for (int i = -9; i <= 9; i++)
        {
            float x = frame.center.x + i * tickSpacing;
            DrawLine(new Vector2(x, meterY), new Vector2(x,
                meterY + (i % 3 == 0 ? 9f : 4f)), etched);
        }
        float needleX = frame.center.x + exposureCompensation * 3f * tickSpacing;
        DrawLine(new Vector2(needleX, meterY - 6f), new Vector2(needleX, meterY + 10f),
            new Color(0.94f, 0.66f, 0.29f));

        float shutter = Mathf.Clamp(profile.referenceShutterSeconds * (preview?.Exposure ?? 1f), 1f / 4000f, 30f);
        int remaining = Mathf.Max(0, profile.cardCapacity - library.Count);
        string ev = exposureCompensation >= 0f
            ? $"+{exposureCompensation:0.0}" : exposureCompensation.ToString("0.0");
        string shutterText = shutter < 1f ? $"1/{Mathf.RoundToInt(1f / shutter)}" : $"{shutter:0.0} sn";
        string status = capturing ? "KAYDEDİLİYOR…" :
            $"Av   f/{profile.aperture:0.#}   {shutterText}   "
            + $"ISO {profile.iso}   EV {ev}   [{remaining}]";
        GUI.Label(new Rect(frame.x, frame.yMax + footerHeight * 0.30f, frame.width, footerHeight * 0.30f),
            status + $"   {zoom:0.0}×", hudStyle);
        GUI.Label(new Rect(4f, frame.yMax + footerHeight * 0.65f, Screen.width - 8f, footerHeight * 0.30f),
            "Sol tık: çek  ·  Tekerlek: zoom  ·  Q/E: poz telafisi  ·  G: fotoğraflar  ·  Sağ tık: çık",
            smallStyle);
    }

    void DrawGallery()
    {
        DrawPhoto($"FOTOĞRAFLAR  ·  {galleryIndex + 1}/{library.Count}");
        if (library.Count > 0)
        {
            string name = Path.GetFileName(library.Files[galleryIndex]);
            GUI.Label(new Rect(0f, Screen.height - 82f, Screen.width, 28f), name, smallStyle);
        }
        GUI.Label(new Rect(0f, Screen.height - 52f, Screen.width, 34f),
            "A/D veya ←/→: gezin  ·  G/Sağ tık: kapat", centerStyle);
    }

    void DrawPhoto(string heading)
    {
        Color previous = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (displayedPhoto != null)
            GUI.DrawTexture(FitRect(3f / 2f, 0.88f), displayedPhoto, ScaleMode.ScaleToFit, false);
        GUI.Label(new Rect(0f, 15f, Screen.width, 36f), heading, galleryStyle);
        GUI.color = previous;
    }

    static Rect FitRect(float aspect, float heightShare)
    {
        float height = Screen.height * heightShare;
        float width = height * aspect;
        if (width > Screen.width * 0.96f)
        {
            width = Screen.width * 0.96f;
            height = width / aspect;
        }
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f,
            width, height);
    }

    static void DrawOutsideMask(Rect frame, float alpha)
    {
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, frame.y), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, frame.yMax, Screen.width, Screen.height - frame.yMax),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, frame.y, frame.x, frame.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(frame.xMax, frame.y, Screen.width - frame.xMax, frame.height),
            Texture2D.whiteTexture);
        GUI.color = previous;
    }

    static void DrawFocusRing(Vector2 center, float radius, Color color)
    {
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
        }
    }

    static void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        Matrix4x4 matrix = GUI.matrix;
        Color previous = GUI.color;
        GUI.color = color;
        Vector2 delta = to - from;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y, delta.magnitude, 1f), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = previous;
    }

    static void DrawOutline(Rect rect, Color color, float thickness)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
            Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void EnsureStyles()
    {
        hudStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.89f, 0.68f, 0.36f) }
        };
        smallStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = { textColor = new Color(0.82f, 0.82f, 0.78f) }
        };
        centerStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        galleryStyle ??= new GUIStyle(centerStyle) { fontSize = 20 };
    }
}
