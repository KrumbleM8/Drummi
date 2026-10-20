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

        [Tooltip("Start time (seconds) within the track for the song-select preview loop. " +
                 "The controller clamps this so previewStart + previewLength ≤ clip.length.")]
        [SerializeField] private float previewStartSeconds = 15f;

        [Header("Progress")]
        [Tooltip("Player's personal best score for this song. Persisted externally (PlayerPrefs / save file).")]
        [SerializeField] private int bestScore;

        [Tooltip("PlayerPrefs key for this song's Bongo high score. Leave empty to derive one from the " +
                 "asset name (\"BongoHS_<asset name>\"). Set to \"GlitchyHS\" to adopt the legacy shared board.")]
        [SerializeField] private string highScoreKey;

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

        /// <summary>
        /// PlayerPrefs key for this song's Bongo high score — the authored key, or
        /// "BongoHS_&lt;asset name&gt;" when none is set.
        /// </summary>
        public string HighScoreKey => string.IsNullOrEmpty(highScoreKey) ? $"BongoHS_{name}" : highScoreKey;

        /// <summary>
        /// Start time (seconds) within the audio track for the song-select preview loop.
        /// The controller clamps this so <c>previewStart + previewLength ≤ clip.length</c>.
        /// </summary>
        public float PreviewStartSeconds => previewStartSeconds;
    }
}
