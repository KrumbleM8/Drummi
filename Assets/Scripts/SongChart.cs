using UnityEngine;

/// <summary>
/// ScriptableObject defining a song's BPM, time signature, looping behaviour,
/// and full note chart. Create via Assets > Create > Drummi > Song Chart.
///
/// Notes are defined in beat-space. The spawner converts beats → virtual time seconds
/// using BeatToSeconds(), keeping everything consistent with GameClock.VirtualTime.
///
/// LOOPING:
///   Enable loopChart and set loopLengthBeats to the pattern duration.
///   The NoteSpawner will automatically re-queue the pattern each loop cycle.
///
/// TIME SIGNATURES:
///   beatsPerBar is cosmetic — it affects the editor grid helper only.
///   The note chart works in any time signature; just place notes at the
///   correct beat values (e.g. 6/8 at 0, 0.33, 0.67, 1.0, 1.33, 1.67...).
/// </summary>
[CreateAssetMenu(fileName = "NewSongChart", menuName = "Drummi/Song Chart")]
public class SongChart : ScriptableObject
{
    // ── Song Info ─────────────────────────────────────────────────────────

    [Header("Song Info")]
    public string songName = "Untitled";
    public AudioClip audioClip;

    // ── Timing ────────────────────────────────────────────────────────────

    [Header("Timing")]
    [Min(1f)]
    [Tooltip("Beats per minute.")]
    public float bpm = 120f;

    [Range(2, 16)]
    [Tooltip("Time signature numerator — beats per bar. Used by the editor grid helper only.")]
    public int beatsPerBar = 4;

    // ── Loop ──────────────────────────────────────────────────────────────

    [Header("Looping")]
    [Tooltip("If true, the chart loops indefinitely after loopLengthBeats.")]
    public bool loopChart = true;

    [Tooltip("Length of one loop cycle in beats. Set to total beat count of your pattern.")]
    [Min(1f)]
    public float loopLengthBeats = 8f;

    // ── Notes ─────────────────────────────────────────────────────────────

    [Header("Notes")]
    [Tooltip("All notes in this chart. Must be sorted ascending by beat — use the context menu Sort button.")]
    public NoteData[] notes;

    // ── Convenience ───────────────────────────────────────────────────────

    /// <summary>Duration of one beat in seconds at this chart's BPM.</summary>
    public float SecondsPerBeat => 60f / bpm;

    /// <summary>Duration of one full loop cycle in seconds.</summary>
    public float LoopLengthSeconds => loopLengthBeats * SecondsPerBeat;

    /// <summary>Convert a beat number (within a single loop cycle) to song-relative seconds.</summary>
    public double BeatToSeconds(float beat) => beat * SecondsPerBeat;

    /// <summary>
    /// Convert a beat in loop-relative space to an absolute virtual time,
    /// given how many loops have already elapsed. Returns double to match GameClock.GameTime.
    /// </summary>
    public double BeatToAbsoluteSeconds(float beat, int loopIndex) =>
        (loopIndex * (double)LoopLengthSeconds) + BeatToSeconds(beat);

    // ── Editor Helpers ────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Sort Notes By Beat")]
    private void SortNotesByBeat()
    {
        System.Array.Sort(notes, (a, b) => a.beat.CompareTo(b.beat));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SongChart] '{songName}' notes sorted.");
    }

    [ContextMenu("Log Chart Info")]
    private void LogChartInfo()
    {
        Debug.Log(
            $"[SongChart] '{songName}' | BPM: {bpm} | {beatsPerBar}/4 | " +
            $"Loop: {loopChart} ({loopLengthBeats} beats = {LoopLengthSeconds:F2}s) | " +
            $"Notes: {(notes != null ? notes.Length : 0)}"
        );
    }
#endif
}