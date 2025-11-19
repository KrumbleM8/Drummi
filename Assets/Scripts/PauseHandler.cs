using UnityEngine;

/// <summary>
/// Handles pause/resume logic with TimingCoordinator integration.
/// </summary>
public class PauseHandler : MonoBehaviour
{
    [SerializeField] private Metronome metronome;
    [SerializeField] private BeatGenerator beatGenerator;
    [SerializeField] private BeatEvaluator beatEvaluator;
    [SerializeField] private BeatVisualScheduler visualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisualHandler;

    public bool IsPaused => GameClock.Instance.IsPaused;

    public void TogglePause()
    {
        // Save high score on every pause toggle
        if (beatEvaluator != null)
            beatEvaluator.SaveHighScore();

        if (!GameClock.Instance.IsPaused)
        {
            // === PAUSE ===
            GameClock.Instance.Pause(); // Pause clock FIRST

            Time.timeScale = 0f;
            if (AudioManager.instance != null)
                AudioManager.instance.PauseAllAudio();

            // Notify all systems
            if (metronome && metronome.enabled) metronome.OnPause();
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnPause();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnPause();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnPause();

            Debug.Log("[PauseHandler] Game paused");
        }
        else
        {
            // === RESUME ===
            Time.timeScale = 1f;
            if (AudioManager.instance != null)
                AudioManager.instance.ResumeAllAudio();

            // Get pause duration from GameClock
            double pauseDuration = GameClock.Instance.GetLastPauseDuration();

            // Notify TimingCoordinator FIRST (it adjusts all future timing)
            if (TimingCoordinator.Instance != null)
            {
                TimingCoordinator.Instance.AdjustForPause(pauseDuration);
            }

            // Notify all systems AFTER coordinator has adjusted timing
            if (metronome && metronome.enabled) metronome.OnResume();
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnResume();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnResume();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnResume();

            GameClock.Instance.Resume(); // Resume clock LAST

            Debug.Log($"[PauseHandler] Game resumed (pause duration: {pauseDuration:F3}s)");
        }
    }
}