using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Defines an album grouping shown as a single card in the song-select carousel.
    /// Each album owns an ordered list of <see cref="SongDefinition"/> assets that
    /// are revealed when the player expands the card.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAlbum", menuName = "Drummi/Album Definition")]
    public class AlbumDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown on the album card.")]
        [SerializeField] private string albumName;

        [Tooltip("Cover art sprite displayed on the carousel card.")]
        [SerializeField] private Sprite cover;

        [Header("Audio Preview")]
        [Tooltip("BPM used for the album's preview loop (e.g. a short teaser clip). " +
                 "Does not need to match any individual song's BPM.")]
        [SerializeField] private float bpm;

        [Header("Progress")]
        [Tooltip("Aggregate best score across all songs in this album. " +
                 "Persisted externally (PlayerPrefs / save file).")]
        [SerializeField] private int bestScore;

        [Header("Songs")]
        [Tooltip("Ordered list of songs revealed when this album card is expanded. " +
                 "At least one entry is required for the card to be interactive.")]
        [SerializeField] private List<SongDefinition> songs = new List<SongDefinition>();

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Display name shown on the album card.</summary>
        public string AlbumName => albumName;

        /// <summary>Cover art sprite for the carousel card.</summary>
        public Sprite Cover => cover;

        /// <summary>BPM used for the album's preview loop.</summary>
        public float Bpm => bpm;

        /// <summary>Aggregate best score across all songs in this album.</summary>
        public int BestScore => bestScore;

        /// <summary>Ordered list of songs in this album.</summary>
        public IReadOnlyList<SongDefinition> Songs => songs;
    }
}
