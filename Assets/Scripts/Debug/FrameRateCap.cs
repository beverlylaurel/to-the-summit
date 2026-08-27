using UnityEngine;

/// Pins the frame rate. VSync is turned off because targetFrameRate is ignored while it is on.
public class FrameRateCap : MonoBehaviour
{
    [SerializeField] int targetFrameRate = 244;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
    }
}
