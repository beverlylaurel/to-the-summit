using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Object = UnityEngine.Object;

/// Renders actual URP shadow receivers with post-processing disabled.
public static class LightningOcclusionTest
{
    public static void RunBatch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("ZZ_LightningRenderTest");
        var lightObject = new GameObject("ZZ_Flash");
        lightObject.SetActive(false);
        Light previousSun = RenderSettings.sun;
        bool previousFog = RenderSettings.fog;
        AmbientMode previousAmbient = RenderSettings.ambientMode;
        Color previousAmbientColor = RenderSettings.ambientLight;
        float previousReflection = RenderSettings.reflectionIntensity;
        RenderTexture previousActive = RenderTexture.active;
        var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBFloat);
        var pixels = new Texture2D(512, 512, TextureFormat.RGBAFloat, false, true);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
        var pipeline = Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
        var pipelineData = new SerializedObject(pipeline);
        var rendererSlot = pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0);
        var rendererData = Object.Instantiate((ScriptableRendererData)rendererSlot.objectReferenceValue);
        // Isolate the lighting measurement from world-specific clouds, fog and exposure.
        rendererData.rendererFeatures.Clear();
        rendererSlot.objectReferenceValue = rendererData;
        pipelineData.ApplyModifiedPropertiesWithoutUndo();
        pipeline.shadowDistance = 30f;
        pipeline.shadowCascadeCount = 1;
        pipeline.mainLightShadowmapResolution = 2048;
        Action<ScriptableRenderContext, Camera> begin = null, end = null;
        bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            QualitySettings.renderPipeline = pipeline;
            material.SetColor("_BaseColor", Color.gray);
            material.SetFloat("_Smoothness", 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(18f, 0.2f, 18f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(root.transform);
            roof.transform.position = new Vector3(-2.5f, 2f, 0f);
            roof.transform.localScale = new Vector3(4f, 0.2f, 4f);
            roof.GetComponent<Renderer>().sharedMaterial = material;
            var sunObject = new GameObject("ZZ_Primary");
            sunObject.transform.SetParent(root.transform);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.02f;
            sun.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            sun.intensity = 1f;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;
            var cameraObject = new GameObject("ZZ_TestCamera");
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0f, 1.2f, -7f);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.farClipPlane = 30f;
            camera.targetTexture = target;
            var flashLight = lightObject.AddComponent<Light>();
            flashLight.type = LightType.Directional;
            var flash = lightObject.AddComponent<LightningFlash>();
            typeof(LightningFlash).GetField("flash", flags).SetValue(flash, flashLight);
            begin = (Action<ScriptableRenderContext, Camera>)Delegate.CreateDelegate(
                typeof(Action<ScriptableRenderContext, Camera>), flash,
                typeof(LightningFlash).GetMethod("BeginCameraLighting", flags));
            end = (Action<ScriptableRenderContext, Camera>)Delegate.CreateDelegate(
                typeof(Action<ScriptableRenderContext, Camera>), flash,
                typeof(LightningFlash).GetMethod("EndCameraLighting", flags));
            RenderPipelineManager.beginCameraRendering += begin;
            RenderPipelineManager.endCameraRendering += end;
            target.Create();
            flashLight.intensity = 0f;
            Capture(camera, target, pixels);
            float outsideBase = Sample(camera, pixels, new Vector3(2.5f, 0f, 0f));
            flashLight.intensity = 4f;
            Capture(camera, target, pixels);
            float outside = Sample(camera, pixels, new Vector3(2.5f, 0f, 0f));
            float inside = Sample(camera, pixels, new Vector3(-2.5f, 0f, 0f));
            File.WriteAllBytes("Logs/lightning-occlusion.png", pixels.EncodeToPNG());
            bool passed = outsideBase > 0.001f && outside > outsideBase * 3f && inside < outside * 0.15f;
            Debug.Log($"Lightning occlusion pixels: baseline={outsideBase:F5}, outside={outside:F5}, inside={inside:F5}; RESULT: {(passed ? "PASSED" : "FAILED")}");
            if (!passed) throw new InvalidOperationException("Lightning shadow pixel regression failed.");
            material.shader = Shader.Find("Cabin/WeatheredLit");
            material.SetColor("_BaseColor", Color.gray);
            foreach (float daylight in new[] { 1f, 0.002f })
            {
                sun.intensity = daylight;
                flashLight.intensity = 0f;
                Capture(camera, target, pixels);
                Capture(camera, target, pixels);
                outsideBase = Sample(camera, pixels, new Vector3(2.5f, 0f, 0f));
                flashLight.intensity = 4f;
                Capture(camera, target, pixels);
                outside = Sample(camera, pixels, new Vector3(2.5f, 0f, 0f));
                inside = Sample(camera, pixels, new Vector3(-2.5f, 0f, 0f));
                float expectedRatio = (daylight + 4f) / daylight;
                passed = outsideBase > daylight * 0.01f &&
                    Mathf.Abs(outside / outsideBase / expectedRatio - 1f) < 0.15f &&
                    inside < outside * 0.15f;
                Debug.Log($"Cabin lightning (main={daylight}): baseline={outsideBase:F5}, outside={outside:F5}, inside={inside:F5}; RESULT: {(passed ? "PASSED" : "FAILED")}");
                if (!passed) throw new InvalidOperationException("Cabin lightning shadow regression failed.");
            }
        }
        finally
        {
            if (begin != null) RenderPipelineManager.beginCameraRendering -= begin;
            if (end != null) RenderPipelineManager.endCameraRendering -= end;
            RenderSettings.sun = previousSun;
            RenderSettings.fog = previousFog;
            RenderSettings.ambientMode = previousAmbient;
            RenderSettings.ambientLight = previousAmbientColor;
            RenderSettings.reflectionIntensity = previousReflection;
            QualitySettings.renderPipeline = previousPipeline;
            ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pixels);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pipeline);
            Object.DestroyImmediate(rendererData);
        }
    }

    static void Capture(Camera camera, RenderTexture target, Texture2D pixels)
    {
        camera.Render();
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
        pixels.Apply();
    }

    static float Sample(Camera camera, Texture2D pixels, Vector3 point)
    {
        Vector3 pixel = camera.WorldToScreenPoint(point);
        Color color = pixels.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }
}
