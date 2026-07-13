using UnityEngine;

/// <summary>
/// Tracks score, combo, and max combo. Subscribes to RhythmLaneManager.OnJudgement.
///
/// SCORE MODEL:
///   Perfect  → basePointsPerfect × combo multiplier
///   Good     → basePointsGood    × combo multiplier
///   Miss     → 0 points, combo reset to 0
///
/// COMBO MULTIPLIER:
///   Multiplier increases by 1 for every multiplierThreshold consecutive hits,
///   up to maxMultiplier. Any miss resets multiplier to 1.
///   e.g. default threshold=10: x2 at 10, x3 at 20, x4 at 30 (capped at x4).
///
/// WIRING:
///   Place on any GameObject. Subscribes automatically in Start().
///   Read Score, Combo, Multiplier, and MaxCombo as needed for UI.
///
/// UI HOOK:
///   Subscribe to OnScoreChanged, OnComboChanged, or OnJudgement to drive
///   animated UI without polling every frame.
/// </summary>
public class RhythmScoreTracker : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Score Values")]
    public int basePointsPerfect = 300;
    public int basePointsGood = 100;

    [Header("Combo Multiplier")]
    [Tooltip("Consecutive hits required to step up the multiplier by 1.")]
    public int multiplierThreshold = 10;

    [Tooltip("Maximum score multiplier achievable.")]
    public int maxMultiplier = 4;

    // ── Public state (read-only) ──────────────────────────────────────────

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public int MaxCombo { get; private set; }
    public int TotalPerfectHits { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Fired whenever Score changes. Param = new score.</summary>
    public event System.Action<int> OnScoreChanged;

    /// <summary>Fired whenever Combo or Multiplier changes. Params = combo, multiplier.</summary>
    public event System.Action<int, int> OnComboChanged;

    /// <summary>Fired on every judgement — useful for per-hit feedback UI.</summary>
    public event System.Action<Lane, HitJudgement> OnJudgement;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Wire into the lane manager's judgement event
        if (RhythmLaneManager.Instance != null)
            RhythmLaneManager.Instance.OnJudgement += HandleJudgement;
        else
            Debug.LogError("[RhythmScoreTracker] RhythmLaneManager not found in scene.", this);

        Multiplier = 1;
    }

    void OnDestroy()
    {
        if (RhythmLaneManager.Instance != null)
            RhythmLaneManager.Instance.OnJudgement -= HandleJudgement;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Reset all state. Call on game start or restart.</summary>
    public void ResetScore()
    {
        Score = 0;
        Combo = 0;
        MaxCombo = 0;
        Multiplier = 1;

        OnScoreChanged?.Invoke(Score);
        OnComboChanged?.Invoke(Combo, Multiplier);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void HandleJudgement(Lane lane, HitJudgement judgement)
    {
        OnJudgement?.Invoke(lane, judgement);

        switch (judgement)
        {
            case HitJudgement.Perfect:
                AddHit(basePointsPerfect);
                break;

            case HitJudgement.Good:
                AddHit(basePointsGood);
                break;

            case HitJudgement.Miss:
                HandleMiss();
                break;
        }
    }

    private void AddHit(int basePoints)
    {
        Combo++;
        if (Combo > MaxCombo) MaxCombo = Combo;

        // Step up multiplier every multiplierThreshold hits
        int newMultiplier = Mathf.Min(1 + (Combo / multiplierThreshold), maxMultiplier);
        bool multiplierChanged = newMultiplier != Multiplier;
        Multiplier = newMultiplier;

        int earned = basePoints * Multiplier;
        Score += earned;

        OnScoreChanged?.Invoke(Score);
        if (multiplierChanged || Combo % multiplierThreshold == 0)
            OnComboChanged?.Invoke(Combo, Multiplier);

        Debug.Log($"[ScoreTracker] Hit +{earned} (x{Multiplier}) | Score: {Score} | Combo: {Combo}");
    }

    private void HandleMiss()
    {
        bool hadCombo = Combo > 0;
        bool hadBonus = Multiplier > 1;
        Combo = 0;
        Multiplier = 1;

        if (hadCombo || hadBonus)
            OnComboChanged?.Invoke(Combo, Multiplier);

        Debug.Log($"[ScoreTracker] Miss — combo reset | Score: {Score}");
    }
}