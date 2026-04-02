using UnityEngine;

/// <summary>
/// Abstract base class for all Drummi game modes.
/// Defines the contract GameManager depends on — every mode must implement these.
///
/// ADDING A NEW MODE:
///   1. Create a new MonoBehaviour that extends ModeController.
///   2. Implement all abstract members.
///   3. Add it as a component to any GameObject in the scene.
///   4. Register it in the GameManager inspector's modeControllers list.
///   5. Call GameManager.instance.SetMode("YourModeName") before StartGame().
///
/// MODE LIFECYCLE (called by GameManager in order):
///   CalculateTotalBeats() → StartMode() → [game runs] → Cleanup() → ResetToInitialState()
/// </summary>
public abstract class ModeController : MonoBehaviour
{
    // ── Identity ──────────────────────────────────────────────────────────

    /// <summary>
    /// Unique identifier for this mode. Used by GameManager.SetMode(string).
    /// Should be a stable, human-readable key e.g. "Bongo", "GuitarHero", "WorldRhythms".
    /// </summary>
    public abstract string ModeId { get; }

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fire this when the mode has finished — win condition met, song ended, etc.
    /// GameManager subscribes to this to trigger the results sequence.
    /// </summary>
    public event System.Action OnModeComplete;

    // ── Lifecycle — implemented by each mode ──────────────────────────────

    /// <summary>
    /// Called by GameManager during StartGameSequence to determine total beats.
    /// Used to initialise TimingCoordinator.
    /// </summary>
    public abstract int CalculateTotalBeats(int bpm);

    /// <summary>
    /// How many bars before song end the final pattern should be generated.
    /// Passed directly to TimingCoordinator.Initialize().
    /// Return 1 if your mode has no concept of a "final bar".
    /// </summary>
    public abstract int BarsBeforeEndForFinalBar { get; }

    /// <summary>
    /// Begin mode-specific gameplay. Called after shared systems are ready.
    /// </summary>
    /// <param name="bpm">Current BPM from Metronome.</param>
    /// <param name="virtualStartTime">GameClock virtual time of game start.</param>
    /// <param name="realDspStartTime">Real DSP time for audio scheduling.</param>
    public abstract void StartMode(int bpm, double virtualStartTime, double realDspStartTime);

    /// <summary>
    /// Called after the results transition cover is complete.
    /// Stop coroutines, unsubscribe events, disable spawners, etc.
    /// Do NOT destroy GameObjects here — use ResetToInitialState for that.
    /// </summary>
    public abstract void Cleanup();

    /// <summary>
    /// Full reset back to pre-game state. Called by GameManager.ResetGameValues().
    /// Should leave the mode ready to call StartMode() again cleanly.
    /// </summary>
    public abstract void ResetToInitialState();

    // ── Scoring — override if mode tracks score ───────────────────────────

    /// <summary>Final score. Read by GameManager to pass to ScoreScreen.</summary>
    public virtual int Score => 0;

    /// <summary>Total perfect hits. Read by GameManager to pass to ScoreScreen.</summary>
    public virtual int TotalPerfectHits => 0;

    // ── Difficulty — override if mode supports difficulty ─────────────────

    /// <summary>
    /// Set difficulty level. Called by GameManager.SetDifficulty().
    /// Default is a no-op — override if your mode supports difficulty.
    /// </summary>
    public virtual void SetDifficulty(int difficultyIndex) { }

    // ── Protected Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Invoke from your mode implementation when gameplay is complete.
    /// </summary>
    protected void CompletMode() => OnModeComplete?.Invoke();
}
