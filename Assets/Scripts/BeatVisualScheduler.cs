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

    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private bool isPaused = false;

    private double VirtualDspTime() => AudioSettings.dspTime - totalPausedTime;

    private bool isFrozen = false;
    private float frozenSliderValue = 1f;

    private class ScheduledVisualEvent
    {
        public double scheduledTime;
        public bool isRightBongo;
    }

    private List<ScheduledVisualEvent> scheduledEvents = new List<ScheduledVisualEvent>();

    private void Start()
    {
        if (metronome == null)
        {
            Debug.LogError("BeatVisualScheduler: Metronome reference is missing!");
            return;
        }

        SetBPM();

        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete += FreezeVisuals;
            Debug.Log("BeatVisualScheduler: Subscribed to OnFinalBarComplete");
        }
        else
        {
            Debug.LogError("BeatVisualScheduler: BeatGenerator reference is missing! Assign it in Inspector.");
        }
    }

    private void OnDisable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
        }
    }

    public void SetBPM()
    {
        double beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = VirtualDspTime();
    }

    private void Update()
    {
        if (isPaused || isFrozen)
        {
            if (isFrozen)
            {
                barSlider.value = frozenSliderValue;
            }
            return;
        }

        double currentTime = VirtualDspTime();
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        if (elapsedLoop >= fullLoopDuration)
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

    public void ScheduleVisualBeat(double scheduledTime, bool isRightBongo)
    {
        if (isFrozen) return;

        double virtualScheduledTime = scheduledTime - totalPausedTime;

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
        if (!isPaused)
        {
            isPaused = true;
            pauseStartTime = AudioSettings.dspTime;
        }
    }

    public void OnResume()
    {
        if (isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            totalPausedTime += pauseDuration;
            isPaused = false;

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

        // Stop everything
        StopAllCoroutines();

        // Clear events
        scheduledEvents.Clear();

        // Remove all indicators
        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }

        // Reset slider
        if (barSlider != null)
        {
            barSlider.value = 0f;
        }

        // Reset state
        isFrozen = false;
        frozenSliderValue = 0f;
        isPaused = false;
        totalPausedTime = 0.0;
        pauseStartTime = 0.0;

        // Re-initialize timing will happen in SetBPM or on first update
        fullLoopStartDspTime = AudioSettings.dspTime - totalPausedTime;

        Debug.Log("[BeatVisualScheduler] Reset complete");
    }
}