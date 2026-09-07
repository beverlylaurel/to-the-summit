using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

/// Moving-capsule regressions, independent of keyboard input and weather randomness.
public static class IndoorTransitionTest
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);

    public static string Run(out bool ok)
    {
        var report = new StringBuilder("# Indoor Transition Test\n");
        ok = true;
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject player = new GameObject("ZZ_TransitionPlayer");
        GameObject managerObject = new GameObject("ZZ_DisabledSnowSource");
        managerObject.SetActive(false);
        Light previousSun = RenderSettings.sun;
        GameObject sunObject = new GameObject("ZZ_TestSun");
        GameObject flashObject = new GameObject("ZZ_TestFlash");
        flashObject.SetActive(false);
        try
        {
            ground.transform.position = new Vector3(200f, -0.5f, 200f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var properties = ground.AddComponent<GroundSurfaceProperties>();
            Set(properties, "acceptsSnow", true);
            floor.transform.position = new Vector3(204f, 0.06f, 200f);
            floor.transform.localScale = new Vector3(4f, 0.12f, 5f);
            player.transform.position = new Vector3(200f, 0.1f, 200f);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.center = Vector3.up * 0.9f;
            body.radius = 0.3f;
            body.skinWidth = 0.02f;
            var offset = player.AddComponent<SnowGroundOffset>();
            Call(offset, "Awake");
            var contact = player.GetComponent<GroundSurfaceContact>();
            Call(contact, "OnEnable");
            var manager = managerObject.AddComponent<SnowManager>();
            Set(manager, "<WorldSnowDepth>k__BackingField", 0.35f);
            Set(offset, "snowManager", manager);
            var rhythm = player.AddComponent<SnowStepRhythm>();
            Set(rhythm, "body", body);
            Call(rhythm, "OnEnable");
            var footObject = new GameObject("ZZ_TestFeet");
            footObject.transform.SetParent(player.transform, false);
            var feet = footObject.AddComponent<SnowFootprintDeformer>();
            Physics.SyncTransforms();

            // Settle on snow, then traverse a raised physical floor and walk back.
            for (int i = 0; i < 120; i++) Tick(body, offset, contact, rhythm, 0f, -2f);
            float snowY = player.transform.position.y;
            Check(report, ref ok, Mathf.Abs(snowY - SnowSurfaceHeight.ReliefWorld(player.transform.position, 0.35f, 0f, Vector2.zero)) < 0.025f && offset.IsSupporting,
                "standing snow support", snowY);
            float maxChange = 0f;
            float oldY = snowY;
            for (int i = 0; i < 180; i++)
            {
                Tick(body, offset, contact, rhythm, 1.5f, -2f);
                maxChange = Mathf.Max(maxChange, Mathf.Abs(player.transform.position.y - oldY));
                oldY = player.transform.position.y;
            }
            for (int i = 0; i < 60; i++) Tick(body, offset, contact, rhythm, 0f, -2f);
            Check(report, ref ok, !contact.SupportsSnow && !offset.IsSupporting &&
                Mathf.Abs(player.transform.position.y - 0.12f) < 0.05f,
                "snow to floor settles at physical height", player.transform.position.y);
            for (int i = 0; i < 180; i++) Tick(body, offset, contact, rhythm, -1.5f, -2f);
            for (int i = 0; i < 120; i++) Tick(body, offset, contact, rhythm, 0f, -2f);
            Check(report, ref ok, offset.IsSupporting && Mathf.Abs(player.transform.position.y - snowY) < 0.02f,
                "floor to snow returns without accumulated lift", player.transform.position.y);
            Check(report, ref ok, maxChange < 0.15f, "doorway per-frame height change", maxChange);

            // Actual capsule trajectory: ascent, apex, descent and landing.
            float vertical = 3f;
            bool silentInAir = true;
            int airFrames = 0;
            for (int i = 0; i < 90; i++)
            {
                int before = rhythm.StepCount;
                vertical -= 9.81f / 60f;
                Tick(body, offset, contact, rhythm, -2.2f, vertical);
                Call(feet, "LateUpdate");
                if (!contact.IsGrounded)
                {
                    airFrames++;
                    silentInAir &= rhythm.StepCount == before;
                    for (int segment = 0; segment < feet.SegmentCount; segment++)
                    {
                        feet.GetSegment(segment, out _, out Vector4 pressure);
                        silentInAir &= pressure.w == 0f;
                    }
                }
                else if (vertical < 0f) vertical = -2f;
            }
            Check(report, ref ok, airFrames > 15 && silentInAir, "no footsteps or footprint pressure during jump", airFrames);
            Check(report, ref ok, contact.IsGrounded, "landing restores contact", player.transform.position.y);

            // Rendering state: preserve world radiance, nest cameras, restore on exit.
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 2f;
            sun.color = new Color(1f, 0.8f, 0.6f);
            Color original = sun.color;
            RenderSettings.sun = sun;
            var light = flashObject.AddComponent<Light>();
            var flash = flashObject.AddComponent<LightningFlash>();
            light.intensity = 4f;
            light.color = Color.white;
            Set(flash, "flash", light);
            Call(flash, "BeginLighting");
            Check(report, ref ok, Mathf.Abs(sun.intensity - 6f) < 0.001f && light.intensity == 0f &&
                sun.shadows == LightShadows.Soft, "flash uses shadowed light with full outdoor energy", sun.intensity);
            Call(flash, "BeginLighting");
            Call(flash, "EndLighting");
            Check(report, ref ok, sun.intensity == 6f, "nested camera does not double flash", sun.intensity);
            Call(flash, "EndLighting");
            Check(report, ref ok, sun.intensity == 2f && sun.color == original && light.intensity == 4f,
                "render exit restores daylight and flash", sun.intensity);
        }
        finally
        {
            RenderSettings.sun = previousSun;
            Object.DestroyImmediate(flashObject);
            Object.DestroyImmediate(sunObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(managerObject);
        }
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static void Tick(CharacterController body, SnowGroundOffset offset,
        GroundSurfaceContact contact, SnowStepRhythm rhythm, float horizontal, float vertical)
    {
        body.Move(new Vector3(horizontal, vertical, 0f) / 60f);
        offset.Resolve(vertical > 0f);
        contact.RefreshNow();
        Call(rhythm, "TickMotion", Mathf.Abs(horizontal), 1f / 60f, contact.IsGrounded);
    }

    static void Check(StringBuilder report, ref bool ok, bool passed, string label, float value)
    {
        ok &= passed;
        report.AppendLine($"  [{(passed ? "+" : "-")}] {label}: {value:F4}");
    }
}
