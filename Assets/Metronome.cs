using System;
using UnityEngine;
using UnityEngine.UI;

public class Metronome : MonoBehaviour
{
    public event Action OnTickEvent;
    public event Action OnFreshBarEvent;

    public bool playTick = false;

    public double bpm = 140.0F;

    double nextTick = 0.0F; // The next tick in dspTime
    double sampleRate = 0.0F;
    bool ticked = false;

    public AudioSource speaker;

    public int beatCount = 0; // Counter for the beats (1 to 4)
    public int loopBeatCount = 0;
    public double timePerTick;
    [NonSerialized] public double nextBeatTime;

    private double lastBeatTime;
    private double beatInterval; // Time interval between each beat in seconds
    private double timeSinceLastBeat; // Track the time passed since the last beat

    // Pause tracking
    private bool isPaused = false;
    private double pauseStartTime = 0.0;

    // Public property to expose pause state (for other scripts)
    public bool IsPaused { get { return isPaused; } }

    private void OnEnable()
    {
    }

    void Start()
    {
        double startTick = AudioSettings.dspTime;
        sampleRate = AudioSettings.outputSampleRate;

        timePerTick = 60.0 / bpm;
        nextTick = startTick + timePerTick;
        nextBeatTime = nextTick;

        beatInterval = 60.0 / bpm; // Calculate the interval between each beat in seconds
        lastBeatTime = AudioSettings.dspTime; // Start from the current DSP time (audio time)
    }

    public void RefreshValues() //This probably shouldn't be like this but who cares
    {
        Debug.Log("Metronome Values Refreshed");
        timePerTick = 60.0 / bpm;
    }

    void LateUpdate()
    {
        if (isPaused)
            return;

        // Check if it's time to tick
        if (!ticked && nextTick >= AudioSettings.dspTime)
        {
            ticked = true;
            beatCount++;
            loopBeatCount++;
            nextBeatTime = nextTick;

            if (beatCount > 4)
            {
                beatCount = 1; // Reset the beat count after 4 beats
            }
            if (loopBeatCount > 8)
            {
                loopBeatCount = 1;
                Debug.Log("Looped");
                OnFreshBarEvent?.Invoke();
            }

            OnTickEvent?.Invoke();
            BroadcastMessage("OnTick");
        }
    }

    void FixedUpdate()
    {
        if (isPaused)
            return;

        double dspTime = AudioSettings.dspTime;

        while (dspTime >= nextTick)
        {
            ticked = false;
            nextTick += timePerTick;
        }
    }

    public double GetNextBeatTime()
    {
        return nextBeatTime;
    }
    public double GetCurrentTime()
    {
        return AudioSettings.dspTime - lastBeatTime; // Get the time since the last beat
    }
    public double GetClosestBeatTime(double currentTime)
    {
        double beatInterval = 60.0 / bpm;
        double elapsedSinceLastBeat = currentTime - lastBeatTime;

        // Estimate total elapsed beats since the last known beat
        int beatOffset = Mathf.RoundToInt((float)(elapsedSinceLastBeat / beatInterval));

        // Calculate the actual closest beat time
        return lastBeatTime + beatInterval * beatOffset;
    }

    void OnTick()
    {
        if (playTick)
            speaker.PlayOneShot(speaker.clip);
    }

    /// <summary>
    /// Call this method to pause the metronome.
    /// </summary>
    public void OnPause()
    {
        if (!isPaused)
        {
            isPaused = true;
            pauseStartTime = AudioSettings.dspTime;
        }
    }

    /// <summary>
    /// Call this method to resume the metronome.
    /// Adjusts timing so that the metronome appears frozen during the pause.
    /// </summary>
    public void OnResume()
    {
        if (isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            nextTick += pauseDuration;
            nextBeatTime += pauseDuration;
            lastBeatTime += pauseDuration;
            isPaused = false;
        }
    }
}
