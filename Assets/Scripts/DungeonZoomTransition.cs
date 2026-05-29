using UnityEngine;
using System.Collections;

/// <summary>
/// Dungeon-exclusive room transition. Scales <see cref="zoomTarget"/> to
/// <see cref="targetScale"/> and fades its <see cref="zoomRenderer"/> alpha to zero
/// over <see cref="transitionDuration"/>, then snaps both back to their original values.
///
/// Implements the same IsScreenCovered / IsTransitioning / StartReveal / StartCover
/// contract as ScreenTransition so RoomController can swap them without logic changes.
/// Cover = zoom-out + fade animation (plays when leaving a room).
/// Reveal = instant reset (new room appears the moment the cover animation finishes).
/// </summary>
public class DungeonZoomTransition : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The transform to scale during the transition (e.g. the BackgroundZoomer object).")]
    [SerializeField] private Transform zoomTarget;

    [Tooltip("The SpriteRenderer whose alpha is animated during the transition.")]
    [SerializeField] private SpriteRenderer zoomRenderer;

    [Header("Transition Settings")]
    [Tooltip("How long the zoom-out and fade take, in seconds.")]
    [SerializeField] private float transitionDuration = 0.6f;

    [Tooltip("The uniform scale multiplier the target reaches at the end of the animation.")]
    [SerializeField] private float targetScale = 5f;

    [Tooltip("Normalised progress [0–1] at which the fade begins. E.g. 0.65 = fade starts when scale reaches 65% of targetScale.")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartThreshold = 0.65f;

    private Vector3 _originalScale;
    private Color   _originalColor;
    private bool    _isTransitioning;
    private bool    _isCovered = true;

    /// <summary>True when fully covered and no transition is running.</summary>
    public bool IsScreenCovered => _isCovered && !_isTransitioning;

    /// <summary>True while the zoom/fade coroutine is running.</summary>
    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        if (zoomTarget   != null) _originalScale = zoomTarget.localScale;
        if (zoomRenderer != null) _originalColor = zoomRenderer.color;
        _isCovered = true;
    }

    private void Start()
    {
        // Mirror ScreenTransition.autoRevealOnStart: immediately reveal so the
        // first room can begin without waiting for an animation.
        StartReveal();
    }

    /// <summary>
    /// Instantly clears the covered state. No animation: the new room content is
    /// already set by the time this is called, so it appears without a wipe.
    /// </summary>
    public void StartReveal()
    {
        if (_isTransitioning) return;
        ResetTarget();
        _isCovered = false;
    }

    /// <summary>
    /// Plays the zoom-out and fade animation, then resets the target.
    /// Sets IsScreenCovered = true when the animation completes.
    /// </summary>
    public void StartCover()
    {
        if (_isTransitioning || _isCovered) return;
        StartCoroutine(CoverRoutine());
    }

    private IEnumerator CoverRoutine()
    {
        _isTransitioning = true;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;

            if (zoomTarget != null)
                zoomTarget.localScale = Vector3.Lerp(_originalScale, _originalScale * targetScale, t);

            if (zoomRenderer != null)
            {
                float fadeT = fadeStartThreshold >= 1f ? 0f
                            : Mathf.Clamp01((t - fadeStartThreshold) / (1f - fadeStartThreshold));
                Color c = _originalColor;
                c.a = Mathf.Lerp(_originalColor.a, 0f, fadeT);
                zoomRenderer.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetTarget();
        _isCovered       = true;
        _isTransitioning = false;
    }

    private void ResetTarget()
    {
        if (zoomTarget   != null) zoomTarget.localScale = _originalScale;
        if (zoomRenderer != null) zoomRenderer.color    = _originalColor;
    }

    private void OnDisable()
    {
        _isTransitioning = false;
    }
}
