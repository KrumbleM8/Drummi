using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a Boss encounter: manages HP, drives phase transitions as HP drops,
/// and applies each phase's pattern durations and visual-indicator flag.
///
/// PHASE TRANSITIONS:
///   Each BossPhaseDefinition has a bossHPThreshold. When boss HP drops below
///   the next phase's threshold, ActivatePhase() is called which restarts
///   the beat cycle with that phase's patternDurations.
///   bossHPThreshold == 0 marks the final phase (no further transitions).
/// </summary>
public class BossController : MonoBehaviour
{
    [SerializeField] private List<BossPhaseDefinition> phases;
    [SerializeField] private int                        maxBossHP         = 200;
    [SerializeField] private DungeonInputReader         inputReader;
    [SerializeField] private DungeonModeController      modeController;
    [SerializeField] private DungeonVisualController    visualController;

    /// <summary>Fired once when boss HP reaches zero.</summary>
    public event Action OnBossDefeated;

    private int  _currentBossHP;
    private int  _currentPhaseIndex;
    private bool _active;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Resets boss HP, activates phase 0, and starts listening for player hits.</summary>
    public void StartBossEncounter()
    {
        if (phases == null || phases.Count == 0)
        {
            Debug.LogError("[BossController] No phases defined — cannot start encounter");
            return;
        }

        _currentBossHP    = maxBossHP;
        _currentPhaseIndex = 0;
        _active            = true;

        inputReader.OnHit += HandleHit;

        ActivatePhase(0);

        Debug.Log($"[BossController] Encounter started — HP: {_currentBossHP}, phases: {phases.Count}");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ActivatePhase(int index)
    {
        var phase = phases[index];

        modeController.StartMode(phase.PatternDurations);

        if (phase.RemoveVisualIndicator)
            visualController.SetVisualIndicatorActive(false);
        else
            visualController.SetVisualIndicatorActive(true);

        Debug.Log($"[BossController] Phase {index} activated — '{phase.PhaseName}', " +
                  $"indicator: {!phase.RemoveVisualIndicator}");
    }

    private void HandleHit(DungeonEnemyType type, InputMatch.MatchQuality quality)
    {
        if (!_active) return;

        int dmg = quality == InputMatch.MatchQuality.Perfect ? 15 : 8;
        _currentBossHP -= dmg;

        Debug.Log($"[BossController] Hit ({quality}): -{dmg} → boss HP {Mathf.Max(0, _currentBossHP)}");

        if (_currentBossHP <= 0)
        {
            _currentBossHP = 0;
            _active        = false;
            inputReader.OnHit -= HandleHit;
            OnBossDefeated?.Invoke();
            Debug.Log("[BossController] Boss defeated");
            return;
        }

        // Check if the next phase threshold has been crossed
        int nextIndex = _currentPhaseIndex + 1;
        if (nextIndex < phases.Count)
        {
            var nextPhase = phases[nextIndex];
            if (nextPhase != null && nextPhase.BossHPThreshold > 0
                && _currentBossHP <= nextPhase.BossHPThreshold)
            {
                _currentPhaseIndex = nextIndex;
                ActivatePhase(_currentPhaseIndex);
            }
        }
    }
}
