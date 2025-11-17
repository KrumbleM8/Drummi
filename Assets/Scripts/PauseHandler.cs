using UnityEngine;

public class PauseHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Metronome metronome;
    [SerializeField] private BeatGenerator beatGenerator;
    [SerializeField] private BeatEvaluator beatEvaluator;
    [SerializeField] private BeatVisualScheduler visualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisualHandler;

    public bool IsPaused => Time.timeScale == 0f;

    public void TogglePause()
    {
        // Preserve original behavior: save on every toggle.
        if (beatEvaluator != null) beatEvaluator.SaveHighScore();

        if (Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            GameClock.Instance.Pause();
            if (AudioManager.instance != null) AudioManager.instance.PauseAllAudio();

            if (metronome && metronome.enabled) metronome.OnPause();
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnPause();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnPause();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnPause();
        }
        else
        {
            Time.timeScale = 1f;
            GameClock.Instance.Resume();
            if (AudioManager.instance != null) AudioManager.instance.ResumeAllAudio();

            if (metronome && metronome.enabled) metronome.OnResume();
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnResume();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnResume();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnResume();
        }
    }
}
