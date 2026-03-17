using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages player input visual feedback synchronized with TimingCoordinator.
/// </summary>
public class PlayerInputVisualHandler : MonoBehaviour
{
    [Header("UI References")]
    public Slider inputSlider;
    public RectTransform indicatorParent;
    public RectTransform starParent;
    private List<Coroutine> activeStarCoroutines = new List<Coroutine>();
    public GameObject inputIndicatorPrefab;
    private Color leftBongoColor = Color.green;
    private Color rightBongoColor = Color.red;
    public GameObject perfectInputStarPrefab;

    [Header("Metronome Reference")]
    public Metronome metronome;

    [Header("Beat Generator Reference")]
    public BeatGenerator beatGenerator;

    private double beatDuration;
    private double barDuration;

    [Header("Lead-in")]
    [Tooltip("How many beats early the handle begins sliding in")]
    [Range(1, 3)] public int leadInBeats = 1;

    [Tooltip("Extra X-offset (px) to park the handle fully off-screen")]
    public float offScreenPadding = 10f;

    private RectTransform handleRect;
    private Vector2 readyPos;
    private Vector2 parkedPos;

    private bool isFrozen = false;

    private void OnEnable()
    {
        isFrozen = false;

        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        if (handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
            beatGenerator.OnFinalBarComplete += FreezeVisuals;
        }
        else
        {
            Debug.LogError("PlayerInputVisualHandler: BeatGenerator reference is missing!");
        }
    }

    private void Start()
    {
        CacheHandlePositions();

        BeatVisualScheduler beatVisualScheduler = GetComponent<BeatVisualScheduler>();
        leftBongoColor = beatVisualScheduler.leftBongoColor;
        rightBongoColor = beatVisualScheduler.rightBongoColor;
    }

    private void OnDisable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
        }
    }

    public void FreezeVisuals()
    {
        if (isFrozen)
        {
            Debug.LogWarning("PlayerInputVisualHandler: Already frozen!");
            return;
        }

        isFrozen = true;
        Debug.Log("PlayerInputVisualHandler: FROZEN");
    }

    public void InitializeBeatValues()
    {
        if (metronome == null)
        {
            Debug.LogError("PlayerInputVisualHandler: Metronome reference is missing!");
            return;
        }

        beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;

        Debug.Log("PlayerInputVisualHandler: Beat values initialized");
    }

    private void CacheHandlePositions()
    {
        if (inputSlider == null) return;

        inputSlider.value = 0f;
        handleRect = inputSlider.handleRect;
        readyPos = handleRect.anchoredPosition;

        float sliderWidth = inputSlider.GetComponent<RectTransform>().rect.width;
        parkedPos = readyPos + Vector2.left * (sliderWidth + offScreenPadding);

        handleRect.anchoredPosition = parkedPos;
    }

    private void Update()
    {
        if (GameClock.Instance.IsPaused || isFrozen) return;

        // Use TimingCoordinator for timing
        var coordinator = TimingCoordinator.Instance;
        if (coordinator == null) return;

        var currentBar = coordinator.CurrentBar;
        double currentTime = GameClock.Instance.GameTime;

        // Calculate lead-in start time
        double leadInStart = currentBar.BarStartTime + (barDuration - (beatDuration * leadInBeats));
        double elapsedSinceBarStart = currentTime - currentBar.BarStartTime;

        // Before lead-in: handle is parked off-screen
        if (elapsedSinceBarStart < (barDuration - (beatDuration * leadInBeats)))
        {
            handleRect.anchoredPosition = parkedPos;
            inputSlider.value = 0f;
            return;
        }

        // During lead-in: slide handle from parked to ready position
        if (elapsedSinceBarStart < barDuration)
        {
            double leadElapsed = currentTime - leadInStart;
            float t = (float)(leadElapsed / (beatDuration * leadInBeats));
            t = Mathf.Clamp01(t);
            handleRect.anchoredPosition = Vector2.Lerp(parkedPos, readyPos, t);
            inputSlider.value = 0f;
            return;
        }

        // During input window: slider progresses
        double elapsedInInputWindow = currentTime - currentBar.InputWindowStart;
        float progress = (float)(elapsedInInputWindow / barDuration);
        progress = Mathf.Clamp01(progress);
        inputSlider.value = progress;
    }

    public void SpawnInputIndicator(bool isRightBongo)
    {
        if (!inputIndicatorPrefab || !indicatorParent || !inputSlider)
        {
            Debug.LogWarning("PlayerInputVisualHandler is missing required references.");
            return;
        }

        GameObject indicator = Instantiate(inputIndicatorPrefab, indicatorParent);
        if (indicator.TryGetComponent(out Image img))
            img.color = isRightBongo ? rightBongoColor : leftBongoColor;

        if (indicator.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderXPosition(), 0f);
    }

    public void SpawnMissedInputIndicator(bool isRightBongo, InputMatch.MatchQuality timing)
    {
        if (!inputIndicatorPrefab || !indicatorParent || !inputSlider)
        {
            Debug.LogWarning("PlayerInputVisualHandler: Missing required references.");
            return;
        }

        GameObject indicator = Instantiate(inputIndicatorPrefab, indicatorParent);

        // --- Position ---
        if (indicator.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderXPosition(), 0f);

        // --- Darken the image ---
        if (indicator.TryGetComponent(out Image img))
        {
            Color baseColor = isRightBongo ? rightBongoColor : leftBongoColor;
            img.color = Color.gray; // darken by reducing RGB, keeps alpha
        }

        // --- Stop the bounce/spawn effect and tilt ---
        if (indicator.TryGetComponent(out UIQuickSpawnEffect spawnEffect))
        {
            float tiltAngle = timing == InputMatch.MatchQuality.TooEarly ? 45f : -45f;
            if (timing == InputMatch.MatchQuality.Miss || timing == InputMatch.MatchQuality.WrongSide)
                tiltAngle = 90f; // Missed entirely, so more extreme tilt

            spawnEffect.rotateAngle = tiltAngle;
            spawnEffect.wrongHit = true;
            spawnEffect.recoilScale = .8f;
        }

        // --- Stop the bounce/spawn effect and tilt ---
        if (indicator.TryGetComponent(out UIRotateWobble wobbleEffect))
        {
            wobbleEffect.enabled = false;
        }
    }

    public void SpawnPerfectInputStar()
    {
        if (!perfectInputStarPrefab || !indicatorParent)
        {
            Debug.LogWarning("PlayerInputVisualHandler: Missing perfectInputStarPrefab or indicatorParent.");
            return;
        }

        GameObject star = Instantiate(perfectInputStarPrefab, starParent);

        if (star.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderXPosition(), 0f);

        Coroutine c = StartCoroutine(AnimatePerfectStar(star));
        activeStarCoroutines.Add(c);
    }

    private IEnumerator AnimatePerfectStar(GameObject star)
    {
        if (!star.TryGetComponent(out RectTransform rt)) yield break;

        // --- Tuneable values ---
        float growDuration = 0.17f;
        float shrinkDuration = 0.12f;
        float peakScale = 1.2f;
        float restingScale = 0.8f;
        float totalSpin = 360f;       // degrees over the full animation
                                      // -----------------------

        float totalDuration = growDuration + shrinkDuration;
        float elapsed = 0f;

        rt.localScale = Vector3.zero;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            // Spin continuously across the entire animation
            float spinAngle = Mathf.Lerp(0f, totalSpin, elapsed / totalDuration);
            rt.localRotation = Quaternion.Euler(0f, 0f, spinAngle);

            // Scale: grow phase then shrink phase
            float scale;
            if (elapsed < growDuration)
            {
                float t = elapsed / growDuration;
                scale = Mathf.Lerp(0f, peakScale, EaseOutBack(t));
            }
            else
            {
                float t = (elapsed - growDuration) / shrinkDuration;
                scale = Mathf.Lerp(peakScale, restingScale, EaseInQuad(t));
            }

            rt.localScale = Vector3.one * scale;

            yield return null;
        }

        // Settle cleanly
        rt.localScale = Vector3.one * restingScale;
    }

    // Overshoots slightly for a punchy pop feel
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseInQuad(float t) => t * t;

    public float GetCurrentSliderXPosition()
    {
        float parentWidth = indicatorParent.rect.width;
        return (inputSlider.value * parentWidth) - (parentWidth / 2f);
    }

    public void ResetVisuals()
    {
        foreach (var c in activeStarCoroutines)
            if (c != null) StopCoroutine(c);

        activeStarCoroutines.Clear();


        for (int i = starParent.childCount - 1; i >= 0; i--)
            Destroy(starParent.GetChild(i).gameObject);

        if (!indicatorParent) return;

        for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            Destroy(indicatorParent.GetChild(i).gameObject);
    }

    public void OnPause()
    {
        // Pause handling done by GameClock and TimingCoordinator
    }

    public void OnResume()
    {
        // Timing adjustment handled by TimingCoordinator
    }

    public void CleanupAndDisable()
    {
        Debug.Log("[PlayerInputVisualHandler] Cleaning up before disable");

        StopAllCoroutines();
        ResetVisuals();

        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        if (handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        enabled = false;

        Debug.Log("[PlayerInputVisualHandler] Cleanup complete");
    }

    public void ResetToInitialState()
    {
        Debug.Log("[PlayerInputVisualHandler] Resetting to initial state");

        StopAllCoroutines();
        ResetVisuals();

        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        if (handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        isFrozen = false;

        Debug.Log("[PlayerInputVisualHandler] Reset complete");
    }

    /// <summary>
    /// Sync with TimingCoordinator (replaces old metronome sync).
    /// </summary>
    public void SyncWithTimingCoordinator()
    {
        if (TimingCoordinator.Instance == null)
        {
            Debug.LogError("[PlayerInputVisualHandler] Cannot sync - TimingCoordinator is null");
            return;
        }

        inputSlider.value = 0f;
        InitializeBeatValues();

        Debug.Log($"[PlayerInputVisualHandler] Synced with TimingCoordinator");
    }
}