using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records Dungeon mode player inputs and triggers immediate visual feedback.
/// Parallel to BongoModeInputReader — subscribes to DrumPadTouch events (now three),
/// maps left/center/right to DungeonEnemyType, and calls DungeonEvaluator for
/// per-hit quality feedback.
///
/// INSPECTOR SETUP:
///   Assign drumPadTouch, evaluator, and visualController in the Inspector.
/// </summary>
public class DungeonInputReader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DrumPadTouch            drumPadTouch;
    [SerializeField] private DungeonEvaluator        evaluator;
    [SerializeField] private DungeonVisualController  visualController;
    [SerializeField] private DungeonHealth            health;

    // Controlled by DungeonBeatManager — false outside the response window
    public bool allowInput = false;

    public List<DungeonInput> playerInputData = new();

    // Set by DungeonBeatManager each bar so single-input evaluation works
    private List<DungeonScheduledBeat> _scheduledBeats;
    private double _timeOffset;

    public void SetScheduledBeats(List<DungeonScheduledBeat> beats) => _scheduledBeats = beats;
    public void SetTimeOffset(double offset)                        => _timeOffset      = offset;

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

    // ── Event Handlers ────────────────────────────────────────────────────

    private void OnLeft()   => TriggerInput(DungeonEnemyType.Left);
    private void OnCenter() => TriggerInput(DungeonEnemyType.Center);
    private void OnRight()  => TriggerInput(DungeonEnemyType.Right);

    // ── Core ──────────────────────────────────────────────────────────────

    public void TriggerInput(DungeonEnemyType type)
    {
        // Always play immediate audio feedback so the player knows their tap registered.
        // TODO: replace with 3 distinct enemy-hit sounds once assets are available.
        switch (type)
        {
            case DungeonEnemyType.Left:   AudioManager.instance?.PlayBongoLeft();  break;
            case DungeonEnemyType.Center: AudioManager.instance?.PlayBongoLeft();  break;
            case DungeonEnemyType.Right:  AudioManager.instance?.PlayBongoRight(); break;
        }

        if (GameClock.Instance.IsPaused) return;
        if (!allowInput)
        {
            // During the spawn/pattern phase (before the input window opens) tapping
            // is expected — don't penalise the player for watching enemies spawn.
            var coordinator = TimingCoordinator.Instance;
            if (coordinator == null || GameClock.Instance.GameTime < coordinator.CurrentBar.InputWindowStart)
                return;

            // Response phase has passed — input is genuinely unnecessary, penalise instantly.
            health?.TakeOutOfWindowPenalty();
            return;
        }

        var input = new DungeonInput(GameClock.Instance.GameTime, type);
        playerInputData.Add(input);

        // Immediate quality feedback and instant penalty for bad hits
        if (_scheduledBeats != null && _scheduledBeats.Count > 0 && evaluator != null)
        {
            var quality = evaluator.EvaluateSingleInput(input, _scheduledBeats, _timeOffset);
            visualController?.SpawnInputMarker(type, quality);

            if (quality != InputMatch.MatchQuality.Perfect && quality != InputMatch.MatchQuality.Good)
                health?.TakeOutOfWindowPenalty();
        }
    }

    public void ResetInputs() => playerInputData.Clear();
}
