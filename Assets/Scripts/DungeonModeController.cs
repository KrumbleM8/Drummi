using UnityEngine;

/// <summary>
/// ModeController subclass for Dungeon mode.
/// Registers as "Dungeon" with GameManager.
/// Delegates all gameplay logic to DungeonBeatManager, DungeonEvaluator,
/// and DungeonVisualController — mirrors BongoModeController's structure exactly.
///
/// ADDING TO GAMEMANAGER:
///   1. Drag this component into GameManager's modeControllers list.
///   2. Call GameManager.instance.SetMode("Dungeon") before StartGame().
/// </summary>
public class DungeonModeController : ModeController
{
    #region Inspector
    [Header("Dungeon Mode Systems")]
    [SerializeField] private DungeonBeatManager      beatManager;
    [SerializeField] private DungeonEvaluator         evaluator;
    [SerializeField] private DungeonVisualController  visualController;
    [SerializeField] private DungeonHealth            health;

    [Header("Song Progression")]
    [SerializeField] private int barsBeforeEndForFinalBar = 1;
    #endregion

    #region ModeController — Identity & Properties
    public override string ModeId                => "Dungeon";
    public override int    BarsBeforeEndForFinalBar => barsBeforeEndForFinalBar;
    public override int    Score                 => evaluator != null ? evaluator.Score : 0;
    public override int    TotalPerfectHits      => evaluator != null ? evaluator.TotalPerfectHits : 0;
    public override bool   IsNewHighScore        => evaluator != null && evaluator.IsNewHighScore;
    #endregion

    #region ModeController — Lifecycle
    public override int CalculateTotalBeats(int bpm)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogError("[DungeonModeController] AudioManager missing — cannot calculate total beats");
            return 0;
        }

        var clip = AudioManager.instance.musicTracks[AudioManager.instance.selectedSongIndex];
        if (clip == null)
        {
            Debug.LogError("[DungeonModeController] Song clip is null");
            return 0;
        }

        int beats = Mathf.FloorToInt((float)(clip.length / (60.0 / bpm)));
        Debug.Log($"[DungeonModeController] {clip.name} | BPM: {bpm} | Beats: {beats}");
        return beats;
    }

    public override void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
    {
        Debug.Log("[DungeonModeController] Starting Dungeon mode");

        if (evaluator      != null) evaluator.ResetScore();
        if (health         != null) health.ResetHealth();

        // Initialize BeatManager first (sets metronome.bpm)
        if (beatManager    != null)
        {
            beatManager.enabled = true;
            beatManager.Initialize(bpm);
        }

        // Initialize visuals after BPM is set (reads metronome.bpm)
        if (visualController != null) visualController.Initialize();

        ScheduleMusic(realDspStartTime);

        if (beatManager != null) beatManager.StartGameplay(virtualStartTime);

        Debug.Log("[DungeonModeController] Dungeon mode active");
    }

    public override void Cleanup()
    {
        if (evaluator        != null) evaluator.SaveHighScore();
        if (visualController != null) visualController.CleanupAndDisable();
        if (beatManager      != null) beatManager.ResetToInitialState();
    }

    public override void ResetToInitialState()
    {
        if (beatManager      != null) beatManager.ResetToInitialState();
        if (visualController != null) visualController.ResetVisuals();
        if (evaluator        != null) evaluator.ResetScore();
        if (health           != null) health.ResetHealth();
        Debug.Log("[DungeonModeController] Reset to initial state");
    }

    public override void OnPause()
    {
        if (evaluator   != null) evaluator.SaveHighScore();
        if (beatManager != null && beatManager.enabled) beatManager.OnPause();
    }

    public override void OnResume()
    {
        if (beatManager != null && beatManager.enabled) beatManager.OnResume();
    }
    #endregion

    #region Unity — Event Subscription
    private void OnEnable()
    {
        if (beatManager != null) beatManager.OnSongComplete += HandleSongComplete;
    }

    private void OnDisable()
    {
        if (beatManager != null) beatManager.OnSongComplete -= HandleSongComplete;
    }
    #endregion

    #region Private
    private void ScheduleMusic(double realDspStartTime)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogError("[DungeonModeController] AudioManager missing — cannot schedule music");
            return;
        }
        AudioManager.instance.scheduledStartTime = realDspStartTime;
        AudioManager.instance.PlayMusic();
        Debug.Log($"[DungeonModeController] Music scheduled at DSP: {realDspStartTime:F4}");
    }

    private void HandleSongComplete()
    {
        Debug.Log("[DungeonModeController] Song complete");
        CompletMode();
    }
    #endregion
}
