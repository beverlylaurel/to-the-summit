using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PlayerMotionTest
{
    const string SettingsPath = "Assets/Settings/PlayerViewMotionSettings.asset";
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("To The Summit/Player/Motion Test", false, 60)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        report.AppendLine("# Player Motion Test");

        PlayerViewMotionSettings settings =
            AssetDatabase.LoadAssetAtPath<PlayerViewMotionSettings>(SettingsPath);
        bool restrained = settings != null
                       && settings.sprintVertical <= 0.02f
                       && settings.walkVertical <= 0.012f
                       && settings.turnLateral <= 0.005f
                       && settings.turnRollDegrees + settings.sprintRollDegrees <= 0.4f;

        MethodInfo stepHorizontal = typeof(FirstPersonController).GetMethod(
            "StepHorizontal", BindingFlags.Static | BindingFlags.NonPublic);
        Vector3 firstFrame = Step(stepHorizontal, Vector3.zero, Vector3.forward * 4f, 11f, 1f / 60f);
        Vector3 stopped = Step(stepHorizontal, Vector3.forward * 4f, Vector3.zero, 15f, 0.1f);
        Vector3 sixty = Vector3.zero;
        Vector3 thirty = Vector3.zero;
        for (int i = 0; i < 60; i++)
            sixty = Step(stepHorizontal, sixty, Vector3.forward * 4f, 11f, 1f / 60f);
        for (int i = 0; i < 30; i++)
            thirty = Step(stepHorizontal, thirty, Vector3.forward * 4f, 11f, 1f / 30f);
        bool locomotion = firstFrame.z > 0f && firstFrame.z < 4f
                       && stopped.z > 0f && stopped.z < 4f
                       && Vector3.Distance(sixty, thirty) < 0.0001f
                       && Mathf.Approximately(sixty.z, 4f);

        PlayerViewMotion sceneMotion = Object.FindAnyObjectByType<PlayerViewMotion>(
            FindObjectsInactive.Include);
        var serialized = sceneMotion != null ? new SerializedObject(sceneMotion) : null;
        BikeRider rider = Object.FindAnyObjectByType<BikeRider>(FindObjectsInactive.Include);
        var serializedRider = rider != null ? new SerializedObject(rider) : null;
        Camera mainCamera = Camera.main;
        bool sceneBound = serialized != null
                       && serialized.FindProperty("movement").objectReferenceValue != null
                       && serialized.FindProperty("body").objectReferenceValue != null
                       && serialized.FindProperty("look").objectReferenceValue != null
                       && serialized.FindProperty("view").objectReferenceValue != null
                       && serialized.FindProperty("settings").objectReferenceValue == settings
                       && serializedRider != null && mainCamera != null
                       && serializedRider.FindProperty("cameraPivot").objectReferenceValue
                          == mainCamera.transform.parent;

        float maxWalkOffset = float.NaN;
        float maxTurnRoll = float.NaN;
        float landingDip = float.NaN;
        bool response = settings != null && ExerciseViewMotion(settings,
            out maxWalkOffset, out maxTurnRoll, out landingDip);

        report.AppendLine($"  [{Mark(locomotion)}] acceleration/braking are gradual and frame-rate stable");
        report.AppendLine($"  [{Mark(restrained)}] camera limits stay inside the restrained motion budget");
        report.AppendLine($"  [{Mark(response)}] measured walk={maxWalkOffset * 100f:F2} cm, "
                        + $"turn={maxTurnRoll:F2} deg, landing={landingDip * 100f:F2} cm");
        report.AppendLine($"  [{Mark(sceneBound)}] view motion and bike use separate camera transform layers in {ScenePath}");

        ok = locomotion && restrained && response && sceneBound;
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static Vector3 Step(MethodInfo method, Vector3 current, Vector3 target,
                        float rate, float deltaTime) =>
        method == null ? new Vector3(float.NaN, 0f, 0f) :
        (Vector3)method.Invoke(null, new object[] { current, target, rate, deltaTime });

    static bool ExerciseViewMotion(PlayerViewMotionSettings settings,
                                   out float maxWalkOffset, out float maxTurnRoll,
                                   out float landingDip)
    {
        var root = new GameObject("Player Motion Test") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        var body = root.AddComponent<CharacterController>();
        var movement = root.AddComponent<FirstPersonController>();
        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(root.transform, false);
        var view = new GameObject("View").transform;
        view.SetParent(pivot, false);
        var look = root.AddComponent<MouseLook>();
        look.Bind(pivot);
        var motion = root.AddComponent<PlayerViewMotion>();
        motion.Bind(movement, body, look, view, settings);

        MethodInfo step = typeof(PlayerViewMotion).GetMethod(
            "StepMotion", BindingFlags.Instance | BindingFlags.NonPublic);
        if (step == null)
        {
            Object.DestroyImmediate(root);
            maxWalkOffset = maxTurnRoll = landingDip = float.NaN;
            return false;
        }

        maxWalkOffset = 0f;
        for (int i = 0; i < 180; i++)
        {
            step.Invoke(motion, new object[] { 2.2f, true, false, Vector2.zero, -2f, 1f / 60f });
            maxWalkOffset = Mathf.Max(maxWalkOffset, view.localPosition.magnitude);
        }

        for (int i = 0; i < 30; i++)
            step.Invoke(motion, new object[] { 0f, true, false,
                new Vector2(3f, 0f), -2f, 1f / 60f });
        maxTurnRoll = Mathf.Abs(Mathf.DeltaAngle(0f, view.localEulerAngles.z));

        step.Invoke(motion, new object[] { 0f, false, false, Vector2.zero, -6f, 1f / 60f });
        step.Invoke(motion, new object[] { 0f, true, false, Vector2.zero, 0f, 1f / 60f });
        landingDip = Mathf.Max(0f, -view.localPosition.y);

        bool result = maxWalkOffset > 0.004f
                   && maxWalkOffset <= settings.walkVertical + settings.walkLateral + 0.001f
                   && maxTurnRoll > 0.03f && maxTurnRoll <= settings.turnRollDegrees + 0.01f
                   && landingDip > 0.001f && landingDip <= settings.landingDip + 0.001f;
        Object.DestroyImmediate(root);
        return result;
    }

    static string Mark(bool value) => value ? "+" : "-";
}
