using System;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Component on each album-card GameObject that is a direct child of the
    /// <see cref="CarouselController"/>. Holds the <see cref="AlbumDefinition"/>
    /// and raises a C# event so <see cref="SongSelectController"/> can respond
    /// without any per-item Inspector coupling to the controller.
    /// </summary>
    public class AlbumItem : MonoBehaviour
    {
        [SerializeField] private AlbumDefinition definition;

        /// <summary>The Image on this card that displays the album cover art.</summary>
        [SerializeField] private Image coverImage;

        /// <summary>The play-button icon shown below the album cover while browsing.
        /// Hidden when this album is expanded; restored when the carousel returns.</summary>
        [SerializeField] private GameObject startIcon;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (coverImage != null && definition != null && definition.Cover != null)
                coverImage.sprite = definition.Cover;
        }

        // ── Event ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when this album card is tapped (after drag suppression).
        /// <see cref="SongSelectController"/> subscribes at runtime; no Inspector
        /// wiring to the controller is needed on individual cards.
        /// </summary>
        public event Action<AlbumItem> Tapped;

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>The album data asset assigned to this carousel card.</summary>
        public AlbumDefinition Definition => definition;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Shows or hides the start icon. Called by <see cref="SongSelectController"/>
        /// when this album is expanded (false) or the carousel returns (true).</summary>
        public void SetStartIconVisible(bool visible) => startIcon?.SetActive(visible);

        // ── Inspector entry point ──────────────────────────────────────────────

        /// <summary>
        /// Wire the <c>TapOrDragFilter.onTap</c> (or <c>TapUnlessDragged.onTap</c>)
        /// UnityEvent on this GameObject to this method in the Inspector.
        /// Raises <see cref="Tapped"/> so <see cref="SongSelectController"/> can respond.
        /// Remove any old <c>onTap → UIMenuManager.ShowPage("Difficulty")</c> wiring —
        /// albums no longer jump directly to the Difficulty page.
        /// </summary>
        public void NotifyTapped() => Tapped?.Invoke(this);
    }
}
