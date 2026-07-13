using System.Collections;
using System.Collections.Generic;
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

        [Tooltip("Duration (seconds) for the popup slide-up / slide-down animation.")]
        [SerializeField] private float popupSlideDuration = 0.25f;

        [Tooltip("Easing curve for the popup slide animation (horizontal axis = normalised time, vertical = normalised position).")]
        [SerializeField] private AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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

        [Header("Vinyl Fan Animation")]
        [Tooltip("Shared start anchoredPosition (relative to SongSelect) that all vinyls " +
                 "animate FROM. Position this at roughly the centre of the lifted album cover.")]
        [SerializeField] private Vector2 vinylFanStartLocalPos = new Vector2(0f, -60f);

        [Tooltip("Uniform scale applied to every vinyl at animation start. Tweens to 1.")]
        [SerializeField] private float vinylFanStartScale = 0.6f;

        [Tooltip("Duration (seconds) for each vinyl's position and scale fan-out tween.")]
        [SerializeField] private float vinylFanDuration = 0.35f;

        [Tooltip("Easing applied to both the position and scale fan-out tweens.")]
        [SerializeField] private Ease vinylFanEase = Ease.OutCubic;

        [Tooltip("Extra delay (seconds) added per slot (slot 0 = no delay, " +
                 "slot 1 = 1×stagger, slot 2 = 2×stagger). Set 0 for simultaneous.")]
        [SerializeField] private float vinylFanStagger = 0.06f;

        [Header("Start Icon")]
        [Tooltip("Overlay indicator that tracks the currently-previewed vinyl. Must be parented " +
                 "under SongSelect so its anchoredPosition space matches the vinyl slots.")]
        [SerializeField] private RectTransform startIcon;

        [Header("Audio Preview")]
        [Tooltip("Dedicated AudioSource for song-select previews. Created automatically in Awake " +
                 "if left null. Must NOT be one of AudioManager.speakers[].")]
        [SerializeField] private AudioSource previewSource;

        [Tooltip("How many seconds of the track to play per loop iteration.")]
        [SerializeField] private float previewLengthSeconds = 10f;

        [Tooltip("Volume fade-in duration (seconds) at the start of each preview iteration.")]
        [SerializeField] private float previewFadeInSeconds = 0.6f;

        [Tooltip("Volume fade-out duration (seconds) at the end of each preview iteration.")]
        [SerializeField] private float previewFadeOutSeconds = 0.4f;

        [Tooltip("Duration (seconds) to fade to silence when StopPreview is called mid-loop.")]
        [SerializeField] private float previewStopFadeSeconds = 0.15f;

        [Tooltip("Duration (seconds) for the simultaneous fade-out/fade-in when switching " +
                 "from one vinyl preview to another while audio is already playing.")]
        [SerializeField] private float crossfadeDuration = 0.4f;

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

        /// <summary>True after <see cref="Start"/> has executed. Guards <see cref="OnEnable"/>
        /// from running reset logic before the controller is fully initialised.</summary>
        private bool _started;

        /// <summary>
        /// Authored rest anchoredPositions read from each vinyl's RectTransform in Start.
        /// Stored separately so that editor repositioning is picked up automatically.
        /// </summary>
        private Vector2[] _vinylRestPos;

        /// <summary>
        /// Running fan-out tweens (position + scale per vinyl, plus the sibling-restoration
        /// callback). All are killed by <see cref="KillVinylTweens"/> when Collapse fires
        /// mid-fan so no vinyl is left stranded behind the carousel or mid-travel.
        /// </summary>
        private readonly List<Tween> _vinylTweens = new List<Tween>();

        private Tween     _previewFadeTween;
        private Tween     _crossfadeFadeTween;
        private Coroutine _previewLoopCoroutine;
        private AudioSource _outgoingSource;

        private RectTransform _popupRect;
        private Vector2       _popupShownPos;
        private Vector2       _popupHiddenPos;
        private Coroutine     _popupCoroutine;

        /// <summary>Current state of the song-select screen.</summary>
        public SelectState State => _state;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Skip on first activation — Start() handles initial setup.
            if (!_started) return;
            ResetToDefault();
        }

        private void Awake()
        {
            if (previewSource == null)
            {
                previewSource              = gameObject.AddComponent<AudioSource>();
                previewSource.playOnAwake  = false;
                previewSource.loop         = false;
                previewSource.spatialBlend = 0f;
            }

            _outgoingSource              = gameObject.AddComponent<AudioSource>();
            _outgoingSource.playOnAwake  = false;
            _outgoingSource.loop         = false;
            _outgoingSource.spatialBlend = 0f;
        }

        private void OnDisable()
        {
            TearDownPreview();
            if (previewSource  != null) previewSource.Stop();
            if (_outgoingSource != null) _outgoingSource.Stop();
        }

        private void Start()
        {
            _started = true;

            // Cache each vinyl's authored anchoredPosition before HideVinyls resets anything.
            // Reading from the RectTransform means Inspector edits are picked up automatically.
            _vinylRestPos = new Vector2[vinyls.Length];
            for (int i = 0; i < vinyls.Length; i++)
            {
                if (vinyls[i] != null)
                    _vinylRestPos[i] = vinyls[i].GetComponent<RectTransform>().anchoredPosition;
            }

            // Listen for page-shown events so we can reset when the Song page is re-entered
            // from any path (e.g. returning from the results screen). SongSelectController
            // lives on a root-level object that is never deactivated, so OnEnable alone
            // is not sufficient — this subscription covers all navigation paths.
            if (menuManager != null) menuManager.OnPageShown += HandlePageShown;

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

            if (popupRoot != null)
            {
                Canvas.ForceUpdateCanvases();
                _popupRect      = popupRoot.GetComponent<RectTransform>();
                _popupShownPos  = _popupRect.anchoredPosition;
                _popupHiddenPos = new Vector2(_popupShownPos.x, _popupShownPos.y - _popupRect.rect.height - 100f);
                _popupRect.anchoredPosition = _popupHiddenPos;
                popupRoot.SetActive(false);
            }
            HideVinyls();
        }

        /// <summary>
        /// Resets the controller to its default browsing state. Called by <see cref="OnEnable"/>
        /// whenever the song-select page is re-activated (e.g. returning from the results screen)
        /// so no stale expanded album, vinyls, popup, or audio preview survives the round-trip.
        /// </summary>
        private void ResetToDefault()
        {
            // Kill any in-flight expand / collapse sequence.
            _activeSequence?.Kill();
            _activeSequence = null;

            // Restore the lifted album card to its pre-expand Y before clearing the reference.
            if (_expandedAlbum != null)
            {
                var albumRect = _expandedAlbum.GetComponent<RectTransform>();
                if (albumRect != null)
                    albumRect.anchoredPosition = new Vector2(albumRect.anchoredPosition.x, _originalAlbumPos.y);
                _expandedAlbum.SetStartIconVisible(true);
                _expandedAlbum = null;
            }

            // Re-enable carousel drag (may have been locked by an expand).
            carousel?.SetInputEnabled(true);

            // Hide vinyls: kills fan tweens, calls CancelSelection on each, resets transforms.
            HideVinyls();

            // Deactivate the tap-catcher overlay.
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(false);

            // Close popup instantly (no slide animation).
            if (_popupCoroutine != null) { StopCoroutine(_popupCoroutine); _popupCoroutine = null; }
            if (popupRoot != null && _popupRect != null)
            {
                _popupRect.anchoredPosition = _popupHiddenPos;
                popupRoot.SetActive(false);
            }

            // Stop any preview audio (OnDisable already ran TearDown, but be defensive).
            TearDownPreview();
            if (previewSource  != null) previewSource.Stop();
            if (_outgoingSource != null) _outgoingSource.Stop();

            // Reset state variables.
            _state              = SelectState.Browsing;
            _previewingSlot     = -1;
            _selectedDifficulty = -1;
        }

        private void OnDestroy()
        {
            if (menuManager != null) menuManager.OnPageShown -= HandlePageShown;

            _activeSequence?.Kill();
            KillVinylTweens();
            TearDownPreview();
            if (previewSource  != null) previewSource.Stop();
            if (_outgoingSource != null) _outgoingSource.Stop();

            if (carousel == null) return;
            for (int i = 0; i < carousel.transform.childCount; i++)
            {
                var album = carousel.transform.GetChild(i).GetComponent<AlbumItem>();
                if (album != null) album.Tapped -= OnAlbumTapped;
            }
        }

        /// <summary>
        /// Received from <see cref="UIMenuManager.OnPageShown"/> whenever any page activates.
        /// Resets to Browsing when the Song page is shown so stale popup/vinyl state from a
        /// prior session is never visible on re-entry.
        /// </summary>
        private void HandlePageShown(string pageName)
        {
            if (pageName == "Song") ResetToDefault();
        }

        private void LateUpdate()
        {
            if (_state != SelectState.Previewing || startIcon == null || _previewingSlot < 0) return;
            if (_previewingSlot >= vinyls.Length || vinyls[_previewingSlot] == null) return;
            startIcon.anchoredPosition = vinyls[_previewingSlot].GetComponent<RectTransform>().anchoredPosition;
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

            // Hide the play icon immediately — it is not relevant while a vinyl is showing.
            album.SetStartIconVisible(false);

            // Store originals BEFORE any animation so Collapse can restore both exactly.
            _originalAlbumPos = albumRect.anchoredPosition;
            _originalOffset   = carousel.CurrentOffset;

            // Lock input and expose the tap-catcher immediately so the player can cancel.
            carousel.SetInputEnabled(false);
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(true);

            // Enter Expanded now — prevents a second OnAlbumTapped re-entering this code,
            // and lets Collapse fire (guarded by Expanded/Previewing) during the animation.
            _state = SelectState.Expanded;

            // Target offset: nearest equivalent of the canonical center so the carousel
            // animates the short way even after multiple wraps.
            float canonical    = ComputeCenteredOffset(album.transform.GetSiblingIndex());
            float targetOffset = NearestEquivalentOffset(canonical, carousel.CurrentOffset);

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
                    // Use the nearest equivalent so collapse never flings across wraps.
                    // Captured before the tween so it isn't re-evaluated as CurrentOffset changes.
                    .Join(DOTween.To(
                        ()  => carousel.CurrentOffset,
                        x   => carousel.CurrentOffset = x,
                        NearestEquivalentOffset(_originalOffset, carousel.CurrentOffset),
                        liftDuration)
                        .SetEase(liftEase))
                    .OnComplete(() =>
                    {
                        _expandedAlbum.SetStartIconVisible(true);
                        _expandedAlbum = null;
                        carousel.SetInputEnabled(true);
                        _state = SelectState.Browsing;
                    })
                    .SetLink(gameObject);
            }
            else
            {
                // Safety path — no rect found, reset synchronously.
                _expandedAlbum.SetStartIconVisible(true);
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
                vinyls[songSlot].Select();
                StartPreview(songSlot);
                if (startIcon != null) startIcon.gameObject.SetActive(true);
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
                    // Capture old slot before overwriting so we can deselect it.
                    int prevSlot    = _previewingSlot;
                    _previewingSlot = songSlot;
                    if (prevSlot >= 0 && prevSlot < vinyls.Length && vinyls[prevSlot] != null)
                        vinyls[prevSlot].Deselect();
                    vinyls[songSlot].Select();
                    CrossfadeToPreview(songSlot);
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

            // 2. Default difficulty to Standard/Normal (1) if nothing has been explicitly chosen.
            //    OpenPopup always calls SelectDifficulty(1), so this is a safety net only.
            int selectedDifficulty = (_selectedDifficulty >= 0) ? _selectedDifficulty : 1;

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
        /// Fades out and stops any currently-playing preview. Safe to call when nothing is playing.
        /// </summary>
        private void StopPreview()
        {
            TearDownPreview();
            if (previewSource != null && previewSource.isPlaying)
            {
                previewSource.DOFade(0f, previewStopFadeSeconds)
                    .OnComplete(() => previewSource.Stop())
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// Resolves the song for <paramref name="slot"/> within the currently expanded album,
        /// then starts a looping preview: seeks to <see cref="SongDefinition.PreviewStartSeconds"/>,
        /// fades in, holds, fades out, re-seeks, and repeats until <see cref="StopPreview"/> is
        /// called. No-op if the song or clip cannot be resolved.
        /// <para>
        /// <paramref name="firstFadeIn"/> overrides the first iteration's fade-in duration —
        /// used by <see cref="CrossfadeToPreview"/> so the incoming clip fades in over
        /// <see cref="crossfadeDuration"/> while the outgoing clip fades out simultaneously.
        /// Subsequent loop iterations always use <see cref="previewFadeInSeconds"/>.
        /// </para>
        /// </summary>
        private void StartPreview(int slot, float firstFadeIn = -1f)
        {
            TearDownPreview();
            if (previewSource != null) previewSource.Stop();

            if (_expandedAlbum == null) return;
            var songs = _expandedAlbum.Definition?.Songs;
            if (songs == null || slot < 0 || slot >= songs.Count) return;
            SongDefinition song = songs[slot];
            if (song == null) return;

            var tracks = AudioManager.instance?.musicTracks;
            if (tracks == null || song.TrackIndex < 0 || song.TrackIndex >= tracks.Length) return;
            AudioClip clip = tracks[song.TrackIndex];
            if (clip == null) return;

            // Clamp so the preview window fits within the clip.
            float start  = song.PreviewStartSeconds;
            float length = previewLengthSeconds;
            if (start + length > clip.length)
                start = Mathf.Max(0f, clip.length - length);

            previewSource.clip   = clip;
            previewSource.volume = 0f;
            previewSource.time   = start;
            previewSource.Play();

            float fadeIn = firstFadeIn >= 0f ? firstFadeIn : previewFadeInSeconds;
            _previewLoopCoroutine = StartCoroutine(PreviewLoop(start, length, fadeIn));
        }

        /// <summary>
        /// Snapshots the currently playing preview into an outgoing <see cref="AudioSource"/>,
        /// fades it out over <see cref="crossfadeDuration"/>, and simultaneously starts the
        /// new clip fading in over the same duration. The outgoing source is stopped automatically
        /// when its fade completes. Safe to call when nothing is playing (degrades to StartPreview).
        /// </summary>
        private void CrossfadeToPreview(int slot)
        {
            // Snapshot current playback into the outgoing source before TearDown kills the tween.
            _crossfadeFadeTween?.Kill();
            if (previewSource != null && previewSource.isPlaying)
            {
                _outgoingSource.clip   = previewSource.clip;
                _outgoingSource.time   = previewSource.time;
                _outgoingSource.volume = previewSource.volume;
                _outgoingSource.Play();
                _crossfadeFadeTween = _outgoingSource.DOFade(0f, crossfadeDuration)
                    .OnComplete(() => _outgoingSource.Stop())
                    .SetLink(gameObject);
            }

            // Start the new clip on previewSource, using crossfadeDuration as the first fade-in
            // so incoming and outgoing overlap perfectly.
            StartPreview(slot, crossfadeDuration);
        }

        /// <summary>
        /// Coroutine that owns the preview loop structure: fade-in → hold → fade-out → re-seek,
        /// repeating indefinitely. DOTween owns the actual volume fades. Uses
        /// <c>WaitForSecondsRealtime</c> so the loop stays accurate if game time is paused.
        /// <paramref name="fadeInDuration"/> applies only to the first iteration; all subsequent
        /// iterations use <see cref="previewFadeInSeconds"/>.
        /// </summary>
        private IEnumerator PreviewLoop(float start, float length, float fadeInDuration)
        {
            while (true)
            {
                float holdTime = Mathf.Max(0f, length - fadeInDuration - previewFadeOutSeconds);

                _previewFadeTween = previewSource.DOFade(1f, fadeInDuration).SetLink(gameObject);
                yield return new WaitForSecondsRealtime(fadeInDuration);

                if (holdTime > 0f)
                    yield return new WaitForSecondsRealtime(holdTime);

                _previewFadeTween = previewSource.DOFade(0f, previewFadeOutSeconds).SetLink(gameObject);
                yield return new WaitForSecondsRealtime(previewFadeOutSeconds);

                previewSource.time   = start;
                previewSource.volume = 0f;

                // After the first iteration all subsequent loops use the normal fade-in.
                fadeInDuration = previewFadeInSeconds;
            }
        }

        /// <summary>
        /// Kills both fade tweens and stops the preview coroutine without touching either
        /// <see cref="AudioSource"/> directly. Called by <see cref="StopPreview"/> (which
        /// then fades out the main source) and at the top of <see cref="StartPreview"/> /
        /// <see cref="CrossfadeToPreview"/> to prevent orphaned coroutines and tweens.
        /// </summary>
        private void TearDownPreview()
        {
            _previewFadeTween?.Kill();
            _previewFadeTween = null;
            _crossfadeFadeTween?.Kill();
            _crossfadeFadeTween = null;
            if (_previewLoopCoroutine != null)
            {
                StopCoroutine(_previewLoopCoroutine);
                _previewLoopCoroutine = null;
            }
            if (_outgoingSource != null) _outgoingSource.Stop();
        }

        /// <summary>
        /// Closes the difficulty popup and returns to <see cref="SelectState.Previewing"/>.
        /// Guard: no-op unless <see cref="SelectState.Popup"/>.
        /// </summary>
        public void ClosePopup()
        {
            if (_state != SelectState.Popup) return;

            AnimatePopup(show: false);
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(true);
            if (startIcon != null) startIcon.gameObject.SetActive(true);

            // Popup is dismissed — re-select the still-previewing vinyl so it
            // scales and spins again as the player returns to Previewing state.
            if (_previewingSlot >= 0 && _previewingSlot < vinyls.Length && vinyls[_previewingSlot] != null)
                vinyls[_previewingSlot].Select();

            // Restart the audio preview for the still-previewing slot.
            StartPreview(_previewingSlot);

            _state = SelectState.Previewing;
        }

        private void AnimatePopup(bool show)
        {
            if (popupRoot == null) return;
            if (_popupCoroutine != null) StopCoroutine(_popupCoroutine);
            _popupCoroutine = StartCoroutine(PopupRoutine(show));
        }

        private IEnumerator PopupRoutine(bool show)
        {
            popupRoot.SetActive(true);

            Vector2 from = show ? _popupHiddenPos : _popupShownPos;
            Vector2 to   = show ? _popupShownPos  : _popupHiddenPos;

            float elapsed = 0f;
            while (elapsed < popupSlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = popupCurve.Evaluate(Mathf.Clamp01(elapsed / popupSlideDuration));
                _popupRect.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }

            _popupRect.anchoredPosition = to;
            if (!show) popupRoot.SetActive(false);
            _popupCoroutine = null;
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
        /// Returns the offset that is visually equivalent to <paramref name="targetOffset"/>
        /// and numerically closest to <paramref name="currentOffset"/>.
        /// <para>
        /// The carousel's offset space is modular with period <c>count × spacing</c>: any two
        /// offsets that differ by an integer multiple of that period produce an identical visual
        /// layout. Animating toward the nearest equivalent prevents cross-wrap lurching when a
        /// player taps an album (or collapses back) after scrolling through several wraps.
        /// </para>
        /// </summary>
        private float NearestEquivalentOffset(float targetOffset, float currentOffset)
        {
            int count = carousel.transform.childCount;
            if (count == 0) return targetOffset;
            float period = count * carousel.spacing;
            if (period <= 0f) return targetOffset;
            float n = Mathf.Round((currentOffset - targetOffset) / period);
            return targetOffset + n * period;
        }

        /// <summary>
        /// Populates each vinyl from the album's song list and kicks off the fan-out animation:
        /// vinyls start at <see cref="vinylFanStartLocalPos"/> (scaled down, behind the album
        /// cover) and tween to their authored rest positions with a per-slot stagger.
        /// Slots with no corresponding song remain inactive.
        /// </summary>
        private void ShowVinyls(AlbumDefinition album)
        {
            if (album == null) return;
            var songs      = album.Songs;
            int lastActive = -1;

            for (int i = 0; i < vinyls.Length; i++)
            {
                if (vinyls[i] == null) continue;

                bool hasSong = i < songs.Count;
                if (!hasSong)
                {
                    vinyls[i].gameObject.SetActive(false);
                    continue;
                }

                vinyls[i].Populate(songs[i]);

                var rt = vinyls[i].GetComponent<RectTransform>();

                // Set start state: positioned at cover centre, scaled down, rotation clean.
                vinyls[i].gameObject.SetActive(true);
                rt.anchoredPosition = vinylFanStartLocalPos;
                rt.localScale       = new Vector3(vinylFanStartScale, vinylFanStartScale, 1f);
                rt.localRotation    = Quaternion.identity;

                // Move behind the carousel (sibling 0) so the vinyl appears to slide out
                // FROM UNDER the album cover during travel. Processing slots 0→1→2 in order
                // means the final arrangement is [Vinyl2, Vinyl1, Vinyl0, Carousel, …] —
                // all three vinyls are below the carousel in render order. ✓
                vinyls[i].transform.SetSiblingIndex(0);

                float delay = i * vinylFanStagger;

                Tween posTween = rt.DOAnchorPos(_vinylRestPos[i], vinylFanDuration)
                    .SetEase(vinylFanEase)
                    .SetDelay(delay)
                    .SetLink(gameObject);

                Tween scaleTween = rt.DOScale(Vector3.one, vinylFanDuration)
                    .SetEase(vinylFanEase)
                    .SetDelay(delay)
                    .SetLink(gameObject);

                _vinylTweens.Add(posTween);
                _vinylTweens.Add(scaleTween);
                lastActive = i;
            }

            if (lastActive >= 0)
            {
                // Single restoration callback fires after the last vinyl arrives.
                // Restoring all indices together (rather than per-vinyl in OnComplete)
                // avoids the cascading sibling-shift that occurs when earlier vinyls
                // complete and shift later ones before they finish travelling.
                float restoreAt = vinylFanDuration + lastActive * vinylFanStagger;
                Tween restoreTween = DOVirtual.DelayedCall(restoreAt, RestoreVinylSiblingOrder, false)
                    .SetLink(gameObject);
                _vinylTweens.Add(restoreTween);
            }
        }

        /// <summary>
        /// Kills any running fan-out tweens and instantly deactivates all vinyl slot GameObjects,
        /// resetting each to its rest transform and authored sibling index. Iterates in reverse
        /// index order so sibling restoration does not cascade-shift lower-index vinyls out of
        /// position. Safe to call from <see cref="Start"/> (before any animation) and from
        /// <see cref="Collapse"/> (mid-fan-out).
        /// </summary>
        private void HideVinyls()
        {
            if (startIcon != null) startIcon.gameObject.SetActive(false);
            KillVinylTweens();

            // Reverse iteration: restoring the highest sibling index first prevents each
            // SetSiblingIndex call from displacing vinyls that haven't been restored yet.
            for (int i = vinyls.Length - 1; i >= 0; i--)
            {
                if (vinyls[i] == null) continue;
                // Kill any running scale tween and stop the spin loop before deactivating
                // so no vinyl is left with _spinning=true or a live tween while inactive.
                vinyls[i].CancelSelection();
                vinyls[i].gameObject.SetActive(false);

                var rt = vinyls[i].GetComponent<RectTransform>();
                if (_vinylRestPos != null && i < _vinylRestPos.Length)
                    rt.anchoredPosition = _vinylRestPos[i];
                rt.localScale    = Vector3.one;
                rt.localRotation = Quaternion.identity;

                vinyls[i].transform.SetSiblingIndex(3 + i);
            }
        }

        /// <summary>
        /// Transitions to <see cref="SelectState.Popup"/>: deactivates the tap-catcher,
        /// populates the popup's song title, preselects Normal difficulty, then activates the popup.
        /// <para>
        /// Difficulty always resets to Standard/Normal (index 1) on every open — intentional.
        /// <see cref="SelectDifficulty"/> both stores the value and drives the indicator visuals,
        /// keeping them in sync via a single call.
        /// </para>
        /// </summary>
        private void OpenPopup()
        {
            if (tapCatcher != null) tapCatcher.gameObject.SetActive(false);
            if (startIcon != null) startIcon.gameObject.SetActive(false);

            // Stop audio preview while the popup is open.
            StopPreview();

            // Settle the selected vinyl — popup covers it, and we don't want it
            // spinning/scaled when the popup reopens or if the popup is dismissed.
            if (_previewingSlot >= 0 && _previewingSlot < vinyls.Length && vinyls[_previewingSlot] != null)
                vinyls[_previewingSlot].Deselect();

            SongDefinition song = GetCurrentSong();
            if (popupSongTitle != null)
                popupSongTitle.text = song?.Title ?? string.Empty;

            // Preselect Standard/Normal (1). SelectDifficulty writes _selectedDifficulty
            // and updates all indicator GameObjects — single source of truth.
            SelectDifficulty(1);

            AnimatePopup(show: true);
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

        /// <summary>
        /// Kills and clears all tracked vinyl fan-out tweens (position, scale, and the
        /// sibling-restoration callback). Pending <c>OnComplete</c> callbacks are suppressed,
        /// so a mid-fan collapse cannot trigger a stale sibling restore.
        /// </summary>
        private void KillVinylTweens()
        {
            foreach (var t in _vinylTweens)
                t?.Kill();
            _vinylTweens.Clear();
        }

        /// <summary>
        /// Restores all vinyl sibling indices to their original authored positions (3 + i).
        /// Iterates from the highest index down so that each <c>SetSiblingIndex</c> call
        /// does not cascade-shift vinyls at lower indices that haven't been restored yet.
        /// Called by a <see cref="DOVirtual.DelayedCall"/> at the end of the fan-out so all
        /// three vinyls are placed in a single synchronised batch.
        /// </summary>
        private void RestoreVinylSiblingOrder()
        {
            for (int i = vinyls.Length - 1; i >= 0; i--)
            {
                if (vinyls[i] != null)
                    vinyls[i].transform.SetSiblingIndex(3 + i);
            }
        }
    }
}
