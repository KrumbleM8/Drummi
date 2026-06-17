using TMPro;
using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Component on each of the 3 persistent vinyl-slot GameObjects that appear
    /// when an album is expanded. Each slot represents one song in the album.
    /// </summary>
    public class VinylItem : MonoBehaviour
    {
        [Tooltip("TMP label that displays the song title. Optional — null-safe.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Zero-based slot index within the expanded album (0 = leftmost vinyl). " +
                 "Must match this object's position in SongSelectController.vinyls[].")]
        [SerializeField] private int slotIndex;

        private SongSelectController _controller;

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Zero-based slot index within the expanded album.</summary>
        public int SlotIndex => slotIndex;

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Establishes the back-reference to the owning controller.
        /// Called by <see cref="SongSelectController"/> in <c>Start()</c> for every slot.
        /// Must be called before taps can be relayed.
        /// </summary>
        public void Initialize(SongSelectController controller)
        {
            _controller = controller;
        }

        // ── Data ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Populates the vinyl with display data from a <see cref="SongDefinition"/>.
        /// Pass <c>null</c> to clear the label (used when a slot has no song).
        /// </summary>
        public void Populate(SongDefinition song)
        {
            if (titleLabel != null)
                titleLabel.text = song != null ? song.Title : string.Empty;
        }

        // ── Inspector entry point ──────────────────────────────────────────────

        /// <summary>
        /// Wire the <c>TapOrDragFilter.onTap</c> UnityEvent on this GameObject to
        /// this method in the Inspector. Relays to
        /// <see cref="SongSelectController.OnSongTapped"/> with this slot's index.
        /// </summary>
        public void NotifyTapped()
        {
            _controller?.OnSongTapped(slotIndex);
        }
    }
}
