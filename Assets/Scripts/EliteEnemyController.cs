using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls an Elite enemy encounter.
/// The elite continuously drains the player's health while alive.
/// Player counters by landing Perfect/Good hits, which deal damage back to the elite.
/// When the elite's HP reaches zero, OnEliteDefeated is fired and the encounter ends.
/// </summary>
public class EliteEnemyController : MonoBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private float drainRatePerSecond = 5f;
    [SerializeField] private int   maxEnemyHP         = 100;
    [SerializeField] private int   damagePerPerfect   = 15;
    [SerializeField] private int   damagePerGood      = 8;

    [Header("References")]
    [SerializeField] private DungeonHealth     playerHealth;
    [SerializeField] private DungeonInputReader inputReader;

    [Header("UI")]
    [Tooltip("Fill image representing enemy HP (fillAmount 0–1).")]
    [SerializeField] private Image enemyHPBar;

    /// <summary>Fired once when the elite's HP reaches zero.</summary>
    public event Action OnEliteDefeated;

    private int       _currentEnemyHP;
    private bool      _active;
    private Coroutine _drainCoroutine;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins the elite encounter: resets HP, starts the drain loop,
    /// and subscribes to player hit events.
    /// </summary>
    public void StartEliteEncounter()
    {
        _currentEnemyHP = maxEnemyHP;
        _active         = true;

        inputReader.OnHit += HandleHit;

        _drainCoroutine = StartCoroutine(DrainCoroutine());

        UpdateHPBar();
        Debug.Log("[EliteEnemyController] Encounter started");
    }

    /// <summary>
    /// Ends the elite encounter cleanly: stops the drain loop and unsubscribes from hit events.
    /// Safe to call even if the encounter is not active.
    /// </summary>
    public void StopEliteEncounter()
    {
        _active = false;

        if (_drainCoroutine != null)
        {
            StopCoroutine(_drainCoroutine);
            _drainCoroutine = null;
        }

        inputReader.OnHit -= HandleHit;

        Debug.Log("[EliteEnemyController] Encounter stopped");
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    private IEnumerator DrainCoroutine()
    {
        while (_active && _currentEnemyHP > 0 && playerHealth.CurrentHealth > 0)
        {
            // Time.deltaTime is used here intentionally — drainRatePerSecond is a visual/
            // gameplay drain value driven by wall-clock frame time, not a beat-critical event.
            // This is the only acceptable use of Time.deltaTime in this codebase;
            // all rhythm/beat logic still uses GameClock.GameTime.
            playerHealth.TakeDrainDamage(drainRatePerSecond * Time.deltaTime);

            UpdateHPBar();

            yield return null; // frame-by-frame
        }

        _drainCoroutine = null;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void HandleHit(DungeonEnemyType type, InputMatch.MatchQuality quality)
    {
        if (!_active) return;

        int dmg = quality == InputMatch.MatchQuality.Perfect ? damagePerPerfect : damagePerGood;
        _currentEnemyHP -= dmg;

        UpdateHPBar();
        Debug.Log($"[EliteEnemyController] Hit ({quality}): -{dmg} → HP {Mathf.Max(0, _currentEnemyHP)}");

        if (_currentEnemyHP <= 0)
        {
            _currentEnemyHP = 0;
            StopEliteEncounter();
            OnEliteDefeated?.Invoke();
            Debug.Log("[EliteEnemyController] Elite defeated");
        }
    }

    private void UpdateHPBar()
    {
        if (enemyHPBar != null)
            enemyHPBar.fillAmount = Mathf.Clamp01((float)_currentEnemyHP / maxEnemyHP);
    }
}
