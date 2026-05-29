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

    // True until the first StartRoom() call in a run. Set back to true by ResetToInitialState().
    private bool _isFirstRoom = true;

    // Virtual time at which the run's first bar started — used to calculate bar boundaries
    // for subsequent rooms so the beat grid stays phase-aligned to the music.
    private double _sessionStartVirtualTime;

    // When true, StartMode skips ScheduleMusic() once (reset to false immediately after).
    // Used by StartRoom() for rooms where the music continues uninterrupted.
    private bool _suppressMusicSchedule;

    // When true, StartMode passes skipGracePeriod=true to BeatManager.Initialize() once.
    // Set by StartRoom() for all rooms after the first.
    private bool _skipGracePeriod;

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
            beatManager.Initialize(bpm, _pendingPatternDurations, _skipGracePeriod);
            _pendingPatternDurations = null;
            _skipGracePeriod         = false;
        }

        // Initialize visuals after BPM is set (reads metronome.bpm)
        // Re-enable in case CleanupAndDisable() left it disabled from a previous round.
        if (visualController != null)
        {
            visualController.enabled = true;
            visualController.Initialize();
        }

        // Skip music scheduling when StartRoom has already handled it (continuous or swapped).
        if (!_suppressMusicSchedule)
            ScheduleMusic(realDspStartTime);
        _suppressMusicSchedule = false;

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
    /// On the first room of a run this behaves identically to the old implementation:
    /// it resets timing systems and schedules the music from scratch.
    /// On subsequent rooms the music continues uninterrupted and the new BeatManager
    /// session is aligned to the next bar boundary so patterns stay phase-locked.
    /// If <see cref="RoomDefinition.OverrideBgm"/> is true the music is swapped at
    /// the same seam point via <see cref="AudioManager.ScheduleTrackSwap"/>.
    /// </summary>
    public void StartRoom(RoomDefinition def)
    {
        if (def == null)
        {
            Debug.LogError("[DungeonModeController] StartRoom: RoomDefinition is null");
            return;
        }

        int bpm = (beatManager?.metronome != null) ? (int)beatManager.metronome.bpm : 120;
        _pendingPatternDurations = def.PatternDurations;

        if (_isFirstRoom)
        {
            _isFirstRoom = false;

            // ── First room: full reset, schedule music fresh ─────────────────
            if (AudioManager.instance != null)
                AudioManager.instance.selectedSongIndex = def.BgmTrackIndex;

            GameClock.Instance?.Reset();
            AudioManager.instance?.ResetState();

            const double LOOKAHEAD  = 0.05;
            double baseStartTime    = AudioSettings.dspTime + LOOKAHEAD;
            double virtualStartTime = GameClock.Instance != null
                ? GameClock.Instance.RealDspToVirtual(baseStartTime)
                : baseStartTime;

            _sessionStartVirtualTime = virtualStartTime;

            if (beatManager?.metronome != null)
            {
                beatManager.metronome.InitializeWithStartTime(virtualStartTime);
                beatManager.metronome.enabled = true;
            }

            int totalBeats = CalculateTotalBeats(bpm);
            TimingCoordinator.Instance?.Initialize(virtualStartTime, bpm, totalBeats, BarsBeforeEndForFinalBar);

            // StartMode will call ScheduleMusic normally (flag is false).
            StartMode(bpm, virtualStartTime, baseStartTime);

            Debug.Log($"[DungeonModeController] StartRoom (first) — Room: '{def.RoomName}', BPM: {bpm}");
        }
        else
        {
            // ── Subsequent room: keep GameClock + music running ───────────────
            // Find the next bar boundary in virtual time so patterns stay
            // phase-aligned to the already-playing music.
            double beatDuration = 60.0 / bpm;
            double barDuration  = GameConstants.BEATS_PER_LOOP * beatDuration;

            double currentVirtual = GameClock.Instance != null ? GameClock.Instance.GameTime : 0.0;
            double elapsed        = currentVirtual - _sessionStartVirtualTime;
            int    barsElapsed    = Mathf.FloorToInt((float)(elapsed / barDuration));
            double seamVirtual    = _sessionStartVirtualTime + (barsElapsed + 1) * barDuration;

            // Safety: if the transition animation has eaten into the next bar,
            // push one bar further so we never schedule in the past.
            const double MIN_LOOKAHEAD = 0.1;
            if (seamVirtual - currentVirtual < MIN_LOOKAHEAD)
                seamVirtual += barDuration;

            double seamDsp = GameClock.Instance != null
                ? GameClock.Instance.VirtualToRealDsp(seamVirtual)
                : AudioSettings.dspTime + barDuration;

            if (def.OverrideBgm)
            {
                // Swap to a different track right on the bar boundary.
                AudioManager.instance?.ScheduleTrackSwap(def.BgmTrackIndex, seamDsp);
            }
            // else: music plays through — no audio changes needed.

            if (beatManager?.metronome != null)
            {
                beatManager.metronome.InitializeWithStartTime(seamVirtual);
                beatManager.metronome.enabled = true;
            }

            // Prefer designer-specified room length; fall back to clip length only for
            // override rooms (where the new clip's duration is meaningful).
            int totalBeats = def.RoomLengthBars > 0
                ? def.RoomLengthBars * GameConstants.BEATS_PER_LOOP
                : CalculateTotalBeats(bpm);
            TimingCoordinator.Instance?.Initialize(seamVirtual, bpm, totalBeats, BarsBeforeEndForFinalBar);

            // Tell StartMode not to call ScheduleMusic — the audio is already handled above.
            // Also skip the grace period so enemies appear immediately.
            _suppressMusicSchedule = true;
            _skipGracePeriod       = true;
            StartMode(bpm, seamVirtual, seamDsp);

            Debug.Log($"[DungeonModeController] StartRoom (continuing) — Room: '{def.RoomName}', seam virtual: {seamVirtual:F4}, override: {def.OverrideBgm}");
        }
    }

    /// <summary>
    /// Clears all visual state (enemies, indicators, slider) immediately without
    /// touching audio, score, or health. Call before the next room's reveal begins
    /// so old visuals are gone while the screen is still covered.
    /// </summary>
    public void ClearVisuals()
    {
        if (visualController != null) visualController.Initialize();
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
        _isFirstRoom             = true;
        _suppressMusicSchedule   = false;
        _skipGracePeriod         = false;
        _sessionStartVirtualTime = 0.0;
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
