using UnityEngine;

/// <summary>
/// Handles pause/resume logic for shared systems (GameClock, Audio, Metronome, Time).
/// Mode-specific pause behaviour is delegated to the active ModeController via OnPause/OnResume.
/// </summary>
public class PauseHandler : MonoBehaviour
{
    [SerializeField] private Metronome metronome;

    public bool IsPaused => GameClock.Instance.IsPaused;

    public void TogglePause()
    {
        if (!GameClock.Instance.IsPaused)
        {
            // === PAUSE ===
            Debug.Log("[PauseHandler] === PAUSING ===");

            // 1. Notify the active mode first (cancel scheduled audio, stop coroutines)
            GameManager.instance.ActiveMode?.OnPause();

            // 2. Pause shared timing systems
            if (metronome && metronome.enabled) metronome.OnPause();
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

            // 3. Resume shared timing systems
            if (metronome && metronome.enabled) metronome.OnResume();

            // 4. Notify the active mode to reschedule (reconverts virtual→real with new totalPausedTime)
            GameManager.instance.ActiveMode?.OnResume();

            Debug.Log($"[PauseHandler] Game resumed - virtual time continues from {GameClock.Instance.GameTime:F4}");
        }
    }
}
