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

    private double barDuration;
    private double fullLoopDuration;
    private double fullLoopStartDspTime;
    public int fullLoopBeats = 8;

    // Updated pause handling to match BeatGenerator approach
    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private bool isPaused = false;

    // Virtual DSP time that accounts for paused time
    private double VirtualDspTime() => AudioSettings.dspTime - totalPausedTime;

    private class ScheduledVisualEvent
    {
        public double scheduledTime; // This will be in virtual time
        public bool isRightBongo;
    }

    private List<ScheduledVisualEvent> scheduledEvents = new List<ScheduledVisualEvent>();

    private void Start()
    {
        if (metronome == null)
        {
            Debug.LogError("Metronome reference is missing!");
            return;
        }

        SetBPM();
    }

    private void OnEnable()
    {
        SetBPM();
    }

    public void SetBPM()
    {
        double beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = VirtualDspTime(); // Use virtual time
    }

    private void Update()
    {
        if (isPaused)
            return; // Freeze updates while paused

        double currentTime = VirtualDspTime(); // Use virtual time consistently
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

        // Process scheduled events using virtual time
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

    public void ScheduleVisualBeat(double scheduledTime, bool isRightBongo)
    {
        // Convert the scheduled time to virtual time for consistent handling
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
        scheduledEvents.Clear();
        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }
    }

    // Call this when pausing - now matches BeatGenerator approach
    public void OnPause()
    {
        if (!isPaused)
        {
            isPaused = true;
            pauseStartTime = AudioSettings.dspTime;
        }
    }

    // Call this when resuming - now properly accumulates pause time
    public void OnResume()
    {
        if (isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            totalPausedTime += pauseDuration;
            isPaused = false;

            // Update the loop start time to virtual time
            fullLoopStartDspTime = VirtualDspTime() - (VirtualDspTime() - fullLoopStartDspTime);
        }
    }
}