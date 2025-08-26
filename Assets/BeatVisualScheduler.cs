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
        fullLoopStartDspTime = AudioSettings.dspTime;
    }

    private void Update()
    {
        if (GameManager.instance.isPaused == true)
            return; // Freeze updates while paused

        double currentTime = GameManager.instance.VirtualDspTime();
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

    public void ScheduleVisualBeat(double scheduledTime, bool isRightBongo)
    {
        ScheduledVisualEvent newEvent = new ScheduledVisualEvent
        {
            scheduledTime = scheduledTime,
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

    // Call this when pausing.
    public void OnPause()
    {
        if (!GameManager.instance.isPaused)
        {

        }
    }

    // Call this when resuming.
    public void OnResume()
    {
        if (GameManager.instance.isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - GameManager.instance.pauseStartTime;
            fullLoopStartDspTime += pauseDuration; // Adjust the reference time
        }
    }
}
