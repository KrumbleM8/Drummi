using UnityEngine;

/// <summary>
/// Simple pause tracker. Does NOT handle time conversion.
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private double lastPauseDuration = 0.0;
    private bool isPaused = false;

    /// <summary>
    /// Current DSP time with pauses removed. For display/UI only.
    /// </summary>
    public double GameTime => isPaused ?
        pauseStartTime - totalPausedTime :
        AudioSettings.dspTime - totalPausedTime;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Pause()
    {
        if (isPaused) return;

        isPaused = true;
        pauseStartTime = AudioSettings.dspTime;
        Debug.Log($"[GameClock] Paused at DSP: {pauseStartTime:F4}");
    }

    public void Resume()
    {
        if (!isPaused) return;

        lastPauseDuration = AudioSettings.dspTime - pauseStartTime;
        totalPausedTime += lastPauseDuration;
        isPaused = false;

        Debug.Log($"[GameClock] Resumed after {lastPauseDuration:F4}s");
    }

    public double GetLastPauseDuration() => lastPauseDuration;
    public double GetPauseStartTime() => pauseStartTime;

    public void Reset()
    {
        totalPausedTime = 0;
        pauseStartTime = 0;
        lastPauseDuration = 0;
        isPaused = false;
        Debug.Log("[GameClock] Reset");
    }
}