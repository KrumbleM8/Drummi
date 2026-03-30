using UnityEngine;

/// <summary>
/// Streams NoteObjects into the scene as their hit time approaches.
/// Uses GameClock.VirtualTime as the time source and supports chart looping.
///
/// SCROLL SPEED MATH:
///   scrollSpeed = scrollDistance / lookaheadSeconds
///   A note spawns exactly lookaheadSeconds before its hit time, at world Y:
///     spawnY = hitZoneY + scrollDistance
///   It then arrives at hitZoneY after lookaheadSeconds of travel.
///   Set scrollDistance to match your camera's vertical world-unit height.
///
/// LOOPING:
///   When loopChart is enabled on the SongChart, the spawner re-queues the
///   same note array each loop cycle, offsetting hit times by loopIndex * loopLengthSeconds.
/// </summary>
public class NoteSpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Chart")]
    public SongChart chart;

    [Header("Prefab")]
    public NoteObject notePrefab;

    [Header("Lane Layout")]
    [Tooltip("World Y position of the hit zone (bottom target line).")]
    public float hitZoneY = -4f;

    [Tooltip("World X positions: index 0 = Left lane, index 1 = Right lane.")]
    public float[] laneXPositions = { -2f, 2f };

    [Header("Scroll Settings")]
    [Tooltip("How many beats ahead of hit time a note is spawned.")]
    public float lookaheadBeats = 4f;

    [Tooltip("World units from hitZoneY to spawn point (top of travel path). " +
             "Match this to your camera's visible world height.")]
    public float scrollDistance = 10f;

    // ── Runtime state ─────────────────────────────────────────────────────

    private int _nextNoteIndex;
    private int _loopIndex;
    private float _scrollSpeed;
    private double _lookaheadSeconds;
    private bool _initialised;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (!ValidateSetup()) return;
        Initialise();
    }

    void Update()
    {
        if (!_initialised) return;
        if (GameClock.Instance.IsPaused) return;

        double virtualTime = GameClock.Instance.GameTime;

        // Check loop boundary before spawning
        if (chart.loopChart)
            AdvanceLoopIfNeeded(virtualTime);

        // Spawn all notes whose hit time has entered the lookahead window
        while (_nextNoteIndex < chart.notes.Length)
        {
            NoteData note = chart.notes[_nextNoteIndex];
            double hitTime = chart.BeatToAbsoluteSeconds(note.beat, _loopIndex);

            if (hitTime - virtualTime <= _lookaheadSeconds)
            {
                SpawnNote(note, hitTime);
                _nextNoteIndex++;
            }
            else break; // chart is sorted — safe to early-exit
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Reset spawner to beginning — call on song restart or scene reload.</summary>
    public void ResetSpawner()
    {
        _nextNoteIndex = 0;
        _loopIndex = 0;
    }

    /// <summary>
    /// Recompute scroll speed — call if BPM changes at runtime
    /// (e.g. tempo ramp or chart hot-swap).
    /// </summary>
    public void RefreshScrollSpeed() => ComputeScrollSpeed();

    // ── Private ───────────────────────────────────────────────────────────

    private void Initialise()
    {
        _nextNoteIndex = 0;
        _loopIndex = 0;
        ComputeScrollSpeed();
        _initialised = true;
    }

    private void ComputeScrollSpeed()
    {
        _lookaheadSeconds = lookaheadBeats * chart.SecondsPerBeat;
        _scrollSpeed = (float)(scrollDistance / _lookaheadSeconds);

        Debug.Log(
            $"[NoteSpawner] BPM: {chart.bpm} | " +
            $"Lookahead: {_lookaheadSeconds:F3}s | " +
            $"ScrollSpeed: {_scrollSpeed:F2} units/s"
        );
    }

    private void AdvanceLoopIfNeeded(double virtualTime)
    {
        double nextLoopStartTime = (_loopIndex + 1) * (double)chart.LoopLengthSeconds;

        // If we've passed the start of the next loop cycle, advance
        if (virtualTime >= nextLoopStartTime - _lookaheadSeconds &&
            _nextNoteIndex >= chart.notes.Length)
        {
            _loopIndex++;
            _nextNoteIndex = 0;
        }
    }

    private void SpawnNote(NoteData data, double hitTime)
    {
        int laneIndex = (int)data.lane;
        float laneX = laneXPositions[laneIndex];

        NoteObject note = Instantiate(notePrefab, transform);
        note.Initialize(data.lane, hitTime, _scrollSpeed, hitZoneY, laneX);

        RhythmLaneManager.Instance.RegisterNote(note);
    }

    private bool ValidateSetup()
    {
        if (chart == null)
        {
            Debug.LogError("[NoteSpawner] No SongChart assigned.", this);
            return false;
        }
        if (notePrefab == null)
        {
            Debug.LogError("[NoteSpawner] No note prefab assigned.", this);
            return false;
        }
        if (laneXPositions == null || laneXPositions.Length < 2)
        {
            Debug.LogError("[NoteSpawner] laneXPositions must have at least 2 entries (Left, Right).", this);
            return false;
        }
        return true;
    }
}