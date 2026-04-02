using UnityEngine;

/// <summary>
/// Guitar Hero mode controller — wires the ArcadeGameController / NoteSpawner /
/// RhythmLaneManager / RhythmScoreTracker system into Drummi's GameManager lifecycle.
///
/// SCENE SETUP:
///   1. Add this component to any GameObject in the Arcade scene.
///   2. Assign arcadeGameController (the RhythmManager GameObject's controller).
///   3. Assign scoreTracker.
///   4. Drag into GameManager > modeControllers list.
///   5. Call GameManager.instance.SetMode("GuitarHero") from a menu button.
///
/// NOTE:
///   ArcadeGameController.StartGame() handles its own audio scheduling internally.
///   This mode controller bridges the GameManager's shared timing init with
///   ArcadeGameController's per-session start, so both use the same virtual start time.
/// </summary>
public class ArcadeModeController : ModeController
{
    // ── ModeController identity ───────────────────────────────────────────

    public override string ModeId => "Arcade";

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Guitar Hero System")]
    [Tooltip("The RhythmGameController on the RhythmManager GameObject.")]
    [SerializeField] private ArcadeGameController arcadeGameController;

    [Tooltip("The RhythmScoreTracker in the scene.")]
    [SerializeField] private RhythmScoreTracker scoreTracker;

    [Header("Song Chart")]
    [Tooltip("Active chart. Must match the SongChart assigned to NoteSpawner.")]
    [SerializeField] private SongChart chart;

    [Header("Bars Before End")]
    public override int BarsBeforeEndForFinalBar => 1;

    // ── Scoring ───────────────────────────────────────────────────────────

    public override int Score => scoreTracker != null ? scoreTracker.Score : 0;
    public override int TotalPerfectHits => scoreTracker != null ? scoreTracker.MaxCombo : 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override int CalculateTotalBeats(int bpm)
    {
        if (chart == null)
        {
            Debug.LogError("[GuitarHeroMode] No SongChart assigned — defaulting to 128 beats.");
            return 128;
        }

        // If the chart has an audio clip, derive from clip length for accuracy
        if (chart.audioClip != null)
            return Mathf.RoundToInt(chart.audioClip.length / chart.SecondsPerBeat);

        // Otherwise use the loop length * a fixed number of loops
        return Mathf.RoundToInt(chart.loopLengthBeats) * 4;
    }

    private double _virtualStartTime;
    private double _realDspStartTime;

    public override void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
    {
        if (arcadeGameController == null)
        {
            Debug.LogError("[GuitarHeroMode] RhythmGameController not assigned.");
            return;
        }

        _virtualStartTime = virtualStartTime;
        _realDspStartTime = realDspStartTime;

        if (scoreTracker != null)
            scoreTracker.ResetScore();

        arcadeGameController.OnSongComplete += HandleSongComplete;

        // Pass the GameManager's synchronized timing anchor through so the spawner
        // and audio are anchored to the same clock as TimingCoordinator.
        arcadeGameController.StartGameWithTiming(virtualStartTime, realDspStartTime);

        Debug.Log("[GuitarHeroMode] Mode started.");
    }

    public override void Cleanup()
    {
        if (arcadeGameController != null)
        {
            arcadeGameController.OnSongComplete -= HandleSongComplete;
            arcadeGameController.StopGame();
        }

        Debug.Log("[GuitarHeroMode] Cleanup complete.");
    }

    public override void ResetToInitialState()
    {
        scoreTracker?.ResetScore();
        Debug.Log("[GuitarHeroMode] Reset to initial state.");
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void HandleSongComplete()
    {
        Debug.Log("[GuitarHeroMode] Song complete — firing CompletMode().");
        CompletMode();
    }
}
