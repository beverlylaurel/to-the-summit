using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Contract between an item and the shared held-item system. Item-specific modes remain inside
/// the item; equipment ownership, switching and the lowered-item actions are shared.
public abstract class EquippableItem : MonoBehaviour
{
    public abstract string ItemId { get; }
    public abstract string DisplayName { get; }
    public abstract string StatusText { get; }
    public abstract ThinTripleIconId DisplayIcon { get; }
    public abstract Key EquipKey { get; }
    public abstract string EquipKeyLabel { get; }
    public abstract bool IsEquipped { get; }
    public abstract bool ShowHeldCard { get; }
    public abstract IReadOnlyList<HeldItemAction> SharedActions { get; }

    public abstract bool CanUnequip(out string reason);
    public abstract void SetEquipped(bool equipped);
    public abstract void ExecuteSharedAction(string actionId);
}
