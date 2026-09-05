using UnityEngine.InputSystem;

public enum HeldItemPointerButton
{
    Left,
    Middle,
    Right
}

/// A device input and its UI representation. The same value is used for dispatch and for the
/// Sapli Kart prompt, preventing displayed controls from drifting away from working controls.
public readonly struct HeldItemInput
{
    public bool IsKey { get; }
    public Key Key { get; }
    public string KeyLabel { get; }
    public HeldItemPointerButton PointerButton { get; }
    public ThinTripleIconId PointerIcon { get; }

    HeldItemInput(Key key, string keyLabel)
    {
        IsKey = true;
        Key = key;
        KeyLabel = keyLabel;
        PointerButton = default;
        PointerIcon = default;
    }

    HeldItemInput(HeldItemPointerButton button, ThinTripleIconId icon)
    {
        IsKey = false;
        Key = Key.None;
        KeyLabel = string.Empty;
        PointerButton = button;
        PointerIcon = icon;
    }

    public static HeldItemInput ForKey(Key key, string label) => new(key, label);

    public static HeldItemInput ForPointer(HeldItemPointerButton button,
                                           ThinTripleIconId icon) => new(button, icon);

    public bool WasPressed(Keyboard keyboard, Mouse mouse)
    {
        if (IsKey) return keyboard != null && keyboard[Key].wasPressedThisFrame;
        if (mouse == null) return false;

        return PointerButton switch
        {
            HeldItemPointerButton.Left => mouse.leftButton.wasPressedThisFrame,
            HeldItemPointerButton.Middle => mouse.middleButton.wasPressedThisFrame,
            HeldItemPointerButton.Right => mouse.rightButton.wasPressedThisFrame,
            _ => false
        };
    }
}

public readonly struct HeldItemAction
{
    public string Id { get; }
    public string Label { get; }
    public HeldItemInput Input { get; }

    public HeldItemAction(string id, string label, HeldItemInput input)
    {
        Id = id;
        Label = label;
        Input = input;
    }
}
