using UnityEngine;

/// Kare hızını sabitler. VSync açıkken targetFrameRate yok sayıldığı için VSync kapatılır.
public class FrameRateCap : MonoBehaviour
{
    [SerializeField] int targetFrameRate = 244;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
    }
}
