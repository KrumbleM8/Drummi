using System;
using UnityEngine;

/// <summary>
/// Owns all Bongo mode gameplay logic: beat generation, evaluation, visuals, and scoring.
/// Activated and cleaned up by GameManager. Fires OnModeComplete when the song ends.
/// </summary>
public class BongoModeController : MonoBehaviour
{
    #region Events
    /// <summary>Fired when the song ends. GameManager listens to trigger the results sequence.</summary>
    public event Action OnModeComplete;
    #endregion

    #region Inspector References
    [Header("Bongo Mode Systems")]
    [SerializeField] private BeatGenerator beatGenerator;
    [SerializeField] private BeatEvaluator beatEvaluator;
    [SerializeField] private BeatVisualScheduler visualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisual;

    [Header("Song Progression")]
    [SerializeField] private int barsBeforeEndForFinalBar = 1;
    #endregion

    #region Public Properties
    public int BarsBeforeEndForFinalBar => barsBeforeEndForFinalBar;
    public int Score => beatEvaluator != null ? beatEvaluator.Score : 0;
    public int TotalPerfectHits => beatEvaluator != null ? beatEvaluator.TotalPerfectHits : 0;
    #endregion

    #region Lifecycle
    private void OnEnable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnSongComplete += HandleSongComplete;
        }
    }

    private void OnDisable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnSongComplete -= HandleSongComplete;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Called by GameManager after shared systems (TimingCoordinator, Metronome, GameClock) are ready.
    /// </summary>
    /// <param name="bpm">Current song BPM, sourced from Metronome.</param>
    /// <param name="virtualStartTime">Virtual start time from GameClock.</param>
    /// <param name="realDspStartTime">Real DSP start time for audio scheduling.</param>
    public void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
    {
        Debug.Log("[BongoModeController] Starting Bongo mode");

        InitializeSystems(bpm);
        SynchronizeVisuals();
        ScheduleMusic(realDspStartTime);

        beatGenerator.StartGameplay(virtualStartTime);

        Debug.Log("[BongoModeController] Bongo mode active");
    }

    public void SetDifficulty(int difficultyIndex)
    {
        if (beatGenerator != null)
        {
            beatGenerator.difficultyIndex = difficultyIndex;
            Debug.Log($"[BongoModeController] Difficulty set to {difficultyIndex}");
        }
    }

    /// <summary>
    /// Returns total beats for the selected song. Called by GameManager before StartMode
    /// so TimingCoordinator can be initialized with the correct value.
    /// </summary>
    public int CalculateTotalBeats(int bpm)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogError("[BongoModeController] AudioManager missing - cannot calculate total beats");
            return 0;
        }

        AudioClip clip = AudioManager.instance.musicTracks[AudioManager.instance.selectedSongIndex];
        if (clip == null)
        {
            Debug.LogError("[BongoModeController] Song clip is null!");
            return 0;
        }

        double beatInterval = 60.0 / bpm;
        int totalBeats = Mathf.FloorToInt((float)(clip.length / beatInterval));

        Debug.Log($"[BongoModeController] Song: {clip.name} | BPM: {bpm} | Beats: {totalBeats} | Duration: {clip.length:F2}s");

        return totalBeats;
    }

    /// <summary>Called by GameManager during the results sequence.</summary>
    public void Cleanup()
    {
        if (visualScheduler != null)
        {
            visualScheduler.CleanupAndDisable();
            Debug.Log("[BongoModeController] BeatVisualScheduler cleaned up");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.CleanupAndDisable();
            Debug.Log("[BongoModeController] PlayerInputVisualHandler cleaned up");
        }

        if (beatGenerator != null)
        {
            beatGenerator.ResetToInitialState();
            Debug.Log("[BongoModeController] BeatGenerator cleaned up");
        }
    }

    /// <summary>Full reset to pre-game state. Called by GameManager.ResetGameValues().</summary>
    public void ResetToInitialState()
    {
        if (beatGenerator != null) beatGenerator.ResetToInitialState();
        if (visualScheduler != null) visualScheduler.ResetToInitialState();
        if (playerInputVisual != null) playerInputVisual.ResetToInitialState();
        if (beatEvaluator != null) beatEvaluator.ResetScore();

        Debug.Log("[BongoModeController] Reset to initial state");
    }
    #endregion

    #region Private - Initialization
    private void InitializeSystems(int bpm)
    {
        if (beatGenerator != null)
        {
            beatGenerator.enabled = true;
            beatGenerator.Initialize(bpm, AudioManager.instance.selectedSongIndex);
            Debug.Log("[BongoModeController] BeatGenerator initialized");
        }

        if (visualScheduler != null)
        {
            visualScheduler.enabled = true;
            visualScheduler.InitalizeBeatValues();
            Debug.Log("[BongoModeController] BeatVisualScheduler initialized");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.enabled = true;
            playerInputVisual.InitializeBeatValues();
            Debug.Log("[BongoModeController] PlayerInputVisualHandler initialized");
        }
    }

    private void SynchronizeVisuals()
    {
        if (visualScheduler != null)
        {
            visualScheduler.SyncWithTimingCoordinator();
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.SyncWithTimingCoordinator();
        }

        Debug.Log("[BongoModeController] Visuals synchronized with TimingCoordinator");
    }

    private void ScheduleMusic(double realDspStartTime)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogError("[BongoModeController] AudioManager missing - cannot schedule music");
            return;
        }

        AudioManager.instance.scheduledStartTime = realDspStartTime;
        AudioManager.instance.PlayMusic();

        Debug.Log($"[BongoModeController] Music scheduled at DSP: {realDspStartTime:F4}");
    }
    #endregion

    #region Private - Song Complete
    private void HandleSongComplete()
    {
        Debug.Log("[BongoModeController] Song complete");
        OnModeComplete?.Invoke();
    }
    #endregion
}