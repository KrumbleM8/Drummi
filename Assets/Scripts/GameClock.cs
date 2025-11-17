using UnityEngine;

public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }
    
    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private bool isPaused = false;
    
    public double GameTime => AudioSettings.dspTime - totalPausedTime;
    public bool IsPaused => isPaused;
    
    void Awake() => Instance = this;
    
    public void Pause()
    {
        if (isPaused) return;
        isPaused = true;
        pauseStartTime = AudioSettings.dspTime;
    }
    
    public void Resume()
    {
        if (!isPaused) return;
        totalPausedTime += AudioSettings.dspTime - pauseStartTime;
        isPaused = false;
    }

    public double GetTotalPauseTime()
    {
        return totalPausedTime;
    }
    public double GetPauseStartTime()
    {
        return pauseStartTime;
    }
}