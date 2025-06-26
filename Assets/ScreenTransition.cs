using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("Duration for the transition (in seconds).")]
    public float transitionDuration = 1.0f;

    [Tooltip("Set to true if you want the transition to complete automatically on start (i.e. finish the last half).")]
    public bool autoRevealOnStart = true;

    // Reference to the RectTransform on the UI Image component.
    private RectTransform _rectTransform;

    // Internal flags for tracking state.
    private bool _isTransitioning = false;
    private bool _isCovered = true;

    /// <summary>
    /// Returns true if the screen is fully covered (and no transition is currently running).
    /// </summary>
    public bool IsScreenCovered
    {
        get { return _isCovered && !_isTransitioning; }
    }

    void Awake()
    {
        // Get the RectTransform component if not assigned.
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            Debug.LogError("ScreenTransition script must be attached to a GameObject with a RectTransform.");
            return;
        }

        // Start fully covering the scene.
        // Anchors should be set to stretch (Min:0,0; Max:1,1) so that the size of the panel equals the screen size.
        _rectTransform.anchoredPosition = Vector2.zero;
        _isTransitioning = false;
        _isCovered = true;
    }

    void Start()
    {
        // If you want to automatically reveal (finish the transition) on start,
        // you may wish to simulate finishing the last half of the wipe.
        // In that case, change the duration accordingly (e.g. use half of the full transitionDuration).
        if (autoRevealOnStart)
        {
            // Option: if you want to instantly jump to half the wipe (simulate that the initial part was played),
            // then adjust the position here. For example:
            // float halfWidth = _rectTransform.rect.width * 0.5f;
            // _rectTransform.anchoredPosition = new Vector2(-halfWidth, 0);
            //
            // Then call a reveal that only moves over the remaining half.
            //
            // In this example the script simply plays the full reveal transition.
            Invoke("StartReveal", 0);
        }
    }

    /// <summary>
    /// Begins the reveal transition. The black panel will animate from covering the screen (position 0)
    /// to completely off-screen (to the left).
    /// </summary>
    public void StartReveal()
    {
        if (!_isTransitioning)
        {
            StartCoroutine(RevealRoutine());
        }
    }

    /// <summary>
    /// Begins the cover transition. The black panel will animate from off-screen (left) back to fully covering the screen.
    /// </summary>
    public void StartCover()
    {
        if (!_isTransitioning)
        {
            StartCoroutine(CoverRoutine());
        }
    }

    /// <summary>
    /// Coroutine that handles the reveal (wipe out) animation.
    /// </summary>
    private IEnumerator RevealRoutine()
    {
        _isTransitioning = true;

        // Get the width of the RectTransform (assumes horizontal wipe).
        float width = _rectTransform.rect.width;

        // Starting position: fully covering (x = 0)
        Vector2 startPos = _rectTransform.anchoredPosition;
        // End position: off-screen to the left (x = -width)
        Vector2 endPos = new Vector2(-width, 0);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final position.
        _rectTransform.anchoredPosition = endPos;
        _isCovered = false;
        _isTransitioning = false;
    }

    /// <summary>
    /// Coroutine that handles the cover (wipe in) animation.
    /// </summary>
    private IEnumerator CoverRoutine()
    {
        _isTransitioning = true;

        float width = _rectTransform.rect.width;
        // Starting position: off-screen to the left (x = -width)
        Vector2 startPos = _rectTransform.anchoredPosition;
        // End position: fully covering (x = 0)
        Vector2 endPos = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _rectTransform.anchoredPosition = endPos;
        _isCovered = true;
        _isTransitioning = false;
    }

    private void OnDisable()
    {
        _isTransitioning = false;
    }
}
