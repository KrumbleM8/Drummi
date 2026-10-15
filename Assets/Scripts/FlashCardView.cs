using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Card presentation variants, chosen per run by FlashCardsModeDefinition.
    /// Grid = the classic memorize-then-perform card. ColumnScroll (the Snake
    /// variant) = a single vertical column of beat squares that steps DOWN one
    /// slot per beat onto an enlarged bottom hit zone — same timing/scoring
    /// mechanics, scrolling sight-reading presentation.
    /// </summary>
    public enum FlashCardLayout
    {
        Grid,
        ColumnScroll,
    }

    /// <summary>
    /// Renders the Flash Card: a 2x4 grid of squares (beats 1–4 top row, 5–8 bottom,
    /// reading order top-left → bottom-right) that pop in from scale 0 on their beats during
    /// the reveal bar and stay visible through the perform bar.
    ///
    /// TIMING CONTRACT: which square reveals, and when, is driven entirely by
    /// FlashCardController's GameClock-polled events (OnCardRevealStart /
    /// OnSquareReveal / OnPerformStart / OnCardEvaluated) — beat accuracy lives in
    /// the controller, matching the CRIndicator convention. DOTween here is cosmetic
    /// only (reveal pop/hit pop), with SetLink + kill-before-reuse. The perform-bar beat cursor
    /// is the one locally polled piece: a pure function of GameClock.GameTime against
    /// the controller's PhraseTiming — no coroutines, no Time.time.
    ///
    /// Layout: the grid is RUNTIME-BUILT from squarePrefab (EnsureGrid) because the
    /// row count is dynamic (2/3/5 rows in endless mode) — manual RectTransform
    /// placement, no GridLayoutGroup; squares persist until the slot count changes.
    ///
    /// This component lives on an always-active object (the FlashCardController's) so
    /// events are never missed; <see cref="cardRoot"/> is toggled for visibility.
    /// All card Images must have raycastTarget = false so pad input passes through.
    ///
    /// INSPECTOR SETUP:
    ///   controller  — FlashCardController whose events drive this view
    ///   cardRoot    — panel root under DungeonCanvas/GameplayUI (starts inactive)
    ///   cardBorder  — border image behind the card background ("your turn" state)
    ///   squarePrefab — FlashCardSquareWidget prefab (root 140x140, back image, glyph + cursor children)
    ///   noteSprite / restSprite / eighthPairSprite / offbeatEighthSprite /
    ///   sixteenthQuadSprite / tripletSprite — per-template notation glyphs
    ///   (placeholder art, drop-in swap later)
    /// </summary>
    public class FlashCardView : MonoBehaviour
    {
        [System.Serializable]
        public class Square
        {
            [Tooltip("Square root — reveal pop tween scales this from 0.")]
            public RectTransform root;
            [Tooltip("Card-back / face background image on the root.")]
            public Image back;
            [Tooltip("Glyph image child — shows the note or rest sprite when face-up.")]
            public Image glyph;
            [Tooltip("Cursor overlay child — enabled while this square's beat is current in the perform bar.")]
            public Image cursor;
        }

        #region Inspector
        [Header("Wiring")]
        [SerializeField] private FlashCardController controller;
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private Image cardBorder;
        [Tooltip("Square prefab instantiated per slot — the grid is runtime-built because the " +
                 "row count is dynamic (2/3/5 rows in endless mode).")]
        [SerializeField] private FlashCardSquareWidget squarePrefab;

        [Header("Grid Layout (defaults match the original authored 2x4 card)")]
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField] private float cellSize = 140f;
        [Tooltip("Horizontal center-to-center spacing between columns.")]
        [SerializeField] private float pitchX = 152f;
        [Tooltip("Vertical center-to-center spacing between rows.")]
        [SerializeField] private float pitchY = 156f;
        [Tooltip("Space between the card's top edge and the first row of squares.")]
        [SerializeField] private float topPadding = 38f;
        [Tooltip("Space between the last row of squares and the card's bottom edge.")]
        [SerializeField] private float bottomPadding = 46f;
        [Tooltip("Card heights beyond this uniformly scale cardRoot down so tall " +
                 "(5-row) cards stay inside the original framing.")]
        [SerializeField] private float maxCardHeight = 560f;

        [Header("Glyphs (drop-in swap for final art)")]
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private Sprite restSprite;
        [SerializeField] private Sprite eighthPairSprite;
        [SerializeField] private Sprite offbeatEighthSprite;
        [SerializeField] private Sprite sixteenthQuadSprite;
        [SerializeField] private Sprite tripletSprite;

        [Header("Colors")]
        [SerializeField] private Color faceDownColor     = new Color(0.16f, 0.13f, 0.22f, 1f);
        [SerializeField] private Color noteFaceColor     = new Color(0.96f, 0.93f, 0.82f, 1f);
        [SerializeField] private Color restFaceColor     = new Color(0.62f, 0.60f, 0.68f, 1f);
        [SerializeField] private Color borderIdleColor   = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color borderYourTurnColor = new Color(1f, 0.82f, 0.25f, 0.9f);
        [SerializeField] private Color cursorColor       = new Color(1f, 1f, 1f, 0.35f);

        [Header("Double Time Telegraph")]
        [Tooltip("Banner root (e.g. a 'DOUBLE TIME!' TMP text under cardRoot, starts inactive). " +
                 "Pops in at a DT card's reveal and stays up through its perform bar.")]
        [SerializeField] private RectTransform doubleTimeBanner;
        [Tooltip("Border tint while a Double Time card is live — distinct from the 'your turn' gold.")]
        [SerializeField] private Color borderDoubleTimeColor = new Color(1f, 0.25f, 0.55f, 0.95f);
        [Tooltip("One-shot cue played at a Double Time card's reveal start (audio telegraph alongside " +
                 "the banner). Played through AudioManager's misc speaker; optional.")]
        [SerializeField] private AudioClip doubleTimeCueClip;

        [Header("Grade Flash Colors (per-card evaluation feedback)")]
        [SerializeField] private Color gradeAllPerfectColor = new Color(1f, 0.85f, 0.30f, 1f);
        [SerializeField] private Color gradeAllGoodColor    = new Color(0.35f, 0.90f, 0.40f, 1f);
        [SerializeField] private Color gradePassableColor   = new Color(0.95f, 0.60f, 0.20f, 1f);
        [SerializeField] private Color gradeFailedColor     = new Color(0.90f, 0.25f, 0.25f, 1f);

        [Header("Reveal Pop (cosmetic)")]
        [Tooltip("Duration of the scale-from-0 pop when a square reveals on its beat. Kept fast/snappy.")]
        [SerializeField] private float revealPopDuration = 0.12f;

        [Header("Hit Feedback (cosmetic per-tap)")]
        [Tooltip("Quality tint on a Perfect hit — colors the chip flash, destroy flash, and shards.")]
        [SerializeField] private Color hitPerfectColor = new Color(1f, 0.85f, 0.30f, 1f);
        [Tooltip("Quality tint on a Good hit — colors the chip flash, destroy flash, and shards.")]
        [SerializeField] private Color hitGoodColor = new Color(0.35f, 0.90f, 0.40f, 1f);
        [SerializeField] private float hitTintFadeDuration = 0.20f;
        [Tooltip("Per-hit damage shrink on multi-note squares: scale drops this much per chip. " +
                 "The final expected note destroys the block instead.")]
        [SerializeField] private float chipShrinkStep = 0.10f;
        [Tooltip("Chip/miss tween duration. Kept fast — subdivided taps can be ~0.25s apart.")]
        [SerializeField] private float chipDuration = 0.08f;
        [Tooltip("Face flash on an out-of-window tap.")]
        [SerializeField] private Color missFlashColor = new Color(0.90f, 0.25f, 0.25f, 1f);
        [Tooltip("Squares shrink to this scale on an out-of-window tap.")]
        [SerializeField] private float missShrinkScale = 0.92f;
        [Tooltip("Per-block judging (endless): tint of a block that breaks because its window closed un-hit.")]
        [SerializeField] private Color blockFailColor = new Color(0.80f, 0.15f, 0.15f, 1f);

        [Header("Destroy (cosmetic)")]
        [Tooltip("Punch-grow scale right before the block collapses.")]
        [SerializeField] private float destroyGrowScale = 1.15f;
        [SerializeField] private float destroyGrowDuration = 0.05f;
        [Tooltip("Collapse-to-zero duration after the punch-grow.")]
        [SerializeField] private float destroyCollapseDuration = 0.10f;
        [Tooltip("Fragments spawned when a block is destroyed (a couple also puff on chips).")]
        [SerializeField] private int shardCount = 5;
        [Tooltip("Shard size as a fraction of the square's rect size.")]
        [SerializeField] private float shardSizeRatio = 0.22f;
        [SerializeField] private float shardDistanceMin = 60f;
        [SerializeField] private float shardDistanceMax = 110f;
        [Tooltip("Shard fly-out/fade duration.")]
        [SerializeField] private float shardDuration = 0.35f;

        [Header("Grade Flash (cosmetic)")]
        [Tooltip("Seconds the border holds the grade color before fading back to idle. " +
                 "The flash starts on the evaluation boundary (controller event); these " +
                 "durations are cosmetic only and bleed into the next card's reveal bar.")]
        [SerializeField] private float gradeHoldDuration = 0.35f;
        [SerializeField] private float gradeFadeDuration = 0.45f;

        [Tooltip("Real-time seconds the card stays visible after normal encounter completion " +
                 "so the final card's grade flash is readable during the room-exit cover. " +
                 "Death teardown (OnEncounterStopped) hides immediately, ignoring this.")]
        [SerializeField] private float completeHideDelay = 0.8f;

        [Header("Perform Wobble (cosmetic)")]
        [Tooltip("Max Z-rotation degrees for the metronome-synced square wobble during the perform bar.")]
        [SerializeField] private float wobbleRotationAmount = 6f;
        [Tooltip("Beats for one full wobble cycle (left→right→left). 2 = one swing per beat.")]
        [SerializeField] private float wobbleBeatsPerCycle = 2f;

        [Header("On-Deck Reveal (pipelined next card)")]
        [Tooltip("Scale of the on-deck (next card) preview relative to the active card. Kept close " +
                 "to full size so the preview reads as a real, imminent card — the dim, the label, " +
                 "and the lower position carry the 'not live yet' distinction instead.")]
        [SerializeField, Range(0.2f, 1f)] private float onDeckScale = 0.95f;
        [Tooltip("Gap between the active card's bottom edge and the top of the on-deck panel " +
                 "(the GET READY label lives inside this gap).")]
        [SerializeField] private float onDeckGap = 84f;
        [Tooltip("Dim multiplier on on-deck square face/glyph colors — reads as 'upcoming', not live. " +
                 "Near 1 = full brightness; the label and lower position carry the distinction.")]
        [SerializeField, Range(0.2f, 1f)] private float onDeckDim = 0.95f;
        [Tooltip("Label above the on-deck grid telegraphing that the card must be played " +
                 "immediately at the boundary.")]
        [SerializeField] private string onDeckLabelText = "GET READY!";
        [SerializeField] private float onDeckLabelFontSize = 56f;
        [Tooltip("Whole-panel scale pulse on each on-deck square reveal — the preview breathes " +
                 "with the beat so the eye is drawn to it while performing.")]
        [SerializeField, Range(1f, 1.3f)] private float onDeckPulseScale = 1.1f;
        [Tooltip("Optional soft tick played on each on-deck square reveal (misc speaker) — an " +
                 "audible 'next card is being dealt' telegraph. None = silent reveal.")]
        [SerializeField] private AudioClip onDeckTickClip;
        [Tooltip("Handoff pop duration: at the boundary the new card's squares grow from the " +
                 "on-deck scale to full size while sliding up from the on-deck direction.")]
        [SerializeField] private float handoffPopDuration = 0.10f;
        [Tooltip("How far below their final position the squares start the handoff slide — reads " +
                 "as the on-deck card physically moving up into the live frame.")]
        [SerializeField] private float handoffSlideDistance = 80f;
        [Tooltip("Uniform flow (endless scene): ALSO present classic reveal bars (run start, " +
                 "post-stage-break) in the on-deck zone, promoting with the same slide-up at " +
                 "perform start — every card arrives through the GET READY telegraph. OFF = " +
                 "classic in-place reveal (keep OFF for dungeon rooms).")]
        [SerializeField] private bool alwaysOnDeckReveal = false;

        [Header("Column Scroll (Snake variant layout)")]
        [Tooltip("Center-to-center vertical spacing between column slots.")]
        [SerializeField] private float columnPitch = 150f;
        [Tooltip("Scale of the bottom (current beat) slot — the hit zone reads bigger than the trail.")]
        [SerializeField] private float columnCurrentScale = 1.25f;
        [Tooltip("Scale of the trail slots above the current beat (on-deck squares match this size).")]
        [SerializeField] private float columnTrailScale = 0.85f;
        [Tooltip("Duration of the discrete per-beat scroll step (column snaps down one pitch).")]
        [SerializeField] private float columnStepDuration = 0.12f;
        [Tooltip("Lane width in column mode — cardRoot becomes a narrow full-height stream.")]
        [SerializeField] private float columnLaneWidth = 240f;
        [Tooltip("Lane bottom edge height above the CANVAS bottom — keeps the hit zone clear of the pad.")]
        [SerializeField] private float columnLaneBottomY = 380f;
        [Tooltip("How far the lane extends PAST the top of the canvas — upcoming squares enter the " +
                 "screen already inside the lane, reading as a stream flowing in from above.")]
        [SerializeField] private float columnLaneTopOverflow = 200f;
        [Tooltip("Hit-zone (slot 0) center height above the lane's bottom edge.")]
        [SerializeField] private float columnHitZoneY = 140f;
        #endregion

        #region State
        private enum ViewState { Hidden, Revealing, Performing, Held }

        private ViewState    _state = ViewState.Hidden;
        private PhraseTiming _timing;
        private double       _beatInterval;
        private int          _cursorIndex = -1;

        // Runtime-built grid: squares + all per-slot state are (re)allocated by
        // EnsureGrid whenever the card's slot count changes.
        private Square[] squares = System.Array.Empty<Square>();
        private int _builtSlotCount;

        // Runtime-added metronome wobble per square — enabled only while Performing
        // (the player's round). Added in EnsureGrid (first build runs in Start so
        // GameManager.instance exists).
        private UIRotateWobble[] _wobbles = System.Array.Empty<UIRotateWobble>();

        private Tween[] _revealTweens = System.Array.Empty<Tween>();
        private Tween[] _popTweens    = System.Array.Empty<Tween>();
        // Per-slot destruction tracking: template (for expected tap count), confirmed
        // hits so far, whether the block has been destroyed this card, and whether
        // an out-of-window tap landed on the slot (keeps miss-tapped rests alive).
        private SquareTemplate[] _templates  = System.Array.Empty<SquareTemplate>();
        private int[]  _hitCounts  = System.Array.Empty<int>();
        private bool[] _destroyed  = System.Array.Empty<bool>();
        private bool[] _missTapped = System.Array.Empty<bool>();
        // Face color applied at reveal (note vs rest) — tint fades return to this,
        // never to a mid-fade sample of back.color.
        private Color[] _faceColors = System.Array.Empty<Color>();
        private Tween            _borderTween;
        private Tween            _hideDelayTween;
        private Tween            _bannerTween;
        private bool             _gradeFlashActive;

        // On-deck grid (pipelined next card): lightweight squares (back + glyph
        // only — no cursor, wobble, or hit state) under an auto-created root
        // parented to cardRoot and anchored to its BOTTOM edge, so it tracks the
        // active card's height automatically. Built/resized on demand.
        private RectTransform _onDeckRoot;
        private Square[] _onDeckSquares = System.Array.Empty<Square>();
        private UIRotateWobble[] _onDeckWobbles = System.Array.Empty<UIRotateWobble>();
        private Tween[]  _onDeckTweens  = System.Array.Empty<Tween>();
        private int      _onDeckBuiltCount;
        private TextMeshProUGUI _onDeckLabel;
        private Tween           _onDeckPulseTween;
        // alwaysOnDeckReveal: this card is revealing in the on-deck zone and must
        // be promoted to the active grid when its perform bar starts.
        private bool            _promoteOnPerform;

        // Authored home position per active square (set in EnsureGrid) — the
        // handoff slide offsets squares below home, so any interrupted slide
        // (KillSquareTweens) must snap position back, never leave it displaced.
        private Vector2[] _homePositions = System.Array.Empty<Vector2>();

        // Column mode (Snake): squares live inside per-slot holder rects under a
        // scroll container — the holder carries layout position + hit-zone
        // emphasis scale, so the square root's own scale stays free for the
        // existing reveal/chip/destroy tweens. The container's Y offset is the
        // discrete scroll: -currentBeat * columnPitch.
        private FlashCardLayout _layoutMode  = FlashCardLayout.Grid;
        private FlashCardLayout _builtLayout = FlashCardLayout.Grid;
        private RectTransform   _columnRoot;
        private RectTransform[] _slotHolders = System.Array.Empty<RectTransform>();
        private Tween[]         _holderScaleTweens = System.Array.Empty<Tween>();
        private Tween           _columnScrollTween;
        // cardRoot's authored rect (grid card framing), captured at Start and
        // restored when switching back to Grid — column mode re-anchors the root
        // into a full-height lane.
        private Vector2 _authoredAnchorMin;
        private Vector2 _authoredAnchorMax;
        private Vector2 _authoredPivot;
        private Vector2 _authoredAnchoredPos;
        private Vector2 _authoredSizeDelta;
        #endregion

        #region Unity
        private void Start()
        {
            // Initial build of the classic 2x4 grid. Deferred to Start because
            // UIRotateWobble.Awake reads GameManager.instance.metronome, which must
            // exist before the wobble components are added.
            if (cardRoot != null)
            {
                _authoredAnchorMin   = cardRoot.anchorMin;
                _authoredAnchorMax   = cardRoot.anchorMax;
                _authoredPivot       = cardRoot.pivot;
                _authoredAnchoredPos = cardRoot.anchoredPosition;
                _authoredSizeDelta   = cardRoot.sizeDelta;
            }
            EnsureGrid(FlashCardPatternGenerator.SlotCount);
        }

        /// <summary>
        /// Selects the card presentation for the next run. Called by
        /// FlashCardsModeController.BeginRun BEFORE StartGame (card hidden).
        /// A mode change tears down the layout roots so the next EnsureGrid
        /// rebuilds everything in the new mode's math.
        /// </summary>
        public void SetLayoutMode(FlashCardLayout layout)
        {
            if (_layoutMode == layout) return;
            _layoutMode = layout;

            // The on-deck root's parent/anchoring is mode-specific — destroy it
            // (and the column container) so both rebuild lazily. The active
            // squares are rebuilt by EnsureGrid via the _builtLayout mismatch.
            if (_onDeckRoot != null) Destroy(_onDeckRoot.gameObject);
            _onDeckRoot       = null;
            _onDeckLabel      = null;
            _onDeckSquares    = System.Array.Empty<Square>();
            _onDeckWobbles    = System.Array.Empty<UIRotateWobble>();
            _onDeckTweens     = System.Array.Empty<Tween>();
            _onDeckPulseTween = null;
            _onDeckBuiltCount = 0;

            if (_columnRoot != null) Destroy(_columnRoot.gameObject);
            _columnRoot        = null;
            _columnScrollTween = null;
        }

        /// <summary>
        /// (Re)builds the squares for <paramref name="slotCount"/> slots in the
        /// active layout mode. No-op when the built count AND layout already
        /// match. Reallocates all per-slot state; layout-specific placement and
        /// card sizing live in BuildGridLayout / BuildColumnLayout.
        /// </summary>
        private void EnsureGrid(int slotCount)
        {
            if (slotCount == _builtSlotCount && _layoutMode == _builtLayout) return;
            if (squarePrefab == null || cardRoot == null)
            {
                Debug.LogWarning("[FlashCardView] squarePrefab or cardRoot not assigned — cannot build grid.");
                return;
            }

            // Tear down the previous squares. SetLink kills in-flight tweens with
            // their GameObjects; wobbles live on the destroyed roots too. Column
            // squares are owned by their slot holders.
            for (int i = 0; i < squares.Length; i++)
            {
                if (i < _slotHolders.Length && _slotHolders[i] != null)
                    Destroy(_slotHolders[i].gameObject);
                else if (squares[i]?.root != null)
                    Destroy(squares[i].root.gameObject);
            }

            squares       = new Square[slotCount];
            _wobbles      = new UIRotateWobble[slotCount];
            _revealTweens = new Tween[slotCount];
            _popTweens    = new Tween[slotCount];
            _templates    = new SquareTemplate[slotCount];
            _hitCounts    = new int[slotCount];
            _destroyed      = new bool[slotCount];
            _missTapped     = new bool[slotCount];
            _faceColors     = new Color[slotCount];
            _homePositions  = new Vector2[slotCount];
            _slotHolders       = new RectTransform[slotCount];
            _holderScaleTweens = new Tween[slotCount];

            if (_layoutMode == FlashCardLayout.ColumnScroll) BuildColumnLayout(slotCount);
            else                                             BuildGridLayout(slotCount);

            // Banner reads over everything; new squares were appended after it.
            if (doubleTimeBanner != null) doubleTimeBanner.SetAsLastSibling();

            _builtSlotCount = slotCount;
            _builtLayout    = _layoutMode;
        }

        /// <summary>
        /// Classic grid placement — 4 columns, one row per 4 beats. Recomputes
        /// the card height and uniformly scales cardRoot down when a tall card
        /// would exceed maxCardHeight. Layout math reproduces the original
        /// authored 2x4 card exactly at slotCount = 8.
        /// </summary>
        private void BuildGridLayout(int slotCount)
        {
            // Leaving column mode: drop the scroll container (holders are gone).
            if (_columnRoot != null)
            {
                Destroy(_columnRoot.gameObject);
                _columnRoot        = null;
                _columnScrollTween = null;
            }

            int rows = Mathf.CeilToInt(slotCount / (float)columns);

            for (int i = 0; i < slotCount; i++)
            {
                int row = i / columns;
                int col = i % columns;

                var widget = Instantiate(squarePrefab, cardRoot);
                widget.name = $"Sq{i}";
                var rt = widget.root;
                rt.anchorMin        = new Vector2(0.5f, 1f);
                rt.anchorMax        = new Vector2(0.5f, 1f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(
                    (col - (columns - 1) * 0.5f) * pitchX,
                    -(topPadding + cellSize * 0.5f + row * pitchY));
                _homePositions[i] = rt.anchoredPosition;

                squares[i] = new Square { root = rt, back = widget.back, glyph = widget.glyph, cursor = widget.cursor };
                AddWobble(i, rt);
                ResetSquare(i);
            }

            // Restore the authored card framing (column mode re-anchors the root
            // into a lane), then apply the usual dynamic height.
            cardRoot.anchorMin        = _authoredAnchorMin;
            cardRoot.anchorMax        = _authoredAnchorMax;
            cardRoot.pivot            = _authoredPivot;
            cardRoot.anchoredPosition = _authoredAnchoredPos;

            // Card height: padding + rows, minus the trailing inter-row gap.
            float height = topPadding + rows * pitchY - (pitchY - cellSize) + bottomPadding;
            var size = _authoredSizeDelta;
            size.y = height;
            cardRoot.sizeDelta = size;
            cardRoot.localScale = Vector3.one * (height > maxCardHeight ? maxCardHeight / height : 1f);

            Debug.Log($"[FlashCardView] Grid built: {slotCount} slots ({rows}x{columns}), height={height:F0}, scale={cardRoot.localScale.x:F3}");
        }

        /// <summary>
        /// Column (Snake) placement — one vertical column, slot 0 at the bottom
        /// hit zone, later beats trailing upward. Each square sits centered in a
        /// per-slot HOLDER that carries position + emphasis scale; the square
        /// root's own scale/position stay the tween playground they are in grid
        /// mode (_homePositions are zero — home is the holder's center).
        /// </summary>
        private void BuildColumnLayout(int slotCount)
        {
            if (_columnRoot == null)
            {
                var go = new GameObject("ColumnRoot", typeof(RectTransform));
                _columnRoot = (RectTransform)go.transform;
                _columnRoot.SetParent(cardRoot, worldPositionStays: false);
                _columnRoot.anchorMin = new Vector2(0.5f, 0f);
                _columnRoot.anchorMax = new Vector2(0.5f, 0f);
                _columnRoot.pivot     = new Vector2(0.5f, 0f);
                _columnRoot.sizeDelta = Vector2.zero;
                _columnRoot.anchoredPosition = Vector2.zero;
            }

            for (int i = 0; i < slotCount; i++)
            {
                var holderGo = new GameObject($"Slot{i}", typeof(RectTransform));
                var holder = (RectTransform)holderGo.transform;
                holder.SetParent(_columnRoot, worldPositionStays: false);
                holder.anchorMin        = new Vector2(0.5f, 0f);
                holder.anchorMax        = new Vector2(0.5f, 0f);
                holder.pivot            = new Vector2(0.5f, 0.5f);
                holder.sizeDelta        = new Vector2(cellSize, cellSize);
                holder.anchoredPosition = new Vector2(0f, columnHitZoneY + i * columnPitch);
                holder.localScale       = Vector3.one * columnTrailScale;
                _slotHolders[i] = holder;

                var widget = Instantiate(squarePrefab, holder);
                widget.name = $"Sq{i}";
                var rt = widget.root;
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = Vector2.zero;
                _homePositions[i]   = Vector2.zero;

                squares[i] = new Square { root = rt, back = widget.back, glyph = widget.glyph, cursor = widget.cursor };
                AddWobble(i, rt);
                ResetSquare(i);
            }

            // Full-height lane framing: stretch from past the TOP of the canvas
            // down to just above the pad, so the column reads as a stream flowing
            // in from the top of the screen. offsetMin/Max define the rect against
            // the stretched anchors. No maxCardHeight down-scale here.
            cardRoot.anchorMin = new Vector2(0.5f, 0f);
            cardRoot.anchorMax = new Vector2(0.5f, 1f);
            cardRoot.pivot     = new Vector2(0.5f, 0f);
            cardRoot.offsetMin = new Vector2(-columnLaneWidth * 0.5f, columnLaneBottomY);
            cardRoot.offsetMax = new Vector2( columnLaneWidth * 0.5f, columnLaneTopOverflow);
            cardRoot.localScale = Vector3.one;
            ResetColumnScroll();

            Debug.Log($"[FlashCardView] Column built: {slotCount} slots, pitch={columnPitch:F0}");
        }

        private void AddWobble(int i, RectTransform rt) => _wobbles[i] = CreateWobble(rt);

        /// <summary>Attaches a disabled, card-unison wobble (shared by active and on-deck squares).</summary>
        private UIRotateWobble CreateWobble(RectTransform rt)
        {
            var wobble = rt.gameObject.AddComponent<UIRotateWobble>();
            wobble.rotationAmount   = wobbleRotationAmount;
            wobble.beatsPerCycle    = wobbleBeatsPerCycle;
            wobble.phaseOffsetBeats = 0f; // unison across the card
            wobble.enabled          = false;
            return wobble;
        }

        /// <summary>
        /// Snaps the column to rest: scroll offset zero (slot 0 at the hit
        /// zone), every holder at trail scale, all step tweens dead.
        /// </summary>
        private void ResetColumnScroll()
        {
            _columnScrollTween?.Kill();
            _columnScrollTween = null;
            if (_columnRoot != null) _columnRoot.anchoredPosition = Vector2.zero;
            for (int i = 0; i < _slotHolders.Length; i++)
            {
                _holderScaleTweens[i]?.Kill();
                _holderScaleTweens[i] = null;
                if (_slotHolders[i] != null)
                    _slotHolders[i].localScale = Vector3.one * columnTrailScale;
            }
        }

        /// <summary>
        /// Discrete scroll step: the column snaps down so slot <paramref name="idx"/>
        /// lands on the hit zone, growing to the current-beat scale while the
        /// slot it replaces shrinks back to trail size. The step MOMENT is the
        /// beat-cursor crossing (virtual time, Update) — the tween is cosmetic.
        /// </summary>
        private void ColumnStep(int idx)
        {
            if (_columnRoot == null) return;

            _columnScrollTween?.Kill();
            _columnScrollTween = _columnRoot.DOAnchorPosY(-idx * columnPitch, columnStepDuration)
                                            .SetEase(Ease.OutCubic)
                                            .SetLink(_columnRoot.gameObject);

            for (int i = 0; i < _slotHolders.Length; i++)
            {
                if (_slotHolders[i] == null) continue;
                float target = i == idx ? columnCurrentScale : columnTrailScale;
                if (Mathf.Approximately(_slotHolders[i].localScale.x, target)) continue;
                _holderScaleTweens[i]?.Kill();
                _holderScaleTweens[i] = _slotHolders[i].DOScale(target, columnStepDuration)
                                                       .SetEase(Ease.OutBack)
                                                       .SetLink(_slotHolders[i].gameObject);
            }
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnCardRevealStart     += HandleCardRevealStart;
            controller.OnSquareReveal        += HandleSquareReveal;
            controller.OnPerformStart        += HandlePerformStart;
            controller.OnPlayerHit           += HandlePlayerHit;
            controller.OnPlayerMissTap       += HandlePlayerMissTap;
            controller.OnBlockJudged         += HandleBlockJudged;
            controller.OnCardEvaluated       += HandleCardEvaluated;
            controller.OnEncounterComplete   += HandleEncounterComplete;
            controller.OnEncounterStopped    += HandleEncounterStopped;
            controller.OnNextCardRevealStart += HandleNextCardRevealStart;
            controller.OnNextSquareReveal    += HandleNextSquareReveal;
            controller.OnCardHandoff         += HandleCardHandoff;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCardRevealStart     -= HandleCardRevealStart;
                controller.OnSquareReveal        -= HandleSquareReveal;
                controller.OnPerformStart        -= HandlePerformStart;
                controller.OnPlayerHit           -= HandlePlayerHit;
                controller.OnPlayerMissTap       -= HandlePlayerMissTap;
                controller.OnBlockJudged         -= HandleBlockJudged;
                controller.OnCardEvaluated       -= HandleCardEvaluated;
                controller.OnEncounterComplete   -= HandleEncounterComplete;
                controller.OnEncounterStopped    -= HandleEncounterStopped;
                controller.OnNextCardRevealStart -= HandleNextCardRevealStart;
                controller.OnNextSquareReveal    -= HandleNextSquareReveal;
                controller.OnCardHandoff         -= HandleCardHandoff;
            }
            HideCard();
        }

        private void Update()
        {
            // Perform-bar beat cursor: pure function of virtual time. Freezes
            // automatically while paused (GameClock.GameTime does not advance).
            if (_state != ViewState.Performing) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;
            int idx = (int)((now - _timing.ResponseStartTime) / _beatInterval);
            idx = Mathf.Clamp(idx, 0, squares.Length - 1);
            if (idx != _cursorIndex)
            {
                SetCursor(idx);
                // Column mode: the beat crossing IS the scroll step — the column
                // snaps down one pitch so the new current beat lands on the hit zone.
                if (_layoutMode == FlashCardLayout.ColumnScroll) ColumnStep(idx);
            }

            // Rests the player correctly left alone auto-destroy once their beat
            // has elapsed; a rest that was miss-tapped persists as the failure tell.
            FinalizeElapsedRests(now);
        }
        #endregion

        #region Event Handlers
        private void HandleCardRevealStart(int cardIndex)
        {
            _timing       = controller.CurrentTiming;
            // Derive from the PERFORM window (eval - responseStart): correct under
            // per-card tempo AND compressed reveals, where the reveal window is
            // shorter than N beats. Drives the cursor + rest finalization only.
            _beatInterval = (_timing.EvaluationTime - _timing.ResponseStartTime)
                            / _timing.PhraseLength;

            // Rebuild the grid when this card's slot count differs (escalation rows).
            EnsureGrid(_timing.PhraseLength);

            // A pending post-completion hide belongs to a previous encounter — cancel it.
            _hideDelayTween?.Kill();
            _hideDelayTween = null;

            // Classic reveal (run start / post-stage-break). With alwaysOnDeckReveal
            // the reveal presents in the on-deck zone instead and HandlePerformStart
            // promotes it with the same slide-up as a pipelined handoff — every card
            // arrives through the same telegraph. Otherwise: in-place reveal.
            // Column mode: a classic reveal means the previous scroll offset is
            // stale — snap the column to rest before anything shows.
            if (_layoutMode == FlashCardLayout.ColumnScroll) ResetColumnScroll();

            if (alwaysOnDeckReveal)
            {
                _promoteOnPerform = true;
                // Column mode: classic reveals (run start / post-stage-break) deal
                // the revealing card PLUS the already-pipelined next+preview cards
                // as one full stack — the stream is constant from the first frame.
                if (_layoutMode == FlashCardLayout.ColumnScroll)
                    DealIntroColumn();
                else
                    ShowOnDeck(_timing.PhraseLength);
            }
            else
            {
                HideOnDeck();
            }

            if (cardRoot != null && !cardRoot.gameObject.activeSelf)
                cardRoot.gameObject.SetActive(true);

            // New card: every square hidden (scale 0) awaiting its reveal pop, cursor off. The border resets to
            // idle UNLESS the previous card's grade flash is still fading — cards chain
            // zero-gap, so the flash must bleed into this reveal bar to be readable.
            for (int i = 0; i < squares.Length; i++) ResetSquare(i);
            SetCursor(-1);

            // Double Time telegraph: banner pops at reveal and stays up for the whole
            // card; the border tints too unless a grade flash is still fading (the
            // banner alone carries the telegraph in that case).
            bool doubleTime = controller.CurrentCardTempoMultiplier > 1f;
            SetDoubleTimeBanner(doubleTime);
            if (doubleTime) PlayDoubleTimeCue();
            if (!_gradeFlashActive)
                SetBorder(doubleTime ? borderDoubleTimeColor : borderIdleColor, animate: doubleTime);
            _state = ViewState.Revealing;
        }

        private void HandleSquareReveal(int cardIndex, int slot, SquareTemplate template)
        {
            // alwaysOnDeckReveal: classic reveal pops render in the on-deck zone;
            // the active grid stays empty until promotion at perform start.
            if (_promoteOnPerform)
            {
                // Column mode already dealt the full card at reveal start.
                if (_layoutMode != FlashCardLayout.ColumnScroll)
                    RevealOnDeckSquare(slot, template);
                return;
            }

            if (slot < 0 || slot >= squares.Length) return;
            var sq = squares[slot];
            if (sq?.root == null) return;

            double expected = _timing.CallStartTime + slot * controller.CurrentRevealSlotInterval;
            double delta    = GameClock.Instance != null ? GameClock.Instance.GameTime - expected : 0.0;
            Debug.Log($"[FlashCardView] Reveal pop | card={cardIndex} slot={slot} {template} " +
                      $"delta=+{delta * 1000.0:F3}ms");

            // Cosmetic reveal pop: face swaps instantly on the beat, then the square
            // snaps up from scale 0 with a fast OutBack overshoot.
            // The reveal MOMENT was beat-accurate (controller event); the tween just dresses it.
            _templates[slot] = template;
            _revealTweens[slot]?.Kill();
            _popTweens[slot]?.Kill();
            _popTweens[slot] = null;
            ShowFace(slot, sq, template);
            sq.root.localRotation = Quaternion.identity;
            sq.root.localScale    = Vector3.zero;
            _revealTweens[slot] = sq.root.DOScale(1f, revealPopDuration)
                                         .SetEase(Ease.OutBack)
                                         .SetLink(sq.root.gameObject);
        }

        private void HandlePerformStart(int cardIndex)
        {
            // alwaysOnDeckReveal: this card revealed in the on-deck zone — promote
            // it onto the active grid now, exactly like a pipelined handoff.
            if (_promoteOnPerform)
            {
                _promoteOnPerform = false;
                PromoteOnDeckVisuals();
            }

            _state = ViewState.Performing;
            // At a pipelined handoff the previous card's grade flash started THIS
            // frame — let it bleed through the perform window's start instead of
            // stomping it (classic cycles have a whole reveal bar between the two).
            if (!_gradeFlashActive)
                SetBorder(borderYourTurnColor, animate: true);
            SetCursor(0);
            // Column mode: the promotion parked the column one pitch high — this
            // step slides slot 0 down onto the hit zone, continuing the per-beat
            // slither through the card boundary with zero visual gap.
            if (_layoutMode == FlashCardLayout.ColumnScroll) ColumnStep(0);
            SetAllWobbles(true);
        }

        /// <summary>
        /// Confirmed Perfect/Good tap: damages the matched block, destroying it on
        /// its final expected note. Single-note squares destroy on the first hit;
        /// subdivided squares chip (shrink + tint flash + small shard puff) per hit.
        /// Destruction: punch-grow → collapse to 0 + shard burst → the slot stays
        /// empty for the rest of the card (the cursor child still renders). The
        /// MOMENT is the reader's hit confirmation (controller event); tweens are
        /// cosmetic.
        /// </summary>
        private void HandlePlayerHit(int slot, InputMatch.MatchQuality quality)
        {
            if (_state != ViewState.Performing) return;
            if (slot < 0 || slot >= squares.Length) return;
            if (_destroyed[slot]) return; // stray rematch on an already-destroyed block
            var sq = squares[slot];
            if (sq?.root == null) return;

            _hitCounts[slot]++;
            int expected = _templates[slot].NoteCount();
            Color tint = quality == InputMatch.MatchQuality.Perfect ? hitPerfectColor : hitGoodColor;

            KillSquareTweens(slot);
            // Tweens own rotation for their duration — pause this square's wobble so
            // the two don't fight over localRotation.
            SetWobble(slot, false);
            sq.root.localRotation = Quaternion.identity;

            if (_hitCounts[slot] < expected)
                ChipBlock(slot, sq, tint);
            else
                DestroyBlock(slot, sq, tint);
        }

        /// <summary>Partial hit on a multi-note square: damage shrink + quality flash + small puff.</summary>
        private void ChipBlock(int slot, Square sq, Color tint)
        {
            float target = Mathf.Max(0.5f, 1f - chipShrinkStep * _hitCounts[slot]);
            var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
            seq.OnComplete(() => SetWobble(slot, _state == ViewState.Performing && !_destroyed[slot]));
            seq.Append(sq.root.DOScale(target, chipDuration).SetEase(Ease.OutBack));
            if (sq.back != null)
            {
                sq.back.color = tint;
                seq.Join(sq.back.DOColor(_faceColors[slot], hitTintFadeDuration));
            }
            _popTweens[slot] = seq;
            SpawnShards(slot, tint, 2);
        }

        /// <summary>
        /// Auto-destroys rest squares whose beat has fully elapsed in the perform
        /// bar, rewarding the player for correctly NOT tapping. Miss-tapped rests
        /// are skipped — they persist (with their red flash) as the failure tell.
        /// Polled from Update (virtual time), same convention as the beat cursor.
        /// </summary>
        private void FinalizeElapsedRests(double now)
        {
            for (int i = 0; i < squares.Length; i++)
            {
                if (_destroyed[i] || _missTapped[i]) continue;
                if (_templates[i] != SquareTemplate.Rest) continue;
                if (now < _timing.ResponseStartTime + (i + 1) * _beatInterval) continue;
                DestroyRestSlot(i);
            }
        }

        /// <summary>Rest auto-destroy: same collapse + shards, tinted with the rest face color.</summary>
        private void DestroyRestSlot(int i)
        {
            var sq = squares[i];
            if (sq?.root == null) return;
            KillSquareTweens(i);
            SetWobble(i, false);
            sq.root.localRotation = Quaternion.identity;
            DestroyBlock(i, sq, restFaceColor);
        }

        /// <summary>Final (or only) note hit: punch-grow, collapse to 0 with a shard burst; slot stays empty.</summary>
        private void DestroyBlock(int slot, Square sq, Color tint)
        {
            _destroyed[slot] = true;
            if (sq.back != null) sq.back.color = tint;

            var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
            seq.Append(sq.root.DOScale(destroyGrowScale, destroyGrowDuration).SetEase(Ease.OutQuad));
            seq.Append(sq.root.DOScale(0f, destroyCollapseDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                // Block visuals off — the slot stays empty for the rest of the card.
                // Root back to full scale so the cursor child still renders.
                sq.root.localScale = Vector3.one;
                if (sq.back  != null) sq.back.enabled  = false;
                if (sq.glyph != null) sq.glyph.enabled = false;
            });
            _popTweens[slot] = seq;
            SpawnShards(slot, tint, shardCount);
        }

        /// <summary>
        /// Out-of-window tap: inverse feel — the nearest square shrinks and flashes
        /// red, then recovers to its face color. Cosmetic only. Also flags the slot
        /// so a miss-tapped rest persists instead of auto-destroying at beat end.
        /// </summary>
        private void HandlePlayerMissTap(int slot)
        {
            if (_state != ViewState.Performing) return;
            if (slot < 0 || slot >= squares.Length) return;
            _missTapped[slot] = true;
            if (_destroyed[slot]) return; // destroyed slot — nothing to animate
            var sq = squares[slot];
            if (sq?.root == null) return;

            KillSquareTweens(slot);
            // Recover to the chip-damaged size, not full size.
            float baseScale = Mathf.Max(0.5f, 1f - chipShrinkStep * _hitCounts[slot]);
            sq.root.localScale    = Vector3.one * baseScale;
            sq.root.localRotation = Quaternion.identity;

            var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
            seq.Append(sq.root.DOScale(baseScale * missShrinkScale, chipDuration).SetEase(Ease.OutQuad));
            seq.Append(sq.root.DOScale(baseScale, chipDuration * 1.5f));
            if (sq.back != null)
            {
                sq.back.color = missFlashColor;
                seq.Join(sq.back.DOColor(_faceColors[slot], hitTintFadeDuration));
            }
            _popTweens[slot] = seq;
        }

        /// <summary>
        /// Per-block judging (endless): a missed block breaks red the moment its
        /// window closes, so failure reads per-block as it passes — distinct from
        /// the gold/green hit shatter and the grey rest dissolve. Cleared blocks
        /// need nothing here: hits already tap-destroy and untouched rests dissolve.
        /// The MOMENT is the controller's judgement; tweens are cosmetic only.
        /// </summary>
        private void HandleBlockJudged(int cardIndex, int slot, bool cleared)
        {
            if (cleared) return;
            if (_state != ViewState.Performing) return;
            if (slot < 0 || slot >= squares.Length) return;
            if (_destroyed[slot]) return;
            var sq = squares[slot];
            if (sq?.root == null) return;

            KillSquareTweens(slot);
            SetWobble(slot, false);
            sq.root.localRotation = Quaternion.identity;

            // Short shrink-shake (miss-tap vocabulary) leading into the collapse,
            // so a failing block visibly "gives out" rather than popping.
            float baseScale = Mathf.Max(0.5f, 1f - chipShrinkStep * _hitCounts[slot]);
            sq.root.localScale = Vector3.one * baseScale;
            var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
            seq.Append(sq.root.DOScale(baseScale * missShrinkScale, chipDuration).SetEase(Ease.OutQuad));
            seq.OnComplete(() => DestroyBlock(slot, sq, blockFailColor));
            _popTweens[slot] = seq;
        }

        private void HandleCardEvaluated(int cardIndex, EvaluationResult result)
        {
            // Final beat's untouched rest ends exactly on the evaluation boundary —
            // Update may not tick between then and now, so finalize here too.
            for (int i = 0; i < squares.Length; i++)
                if (!_destroyed[i] && !_missTapped[i] && _templates[i] == SquareTemplate.Rest)
                    DestroyRestSlot(i);

            // Card stays visible (Held) until the next card's reveal resets it —
            // cycles chain zero-gap, so this state is typically one frame long.
            _state = ViewState.Held;
            SetCursor(-1);
            SetAllWobbles(false);

            // Grade flash: the MOMENT is the evaluation boundary (controller event,
            // virtual time); the hold/fade back to idle is cosmetic only.
            if (result != null)
                FlashGradeBorder(GradeColor(result.Grade));
            else
                SetBorder(borderIdleColor, animate: false);
        }

        /// <summary>
        /// Pipelined reveal: the NEXT card starts revealing in the dimmed on-deck
        /// zone below the active card, while the player is still performing. The
        /// active card is untouched. DT telegraph moves here — the banner/cue fire
        /// when the upcoming card starts revealing, one perform window early.
        /// </summary>
        private void HandleNextCardRevealStart(int cardIndex, int slotCount, float tempoMultiplier)
        {
            // Column mode: the stream is CONSTANT — the on-deck stack is dealt
            // whole (next card + preview card) the moment it exists. New bars
            // spawn above the screen top and only enter by scrolling down, so
            // nothing ever visibly pops into the frame. Grid mode keeps the
            // beat-by-beat on-deck reveal (OnNextSquareReveal).
            if (_layoutMode == FlashCardLayout.ColumnScroll)
                DealOnDeckColumn();
            else
                ShowOnDeck(slotCount);

            if (tempoMultiplier > 1f)
            {
                SetDoubleTimeBanner(true);
                PlayDoubleTimeCue();
            }
        }

        /// <summary>
        /// Column mode: the in-perform on-deck stack — the controller's NEXT card
        /// with the PREVIEW card above it, directly above the active card's last
        /// slot. Pull-based so the handoff redeal (next already consumed →
        /// preview only) and the in-perform deal (both) share one code path.
        /// </summary>
        private void DealOnDeckColumn()
            => DealColumnStack(columnHitZoneY + _builtSlotCount * columnPitch,
                               controller.NextTemplates, controller.PreviewTemplates);

        /// <summary>
        /// Column mode intro (classic reveal at run start / post-stage-break):
        /// the revealing card itself plus next+preview, parked one pitch above
        /// the hit zone — the perform-start step drops slot 0 onto the zone.
        /// Tier positions match the post-promotion layout exactly, so promotion
        /// is world-seamless.
        /// </summary>
        private void DealIntroColumn()
            => DealColumnStack(columnHitZoneY + columnPitch,
                               controller.CurrentTemplates, controller.NextTemplates, controller.PreviewTemplates);

        /// <summary>
        /// (Re)builds the on-deck stack from the given tiers (bottom-up) with
        /// every face shown instantly — no pop tweens. New blocks materialize
        /// above the screen top and only enter the frame by scrolling, and a
        /// rebuild + face set completes within one frame, so visible blocks
        /// never blink or pop across redeals.
        /// </summary>
        private void DealColumnStack(float baseY, params System.Collections.Generic.IReadOnlyList<SquareTemplate>[] tiers)
        {
            int total = 0;
            foreach (var tier in tiers) total += tier?.Count ?? 0;
            if (total == 0) { HideOnDeck(); return; }

            EnsureOnDeckGrid(total);
            if (_onDeckRoot == null) return;

            _onDeckPulseTween?.Kill();
            _onDeckPulseTween = null;
            // The root lives inside the scroll container, so the whole stack
            // slithers with the stream.
            _onDeckRoot.anchoredPosition = new Vector2(0f, baseY);
            _onDeckRoot.localScale       = Vector3.one;

            int idx = 0;
            foreach (var tier in tiers)
            {
                if (tier == null) continue;
                for (int j = 0; j < tier.Count && idx < _onDeckSquares.Length; j++, idx++)
                {
                    var sq = _onDeckSquares[idx];
                    if (sq?.root == null) continue;
                    _onDeckTweens[idx]?.Kill();
                    _onDeckTweens[idx] = null;
                    ShowOnDeckFace(sq, tier[j]);
                    sq.root.localRotation = Quaternion.identity;
                    sq.root.localScale    = Vector3.one;
                    // The whole visible stream wobbles in unison with the active card —
                    // but only while input is open (perform bar). Run-start/reveal deals
                    // stay still; SetAllWobbles(true) at perform start switches them on.
                    SetOnDeckWobble(idx, _state == ViewState.Performing);
                }
            }
            _onDeckRoot.gameObject.SetActive(true);
        }

        /// <summary>Builds/resets the on-deck panel for a fresh card and shows it.</summary>
        private void ShowOnDeck(int slotCount)
        {
            EnsureOnDeckGrid(slotCount);
            if (_onDeckRoot == null) return;

            for (int i = 0; i < _onDeckSquares.Length; i++) ResetOnDeckSquare(i);
            // A killed beat pulse must never leave the panel off its base scale.
            _onDeckPulseTween?.Kill();
            _onDeckPulseTween = null;

            if (_layoutMode == FlashCardLayout.ColumnScroll)
            {
                // Stack the continuation above the active card's LAST slot. In a
                // classic reveal (_promoteOnPerform — run start / post-stage-break)
                // there is no live card below: park slot 0 one pitch above the hit
                // zone, ready to step in at perform start.
                int stackBase = _promoteOnPerform ? 1 : _builtSlotCount;
                _onDeckRoot.anchoredPosition = new Vector2(0f, columnHitZoneY + stackBase * columnPitch);
                _onDeckRoot.localScale = Vector3.one;
            }
            else
            {
                _onDeckRoot.localScale = Vector3.one * onDeckScale;
            }
            if (_onDeckLabel != null) _onDeckLabel.text = onDeckLabelText;
            _onDeckRoot.gameObject.SetActive(true);
        }

        private void HandleNextSquareReveal(int cardIndex, int slot, SquareTemplate template)
        {
            // Column mode dealt the full card at reveal start.
            if (_layoutMode == FlashCardLayout.ColumnScroll) return;
            RevealOnDeckSquare(slot, template);
        }

        /// <summary>On-deck square flips on its beat — same pop vocabulary as the live reveal, dimmed.</summary>
        private void RevealOnDeckSquare(int slot, SquareTemplate template)
        {
            if (slot < 0 || slot >= _onDeckSquares.Length) return;
            var sq = _onDeckSquares[slot];
            if (sq?.root == null) return;

            _onDeckTweens[slot]?.Kill();
            ShowOnDeckFace(sq, template);
            sq.root.localRotation = Quaternion.identity;
            sq.root.localScale    = Vector3.zero;
            _onDeckTweens[slot] = sq.root.DOScale(1f, revealPopDuration)
                                         .SetEase(Ease.OutBack)
                                         .SetLink(sq.root.gameObject);
            // Revealed on-deck squares wobble with the beat like the live card
            // (pop tween owns scale only — no fight over rotation). Gated on the
            // perform bar so nothing animates before input opens.
            SetOnDeckWobble(slot, _state == ViewState.Performing);

            // Whole-panel beat pulse + optional tick: the preview breathes with
            // the beat so the player's eye is drawn down while still performing.
            // Column mode skips the pulse — the panel is the column's continuation
            // and pulsing it would shift the stacked squares off their pitch.
            if (_onDeckRoot != null && _layoutMode != FlashCardLayout.ColumnScroll)
            {
                _onDeckPulseTween?.Kill();
                _onDeckRoot.localScale = Vector3.one * onDeckScale;
                _onDeckPulseTween = _onDeckRoot.DOScale(onDeckScale * onDeckPulseScale, 0.06f)
                                               .SetLoops(2, LoopType.Yoyo)
                                               .SetLink(_onDeckRoot.gameObject);
            }
            PlayOnDeckTick();
        }

        /// <summary>
        /// Pipelined handoff: the on-deck card just became the CURRENT card at the
        /// zero-gap boundary. The card arrives FULLY revealed (it was read in the
        /// on-deck zone), so every face shows immediately — the grow from the
        /// on-deck scale to full size is the only dressing, reading as the preview
        /// snapping up into the live frame. State/border/cursor/wobbles follow via
        /// OnPerformStart, which the controller fires immediately after.
        /// </summary>
        private void HandleCardHandoff(int cardIndex)
        {
            _timing       = controller.CurrentTiming;
            _beatInterval = (_timing.EvaluationTime - _timing.ResponseStartTime)
                            / _timing.PhraseLength;
            EnsureGrid(_timing.PhraseLength);

            _hideDelayTween?.Kill();
            _hideDelayTween = null;
            if (cardRoot != null && !cardRoot.gameObject.activeSelf)
                cardRoot.gameObject.SetActive(true);

            _promoteOnPerform = false;
            PromoteOnDeckVisuals();
        }

        /// <summary>
        /// Promotes the on-deck card's content onto the active grid: hides the
        /// preview, shows every face immediately, and slides the squares up from
        /// the on-deck direction while growing to full size — the preview visibly
        /// BECOMES the live card. An interrupted slide snaps home via
        /// KillSquareTweens (_homePositions). Shared by the pipelined handoff and
        /// the alwaysOnDeckReveal classic path.
        /// </summary>
        private void PromoteOnDeckVisuals()
        {
            bool column = _layoutMode == FlashCardLayout.ColumnScroll && _columnRoot != null;
            // Column mode keeps the on-deck stack alive through the handoff frame —
            // DealOnDeckColumn below redeals it (preview tier) with no hidden gap.
            if (!column) HideOnDeck();
            if (column)
            {
                // Park the column one pitch HIGH: the new card's squares then sit
                // exactly where the on-deck stack just was (slot 0 one pitch above
                // the hit zone), and HandlePerformStart's ColumnStep(0) slides the
                // whole column down — the handoff reads as one more slither step,
                // not a rebuild.
                ResetColumnScroll();
                _columnRoot.anchoredPosition = new Vector2(0f, columnPitch);
            }

            var templates = controller.CurrentTemplates;
            for (int i = 0; i < squares.Length; i++)
            {
                ResetSquare(i);
                var sq = squares[i];
                if (sq?.root == null) continue;
                var template  = templates != null && i < templates.Count ? templates[i] : SquareTemplate.Rest;
                _templates[i] = template;
                ShowFace(i, sq, template);
                if (column)
                {
                    // Seamless swap: trail-scaled holders at the on-deck squares'
                    // exact size/positions — no per-square dressing needed.
                    sq.root.localScale = Vector3.one;
                    continue;
                }
                sq.root.localScale       = Vector3.one * onDeckScale;
                sq.root.anchoredPosition = _homePositions[i] + Vector2.down * handoffSlideDistance;
                var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
                seq.Join(sq.root.DOScale(1f, handoffPopDuration).SetEase(Ease.OutBack));
                seq.Join(sq.root.DOAnchorPos(_homePositions[i], handoffPopDuration).SetEase(Ease.OutCubic));
                _revealTweens[i] = seq;
            }

            // Redeal the stack above the new active card: at a pipelined handoff
            // the next card is already consumed, so this shows the PREVIEW card —
            // the same blocks the player was just reading, at identical world
            // positions (slot math is seamless across the park+step).
            if (column) DealOnDeckColumn();

            // DT banner persists through a Double Time card, drops otherwise.
            SetDoubleTimeBanner(controller.CurrentCardTempoMultiplier > 1f);
        }

        // Normal completion: hold the card briefly so the final card's grade flash is
        // readable while the room-exit transition covers the screen. Real-time delay,
        // cosmetic only — the hide moment is not beat-critical.
        private void HandleEncounterComplete()
        {
            _hideDelayTween?.Kill();
            _hideDelayTween = DOVirtual.DelayedCall(completeHideDelay, HideCard, ignoreTimeScale: true)
                                       .SetLink(gameObject);
        }

        // External stop (death, room interrupt): tear down immediately.
        private void HandleEncounterStopped() => HideCard();
        #endregion

        #region Private — Visual State
        private void KillSquareTweens(int i)
        {
            _revealTweens[i]?.Kill();
            _revealTweens[i] = null;
            _popTweens[i]?.Kill();
            _popTweens[i] = null;
            // A killed handoff slide must never leave the square displaced —
            // snap back to its authored grid position (scale/color are always
            // re-set by whoever called this; position is not).
            if (i < _homePositions.Length && squares[i]?.root != null)
                squares[i].root.anchoredPosition = _homePositions[i];
        }

        private void ResetSquare(int i)
        {
            var sq = squares[i];
            if (sq?.root == null) return;
            KillSquareTweens(i);
            SetWobble(i, false);
            _templates[i]  = SquareTemplate.Rest;
            _hitCounts[i]  = 0;
            _destroyed[i]  = false;
            _missTapped[i] = false;
            // Hidden until its beat: the reveal pop scales the square in from 0.
            sq.root.localScale    = Vector3.zero;
            sq.root.localRotation = Quaternion.identity;
            _faceColors[i] = faceDownColor;
            if (sq.back != null)
            {
                sq.back.enabled = true; // re-arm after a destroyed card hid it
                sq.back.color   = faceDownColor;
            }
            if (sq.glyph  != null) sq.glyph.enabled = false;
            if (sq.cursor != null) sq.cursor.enabled = false;
        }

        private void ShowFace(int slot, Square sq, SquareTemplate template)
        {
            bool isNote = template.IsNoteBearing();
            _faceColors[slot] = isNote ? noteFaceColor : restFaceColor;
            if (sq.back != null) sq.back.color = _faceColors[slot];
            if (sq.glyph != null)
            {
                sq.glyph.sprite  = GlyphSprite(template);
                sq.glyph.enabled = sq.glyph.sprite != null;
                // Rests read quieter: dim the glyph slightly.
                sq.glyph.color = isNote ? Color.black : new Color(0.15f, 0.15f, 0.2f, 0.8f);
            }
        }

        /// <summary>
        /// (Re)builds the on-deck mini-grid for <paramref name="slotCount"/> slots.
        /// The root is created on demand under cardRoot, anchored to its BOTTOM
        /// edge (pivot top) so it tracks the active card's height automatically,
        /// scaled by onDeckScale. Squares reuse the live layout math; no cursor
        /// or hit state — the on-deck card is read-only. Revealed squares DO
        /// wobble (beat-synced, unison with the live card) so the whole visible
        /// stream animates, not just the active four.
        /// </summary>
        private void EnsureOnDeckGrid(int slotCount)
        {
            if (squarePrefab == null || cardRoot == null) return;
            bool column = _layoutMode == FlashCardLayout.ColumnScroll;
            if (column && _columnRoot == null) return; // active column always builds first

            if (_onDeckRoot == null)
            {
                var go = new GameObject("OnDeck", typeof(RectTransform));
                _onDeckRoot = (RectTransform)go.transform;

                if (column)
                {
                    // Column mode: the on-deck stack is simply the column's
                    // CONTINUATION — parented inside the scroll container so it
                    // slithers down with the active card. No label, no gap: the
                    // visible trail is its own telegraph.
                    _onDeckRoot.SetParent(_columnRoot, worldPositionStays: false);
                    _onDeckRoot.anchorMin  = new Vector2(0.5f, 0f);
                    _onDeckRoot.anchorMax  = new Vector2(0.5f, 0f);
                    _onDeckRoot.pivot      = new Vector2(0.5f, 0f);
                    _onDeckRoot.sizeDelta  = Vector2.zero;
                    _onDeckRoot.localScale = Vector3.one;
                }
                else
                {
                    _onDeckRoot.SetParent(cardRoot, worldPositionStays: false);
                    _onDeckRoot.anchorMin  = new Vector2(0.5f, 0f);
                    _onDeckRoot.anchorMax  = new Vector2(0.5f, 0f);
                    _onDeckRoot.pivot      = new Vector2(0.5f, 1f);
                    _onDeckRoot.sizeDelta  = new Vector2(cardRoot.sizeDelta.x, 0f);
                    _onDeckRoot.localScale = Vector3.one * onDeckScale;

                    // Telegraph label: sits on top of the preview (inside the onDeckGap),
                    // gold like the 'your turn' border — "this is about to be yours".
                    var labelGo = new GameObject("OnDeckLabel", typeof(RectTransform));
                    _onDeckLabel = labelGo.AddComponent<TextMeshProUGUI>();
                    var labelRt = (RectTransform)labelGo.transform;
                    labelRt.SetParent(_onDeckRoot, worldPositionStays: false);
                    labelRt.anchorMin        = new Vector2(0.5f, 1f);
                    labelRt.anchorMax        = new Vector2(0.5f, 1f);
                    labelRt.pivot            = new Vector2(0.5f, 0f);
                    labelRt.sizeDelta        = new Vector2(600f, onDeckLabelFontSize * 1.3f);
                    labelRt.anchoredPosition = new Vector2(0f, 4f);
                    _onDeckLabel.text          = onDeckLabelText;
                    _onDeckLabel.fontSize      = onDeckLabelFontSize;
                    _onDeckLabel.fontStyle     = FontStyles.Bold;
                    _onDeckLabel.alignment     = TextAlignmentOptions.Center;
                    _onDeckLabel.color         = borderYourTurnColor;
                    _onDeckLabel.raycastTarget = false;
                }

                // Banner reads over everything, including the preview.
                if (doubleTimeBanner != null) doubleTimeBanner.SetAsLastSibling();
            }
            // Column mode: ShowOnDeck owns the stack position (it depends on the
            // active card's slot count). Grid mode: fixed below the card.
            if (!column) _onDeckRoot.anchoredPosition = new Vector2(0f, -onDeckGap);

            if (slotCount == _onDeckBuiltCount) return;

            for (int i = 0; i < _onDeckSquares.Length; i++)
            {
                _onDeckTweens[i]?.Kill();
                if (_onDeckSquares[i]?.root != null)
                    Destroy(_onDeckSquares[i].root.gameObject);
            }

            _onDeckSquares = new Square[slotCount];
            _onDeckWobbles = new UIRotateWobble[slotCount];
            _onDeckTweens  = new Tween[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                var widget = Instantiate(squarePrefab, _onDeckRoot);
                widget.name = $"OnDeckSq{i}";
                var rt = widget.root;
                if (column)
                {
                    // Vertical continuation: slot i one pitch above the previous,
                    // sized to match the trail (holder-scaled) active squares.
                    rt.anchorMin        = new Vector2(0.5f, 0f);
                    rt.anchorMax        = new Vector2(0.5f, 0f);
                    rt.pivot            = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta        = Vector2.one * (cellSize * columnTrailScale);
                    rt.anchoredPosition = new Vector2(0f, i * columnPitch);
                }
                else
                {
                    int row = i / columns;
                    int col = i % columns;
                    rt.anchorMin        = new Vector2(0.5f, 1f);
                    rt.anchorMax        = new Vector2(0.5f, 1f);
                    rt.pivot            = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta        = new Vector2(cellSize, cellSize);
                    rt.anchoredPosition = new Vector2(
                        (col - (columns - 1) * 0.5f) * pitchX,
                        -(cellSize * 0.5f + row * pitchY));
                }

                _onDeckSquares[i] = new Square { root = rt, back = widget.back, glyph = widget.glyph, cursor = widget.cursor };
                _onDeckWobbles[i] = CreateWobble(rt);
                ResetOnDeckSquare(i);
            }

            _onDeckBuiltCount = slotCount;
            Debug.Log($"[FlashCardView] On-deck grid built: {slotCount} slots ({(column ? "column" : "grid")})");
        }

        private void ResetOnDeckSquare(int i)
        {
            var sq = _onDeckSquares[i];
            if (sq?.root == null) return;
            _onDeckTweens[i]?.Kill();
            _onDeckTweens[i] = null;
            SetOnDeckWobble(i, false);
            sq.root.localScale    = Vector3.zero;
            sq.root.localRotation = Quaternion.identity;
            if (sq.back   != null) { sq.back.enabled = true; sq.back.color = faceDownColor; }
            if (sq.glyph  != null) sq.glyph.enabled  = false;
            if (sq.cursor != null) sq.cursor.enabled = false;
        }

        /// <summary>Dimmed variant of ShowFace — never touches the live card's per-slot state.</summary>
        private void ShowOnDeckFace(Square sq, SquareTemplate template)
        {
            bool  isNote = template.IsNoteBearing();
            Color dim    = new Color(onDeckDim, onDeckDim, onDeckDim, 1f);
            if (sq.back != null) sq.back.color = (isNote ? noteFaceColor : restFaceColor) * dim;
            if (sq.glyph != null)
            {
                sq.glyph.sprite  = GlyphSprite(template);
                sq.glyph.enabled = sq.glyph.sprite != null;
                sq.glyph.color   = isNote ? new Color(0f, 0f, 0f, 0.85f)
                                          : new Color(0.15f, 0.15f, 0.2f, 0.6f);
            }
        }

        private void HideOnDeck()
        {
            for (int i = 0; i < _onDeckTweens.Length; i++)
            {
                _onDeckTweens[i]?.Kill();
                _onDeckTweens[i] = null;
            }
            _onDeckPulseTween?.Kill();
            _onDeckPulseTween = null;
            if (_onDeckRoot != null)
            {
                _onDeckRoot.localScale = _layoutMode == FlashCardLayout.ColumnScroll
                    ? Vector3.one
                    : Vector3.one * onDeckScale;
                _onDeckRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>Optional audible 'card being dealt' tick — same misc speaker as the DT cue.</summary>
        private void PlayOnDeckTick()
        {
            if (onDeckTickClip == null) return;
            var audio = AudioManager.instance;
            if (audio == null || audio.speakers == null || audio.speakers.Length < 3) return;
            if (audio.speakers[2] == null) return;
            audio.speakers[2].PlayOneShot(onDeckTickClip);
        }

        private Sprite GlyphSprite(SquareTemplate template) => template switch
        {
            SquareTemplate.Quarter       => noteSprite,
            SquareTemplate.EighthPair    => eighthPairSprite,
            SquareTemplate.OffbeatEighth => offbeatEighthSprite,
            SquareTemplate.SixteenthQuad => sixteenthQuadSprite,
            SquareTemplate.Triplet       => tripletSprite,
            _                            => restSprite,
        };

        private void SetCursor(int index)
        {
            if (_cursorIndex >= 0 && _cursorIndex < squares.Length)
            {
                var prev = squares[_cursorIndex];
                if (prev?.cursor != null) prev.cursor.enabled = false;
            }
            _cursorIndex = index;
            if (index >= 0 && index < squares.Length)
            {
                var sq = squares[index];
                if (sq?.cursor != null)
                {
                    sq.cursor.color   = cursorColor;
                    sq.cursor.enabled = true;
                }
            }
        }

        private Color GradeColor(EvaluationGrade grade) => grade switch
        {
            EvaluationGrade.AllPerfect => gradeAllPerfectColor,
            EvaluationGrade.AllGood    => gradeAllGoodColor,
            EvaluationGrade.Passable   => gradePassableColor,
            _                          => gradeFailedColor,
        };

        /// <summary>
        /// Border grade flash: snap to the grade color with a small punch, hold, then
        /// fade back to idle. Kill-before-reuse via the shared _borderTween slot —
        /// SetBorder and HideCard stomp this naturally.
        /// </summary>
        private void FlashGradeBorder(Color color)
        {
            if (cardBorder == null) return;
            _borderTween?.Kill();
            cardBorder.transform.localScale = Vector3.one;
            cardBorder.color = color;
            _gradeFlashActive = true;

            var seq = DOTween.Sequence().SetLink(cardBorder.gameObject);
            seq.Append(cardBorder.transform
                .DOPunchScale(new Vector3(0.04f, 0.04f, 0f), 0.25f, vibrato: 1));
            seq.AppendInterval(Mathf.Max(0f, gradeHoldDuration - 0.25f));
            seq.Append(cardBorder.DOColor(borderIdleColor, gradeFadeDuration));
            seq.OnKill(() => _gradeFlashActive = false);
            _borderTween = seq;
        }

        /// <summary>
        /// Shows/hides the Double Time banner. Show pops it in from scale 0 (same
        /// OutBack vocabulary as the reveal pops); hide is immediate — the banner's
        /// exit moment is the next normal card's reveal, which is not a beat the
        /// player needs dressed.
        /// </summary>
        private void SetDoubleTimeBanner(bool show)
        {
            if (doubleTimeBanner == null) return;
            _bannerTween?.Kill();
            _bannerTween = null;
            if (show)
            {
                doubleTimeBanner.gameObject.SetActive(true);
                doubleTimeBanner.localScale = Vector3.zero;
                _bannerTween = doubleTimeBanner.DOScale(1f, 0.15f)
                                               .SetEase(Ease.OutBack)
                                               .SetLink(doubleTimeBanner.gameObject);
            }
            else
            {
                doubleTimeBanner.localScale = Vector3.one;
                doubleTimeBanner.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Audio half of the Double Time telegraph: one-shot on the misc speaker at
        /// reveal start. The reveal bar is otherwise silent by design, so a single
        /// cue reads clearly without fighting the BGM.
        /// </summary>
        private void PlayDoubleTimeCue()
        {
            if (doubleTimeCueClip == null) return;
            var audio = AudioManager.instance;
            if (audio == null || audio.speakers == null || audio.speakers.Length < 3) return;
            if (audio.speakers[2] == null) return;
            audio.speakers[2].PlayOneShot(doubleTimeCueClip);
        }

        private void SetBorder(Color color, bool animate)
        {
            if (cardBorder == null) return;
            _borderTween?.Kill();
            if (animate)
            {
                // Quick attention pop on the "your turn" transition.
                cardBorder.color = color;
                cardBorder.transform.localScale = Vector3.one;
                _borderTween = cardBorder.transform
                    .DOPunchScale(new Vector3(0.02f, 0.02f, 0f), 0.25f, vibrato: 1)
                    .SetLink(cardBorder.gameObject);
            }
            else
            {
                cardBorder.transform.localScale = Vector3.one;
                cardBorder.color = color;
            }
        }

        /// <summary>
        /// Spawns small quality-tinted fragments at a square's position that fly
        /// outward, spin, shrink, and fade. Transient plain-Image objects under
        /// cardRoot (on top of the squares); OnKill destroys them on completion or
        /// interruption (card hide / teardown). Cosmetic only.
        /// </summary>
        private void SpawnShards(int slot, Color color, int count)
        {
            var sq = squares[slot];
            if (sq?.root == null || cardRoot == null) return;

            Vector2 squareSize = sq.root.rect.size;
            float shardSize = Mathf.Max(4f, Mathf.Min(squareSize.x, squareSize.y) * shardSizeRatio);

            for (int i = 0; i < count; i++)
            {
                var go  = new GameObject("Shard", typeof(RectTransform), typeof(Image));
                var rt  = (RectTransform)go.transform;
                var img = go.GetComponent<Image>();
                img.color         = color;
                img.raycastTarget = false;

                rt.SetParent(cardRoot, worldPositionStays: false);
                rt.sizeDelta     = Vector2.one * shardSize;
                rt.position      = sq.root.position;
                rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                rt.localScale    = Vector3.one * UnityEngine.Random.Range(0.7f, 1.2f);

                Vector2 dir  = UnityEngine.Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                Vector2 dest = rt.anchoredPosition
                             + dir * UnityEngine.Random.Range(shardDistanceMin, shardDistanceMax);
                float spin   = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

                var shardGo = go; // capture for OnKill
                var seq = DOTween.Sequence().SetLink(shardGo);
                seq.Join(rt.DOAnchorPos(dest, shardDuration).SetEase(Ease.OutCubic));
                seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, spin), shardDuration, RotateMode.LocalAxisAdd));
                seq.Join(rt.DOScale(0f, shardDuration).SetEase(Ease.InQuad));
                seq.Join(img.DOFade(0f, shardDuration).SetEase(Ease.InQuad));
                seq.OnKill(() => { if (shardGo != null) Destroy(shardGo); });
            }
        }

        private void SetWobble(int i, bool on)
        {
            if (i >= 0 && i < _wobbles.Length && _wobbles[i] != null)
                _wobbles[i].enabled = on;
        }

        private void SetOnDeckWobble(int i, bool on)
        {
            if (i >= 0 && i < _onDeckWobbles.Length && _onDeckWobbles[i] != null)
                _onDeckWobbles[i].enabled = on;
        }

        private void SetAllWobbles(bool on)
        {
            for (int i = 0; i < _wobbles.Length; i++)
                SetWobble(i, on);
            // On-deck stream wobbles in lockstep with the live card: on at perform
            // start (input open), off at evaluation/teardown.
            for (int i = 0; i < _onDeckWobbles.Length; i++)
                SetOnDeckWobble(i, on);
        }

        private void HideCard()
        {
            for (int i = 0; i < _revealTweens.Length; i++)
                KillSquareTweens(i);
            SetAllWobbles(false);
            HideOnDeck();
            ResetColumnScroll();
            _borderTween?.Kill();
            _borderTween = null;
            _hideDelayTween?.Kill();
            _hideDelayTween   = null;
            SetDoubleTimeBanner(false);
            _gradeFlashActive = false;
            _promoteOnPerform = false;
            _state       = ViewState.Hidden;
            _cursorIndex = -1;
            if (cardRoot != null) cardRoot.gameObject.SetActive(false);
        }
        #endregion
    }
}
