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
    public GameObject inputIndicatorPrefab;

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
            img.color = isRightBongo ? Color.red : Color.green;

        if (indicator.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderXPosition(), 0f);
    }

    public float GetCurrentSliderXPosition()
    {
        float parentWidth = indicatorParent.rect.width;
        return (inputSlider.value * parentWidth) - (parentWidth / 2f);
    }

    public void ResetVisuals()
    {
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