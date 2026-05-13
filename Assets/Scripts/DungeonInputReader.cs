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

    // Controlled by DungeonBeatManager — false outside the response window
    public bool allowInput = false;

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

    private void OnLeft()   { AudioManager.instance?.PlayBongoLeft();  TriggerInput(DungeonEnemyType.Left);   }
    private void OnCenter() { AudioManager.instance?.PlayBongoLeft();  TriggerInput(DungeonEnemyType.Center); }
    private void OnRight()  { AudioManager.instance?.PlayBongoRight(); TriggerInput(DungeonEnemyType.Right);  }

    // ── Core ──────────────────────────────────────────────────────────────

    public void TriggerInput(DungeonEnemyType type)
    {
        // Audio is played by the event handlers (OnLeft/OnCenter/OnRight) so that
        // coyote-buffer replays via ReplayBuffer() don't double-fire the sound.
        if (GameClock.Instance.IsPaused) return;

        if (!allowInput)
        {
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

        var input = new DungeonInput(GameClock.Instance.GameTime, type);
        playerInputData.Add(input);

        // Immediate quality feedback; pass the matched beat to the visual controller
        // so it can despawn the exact paired enemy (Bug 2 fix).
        if (_scheduledBeats != null && _scheduledBeats.Count > 0 && evaluator != null)
        {
            var (quality, matchedBeat) = evaluator.EvaluateSingleInput(input, _scheduledBeats, _timeOffset);
            bool isHit                 = quality == InputMatch.MatchQuality.Perfect
                                      || quality == InputMatch.MatchQuality.Good;

            visualController?.SpawnInputMarker(type, quality);
            if (isHit)
                visualController?.NotifyEnemyHit(matchedBeat);
            else
                health?.TakeOutOfWindowPenalty();
        }
    }

    public void ResetInputs()
    {
        playerInputData.Clear();
        _bufferedInput = null; // discard any stale coyote input from the previous bar
    }

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
