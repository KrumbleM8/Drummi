using UnityEngine;

/// <summary>
/// Handles pause/resume logic with virtual time system.
/// Virtual time automatically handles pause duration - no manual adjustment needed!
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
            Debug.Log("[PauseHandler] === PAUSING ===");

            // 1. Notify systems FIRST (cancel scheduled audio, stop coroutines)
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnPause();
            if (metronome && metronome.enabled) metronome.OnPause();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnPause();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnPause();

            // 2. Pause GameClock (records pause start, freezes virtual time)
            GameClock.Instance.Pause();

            // 3. Pause time and audio
            Time.timeScale = 0f;
            if (AudioManager.instance != null)
                AudioManager.instance.PauseAllAudio();

            Debug.Log("[PauseHandler] Game paused - virtual time frozen");
        }
        else
        {
            // === RESUME ===
            Debug.Log("[PauseHandler] === RESUMING ===");

            // 1. Resume GameClock FIRST (updates totalPausedTime)
            GameClock.Instance.Resume();
            double pauseDuration = GameClock.Instance.GetLastPauseDuration();

            Debug.Log($"[PauseHandler] Pause duration: {pauseDuration:F3}s, Total paused: {GameClock.Instance.GetTotalPausedTime():F3}s");

            // 2. Resume time and audio
            Time.timeScale = 1f;
            if (AudioManager.instance != null)
                AudioManager.instance.ResumeAllAudio();

            // 3. NO NEED to adjust TimingCoordinator!
            //    Virtual time system handles it automatically via VirtualToRealDsp()

            // 4. Notify systems to reschedule (they reconvert virtual→real with new totalPausedTime)
            if (metronome && metronome.enabled) metronome.OnResume();
            if (beatGenerator && beatGenerator.enabled) beatGenerator.OnResume();
            if (playerInputVisualHandler && playerInputVisualHandler.enabled) playerInputVisualHandler.OnResume();
            if (visualScheduler && visualScheduler.enabled) visualScheduler.OnResume();

            Debug.Log($"[PauseHandler] Game resumed - virtual time continues from {GameClock.Instance.GameTime:F4}");
        }
    }
}