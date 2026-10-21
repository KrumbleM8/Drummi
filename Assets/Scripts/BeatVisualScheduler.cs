using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages visual beat indicators synchronized with TimingCoordinator.
/// </summary>
public class BeatVisualScheduler : MonoBehaviour
{
    public CustardAnimationHandler custardAnimator;

    [Header("UI References")]
    public Slider barSlider;
    public RectTransform indicatorParent;
    public GameObject beatIndicatorPrefab;
    public Color leftBongoColor = Color.green;
    public Color rightBongoColor = Color.red;

    [Header("Flash Mode")]
    [Tooltip("Optional. When assigned and its Use Flash is on, Custard's beats flash large near screen centre instead of spawning dots on the slider.")]
    public BongoBeatFlash beatFlash;

    [Header("Metronome Reference")]
    public Metronome metronome;

    [Header("Beat Generator Reference")]
    public BeatGenerator beatGenerator;

    private double barDuration;
    private double fullLoopDuration;
    public int fullLoopBeats = 8;

    private bool isFrozen = false;
    private float frozenSliderValue = 1f;

    public double scheduledTimeOffset = 0.0; // No longer needed with coordinator

    private class ScheduledVisualEvent
    {
        public double scheduledTime;
        public bool isRightBongo;
    }

    private List<ScheduledVisualEvent> scheduledEvents = new List<ScheduledVisualEvent>();

    private void Awake()
    {
        // Scene wiring is preferred; fall back to a lookup so the flash still works if the reference is lost.
        if (beatFlash == null)
            beatFlash = FindFirstObjectByType<BongoBeatFlash>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        isFrozen = false;

        if (barSlider != null)
        {
            barSlider.value = 0f;
            frozenSliderValue = 0f;
        }

        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
            beatGenerator.OnFinalBarComplete += FreezeVisuals;
        }
        else
        {
            Debug.LogError("BeatVisualScheduler: BeatGenerator reference is missing!");
        }
    }

    private void OnDisable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
        }
    }

    public void InitalizeBeatValues()
    {
        if (metronome == null)
        {
            Debug.LogError("BeatVisualScheduler: Metronome reference is missing!");
            return;
        }

        double beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;

        Debug.Log("BeatVisualScheduler: Beat values initialized");
    }

    private void Update()
    {
        if (GameClock.Instance.IsPaused || isFrozen)
        {
            if (isFrozen)
            {
                barSlider.value = frozenSliderValue;
            }
            return;
        }

        // Use TimingCoordinator for timing
        var coordinator = TimingCoordinator.Instance;
        if (coordinator == null) return;

        var currentBar = coordinator.CurrentBar;
        double currentTime = GameClock.Instance.GameTime;

        // Calculate progress within the current bar (first 4 beats)
        double elapsedInBar = currentTime - currentBar.BarStartTime;

        if (elapsedInBar < barDuration && elapsedInBar >= 0)
        {
            barSlider.value = (float)(elapsedInBar / barDuration);
        }
        else if (elapsedInBar >= barDuration)
        {
            barSlider.value = 1f;
        }
        else
        {
            barSlider.value = 0f;
        }

        // Process scheduled visual events
        List<ScheduledVisualEvent> triggeredEvents = new List<ScheduledVisualEvent>();
        foreach (var evt in scheduledEvents)
        {
            if (currentTime >= evt.scheduledTime)
            {
                CreateBeatIndicator(evt.isRightBongo);
                triggeredEvents.Add(evt);
            }
        }
        foreach (var evt in triggeredEvents)
        {
            scheduledEvents.Remove(evt);
        }
    }

    public void FreezeVisuals()
    {
        if (isFrozen)
        {
            Debug.LogWarning("BeatVisualScheduler: Already frozen!");
            return;
        }

        isFrozen = true;
        frozenSliderValue = barSlider.value;
        scheduledEvents.Clear();

        Debug.Log($"BeatVisualScheduler: FROZEN at slider value {frozenSliderValue:F2}");
    }

    public void UnfreezeVisuals()
    {
        isFrozen = false;
        Debug.Log("BeatVisualScheduler: Visuals unfrozen");
    }

    public void ScheduleVisualBeat(double scheduledTimeDsp, bool isRightBongo)
    {
        if (isFrozen) return;

        // No offset needed - TimingCoordinator handles all timing
        ScheduledVisualEvent newEvent = new ScheduledVisualEvent
        {
            scheduledTime = scheduledTimeDsp,
            isRightBongo = isRightBongo
        };
        scheduledEvents.Add(newEvent);
    }

    private void CreateBeatIndicator(bool isRightBongo)
    {
        if (isFrozen) return;

        if (beatIndicatorPrefab == null || indicatorParent == null || barSlider == null)
        {
            Debug.LogWarning("BeatVisualScheduler is missing required references.");
            return;
        }

        if (!isRightBongo)
            custardAnimator.PlayLeftBongo();
        else
            custardAnimator.PlayRightBongo();

        // Flash mode: big centre flash instead of a dot on the (hidden) slider bar.
        if (beatFlash != null && beatFlash.useFlash)
        {
            beatFlash.FlashCall(beatIndicatorPrefab, isRightBongo, isRightBongo ? rightBongoColor : leftBongoColor);
            return;
        }

        GameObject indicator = Instantiate(beatIndicatorPrefab, indicatorParent);
        Image img = indicator.GetComponent<Image>();
        if (img != null)
        {
            img.color = isRightBongo ? rightBongoColor : leftBongoColor;
        }

        float posX = GetCurrentSliderValueXPosition();
        RectTransform rt = indicator.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(posX, 0);
        }
    }

    public float GetCurrentSliderValueXPosition()
    {
        float parentWidth = indicatorParent.rect.width;
        float posX = (barSlider.value * parentWidth) - (parentWidth / 2f);
        return posX;
    }

    public void ResetVisuals()
    {
        if (isFrozen) return;

        scheduledEvents.Clear();
        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }
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
        Debug.Log("[BeatVisualScheduler] Cleaning up before disable");

        StopAllCoroutines();
        scheduledEvents.Clear();

        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }

        if (barSlider != null)
        {
            barSlider.value = 0f;
            frozenSliderValue = 0f;
        }

        if (beatFlash != null) beatFlash.Clear();

        enabled = false;

        Debug.Log("[BeatVisualScheduler] Cleanup complete");
    }

    public void ResetToInitialState()
    {
        Debug.Log("[BeatVisualScheduler] Resetting to initial state");

        StopAllCoroutines();
        scheduledEvents.Clear();

        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }

        if (barSlider != null)
        {
            barSlider.value = 0f;
        }

        if (beatFlash != null) beatFlash.Clear();

        isFrozen = false;
        frozenSliderValue = 0f;

        Debug.Log("[BeatVisualScheduler] Reset complete");
    }

    /// <summary>
    /// Sync with TimingCoordinator (replaces old metronome sync).
    /// </summary>
    public void SyncWithTimingCoordinator()
    {
        if (TimingCoordinator.Instance == null)
        {
            Debug.LogError("[BeatVisualScheduler] Cannot sync - TimingCoordinator is null");
            return;
        }

        barSlider.value = 0f;
        InitalizeBeatValues();

        Debug.Log($"[BeatVisualScheduler] Synced with TimingCoordinator");
    }
}