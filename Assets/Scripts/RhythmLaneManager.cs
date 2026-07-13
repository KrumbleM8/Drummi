using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central hub for the Guitar Hero system. Maintains per-lane note queues,
/// handles auto-miss detection, and exposes TryHitLane() for HitJudge.
///
/// Fires OnJudgement(lane, judgement) for any listener — wire your
/// score manager, combo system, or UI feedback here.
/// </summary>
public class RhythmLaneManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────

    public static RhythmLaneManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Miss Window")]
    [Tooltip("Seconds past a note's hit time before it's auto-missed. " +
             "Should be >= your Good window. Tune alongside HitJudge.goodWindowSeconds.")]
    public double missWindowSeconds = 0.15;

    // TimingCoordinator in this project is bar-based and doesn't expose per-note
    // timing windows — those live in HitJudge. missWindowSeconds is inspector-only.

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever a note is judged (hit or auto-missed).
    /// Wire ScoreManager, combo system, and feedback UI here.
    /// </summary>
    public event System.Action<Lane, HitJudgement> OnJudgement;

    // ── Private ───────────────────────────────────────────────────────────

    private const int LaneCount = 2;
    private Queue<NoteObject>[] _laneQueues;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _laneQueues = new Queue<NoteObject>[LaneCount];
        for (int i = 0; i < LaneCount; i++)
            _laneQueues[i] = new Queue<NoteObject>();
    }

    void Update()
    {
        if (GameClock.Instance.IsPaused) return;

        double missWindow = ResolveMissWindow();
        double virtualTime = GameClock.Instance.GameTime;

        for (int i = 0; i < LaneCount; i++)
        {
            Queue<NoteObject> queue = _laneQueues[i];

            while (queue.Count > 0)
            {
                NoteObject front = queue.Peek();

                // Purge destroyed or already-resolved notes
                if (front == null || front.IsResolved)
                {
                    queue.Dequeue();
                    continue;
                }

                // Auto-miss if past the miss window
                if (virtualTime > front.HitTime + missWindow)
                {
                    queue.Dequeue();
                    front.ResolveMiss();
                    OnJudgement?.Invoke((Lane)i, HitJudgement.Miss);
                }
                else break; // notes are ordered by hit time — stop here
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Called by NoteSpawner immediately after instantiating a note.</summary>
    public void RegisterNote(NoteObject note)
    {
        _laneQueues[(int)note.Lane].Enqueue(note);
    }

    /// <summary>
    /// Attempts to judge the frontmost unresolved note in the given lane.
    /// Returns the judgement and resolves the note on success.
    /// Returns Miss (and does nothing to any note) if no note is in window.
    /// </summary>
    /// <param name="lane">Lane the player tapped.</param>
    /// <param name="perfectWindow">Seconds either side for Perfect.</param>
    /// <param name="goodWindow">Seconds either side for Good.</param>
    public HitJudgement TryHitLane(Lane lane, double perfectWindow, double goodWindow)
    {
        Queue<NoteObject> queue = _laneQueues[(int)lane];

        // Purge stale entries
        while (queue.Count > 0 && (queue.Peek() == null || queue.Peek().IsResolved))
            queue.Dequeue();

        if (queue.Count == 0)
            return HitJudgement.Miss;

        NoteObject note = queue.Peek();
        double delta = System.Math.Abs(GameClock.Instance.GameTime - note.HitTime);

        HitJudgement judgement;
        if (delta <= perfectWindow) judgement = HitJudgement.Perfect;
        else if (delta <= goodWindow) judgement = HitJudgement.Good;
        else return HitJudgement.Miss; // too early or too late

        queue.Dequeue();
        note.ResolveHit(judgement);
        OnJudgement?.Invoke(lane, judgement);
        return judgement;
    }

    /// <summary>Clear all queues — call on song restart.</summary>
    public void ClearQueues()
    {
        foreach (var queue in _laneQueues)
            queue.Clear();
    }

    // ── Private ───────────────────────────────────────────────────────────

    private double ResolveMissWindow() => missWindowSeconds;
}