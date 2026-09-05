using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Owns the single active hand-held item and routes its lowered-state interactions.
/// Adding another item requires registration through Bind, not another player input loop.
[DisallowMultipleComponent]
public sealed class HeldItemSystem : MonoBehaviour
{
    [SerializeField] EquippableItem[] items = Array.Empty<EquippableItem>();
    [SerializeField] Font regularFont;
    [SerializeField] Font mediumFont;
    [SerializeField] Font semiboldFont;
    [SerializeField] ThinTripleIconSet iconSet;

    readonly List<HeldItemAction> displayedActions = new(6);
    EquippableItem activeItem;
    EquippableItem presentedItem;
    HeldItemHud hud;
    string notice;
    float noticeUntil;
    bool initialized;

    public EquippableItem ActiveItem => activeItem;

    public void Bind(EquippableItem[] registeredItems, Font uiRegularFont,
                     Font uiMediumFont, Font uiSemiboldFont, ThinTripleIconSet uiIconSet)
    {
        items = registeredItems ?? Array.Empty<EquippableItem>();
        regularFont = uiRegularFont;
        mediumFont = uiMediumFont;
        semiboldFont = uiSemiboldFont;
        iconSet = uiIconSet;
        hud = null;
        ValidateDependencies();

        if (Application.isPlaying) Initialize();
    }

    void Start() => Initialize();

    void Initialize()
    {
        ValidateDependencies();
        if (initialized && activeItem == null) return;

        for (int i = 0; i < items.Length; i++)
            items[i].SetEquipped(false);
        activeItem = null;
        presentedItem = null;
        initialized = true;
    }

    void OnDisable()
    {
        if (activeItem != null) activeItem.SetEquipped(false);
        activeItem = null;
        presentedItem = null;
    }

    void Update()
    {
        if (!initialized || Cursor.lockState != CursorLockMode.Locked) return;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null) return;

        for (int i = 0; i < items.Length; i++)
        {
            EquippableItem item = items[i];
            if (keyboard[item.EquipKey].wasPressedThisFrame)
            {
                Toggle(item);
                return;
            }
        }

        if (activeItem == null || !activeItem.ShowHeldCard) return;
        IReadOnlyList<HeldItemAction> actions = activeItem.SharedActions;
        for (int i = 0; i < actions.Count; i++)
        {
            HeldItemAction action = actions[i];
            if (!action.Input.WasPressed(keyboard, mouse)) continue;
            activeItem.ExecuteSharedAction(action.Id);
            return;
        }
    }

    void Toggle(EquippableItem requested)
    {
        if (requested == null) return;
        if (activeItem == requested)
        {
            TryUnequipActive();
            return;
        }

        if (activeItem != null && !TryUnequipActive()) return;
        activeItem = requested;
        presentedItem = requested;
        activeItem.SetEquipped(true);
    }

    bool TryUnequipActive()
    {
        if (activeItem == null) return true;
        if (!activeItem.CanUnequip(out string reason))
        {
            ShowNotice(reason);
            return false;
        }

        activeItem.SetEquipped(false);
        activeItem = null;
        return true;
    }

    void OnGUI()
    {
        if (!initialized) return;
        GUI.depth = -90;
        hud ??= new HeldItemHud(regularFont, mediumFont, semiboldFont, iconSet);
        bool visible = activeItem != null && activeItem.ShowHeldCard;
        hud.SetVisible(visible);

        if (activeItem != null) presentedItem = activeItem;
        if (presentedItem != null && (visible || hud.IsVisible))
        {
            displayedActions.Clear();
            IReadOnlyList<HeldItemAction> itemActions = presentedItem.SharedActions;
            for (int i = 0; i < itemActions.Count; i++) displayedActions.Add(itemActions[i]);
            displayedActions.Add(new HeldItemAction("unequip", "KALDIR",
                HeldItemInput.ForKey(presentedItem.EquipKey, presentedItem.EquipKeyLabel)));
            hud.Draw(presentedItem, displayedActions);
        }
        else if (activeItem == null)
            presentedItem = null;

        if (noticeUntil > 0f && Time.unscaledTime >= noticeUntil) notice = string.Empty;
        hud.DrawNotice(notice);
    }

    void ShowNotice(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        notice = message;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    void ValidateDependencies()
    {
        if (regularFont == null || mediumFont == null || semiboldFont == null || iconSet == null)
            throw new InvalidOperationException($"{nameof(HeldItemSystem)}: UI dependencies are not assigned.");

        var keys = new HashSet<Key>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            EquippableItem item = items[i];
            if (item == null)
                throw new InvalidOperationException($"{nameof(HeldItemSystem)}: registered item {i} is null.");
            if (item.EquipKey == Key.None || !keys.Add(item.EquipKey))
                throw new InvalidOperationException($"{nameof(HeldItemSystem)}: equip keys must be unique and assigned.");
            if (string.IsNullOrWhiteSpace(item.ItemId) || !ids.Add(item.ItemId))
                throw new InvalidOperationException($"{nameof(HeldItemSystem)}: item ids must be unique and assigned.");
        }
    }

#if UNITY_EDITOR
    public void EditorToggleForTest(EquippableItem item)
    {
        initialized = true;
        Toggle(item);
    }
#endif
}
