using System;
using UnityEngine;


public class Metronome : MonoBehaviour
{
    // Events kept for compatibility with bounce animations
    public event Action OnTickEvent;

    public double bpm;
    public bool playTick = false;
    public AudioSource speaker;

    private double nextTick = 0.0F;
    private double timePerTick;
    private bool ticked = false;
    [NonSerialized]
    public double startTick;

    // Simple beat counter for visual feedback
    public int beatCount = 0;

    public void InitializeWithStartTime(double futureStartTime)
    {
        timePerTick = 60.0 / bpm;
        startTick = futureStartTime;
        nextTick = startTick + timePerTick;
        beatCount = 0;
        ticked = false;

        Debug.Log($"[Metronome] Initialized for visual feedback");
        Debug.Log($"  Start time: {futureStartTime:F4}");
        Debug.Log($"  BPM: {bpm}");
        Debug.Log($"  First tick at: {nextTick:F4}");
    }

    private void Update()
    {
        if (GameClock.Instance.IsPaused)
            return;

        double dspTime = AudioSettings.dspTime;

        while (dspTime >= nextTick)
        {
            ticked = false;
            nextTick += timePerTick;
        }
    }

    private void LateUpdate()
    {
        if (GameClock.Instance.IsPaused)
            return;

        double currentDspTime = AudioSettings.dspTime;

        if (!ticked && nextTick >= currentDspTime)
        {
            ticked = true;
            beatCount++;

            if (beatCount > 8)
            {
                beatCount = 1;
            }

            // Fire tick event for visual feedback (bounce animations, etc.)
            OnTickEvent?.Invoke();

            if (playTick && speaker != null)
            {
                speaker.PlayOneShot(speaker.clip);
            }
        }
    }

    public void OnPause()
    {
        // Timing adjustment handled by TimingCoordinator
    }

    public void OnResume()
    {
        if (GameClock.Instance.IsPaused)
        {
            double pauseDuration = AudioSettings.dspTime - GameClock.Instance.GetPauseStartTime();
            nextTick += pauseDuration;
            startTick += pauseDuration;
        }
    }

    public void ResetToInitialState()
    {
        Debug.Log("[Metronome] Resetting to initial state");

        beatCount = 0;
        ticked = false;
        startTick = AudioSettings.dspTime;
        nextTick = startTick + timePerTick;

        Debug.Log("[Metronome] Reset complete");
    }
}