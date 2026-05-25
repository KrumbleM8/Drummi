using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a punch-scale bounce animation on this GameObject when its assigned
/// drum-pad input fires. Attach to each of the three circular pad visuals
/// and set <see cref="side"/> to match the pad's role.
///
/// No external tween library required — uses a plain coroutine.
/// Uses <see cref="Time.unscaledDeltaTime"/> so the bounce plays correctly
/// even if the game clock is paused.
/// </summary>
public class PadBounceFeedback : MonoBehaviour
{
    public enum PadSide { Left, Center, Right }

    [SerializeField] private DrumPadTouch drumPadTouch;
    [SerializeField] private PadSide side;

    [Header("Bounce Settings")]
    [SerializeField] private float scaleMultiplier = 1.18f;
    [SerializeField] private float timeScaleUp     = 0.10f;
    [SerializeField] private float timeScaleDown   = 0.15f;

    private Vector3    _baseScale;
    private Coroutine  _routine;

    private void Awake() => _baseScale = transform.localScale;

    private void OnEnable()
    {
        if (drumPadTouch == null) { Debug.LogWarning($"[PadBounceFeedback] DrumPadTouch not assigned on {name}."); return; }
        switch (side)
        {
            case PadSide.Left:   drumPadTouch.OnLeftHit   += Bounce; break;
            case PadSide.Center: drumPadTouch.OnCenterHit += Bounce; break;
            case PadSide.Right:  drumPadTouch.OnRightHit  += Bounce; break;
        }
    }

    private void OnDisable()
    {
        if (drumPadTouch == null) return;
        switch (side)
        {
            case PadSide.Left:   drumPadTouch.OnLeftHit   -= Bounce; break;
            case PadSide.Center: drumPadTouch.OnCenterHit -= Bounce; break;
            case PadSide.Right:  drumPadTouch.OnRightHit  -= Bounce; break;
        }
    }

    /// <summary>Triggers the bounce, restarting cleanly if already in progress.</summary>
    private void Bounce()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        Vector3 peak = _baseScale * scaleMultiplier;

        // Scale up to peak
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / timeScaleUp;
            transform.localScale = Vector3.LerpUnclamped(_baseScale, peak, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        transform.localScale = peak;

        // Ease back to base
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / timeScaleDown;
            transform.localScale = Vector3.LerpUnclamped(peak, _baseScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        transform.localScale = _baseScale;
        _routine = null;
    }
}
