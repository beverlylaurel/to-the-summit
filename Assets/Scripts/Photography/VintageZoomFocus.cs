using UnityEngine;

/// Transient focus error driven by relative lens speed, independent of exposure metering.
internal sealed class VintageZoomFocus
{
    readonly VintageDslrProfile settings;
    internal float Amount { get; private set; }

    internal VintageZoomFocus(VintageDslrProfile settings) => this.settings = settings;

    internal void Tick(float relativeZoomSpeed, float deltaTime)
    {
        float target = Mathf.Clamp01(relativeZoomSpeed / settings.zoomDefocusSpeed)
            * settings.zoomDefocusStrength;
        float seconds = target > Amount ? 0.025f : settings.zoomFocusRecoverySeconds;
        Amount = Mathf.MoveTowards(Amount, target, deltaTime / Mathf.Max(0.01f, seconds));
    }

    internal void Reset() => Amount = 0f;
}
