using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private double fullLoopStartDspTime;
    public int fullLoopBeats = 8;

    private double VirtualDspTime() => metronome.VirtualDspTime;

    private bool isFrozen = false;
    private float frozenSliderValue = 1f;

    public double scheduledTimeOffset = 0.003f;

    private class ScheduledVisualEvent
    {
        public double scheduledTime;
        public bool isRightBongo;
    }

    private List<ScheduledVisualEvent> scheduledEvents = new List<ScheduledVisualEvent>();

    private void OnEnable()
    {
        if (metronome == null)
        {
            Debug.LogError("BeatVisualScheduler: Metronome reference is missing!");
            enabled = false;
            return;
        }

        // DON'T call ResetLoopStartTime() here!
        // Timing will be set explicitly when synchronized with metronome

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
            Debug.LogError("BeatVisualScheduler: BeatGenerator reference is missing! Assign it in Inspector.");
        }
    }

    private void Start()
    {

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
        double beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = VirtualDspTime();
        Debug.Log("BeatVisualScheduler: Beat values initialized");
        Debug.Log("BeatVisualScheduler: fullLoopStartDspTime = " + fullLoopStartDspTime);
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

        double currentTime = VirtualDspTime();
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        // Use while instead of if to handle large jumps (e.g. after hiccups)
        while (elapsedLoop >= fullLoopDuration)
        {
            fullLoopStartDspTime += fullLoopDuration;
            elapsedLoop = currentTime - fullLoopStartDspTime;
        }

        if (elapsedLoop < barDuration)
        {
            barSlider.value = (float)(elapsedLoop / barDuration);
        }
        else
        {
            barSlider.value = 1f;
        }

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

        double virtualScheduledTime = scheduledTimeDsp + scheduledTimeOffset;

        ScheduledVisualEvent newEvent = new ScheduledVisualEvent
        {
            scheduledTime = virtualScheduledTime,
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

    }

    public void OnResume()
    {
        if (GameClock.Instance.IsPaused)
        {
            double pauseDuration = AudioSettings.dspTime - GameClock.Instance.GetPauseStartTime();

            // Preserve current loop phase when resuming
            fullLoopStartDspTime = VirtualDspTime() - (VirtualDspTime() - fullLoopStartDspTime);
        }
    }

    public void CleanupAndDisable()
    {
        Debug.Log("[BeatVisualScheduler] Cleaning up before disable");

        // Stop any ongoing transitions
        StopAllCoroutines();

        // Clear all scheduled events
        scheduledEvents.Clear();

        // Remove all spawned indicators
        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }

        // Reset slider to start
        if (barSlider != null)
        {
            barSlider.value = 0f;
            frozenSliderValue = 0f;
        }
        

        // Disable the component
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

        // DON'T call ResetLoopStartTime() here!
        // Timing will be synchronized when game starts

        Debug.Log("[BeatVisualScheduler] Reset complete");
    }
    // In BeatVisualScheduler.cs - MODIFY to accept cached time
    public void SyncWithMetronome(double nextBeatTime)
    {
        if (metronome == null)
        {
            Debug.LogError("[BeatVisualScheduler] Cannot sync - metronome is null");
            return;
        }

        barSlider.value = 0f;
        InitalizeBeatValues();

        Debug.Log($"[BeatVisualScheduler] Synced - nextBeat: {nextBeatTime:F4}, fullLoopStartDspTime: {fullLoopStartDspTime:F4}");
    }
    private void ResetLoopStartTime()
    {
        fullLoopStartDspTime = VirtualDspTime();
    }
}
