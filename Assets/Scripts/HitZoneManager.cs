using UnityEngine;

/// <summary>
/// Central wiring point for hit zone visuals.
/// Bridges RhythmLaneManager.OnJudgement and RhythmInputHandler press events
/// to the correct HitZoneVisual instance.
///
/// SETUP:
///   1. Add to any GameObject in the scene.
///   2. Assign leftHitZone and rightHitZone (each with HitZoneVisual attached).
///   3. Call NotifyPress(lane) from RhythmInputHandler when a tap occurs.
/// </summary>
public class HitZoneManager : MonoBehaviour
{
    [Header("Hit Zone Visuals")]
    public HitZoneVisual leftHitZone;
    public HitZoneVisual rightHitZone;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (RhythmLaneManager.Instance != null)
            RhythmLaneManager.Instance.OnJudgement += HandleJudgement;
        else
            Debug.LogError("[HitZoneManager] RhythmLaneManager not found.", this);
    }

    void OnDestroy()
    {
        if (RhythmLaneManager.Instance != null)
            RhythmLaneManager.Instance.OnJudgement -= HandleJudgement;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from RhythmInputHandler.FireLane() immediately on tap —
    /// before judgement resolves, so the press flash is instant.
    /// </summary>
    public void NotifyPress(Lane lane)
    {
        GetVisual(lane)?.NotifyPress();
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void HandleJudgement(Lane lane, HitJudgement judgement)
    {
        GetVisual(lane)?.NotifyJudgement(judgement);
    }

    private HitZoneVisual GetVisual(Lane lane) =>
        lane == Lane.Left ? leftHitZone : rightHitZone;
}
