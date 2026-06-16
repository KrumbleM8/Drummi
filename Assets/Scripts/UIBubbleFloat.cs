using UnityEngine;

/// <summary>
/// Animates a UI element with a continuous bubble-like float effect.
/// Movement is driven by Perlin noise so it feels organic and unpredictable
/// while always staying within <see cref="driftRadius"/> pixels of the original
/// anchored position.
///
/// SCENE SETUP:
///   Add to any RectTransform. No other dependencies — self-contained.
///   All parameters are tuneable in the Inspector at runtime.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIBubbleFloat : MonoBehaviour
{
    [Header("Drift")]
    [Tooltip("Maximum pixel distance the element can wander from its resting position.")]
    [Min(0f)]
    [SerializeField] private float driftRadius = 12f;

    [Tooltip("How fast the drift position changes. Higher = more frantic.")]
    [Min(0.01f)]
    [SerializeField] private float driftSpeed = 0.4f;

    [Header("Rotation")]
    [Tooltip("Enables a subtle rolling tilt that mirrors the horizontal drift.")]
    [SerializeField] private bool enableRotation = true;

    [Tooltip("Maximum degrees of tilt in either direction.")]
    [Min(0f)]
    [SerializeField] private float rotationAmount = 4f;

    [Header("Scale Breathe")]
    [Tooltip("Enables a slow, gentle scale pulse to mimic a bubble expanding and contracting.")]
    [SerializeField] private bool enableScaleBreathe = true;

    [Tooltip("How much the scale oscillates around 1. E.g. 0.04 → scale oscillates between 0.96 and 1.04.")]
    [Min(0f)]
    [SerializeField] private float breatheAmount = 0.04f;

    [Tooltip("Speed of the breathe cycle, independent of drift speed.")]
    [Min(0.01f)]
    [SerializeField] private float breatheSpeed = 0.25f;

    // ── Private ───────────────────────────────────────────────────────────────

    private RectTransform _rt;
    private Vector2 _restPosition;
    private Vector3 _restScale;

    // Random per-instance offsets so multiple bubbles don't move in sync.
    private float _noiseOffsetX;
    private float _noiseOffsetY;
    private float _noiseOffsetBreathe;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _restPosition   = _rt.anchoredPosition;
        _restScale      = _rt.localScale;

        // Spread instances across the noise field.
        _noiseOffsetX       = Random.Range(0f, 1000f);
        _noiseOffsetY       = Random.Range(0f, 1000f);
        _noiseOffsetBreathe = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        // Snap to rest state when re-enabled so there's no jump.
        _rt.anchoredPosition = _restPosition;
        _rt.localScale       = _restScale;
    }

    private void Update()
    {
        float t = Time.time;

        // ── Drift ────────────────────────────────────────────────────────────
        // Sample two uncorrelated Perlin noise axes, remap [0,1] → [-1,1].
        float nx = Mathf.PerlinNoise(_noiseOffsetX, t * driftSpeed) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_noiseOffsetY, t * driftSpeed) * 2f - 1f;

        Vector2 drift = new Vector2(nx, ny) * driftRadius;
        _rt.anchoredPosition = _restPosition + drift;

        // ── Rotation ─────────────────────────────────────────────────────────
        // Tilt follows horizontal drift so it feels physically grounded.
        if (enableRotation)
        {
            float angleZ = -nx * rotationAmount; // negative: lean into the direction of travel
            _rt.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        }

        // ── Scale breathe ────────────────────────────────────────────────────
        if (enableScaleBreathe)
        {
            float breathe = Mathf.PerlinNoise(_noiseOffsetBreathe, t * breatheSpeed) * 2f - 1f;
            float scale   = 1f + breathe * breatheAmount;
            _rt.localScale = _restScale * scale;
        }
    }

    private void OnDisable()
    {
        if (_rt == null) return;
        _rt.anchoredPosition = _restPosition;
        _rt.localRotation    = Quaternion.identity;
        _rt.localScale       = _restScale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Live-preview drift radius as a gizmo label in the Scene view.
        // (No scene gizmo API needed — just keeps Inspector values sane.)
        driftRadius   = Mathf.Max(0f, driftRadius);
        breatheAmount = Mathf.Max(0f, breatheAmount);
    }
#endif
}
