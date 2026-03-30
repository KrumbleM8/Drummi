using UnityEngine;

/// <summary>
/// Converts a lane tap into a HitJudgement by querying RhythmLaneManager.
///
/// NOTE ON TIMINGCOORDINATOR INTEGRATION:
/// The existing TimingCoordinator is bar-based (manages bar/beat cycle progression)
/// and does not expose per-note timing windows. Windows are therefore configured
/// here in the inspector as double values (matching GameClock.GameTime precision).
///
/// To make windows BPM-proportional, call SetWindowsFromBpm() at game start,
/// passing the same BPM you pass to TimingCoordinator.Initialize().
///
/// Place on the same GameObject as ArcadeInputHandler.
/// </summary>
public class HitJudge : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Timing Windows")]
    [Tooltip("Seconds either side of hit time that counts as Perfect.")]
    public double perfectWindowSeconds = 0.05;

    [Tooltip("Seconds either side of hit time that counts as Good.")]
    public double goodWindowSeconds = 0.10;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Optionally called at game start to scale windows proportionally to BPM.
    /// perfectFraction and goodFraction are fractions of one beat.
    /// e.g. perfectFraction=0.15 means 15% of a beat either side = Perfect.
    ///
    /// Example wiring (in your game initialiser):
    ///   TimingCoordinator.Instance.Initialize(startTime, bpm, totalBeats);
    ///   GetComponent&lt;HitJudge&gt;().SetWindowsFromBpm(bpm);
    /// </summary>
    public void SetWindowsFromBpm(double bpm, double perfectFraction = 0.15, double goodFraction = 0.25)
    {
        double beatInterval = 60.0 / bpm;
        perfectWindowSeconds = beatInterval * perfectFraction;
        goodWindowSeconds = beatInterval * goodFraction;

        Debug.Log(
            $"[HitJudge] Windows set from BPM {bpm}: " +
            $"Perfect={perfectWindowSeconds * 1000:F1}ms  " +
            $"Good={goodWindowSeconds * 1000:F1}ms"
        );
    }

    /// <summary>
    /// Called by ArcadeInputHandler when the player taps a lane.
    /// Resolves the nearest note and returns the judgement.
    /// </summary>
    public HitJudgement Judge(Lane lane)
    {
        HitJudgement result = RhythmLaneManager.Instance.TryHitLane(
            lane,
            perfectWindowSeconds,
            goodWindowSeconds
        );

        // TODO: trigger per-judgement feedback
        // e.g. judgementDisplay.Show(result);
        //      AudioManager.Instance.PlaySFX(result == HitJudgement.Perfect ? perfectClip : goodClip);

        return result;
    }
}