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

    public double startTick;

    public AudioSource speaker;

    public int beatCount = 0; // Counter for the beats (1 to 4)
    public int loopBeatCount = 0;
    public double timePerTick;
    [NonSerialized] public double nextBeatTime;

    private double lastBeatTime;
    private double beatInterval; // Time interval between each beat in seconds
    private double timeSinceLastBeat; // Track the time passed since the last beat

    // Timing synchronization variables
    private double lastDspTime = 0.0; // Track the last DSP time we checked
    private const double MAX_FRAME_GAP = 0.1; // Maximum expected gap between frames (100ms)
    private bool hasStarted = false; // Track if we've had our first tick

    // Pause tracking
    private bool isPaused = false;
    private double pauseStartTime = 0.0;

    // ADDED: Track the last tick time we processed to prevent double-processing
    private double lastProcessedTick = 0.0;

    // Public property to expose pause state (for other scripts)
    public bool IsPaused { get { return isPaused; } }

    private void OnEnable()
    {
    }

    void Start()
    {
        startTick = AudioSettings.dspTime;
        sampleRate = AudioSettings.outputSampleRate;

        timePerTick = 60.0 / bpm;
        nextTick = startTick + timePerTick;
        nextBeatTime = nextTick;

        beatInterval = 60.0 / bpm; // Calculate the interval between each beat in seconds
        lastBeatTime = AudioSettings.dspTime; // Start from the current DSP time (audio time)
        lastDspTime = AudioSettings.dspTime; // Initialize last DSP time tracking

        // Initialize the last processed tick to ensure first tick fires properly
        lastProcessedTick = startTick;
    }

    public void RefreshValues() //This probably shouldn't be like this but who cares
    {
        Debug.Log("Metronome Values Refreshed");
        timePerTick = 60.0 / bpm;
        beatInterval = 60.0 / bpm;
    }

    void LateUpdate()
    {
        if (isPaused)
            return;

        double currentDspTime = AudioSettings.dspTime;

        // Check for timing disruptions ONLY after we've started (not on first frame)
        if (hasStarted)
        {
            double timeSinceLastFrame = currentDspTime - lastDspTime;

            // If there's been a significant gap, resynchronize
            if (timeSinceLastFrame > MAX_FRAME_GAP && lastDspTime > 0)
            {
                ResynchronizeMetronome(currentDspTime);
            }
        }

        lastDspTime = currentDspTime;

        // FIXED: Only process if we haven't already processed this tick time
        // and ensure we're at or past the tick time
        if (!ticked && nextTick >= currentDspTime && nextTick > lastProcessedTick)
        {
            ticked = true;
            hasStarted = true; // Mark that we've had our first tick
            beatCount++;
            loopBeatCount++;
            nextBeatTime = nextTick;

            // ADDED: Record this tick time as processed
            lastProcessedTick = nextTick;

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

    /// <summary>
    /// Resynchronizes the metronome after a timing disruption.
    /// Calculates where we should be based on elapsed time and adjusts beat counts accordingly.
    /// </summary>
    private void ResynchronizeMetronome(double currentDspTime)
    {
        Debug.Log("Metronome resynchronizing due to timing gap");

        // Calculate total elapsed time since start
        double totalElapsedTime = currentDspTime - startTick;

        // Calculate how many beats should have occurred
        double totalBeatsElapsed = totalElapsedTime / timePerTick;
        int totalBeatsInt = Mathf.FloorToInt((float)totalBeatsElapsed);

        // Only resync if we've actually missed beats
        double expectedNextTick = startTick + (totalBeatsInt + 1) * timePerTick;

        // If our nextTick is significantly different from where it should be, adjust
        if (Mathf.Abs((float)(nextTick - expectedNextTick)) > timePerTick * 0.1) // 10% tolerance
        {
            // Update beat counts based on where we should be
            int newBeatCount = (totalBeatsInt % 4) + 1; // 1-4 for beat count
            int newLoopBeatCount = (totalBeatsInt % 8) + 1; // 1-8 for loop count

            // Check if we've crossed any 8-beat boundaries and need to fire fresh bar events
            int currentLoop = (beatCount - 1 + (loopBeatCount - 1) * 4) / 8;
            int expectedLoop = totalBeatsInt / 8;

            // Fire any missed fresh bar events
            for (int i = currentLoop + 1; i <= expectedLoop; i++)
            {
                OnFreshBarEvent?.Invoke();
            }

            // Update counts
            beatCount = newBeatCount;
            loopBeatCount = newLoopBeatCount;

            // Align nextTick to the correct position
            nextTick = expectedNextTick;
            nextBeatTime = nextTick;

            // Update lastBeatTime
            lastBeatTime = startTick + totalBeatsInt * timePerTick;

            // ADDED: Update lastProcessedTick during resync
            lastProcessedTick = nextTick - timePerTick;

            // Reset ticked flag
            ticked = false;
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
            startTick += pauseDuration; // Also adjust start time to maintain sync

            // ADDED: Also adjust lastProcessedTick during resume
            lastProcessedTick += pauseDuration;

            isPaused = false;

            // Reset DSP time tracking after resume
            lastDspTime = AudioSettings.dspTime;
        }
    }
}