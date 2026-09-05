using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class HeldItemSystemTest
{
    const string SystemSource = "Assets/Scripts/Items/HeldItemSystem.cs";
    const string ItemSource = "Assets/Scripts/Items/EquippableItem.cs";
    const string ActionSource = "Assets/Scripts/Items/HeldItemAction.cs";
    const string HudSource = "Assets/Scripts/Items/UI/HeldItemHud.cs";

    [MenuItem("To The Summit/Items/Common Item System Test", false, 62)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder("# Common Held Item System Test\n");
        HeldItemSystem system = Object.FindAnyObjectByType<HeldItemSystem>(FindObjectsInactive.Include);
        VintagePhotoMode camera = Object.FindAnyObjectByType<VintagePhotoMode>(FindObjectsInactive.Include);

        var serialized = system != null ? new SerializedObject(system) : null;
        SerializedProperty registered = serialized?.FindProperty("items");
        bool registration = registered != null && registered.arraySize == 1
                         && registered.GetArrayElementAtIndex(0).objectReferenceValue == camera;

        IReadOnlyList<HeldItemAction> actions = camera?.SharedActions;
        bool actionData = actions != null && actions.Count == 2
                       && actions[0].Id == "viewfinder"
                       && !actions[0].Input.IsKey
                       && actions[0].Input.PointerIcon == ThinTripleIconId.MouseRight
                       && actions[1].Id == "gallery"
                       && actions[1].Input.IsKey
                       && actions[1].Input.Key == Key.G;

        string systemText = File.ReadAllText(SystemSource);
        string itemText = File.ReadAllText(ItemSource);
        string actionText = File.ReadAllText(ActionSource);
        string hudText = File.ReadAllText(HudSource);
        bool architecture = itemText.Contains("abstract class EquippableItem")
                         && actionText.Contains("readonly struct HeldItemAction")
                         && systemText.Contains("activeItem.SharedActions")
                         && systemText.Contains("action.Input.WasPressed")
                         && hudText.Contains("IReadOnlyList<HeldItemAction> actions")
                         && hudText.Contains("action.Input.IsKey");

        bool lifecycle = false;
        if (system != null && camera != null)
        {
            camera.SetEquipped(false);
            system.EditorToggleForTest(camera);
            bool equipped = system.ActiveItem == camera && camera.IsEquipped && camera.ShowHeldCard;
            system.EditorToggleForTest(camera);
            lifecycle = equipped && system.ActiveItem == null && !camera.IsEquipped;
        }

        report.AppendLine($"  [{Mark(registration)}] Game scene registers only the vintage camera");
        report.AppendLine($"  [{Mark(actionData)}] camera exposes viewfinder and gallery as typed actions");
        report.AppendLine($"  [{Mark(architecture)}] dispatch and Saplı Kart consume the same action data");
        report.AppendLine($"  [{Mark(lifecycle)}] one controller owns equip and unequip lifecycle");
        ok = registration && actionData && architecture && lifecycle;
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static string Mark(bool value) => value ? "+" : "-";
}
