using System.Collections;
using UnityEngine;

/// <summary>
/// Applies a brief positional shake to the camera whenever any drum pad is hit.
/// Attach to the same GameObject as the Camera.
/// Assign the scene's DrumPadTouch in the Inspector.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DrumPadTouch drumPadTouch;

    [Header("Shake Settings")]
    [Tooltip("How long the shake lasts in seconds.")]
    [SerializeField] private float duration = 0.15f;

    [Tooltip("Peak displacement in world units at the start of the shake.")]
    [SerializeField] private float magnitude = 0.08f;

    [Tooltip("How quickly the magnitude decays over the shake duration (higher = faster falloff).")]
    [SerializeField] [Range(0f, 10f)] private float damping = 4f;

    // ── Runtime state ──────────────────────────────────────────────────────

    private Vector3 _shakeOffset;
    private Coroutine _shakeRoutine;

    // ── Unity ──────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (drumPadTouch == null) return;
        drumPadTouch.OnLeftHit   += TriggerShake;
        drumPadTouch.OnCenterHit += TriggerShake;
        drumPadTouch.OnRightHit  += TriggerShake;
    }

    private void OnDisable()
    {
        if (drumPadTouch == null) return;
        drumPadTouch.OnLeftHit   -= TriggerShake;
        drumPadTouch.OnCenterHit -= TriggerShake;
        drumPadTouch.OnRightHit  -= TriggerShake;
    }

    private void LateUpdate()
    {
        transform.position += _shakeOffset;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Triggers a shake using the component's configured settings.</summary>
    public void TriggerShake()
    {
        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    // ── Private ────────────────────────────────────────────────────────────

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentMagnitude = magnitude * (1f - Mathf.Pow(t, 1f / Mathf.Max(damping, 0.01f)));

            _shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentMagnitude,
                Random.Range(-1f, 1f) * currentMagnitude,
                0f
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        _shakeOffset = Vector3.zero;
        _shakeRoutine = null;
    }
}
