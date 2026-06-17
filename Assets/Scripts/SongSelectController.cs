using DG.Tweening;
using TMPro;
using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Central state machine for the Bongo song-select flow.
    ///
    /// <para><b>State model:</b></para>
    /// <list type="bullet">
    ///   <item><b>Browsing</b> — carousel drag enabled; only album-card taps are live.</item>
    ///   <item><b>Expanded</b> — carousel drag disabled; vinyl taps and tap-catcher are live.
    ///         Entered immediately when an album is tapped (before the expand animation completes)
    ///         so the tap-catcher can interrupt the animation via Collapse.</item>
    ///   <item><b>Previewing</b> — same gates as Expanded; same-vinyl tap again opens Popup,
    ///         different-vinyl tap switches the preview target.</item>
    ///   <item><b>Popup</b> — difficulty buttons, Start, and Close are live; everything else
    ///         is blocked; tap-catcher is inactive (popup has its own blocking backdrop).</item>
    /// </list>
    ///
    /// <para>Phase 2: carousel-to-center and card-lift animations via DOTween.
    /// Collapse cleanly interrupts a half-finished expand and reverses the lift.
    /// All other transitions remain instant.</para>
    /// </summary>
    public class SongSelectController : MonoBehaviour
    {
        // ── State enum ────────────────────────────────────────────────────────

        /// <summary>Lifecycle states of the song-select screen.</summary>
        public enum SelectState
        {
            /// <summary>Player is swiping through album cards in the carousel.</summary>
            Browsing,

            /// <summary>
            /// An album card has been tapped. Carousel input is disabled. The expand animation
            /// may still be running; state is set to Expanded immediately so the tap-catcher
            /// can call Collapse to interrupt mid-animation.
            /// </summary>
            Expanded,

            /// <summary>A vinyl is selected; a second tap on the same vinyl opens the popup.</summary>
            Previewing,

            /// <summary>The difficulty / confirm popup is open above the expanded album.</summary>
            Popup,
        }

        // ── Inspector references ──────────────────────────────────────────────

        [Header("Carousel")]
        [Tooltip("The CarouselController that hosts the album cards.")]
        [SerializeField] private CarouselController carousel;

        [Tooltip("UIMenuManager — kept for future page transitions (Phase 2+).")]
        [SerializeField] private UIMenuManager menuManager;

        [Header("Vinyls")]
        [Tooltip("Exactly 3 persistent vinyl-slot GameObjects, ordered left to right (slot 0, 1, 2). " +
                 "They are activated/deactivated by this controller; do not drive SetActive elsewhere.")]
        [SerializeField] private VinylItem[] vinyls = new VinylItem[3];

        [Header("Tap Catcher")]
        [Tooltip("Full-screen transparent Image that collapses the view on press. " +
                 "See TapCatcher component for scene-hierarchy setup requirements.")]
        [SerializeField] private TapCatcher tapCatcher;

        [Header("Popup")]
        [Tooltip("Root GameObject of the difficulty/confirm popup. Hidden at startup.")]
        [SerializeField] private GameObject popupRoot;

        [Tooltip("Text label inside the popup that displays the selected song title.")]
        [SerializeField] private TMP_Text popupSongTitle;

        [Tooltip("One indicator GameObject per difficulty slot (index 0 / 1 / 2). " +
                 "Made active for the selected difficulty, inactive for all others. " +
                 "Wire each difficulty button's onClick → SelectDifficulty(n) in the Inspector.")]
        [SerializeField] private GameObject[] difficultySelectedIndicators = new GameObject[3];

        [Header("Expand Animation")]
        [Tooltip("Time (seconds) to slide the carousel so the tapped album is centred.")]
        [SerializeField] private float centerDuration = 0.35f;

        [Tooltip("Easing curve for the carousel-centre tween.")]
        [SerializeField] private Ease centerEase = Ease.OutCubic;

        [Tooltip("Time (seconds) to lift the expanded album card up after centring.")]
        [SerializeField] private float liftDuration = 0.3f;

        [Tooltip("Easing curve for the lift (and reverse-lift on collapse).")]
        [SerializeField] private Ease liftEase = Ease.OutCubic;

        [Tooltip("How far (RectTransform units) the album card rises when expanded.")]
        [SerializeField] private float liftDistance = 150f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private SelectState _state = SelectState.Browsing;
        private AlbumItem   _expandedAlbum;
        private int         _previewingSlot     = -1;
        private int         _selectedDifficulty = -1;

        /// <summary>
        /// The album card's anchoredPosition captured immediately before any expand animation
        /// begins. Used by Collapse to restore the exact original Y regardless of how far
        /// the lift progressed.
        /// </summary>
        private Vector2 _originalAlbumPos;

        /// <summary>
        /// The carousel's <see cref="CarouselController.CurrentOffset"/> captured immediately
        /// before the expand animation begins (same moment as <see cref="_originalAlbumPos"/>).
        /// Used by Collapse to tween the carousel back to its exact pre-tap position so abort
        /// always feels like a full undo.
        /// </summary>
        private float _originalOffset;

        /// <summary>
        /// The single running DOTween Sequence for the current transition.
        /// Every new transition kills this first to prevent stacking.
        /// </summary>
        private Sequence _activeSequence;

        /// <summary>Current state of the song-select screen.</summary>
        public SelectState State => _state;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            // Subscribe to every AlbumItem that is a direct carousel child.
            for (int i = 0; i < carousel.transform.childCount; i++)
            {
                var album = carousel.transform.GetChild(i).GetComponent<AlbumItem>();
                if (album != null) album.Tapped += OnAlbumTapped;
            }

            // Give each vinyl a back-reference so it can relay taps.
            for (int i = 0; i < vinyls.Length; i++)
            {
                if (vinyls[i] != null) vinyls[i].Initialize(this);
            }

            // Tap-catcher and popup start inactive.
            if (tapCatcher != null)
            {
                tapCatcher.Initialize(this);
                tapCatcher.gameObject.SetActive(false);
            }

            if (popupRoot != null) popupRoot.SetActive(false);
            HideVinyls();
        }

        private void OnDestroy()
        {
            _activeSequence?.Kill();

            if (carousel == null) return;
            for (int i = 0; i < carousel.transform.childCount; i++)
            {
                var album = carousel.transform.GetChild(i).GetComponent<AlbumItem>();
                if (album != null) album.Tapped -= OnAlbumTapped;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called (via <see cref="AlbumItem.Tapped"/> event) when an album card is tapped.
        /// Guard: no-op unless <see cref="SelectState.Browsing"/>.
        /// <para>
        /// Immediately disables carousel drag, activates the tap-catcher, and transitions to
        /// <see cref="SelectState.Expanded"/> so Collapse can interrupt the animation.
        /// Then runs a two-step DOTween sequence: (1) slide carousel to centre the album,
        /// (2) lift the album card up. Vinyls are shown on sequence complete.
        /// </para>
        /// </summary>
        public void OnAlbumTapped(AlbumItem album)
        {
            if (_state != SelectState.Browsing) return;

            _expandedAlbum = album;
            var albumRect = album.GetComponent<RectTransform>();

            // Store originals BEFORE any animation so Collapse can restore both exactly.
            _originalAlbumPos = albumRect.anchoredPosition;
            _originalOffset   = carousel.CurrentOffset;

            // Lock input and expose the tap-catcher immediately so the player can cancel.
            carousel.SetInputEnabled(false);
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(true);

            // Enter Expanded now — prevents a second OnAlbumTapped re-entering this code,
            // and lets Collapse fire (guarded by Expanded/Previewing) during the animation.
            _state = SelectState.Expanded;

            // Target offset: the value of CurrentOffset at which this child sits at X = 0.
            float targetOffset = ComputeCenteredOffset(album.transform.GetSiblingIndex());

            _activeSequence?.Kill();
            _activeSequence = DOTween.Sequence()
                // Step 1 — slide carousel so the album is centred.
                .Append(DOTween.To(
                    () => carousel.CurrentOffset,
                    x  => carousel.CurrentOffset = x,
                    targetOffset,
                    centerDuration)
                    .SetEase(centerEase))
                // Step 2 — lift the album card upward. Only tweens Y; X is owned by the carousel.
                .Append(DOTween.To(
                    ()  => albumRect.anchoredPosition.y,
                    y   => albumRect.anchoredPosition = new Vector2(albumRect.anchoredPosition.x, y),
                    _originalAlbumPos.y + liftDistance,
                    liftDuration)
                    .SetEase(liftEase))
                // Complete — show vinyls now that the card is in position.
                .OnComplete(() => ShowVinyls(album.Definition))
                .SetLink(gameObject);
        }

        /// <summary>
        /// Collapses the expanded album and returns to <see cref="SelectState.Browsing"/>.
        /// Guard: no-op unless <see cref="SelectState.Expanded"/> or
        /// <see cref="SelectState.Previewing"/>.
        /// <para>
        /// Kills any in-flight expand animation, then runs a reverse sequence that simultaneously
        /// lowers the album card back to <see cref="_originalAlbumPos"/>.y and slides the carousel
        /// back to <see cref="_originalOffset"/>. Both axes animate over <c>liftDuration</c> via
        /// <c>.Join</c> so they arrive together. Carousel drag is re-enabled in OnComplete.
        /// </para>
        /// </summary>
        public void Collapse()
        {
            if (_state != SelectState.Expanded && _state != SelectState.Previewing) return;

            // Deactivate interactive elements immediately so no further taps arrive.
            HideVinyls();
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(false);
            StopPreview();
            _previewingSlot = -1;

            var albumRect = _expandedAlbum?.GetComponent<RectTransform>();
            float restoreY = _originalAlbumPos.y; // read before clearing _expandedAlbum

            _activeSequence?.Kill();

            if (albumRect != null)
            {
                _activeSequence = DOTween.Sequence()
                    // Lower the album card back to its stored original Y. If collapse fires
                    // mid-center-tween (before lift started) the current Y already equals
                    // restoreY, so DOTween treats this as a zero-distance tween and the
                    // sequence advances to OnComplete on the very next frame.
                    .Append(DOTween.To(
                        ()  => albumRect.anchoredPosition.y,
                        y   => albumRect.anchoredPosition = new Vector2(albumRect.anchoredPosition.x, y),
                        restoreY,
                        liftDuration)
                        .SetEase(liftEase))
                    // Simultaneously slide the carousel back to its exact pre-tap offset.
                    // Joined so both axes animate together over the same liftDuration.
                    .Join(DOTween.To(
                        ()  => carousel.CurrentOffset,
                        x   => carousel.CurrentOffset = x,
                        _originalOffset,
                        liftDuration)
                        .SetEase(liftEase))
                    .OnComplete(() =>
                    {
                        _expandedAlbum = null;
                        carousel.SetInputEnabled(true);
                        _state = SelectState.Browsing;
                    })
                    .SetLink(gameObject);
            }
            else
            {
                // Safety path — no rect found, reset synchronously.
                _expandedAlbum = null;
                carousel.SetInputEnabled(true);
                _state = SelectState.Browsing;
            }
        }

        /// <summary>
        /// Called (via <see cref="VinylItem.NotifyTapped"/>) when a vinyl slot is tapped.
        /// Guard: no-op in <see cref="SelectState.Browsing"/> or <see cref="SelectState.Popup"/>.
        /// <list type="bullet">
        ///   <item>Expanded → enter Previewing on the tapped slot.</item>
        ///   <item>Previewing, same slot → open the difficulty popup.</item>
        ///   <item>Previewing, different slot → switch preview target, stay Previewing.</item>
        /// </list>
        /// </summary>
        public void OnSongTapped(int songSlot)
        {
            if (_state == SelectState.Browsing || _state == SelectState.Popup) return;

            if (_state == SelectState.Expanded)
            {
                _previewingSlot = songSlot;
                // TODO(Phase 3): scale + spin vinyl at songSlot
                // TODO(Phase 5): StartPreview(songSlot)
                _state = SelectState.Previewing;
                return;
            }

            if (_state == SelectState.Previewing)
            {
                if (songSlot == _previewingSlot)
                {
                    OpenPopup();
                }
                else
                {
                    _previewingSlot = songSlot;
                    // TODO(Phase 3): move scale + spin to new vinyl
                    // TODO(Phase 5): restart preview for new slot
                    // State stays Previewing.
                }
            }
        }

        /// <summary>
        /// Commits the previewed song and launches the Bongo game mode.
        /// Guard: no-op unless <see cref="SelectState.Popup"/>.
        ///
        /// <para><b>Order is mandatory:</b> BPM and difficulty must both be set before
        /// <c>GameManager.StartGame()</c> — <c>BeatGenerator.Initialize</c> reads
        /// <c>difficultyIndex</c> at that point and never re-reads it, and
        /// <c>TimingCoordinator.Initialize</c> reads <c>metronome.bpm</c>.</para>
        /// </summary>
        public void FinalizeSong()
        {
            if (_state != SelectState.Popup) return;

            // 1. Resolve the chosen song; abort if nothing is selected.
            SongDefinition song = GetCurrentSong();
            if (song == null)
            {
                Debug.LogWarning("[SongSelect] FinalizeSong — no valid song resolved; aborting launch.");
                return;
            }

            // 2. Default difficulty to Starter (0) if nothing has been explicitly chosen.
            int selectedDifficulty = (_selectedDifficulty >= 0) ? _selectedDifficulty : 0;

            // 3. Stop any preview audio (Phase 5 will implement the body).
            StopPreview();

            // 4. Feed gameplay data directly from SongDefinition, bypassing SetMusic which
            //    iterates the old SongItem-based carousel children.
            GameManager.instance.metronome.bpm   = song.Bpm;
            AudioManager.instance.selectedSongIndex = song.TrackIndex;

            // 5–6. Set mode and difficulty before StartGame so BeatGenerator and
            //      TimingCoordinator read the correct values during initialisation.
            GameManager.instance.SetMode("Bongo");
            GameManager.instance.SetDifficulty(selectedDifficulty);

            // 7. Hand off to GameManager — the screen-cover transition will hide all UI.
            GameManager.instance.StartGame();
        }

        /// <summary>
        /// Stops any currently-playing song preview.
        /// Phase 5 will implement the body; the stub exists so callers compile cleanly.
        /// </summary>
        private void StopPreview()
        {
            // TODO(Phase 5): stop preview audio
        }

        /// <summary>
        /// Closes the difficulty popup and returns to <see cref="SelectState.Previewing"/>.
        /// Guard: no-op unless <see cref="SelectState.Popup"/>.
        /// </summary>
        public void ClosePopup()
        {
            if (_state != SelectState.Popup) return;

            if (popupRoot != null) popupRoot.SetActive(false);
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(true);
            _state = SelectState.Previewing;
        }

        /// <summary>
        /// Selects a difficulty by index and updates the indicator visuals.
        /// Wire each difficulty button's <c>onClick</c> to this method with the appropriate
        /// int parameter in the Inspector (Unity supports dynamic int arguments on UnityEvent).
        /// </summary>
        /// <param name="index">0 = Easy, 1 = Normal, 2 = Hard (or your naming convention).</param>
        public void SelectDifficulty(int index)
        {
            _selectedDifficulty = index;
            for (int i = 0; i < difficultySelectedIndicators.Length; i++)
            {
                if (difficultySelectedIndicators[i] != null)
                    difficultySelectedIndicators[i].SetActive(i == index);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Computes the <c>CurrentOffset</c> value at which the carousel child at
        /// <paramref name="childIndex"/> sits at anchoredPosition.x == 0. Mirrors the
        /// formula used internally by <see cref="CarouselController.CenterOnChild"/>.
        /// </summary>
        private float ComputeCenteredOffset(int childIndex)
        {
            int count = carousel.transform.childCount;
            if (count == 0) return 0f;
            float centerIndex = (count - 1) / 2f;
            return -(childIndex - centerIndex) * carousel.spacing;
        }

        /// <summary>
        /// Activates vinyl slots and populates each one from the album's song list.
        /// Slots with no corresponding song remain inactive.
        /// </summary>
        private void ShowVinyls(AlbumDefinition album)
        {
            if (album == null) return;
            var songs = album.Songs;
            for (int i = 0; i < vinyls.Length; i++)
            {
                if (vinyls[i] == null) continue;
                bool hasSong = i < songs.Count;
                vinyls[i].gameObject.SetActive(hasSong);
                if (hasSong) vinyls[i].Populate(songs[i]);
            }
        }

        /// <summary>Deactivates all vinyl slot GameObjects.</summary>
        private void HideVinyls()
        {
            foreach (var vinyl in vinyls)
            {
                if (vinyl != null) vinyl.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Transitions to <see cref="SelectState.Popup"/>: deactivates the tap-catcher,
        /// populates the popup's song title, resets difficulty selection, then activates the popup.
        /// </summary>
        private void OpenPopup()
        {
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(false);

            SongDefinition song = GetCurrentSong();
            if (popupSongTitle != null)
                popupSongTitle.text = song?.Title ?? string.Empty;

            _selectedDifficulty = -1;
            for (int i = 0; i < difficultySelectedIndicators.Length; i++)
            {
                if (difficultySelectedIndicators[i] != null)
                    difficultySelectedIndicators[i].SetActive(false);
            }

            if (popupRoot != null) popupRoot.SetActive(true);
            _state = SelectState.Popup;
        }

        /// <summary>
        /// Returns the <see cref="SongDefinition"/> at the current previewing slot,
        /// or <c>null</c> if unavailable.
        /// </summary>
        private SongDefinition GetCurrentSong()
        {
            if (_expandedAlbum == null || _previewingSlot < 0) return null;
            var songs = _expandedAlbum.Definition?.Songs;
            if (songs == null || _previewingSlot >= songs.Count) return null;
            return songs[_previewingSlot];
        }
    }
}
