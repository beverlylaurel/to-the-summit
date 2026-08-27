using UnityEngine;
using UnityEngine.InputSystem;

/// The single owner of the cursor lock. No other system writes to Cursor.lockState;
/// systems that open a UI call Release/Restore.
public class CursorLock : MonoBehaviour
{
    int holders;

    void Start() => Apply(true);

    /// A UI wants the cursor. Because several panels can be open at once it is kept with a
    /// counter: the lock does not come back until the last one closes.
    public void Release() => SetHolders(holders + 1);

    public void Restore() => SetHolders(holders - 1);

    void SetHolders(int value)
    {
        holders = Mathf.Max(0, value);
        Apply(holders == 0);
    }

    static void Apply(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
