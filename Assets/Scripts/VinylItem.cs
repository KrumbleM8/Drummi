using DG.Tweening;
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

        [Header("Selection Animation")]
        [Tooltip("Uniform scale applied to this vinyl when it is the active preview.")]
        [SerializeField] private float selectedScale = 1.15f;

        [Tooltip("Duration (seconds) for the scale-up (Select) and scale-down (Deselect) tweens.")]
        [SerializeField] private float scaleDuration = 0.18f;

        [Tooltip("Easing for the scale-up tween. Scale-down uses DOTween's default (linear).")]
        [SerializeField] private Ease scaleEase = Ease.OutBack;

        [Tooltip("Degrees per second for the continuous Z-rotation while selected.")]
        [SerializeField] private float spinDegreesPerSecond = 120f;

        [Header("Start Icon")]
        [Tooltip("Optional 'tap to play' indicator shown only while this vinyl is the active preview. " +
                 "Assign a child GameObject that lives under a non-rotating anchor, OR leave it as a " +
                 "direct child — its world rotation is clamped to identity each frame to stay upright. " +
                 "Must be set inactive by default in the prefab.")]
        [SerializeField] private GameObject startIcon;

        private SongSelectController _controller;
        private bool  _spinning;
        private Tween _scaleTween;

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Zero-based slot index within the expanded album.</summary>
        public int SlotIndex => slotIndex;

        // ── Unity messages (early) ────────────────────────────────────────────

        private void OnEnable()
        {
            // Ensure the icon is hidden whenever this vinyl is activated (initial
            // show or pool-reuse), before any Select() call has been made.
            startIcon?.SetActive(false);
        }

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

        // ── Selection animation ───────────────────────────────────────────────

        /// <summary>
        /// Marks this vinyl as the active preview: scales it up and starts the
        /// continuous Z-spin. Safe to call while already selected (re-entrancy is
        /// handled by killing the previous scale tween first).
        /// </summary>
        public void Select()
        {
            _scaleTween?.Kill();
            _scaleTween = transform
                .DOScale(new Vector3(selectedScale, selectedScale, 1f), scaleDuration)
                .SetEase(scaleEase)
                .SetLink(gameObject);
            _spinning = true;
            startIcon?.SetActive(true);
        }

        /// <summary>
        /// Marks this vinyl as inactive: scales it back to 1 and stops the spin.
        /// Rotation is snapped to identity immediately — the shrink animation covers the
        /// snap visually, and a residual angle would break the Phase 3 rest-state contract.
        /// </summary>
        public void Deselect()
        {
            _scaleTween?.Kill();
            _scaleTween = transform
                .DOScale(Vector3.one, scaleDuration)
                .SetLink(gameObject);
            _spinning = false;
            transform.localRotation = Quaternion.identity;
            startIcon?.SetActive(false);
        }

        /// <summary>
        /// Hard-stops selection state without starting any new tween. Called by
        /// <see cref="SongSelectController.HideVinyls"/> before <c>SetActive(false)</c>
        /// so the controller can reset transforms directly with no live tweens remaining.
        /// </summary>
        public void CancelSelection()
        {
            _scaleTween?.Kill();
            _scaleTween = null;
            _spinning   = false;
            startIcon?.SetActive(false);
        }

        // ── Unity messages ────────────────────────────────────────────────────

        private void Update()
        {
            if (!_spinning) return;
            transform.Rotate(0f, 0f, -spinDegreesPerSecond * Time.deltaTime);

            // Counter-rotation: the startIcon is a child of this (rotating) transform,
            // so lock its world rotation to identity each frame to keep it upright.
            if (startIcon != null && startIcon.activeSelf)
                startIcon.transform.rotation = Quaternion.identity;
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
