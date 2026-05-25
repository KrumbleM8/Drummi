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

    // Stashed by StartRoom(); consumed and cleared on the next StartMode() call.
    private float[] _pendingPatternDurations;

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

        // Score and health are NOT reset here — this method fires on every room start.
        // Reset is done once at run/session start: DungeonRunner.StartRun() for the
        // roguelike path, GameManager.StartGameSequence() for standalone play.

        // Initialize BeatManager first (sets metronome.bpm).
        // _pendingPatternDurations is non-null when called via StartRoom(); null otherwise (uses defaults).
        if (beatManager    != null)
        {
            beatManager.enabled = true;
            beatManager.Initialize(bpm, _pendingPatternDurations);
            _pendingPatternDurations = null;
        }

        // Initialize visuals after BPM is set (reads metronome.bpm)
        // Re-enable in case CleanupAndDisable() left it disabled from a previous round.
        if (visualController != null)
        {
            visualController.enabled = true;
            visualController.Initialize();
        }

        ScheduleMusic(realDspStartTime);

        if (beatManager != null) beatManager.StartGameplay(virtualStartTime);

        Debug.Log("[DungeonModeController] Dungeon mode active");
    }

    /// <summary>
    /// Overload for callers that need to specify pattern durations without a full RoomDefinition
    /// (e.g. DungeonRunner, test harnesses).
    /// Stashes <paramref name="patternDurations"/> so the existing StartMode(bpm, …) path
    /// passes them to DungeonBeatManager.Initialize(); falls through to default durations if null.
    /// Timing setup mirrors StartRoom — use StartRoom when a full RoomDefinition is available.
    /// </summary>
    /// <param name="patternDurations">
    /// Duration weights for DungeonPatternGenerator, matching the parameter name in
    /// DungeonBeatManager.Initialize(int bpm, float[] patternDurations).
    /// Pass null to use the default weights { 1f, 0.5f }.
    /// </param>
    public void StartMode(float[] patternDurations)
    {
        // Standalone (non-run) entry point — reset once here since there is no
        // DungeonRunner.StartRun() call to do it.
        if (evaluator != null) evaluator.ResetScore();
        if (health    != null) health.ResetHealth();

        _pendingPatternDurations = (patternDurations != null && patternDurations.Length > 0)
            ? patternDurations
            : null;

        GameClock.Instance?.Reset();
        AudioManager.instance?.ResetState();

        const double LOOKAHEAD  = 0.05;
        double baseStartTime    = AudioSettings.dspTime + LOOKAHEAD;
        double virtualStartTime = GameClock.Instance != null
            ? GameClock.Instance.RealDspToVirtual(baseStartTime)
            : baseStartTime;

        int bpm = (beatManager?.metronome != null) ? (int)beatManager.metronome.bpm : 120;

        if (beatManager?.metronome != null)
        {
            beatManager.metronome.InitializeWithStartTime(virtualStartTime);
            beatManager.metronome.enabled = true;
        }

        int totalBeats = CalculateTotalBeats(bpm);
        TimingCoordinator.Instance?.Initialize(virtualStartTime, bpm, totalBeats, BarsBeforeEndForFinalBar);

        StartMode(bpm, virtualStartTime, baseStartTime);
    }

    /// <summary>
    /// High-level entry point called by RoomController instead of GameManager.StartGame().
    /// Sets the BGM track and pattern durations from <paramref name="def"/>, resets timing
    /// systems, then drives the same startup sequence as GameManager.StartGameSequence()
    /// (minus screen transitions — those are RoomController's responsibility).
    /// </summary>
    public void StartRoom(RoomDefinition def)
    {
        if (def == null)
        {
            Debug.LogError("[DungeonModeController] StartRoom: RoomDefinition is null");
            return;
        }

        // Apply room-specific BGM track
        if (AudioManager.instance != null)
            AudioManager.instance.selectedSongIndex = def.BgmTrackIndex;

        // Stash pattern durations — StartMode will pick them up and clear the field
        _pendingPatternDurations = def.PatternDurations;

        // Reset timing systems (mirrors GameManager.StartGameSequence)
        GameClock.Instance?.Reset();
        AudioManager.instance?.ResetState();

        const double LOOKAHEAD     = 0.05;
        double baseStartTime       = AudioSettings.dspTime + LOOKAHEAD;
        double virtualStartTime    = GameClock.Instance != null
            ? GameClock.Instance.RealDspToVirtual(baseStartTime)
            : baseStartTime;

        int bpm = (beatManager?.metronome != null) ? (int)beatManager.metronome.bpm : 120;

        if (beatManager?.metronome != null)
        {
            beatManager.metronome.InitializeWithStartTime(virtualStartTime);
            beatManager.metronome.enabled = true;
        }

        int totalBeats = CalculateTotalBeats(bpm);
        TimingCoordinator.Instance?.Initialize(virtualStartTime, bpm, totalBeats, BarsBeforeEndForFinalBar);

        StartMode(bpm, virtualStartTime, baseStartTime);

        Debug.Log($"[DungeonModeController] StartRoom — Room: '{def.RoomName}', Tier: {def.Tier}, BPM: {bpm}");
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
