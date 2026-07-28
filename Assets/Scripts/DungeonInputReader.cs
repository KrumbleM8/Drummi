using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records Dungeon mode player inputs and triggers immediate visual feedback.
/// Parallel to BongoModeInputReader — subscribes to DrumPadTouch events (three pads),
/// maps left/center/right to DungeonEnemyType, and calls DungeonEvaluator for
/// per-hit quality feedback.
///
/// BAR-START COYOTE BUFFER (Bug 1 fix):
///   Inputs fired within barStartInputBufferSeconds before the input window opens are
///   stored and replayed the moment allowInput transitions to true.  Only one input is
///   buffered (last write wins); the buffer applies only to the first indicator of each
///   bar and is cleared on ResetInputs().  All timestamps use GameClock.GameTime
///   (virtual DSP time) rather than Time.time.
///
/// HIT RESOLUTION (Bug 2 fix):
///   EvaluateSingleInput now returns the exact DungeonScheduledBeat that was matched.
///   On a Perfect/Good hit, NotifyEnemyHit(matchedBeat) is called so the visual
///   controller can despawn the enemy paired to that specific beat instance.
///
/// INSPECTOR SETUP:
///   Assign drumPadTouch, evaluator, and visualController in the Inspector.
/// </summary>
public class DungeonInputReader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DrumPadTouch             drumPadTouch;
    [SerializeField] private DungeonEvaluator         evaluator;
    [SerializeField] private DungeonVisualController  visualController;
    [SerializeField] private DungeonHealth            health;

    [Header("Timing")]
    [SerializeField] private float barStartInputBufferSeconds = 0.125f;

    [Tooltip("Sub-frame race tolerance (virtual-time seconds): a tap landing AFTER the window's " +
             "scheduled open time but BEFORE the controlling script's Update has set allowInput " +
             "true that frame would otherwise be silently dropped. Taps up to this long past the " +
             "boundary are coyote-buffered and replayed at window open. Should stay >= one frame " +
             "at the lowest supported frame rate; may need tuning against mobile frame pacing.")]
    [SerializeField] private float windowOpenLateToleranceSeconds = 0.05f;

    /// <summary>
    /// Fired immediately after a Perfect or Good hit is confirmed by DungeonEvaluator.
    /// Subscribers (e.g. EliteEnemyController) can register damage without a direct
    /// reference back to DungeonInputReader.
    /// </summary>
    public event Action<DungeonEnemyType, InputMatch.MatchQuality> OnHit;

    // Controlled by DungeonBeatManager — false outside the response window.
    // Property wraps a backing field purely to fire AllowInputChanged on transitions;
    // gating semantics are identical to the previous public field.
    public bool allowInput
    {
        get => _allowInput;
        set
        {
            if (_allowInput == value) return;
            _allowInput = value;
            AllowInputChanged?.Invoke(value);
        }
    }
    private bool _allowInput = false;

    /// <summary>
    /// Fired whenever allowInput transitions (false↔true). Purely observational —
    /// subscribers (e.g. DrumPadStateVisual) react cosmetically and must never
    /// drive input logic from this event.
    /// </summary>
    public event Action<bool> AllowInputChanged;

    /// <summary>
    /// True only while a Dungeon encounter is actively running.
    /// Set by DungeonBeatManager on StartGameplay / HandleGameComplete / ClearState.
    /// Inputs received while false are silently ignored — no health loss, no evaluation.
    /// This prevents penalty application on the intro screen, during transitions,
    /// between rounds, and after game-over.
    /// </summary>
    public bool EncounterActive { get; set; } = false;

    /// <summary>
    /// When true, SpawnInputMarker calls are suppressed.
    /// Set by CallAndResponseController during C&R response windows — the standard bar
    /// slider is BarTiming-coupled and would place markers at wrong positions.
    /// Default false; standard combat is unaffected.
    /// </summary>
    public bool SuppressSpawnMarkers { get; set; }

    /// <summary>
    /// When set, replaces TimingCoordinator.CurrentBar.InputWindowStart for the coyote buffer.
    /// Inputs arriving within barStartInputBufferSeconds before this time are buffered and
    /// replayed when allowInput opens; all other early inputs are silently ignored (no penalty).
    /// Set by CallAndResponseController for the duration of each C&R call window; null otherwise.
    /// </summary>
    public double? CoyoteWindowStartOverride { get; set; }

    /// <summary>
    /// When non-null, every tap recorded during an open echo window is retagged to this type
    /// before being stored or evaluated. This collapses direction matching to timing-only grading
    /// (SingleDrum C&amp;R mode). Null in all other contexts — standard combat, Directional C&amp;R,
    /// and outside the echo window. Set and cleared exclusively by
    /// CallAndResponseController.OpenEchoWindow / CloseEchoWindow.
    /// </summary>
    public DungeonEnemyType? SingleDrumEchoConstant { get; set; }

    public List<DungeonInput> playerInputData = new();

    // Set by DungeonBeatManager each bar so single-input evaluation works
    private List<DungeonScheduledBeat> _scheduledBeats;
    private double _timeOffset;

    public void SetScheduledBeats(List<DungeonScheduledBeat> beats) => _scheduledBeats = beats;
    public void SetTimeOffset(double offset)                        => _timeOffset      = offset;

    // ── Bar-start coyote buffer (Bug 1) ───────────────────────────────────

    private struct BufferedInput
    {
        public DungeonEnemyType type;
        public double           virtualTime; // GameClock.GameTime when the input was recorded
    }
    private BufferedInput? _bufferedInput;
    private bool           _prevAllowInput;

    // ── Unity ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit   += OnLeft;
            drumPadTouch.OnCenterHit += OnCenter;
            drumPadTouch.OnRightHit  += OnRight;
        }
        else
        {
            Debug.LogError("[DungeonInputReader] DrumPadTouch not assigned!");
        }
    }

    private void OnDisable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit   -= OnLeft;
            drumPadTouch.OnCenterHit -= OnCenter;
            drumPadTouch.OnRightHit  -= OnRight;
        }
    }

    private void Update()
    {
        // Detect the moment allowInput transitions false → true and replay any
        // buffered input from the coyote window just before the input phase opened.
        bool transitioned = allowInput && !_prevAllowInput;
        _prevAllowInput   = allowInput;
        if (transitioned) ReplayBuffer();
    }

    // ── Event Handlers ────────────────────────────────────────────────────

    private void OnLeft()   { AudioManager.instance?.PlayPadLeft();  TriggerInput(DungeonEnemyType.Left);   }
    private void OnCenter() { AudioManager.instance?.PlayPadCenter();  TriggerInput(DungeonEnemyType.Center); }
    private void OnRight()  { AudioManager.instance?.PlayPadRight(); TriggerInput(DungeonEnemyType.Right);  }

    // ── Core ──────────────────────────────────────────────────────────────

    public void TriggerInput(DungeonEnemyType type)
    {
        // Audio is played by the event handlers (OnLeft/OnCenter/OnRight) so that
        // coyote-buffer replays via ReplayBuffer() don't double-fire the sound.
        if (GameClock.Instance.IsPaused) return;

        // Silently ignore all inputs when no encounter is running (intro screen,
        // transitions, between rounds, post-game-over). Only DungeonBeatManager
        // activates this flag via StartGameplay / HandleGameComplete / ClearState.
        if (!EncounterActive) return;

        if (!allowInput)
        {
            // C&R / Flash Card call window: use the response-start override set by the
            // encounter controller. Buffer if within the coyote window; silently ignore all
            // other early inputs (no penalty). The negative tolerance covers the sub-frame
            // race where the boundary has passed but the controller's Update has not yet
            // opened the window this frame — without it, an on-time boundary tap is eaten.
            if (CoyoteWindowStartOverride.HasValue)
            {
                double now             = GameClock.Instance.GameTime;
                double timeUntilWindow = CoyoteWindowStartOverride.Value - now;
                if (timeUntilWindow >= -windowOpenLateToleranceSeconds && timeUntilWindow <= barStartInputBufferSeconds)
                    _bufferedInput = new BufferedInput { type = type, virtualTime = now };
                return;
            }

            var coordinator = TimingCoordinator.Instance;
            if (coordinator == null || GameClock.Instance.GameTime < coordinator.CurrentBar.InputWindowStart)
            {
                // Still in the spawn phase — buffer the input if it falls within the
                // coyote window just before the input phase opens (Bug 1 fix).
                if (coordinator != null)
                {
                    double now             = GameClock.Instance.GameTime;
                    double timeUntilWindow = coordinator.CurrentBar.InputWindowStart - now;
                    if (timeUntilWindow >= 0 && timeUntilWindow <= barStartInputBufferSeconds)
                        _bufferedInput = new BufferedInput { type = type, virtualTime = now };
                }
                return;
            }

            // Response phase has passed — input is genuinely out-of-window, penalise.
            health?.TakeOutOfWindowPenalty();
            return;
        }

        // SingleDrum C&R mode: retag every echo tap to the constant type so all beats
        // match on type and EvaluateBar grades on timing alone. Null in all other contexts.
        if (SingleDrumEchoConstant.HasValue)
            type = SingleDrumEchoConstant.Value;

        var input = new DungeonInput(GameClock.Instance.GameTime, type);
        playerInputData.Add(input);

        // Immediate quality feedback; pass the matched beat to the visual controller
        // so it can despawn the exact paired enemy (Bug 2 fix).
        if (_scheduledBeats != null && _scheduledBeats.Count > 0 && evaluator != null)
        {
            var (quality, matchedBeat) = evaluator.EvaluateSingleInput(input, _scheduledBeats, _timeOffset);
            bool isHit                 = quality == InputMatch.MatchQuality.Perfect
                                      || quality == InputMatch.MatchQuality.Good;

            if (!SuppressSpawnMarkers) visualController?.SpawnInputMarker(type, quality);
            if (isHit)
            {
                visualController?.NotifyEnemyHit(matchedBeat);
                OnHit?.Invoke(type, quality);
            }
            else
                health?.TakeOutOfWindowPenalty();
        }
    }

    public void ResetInputs()
    {
        playerInputData.Clear();
        _bufferedInput = null; // discard any stale coyote input from the previous bar
    }

    /// <summary>
    /// Clears recorded taps but preserves the coyote buffer. Used by
    /// CallAndResponseController.OpenEchoWindow — a buffered early tap for the
    /// first echo beat must survive the window-open reset so ReplayBuffer can fire it.
    /// </summary>
    public void ClearRecordedInputs() => playerInputData.Clear();

    // ── Private ───────────────────────────────────────────────────────────

    private void ReplayBuffer()
    {
        if (!_bufferedInput.HasValue) return;
        var buf = _bufferedInput.Value;
        _bufferedInput = null;

        // Discard if too stale (e.g., clock jumped or game was paused)
        double age = GameClock.Instance.GameTime - buf.virtualTime;
        if (age <= barStartInputBufferSeconds)
            TriggerInput(buf.type);
    }
}
