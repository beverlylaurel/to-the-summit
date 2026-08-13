using UnityEngine;
using UnityEngine.InputSystem;

/// İmleç kilidinin tek sahibi. Başka hiçbir sistem Cursor.lockState'e yazmaz;
/// arayüz açan sistemler Release/Restore çağırır.
public class CursorLock : MonoBehaviour
{
    int holders;

    void Start() => Apply(true);

    /// Bir arayüz imleci istiyor. Birden fazla panel aynı anda açılabildiği için
    /// sayaçla tutulur: sonuncusu kapanana kadar kilit geri gelmez.
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
