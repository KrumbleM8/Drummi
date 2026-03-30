using UnityEngine;

/// <summary>
/// Placed on the note prefab. Positions itself each frame purely from
/// GameClock.GameTime — making pause, resume, and seek completely safe
/// with zero drift. Consistent with Drummi's virtual-time architecture.
///
/// Y formula:  noteY = hitZoneY + (hitTime - GameTime) * scrollSpeed
///   - When GameTime == hitTime  →  note is exactly at the hit zone.
///   - When GameTime < hitTime   →  note is above the hit zone.
///   - When GameTime > hitTime   →  note has passed (should be resolved or missed).
///
/// hitTime uses double to match GameClock.GameTime precision.
/// scrollSpeed and Y positions are float (transform space).
/// </summary>
public class NoteObject : MonoBehaviour
{
    // ── Public state ──────────────────────────────────────────────────────

    public Lane Lane { get; private set; }

    /// <summary>Absolute virtual time (double seconds) when this note should be hit.</summary>
    public double HitTime { get; private set; }

    public bool IsResolved { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────

    private float _scrollSpeed;
    private float _hitZoneY;

    // ── Initialisation ────────────────────────────────────────────────────

    /// <param name="lane">Left or Right.</param>
    /// <param name="hitTime">Absolute virtual time (double seconds) at which this note should be hit.</param>
    /// <param name="scrollSpeed">World units per second of downward travel.</param>
    /// <param name="hitZoneY">World Y of the hit zone.</param>
    /// <param name="laneX">World X for this lane column.</param>
    public void Initialize(Lane lane, double hitTime, float scrollSpeed, float hitZoneY, float laneX)
    {
        Lane = lane;
        HitTime = hitTime;
        _scrollSpeed = scrollSpeed;
        _hitZoneY = hitZoneY;
        IsResolved = false;

        // Snap X immediately; Y is set on first Update
        Vector3 pos = transform.position;
        pos.x = laneX;
        transform.position = pos;
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        if (GameClock.Instance.IsPaused) return;

        float timeUntilHit = (float)(HitTime - GameClock.Instance.GameTime);
        Vector3 pos = transform.position;
        pos.y = _hitZoneY + timeUntilHit * _scrollSpeed;
        transform.position = pos;
    }

    // ── Resolution ────────────────────────────────────────────────────────

    /// <summary>Called by RhythmLaneManager when the player successfully hits this note.</summary>
    public void ResolveHit(HitJudgement judgement)
    {
        if (IsResolved) return;
        IsResolved = true;

        // TODO: trigger hit VFX / sound based on judgement
        // e.g. hitParticles.Play(); AudioManager.Instance.PlaySFX(hitClip);

        Destroy(gameObject, 0.05f);
    }

    /// <summary>Called by RhythmLaneManager when the note scrolls past the miss window.</summary>
    public void ResolveMiss()
    {
        if (IsResolved) return;
        IsResolved = true;

        // TODO: trigger miss VFX / flash hit zone red

        Destroy(gameObject, 0.1f);
    }
}