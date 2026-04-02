using UnityEngine;

/// <summary>
/// Streams NoteObjects into the scene as their hit time approaches.
/// Uses GameClock.GameTime as the time source and supports chart looping.
///
/// IMPORTANT — spawning is gated. NoteSpawner does NOT begin spawning at Start().
/// Call StartSpawning(virtualStartTime) from RhythmGameController after GameClock
/// is reset and the session anchor time is known. Without this, all beat 0-relative
/// hit times would be in the deep past relative to DSP time and every note would
/// spawn and auto-miss on the first frame.
///
/// SCROLL SPEED MATH:
///   scrollSpeed = scrollDistance / lookaheadSeconds
///   A note spawns exactly lookaheadSeconds before its hit time, at world Y:
///     spawnY = hitZoneY + scrollDistance
///   It then arrives at hitZoneY after lookaheadSeconds of travel.
///   Set scrollDistance to match your camera's vertical world-unit height.
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
    private double _startVirtualTime;  // GameClock anchor for beat 0
    private bool _initialised;       // Only true after StartSpawning() is called

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Validate references at scene load but do NOT begin spawning.
        // Spawning only begins when StartSpawning(virtualStartTime) is called.
        ValidateSetup();
    }

    void Update()
    {
        if (!_initialised) return;
        if (GameClock.Instance.IsPaused) return;

        double virtualTime = GameClock.Instance.GameTime;

        if (chart.loopChart)
            AdvanceLoopIfNeeded(virtualTime);

        while (_nextNoteIndex < chart.notes.Length)
        {
            NoteData note = chart.notes[_nextNoteIndex];
            double hitTime = _startVirtualTime + chart.BeatToAbsoluteSeconds(note.beat, _loopIndex);

            if (hitTime - virtualTime <= _lookaheadSeconds)
            {
                SpawnNote(note, hitTime);
                _nextNoteIndex++;
            }
            else break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Begin spawning notes anchored to a specific GameClock virtual time.
    /// Call this from RhythmGameController after GameClock is reset and the
    /// session start time is known.
    ///
    /// All note hit times are offset by startVirtualTime so they align with
    /// real DSP time rather than starting from 0.
    /// </summary>
    public void StartSpawning(double startVirtualTime)
    {
        if (!ValidateSetup()) return;

        _startVirtualTime = startVirtualTime;
        _nextNoteIndex = 0;
        _loopIndex = 0;
        ComputeScrollSpeed();
        _initialised = true;

        Debug.Log($"[NoteSpawner] StartSpawning — VirtualStart: {startVirtualTime:F4} | " +
                  $"ScrollSpeed: {_scrollSpeed:F2} units/s | Lookahead: {_lookaheadSeconds:F3}s");
    }

    /// <summary>
    /// Stop spawning and reset to idle state.
    /// Call StartSpawning() again to restart.
    /// </summary>
    public void ResetSpawner()
    {
        _initialised = false;
        _nextNoteIndex = 0;
        _loopIndex = 0;
        _startVirtualTime = 0;
    }

    /// <summary>Recompute scroll speed — call if BPM changes at runtime.</summary>
    public void RefreshScrollSpeed() => ComputeScrollSpeed();

    // ── Private ───────────────────────────────────────────────────────────

    private void ComputeScrollSpeed()
    {
        _lookaheadSeconds = lookaheadBeats * chart.SecondsPerBeat;
        _scrollSpeed = (float)(scrollDistance / _lookaheadSeconds);
    }

    private void AdvanceLoopIfNeeded(double virtualTime)
    {
        double nextLoopStartTime = _startVirtualTime + (_loopIndex + 1) * (double)chart.LoopLengthSeconds;

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