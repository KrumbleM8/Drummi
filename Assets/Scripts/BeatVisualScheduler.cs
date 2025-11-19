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
        double currentTime = AudioSettings.dspTime;

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

        GameObject indicator = Instantiate(beatIndicatorPrefab, indicatorParent);
        Image img = indicator.GetComponent<Image>();
        if (img != null)
        {
            img.color = isRightBongo ? Color.red : Color.green;
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