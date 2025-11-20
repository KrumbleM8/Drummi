using UnityEngine;

/// <summary>
/// Manages pause state and provides virtual time (with pauses removed).
/// Virtual time = DSP time with all pause durations subtracted.
/// All game logic should use virtual time (GameTime).
/// Audio scheduling must convert to real DSP time (VirtualToRealDsp).
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private double lastPauseDuration = 0.0;
    private bool isPaused = false;

    /// <summary>
    /// Virtual game time - DSP time with pauses removed.
    /// This is what all game logic should use.
    /// When paused, this time freezes.
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

    /// <summary>
    /// Convert virtual time to real DSP time for audio scheduling.
    /// Use this when calling AudioSource.PlayScheduled().
    /// Real DSP = Virtual + Total Paused Time
    /// </summary>
    public double VirtualToRealDsp(double virtualTime)
    {
        return virtualTime + totalPausedTime;
    }

    /// <summary>
    /// Convert real DSP time to virtual time.
    /// Rarely needed, but available for symmetry.
    /// Virtual = Real DSP - Total Paused Time
    /// </summary>
    public double RealDspToVirtual(double realDspTime)
    {
        return realDspTime - totalPausedTime;
    }

    public void Pause()
    {
        if (isPaused) return;

        isPaused = true;
        pauseStartTime = AudioSettings.dspTime;

        Debug.Log($"[GameClock] === PAUSED ===");
        Debug.Log($"  Real DSP: {pauseStartTime:F4}");
        Debug.Log($"  Virtual: {GameTime:F4}");
        Debug.Log($"  Total paused so far: {totalPausedTime:F4}s");
    }

    public void Resume()
    {
        if (!isPaused) return;

        lastPauseDuration = AudioSettings.dspTime - pauseStartTime;
        totalPausedTime += lastPauseDuration;
        isPaused = false;

        Debug.Log($"[GameClock] === RESUMED ===");
        Debug.Log($"  Pause duration: {lastPauseDuration:F4}s");
        Debug.Log($"  Total paused: {totalPausedTime:F4}s");
        Debug.Log($"  Virtual time: {GameTime:F4}");
    }

    public double GetLastPauseDuration() => lastPauseDuration;
    public double GetTotalPausedTime() => totalPausedTime;
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