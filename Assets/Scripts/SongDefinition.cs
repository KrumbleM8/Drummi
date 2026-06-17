using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Defines a single playable song for the Bongo song-select carousel.
    /// Assign instances to an <see cref="AlbumDefinition.Songs"/> list.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSong", menuName = "Drummi/Song Definition")]
    public class SongDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in the song-select UI.")]
        [SerializeField] private string title;

        [Header("Timing")]
        [Tooltip("Beats per minute — must match the source audio track.")]
        [SerializeField] private float bpm;

        [Header("Audio")]
        [Tooltip(
            "Zero-based index into AudioManager.musicTracks[]. " +
            "The order of clips in that array and the value of this field must stay in sync manually " +
            "until AudioManager is refactored to accept SongDefinition directly.")]
        [SerializeField] private int trackIndex;

        [Header("Progress")]
        [Tooltip("Player's personal best score for this song. Persisted externally (PlayerPrefs / save file).")]
        [SerializeField] private int bestScore;

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Display name shown in the song-select UI.</summary>
        public string Title => title;

        /// <summary>Beats per minute for this track.</summary>
        public float Bpm => bpm;

        /// <summary>
        /// Zero-based index into <c>AudioManager.musicTracks[]</c>.
        /// <para>
        /// Coupling note: this value must match the clip's position in
        /// <c>AudioManager.musicTracks</c>. When reordering clips in the
        /// Inspector, update this field accordingly. Phase 2 will centralise
        /// audio lookup so this manual sync is no longer required.
        /// </para>
        /// </summary>
        public int TrackIndex => trackIndex;

        /// <summary>Player's personal best score for this song.</summary>
        public int BestScore => bestScore;
    }
}
