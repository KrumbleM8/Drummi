using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
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
    /// Layout follows the GardenPlot/DrumGridCell pattern: 8 persistent squares with
    /// manual RectTransform placement, no GridLayoutGroup, no pooling.
    ///
    /// This component lives on an always-active object (the FlashCardController's) so
    /// events are never missed; <see cref="cardRoot"/> is toggled for visibility.
    /// All card Images must have raycastTarget = false so pad input passes through.
    ///
    /// INSPECTOR SETUP:
    ///   controller  — FlashCardController whose events drive this view
    ///   cardRoot    — panel root under DungeonCanvas/GameplayUI (starts inactive)
    ///   cardBorder  — border image behind the card background ("your turn" state)
    ///   squares     — 8 entries in reading order (back image, glyph image, cursor overlay)
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
        [SerializeField] private Square[] squares = new Square[8];

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
        #endregion

        #region State
        private enum ViewState { Hidden, Revealing, Performing, Held }

        private ViewState    _state = ViewState.Hidden;
        private PhraseTiming _timing;
        private double       _beatInterval;
        private int          _cursorIndex = -1;

        // Runtime-added metronome wobble per square — enabled only while Performing
        // (the player's round). Added in Start so GameManager.instance exists.
        private readonly UIRotateWobble[] _wobbles = new UIRotateWobble[8];

        private readonly Tween[] _revealTweens = new Tween[8];
        private readonly Tween[] _popTweens  = new Tween[8];
        // Per-slot destruction tracking: template (for expected tap count), confirmed
        // hits so far, whether the block has been destroyed this card, and whether
        // an out-of-window tap landed on the slot (keeps miss-tapped rests alive).
        private readonly SquareTemplate[] _templates  = new SquareTemplate[8];
        private readonly int[]  _hitCounts  = new int[8];
        private readonly bool[] _destroyed  = new bool[8];
        private readonly bool[] _missTapped = new bool[8];
        // Face color applied at reveal (note vs rest) — tint fades return to this,
        // never to a mid-fade sample of back.color.
        private readonly Color[] _faceColors = new Color[8];
        private Tween            _borderTween;
        private Tween            _hideDelayTween;
        private Tween            _bannerTween;
        private bool             _gradeFlashActive;
        #endregion

        #region Unity
        private void Start()
        {
            // UIRotateWobble.Awake reads GameManager.instance.metronome, so add the
            // components after all scene Awakes have run.
            for (int i = 0; i < squares.Length; i++)
            {
                if (squares[i]?.root == null) continue;
                var wobble = squares[i].root.gameObject.AddComponent<UIRotateWobble>();
                wobble.rotationAmount   = wobbleRotationAmount;
                wobble.beatsPerCycle    = wobbleBeatsPerCycle;
                wobble.phaseOffsetBeats = 0f; // unison across the card
                wobble.enabled          = false;
                _wobbles[i] = wobble;
            }
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnCardRevealStart   += HandleCardRevealStart;
            controller.OnSquareReveal      += HandleSquareReveal;
            controller.OnPerformStart      += HandlePerformStart;
            controller.OnPlayerHit         += HandlePlayerHit;
            controller.OnPlayerMissTap     += HandlePlayerMissTap;
            controller.OnCardEvaluated     += HandleCardEvaluated;
            controller.OnEncounterComplete += HandleEncounterComplete;
            controller.OnEncounterStopped  += HandleEncounterStopped;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCardRevealStart   -= HandleCardRevealStart;
                controller.OnSquareReveal      -= HandleSquareReveal;
                controller.OnPerformStart      -= HandlePerformStart;
                controller.OnPlayerHit         -= HandlePlayerHit;
                controller.OnPlayerMissTap     -= HandlePlayerMissTap;
                controller.OnCardEvaluated     -= HandleCardEvaluated;
                controller.OnEncounterComplete -= HandleEncounterComplete;
                controller.OnEncounterStopped  -= HandleEncounterStopped;
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
            if (idx != _cursorIndex) SetCursor(idx);

            // Rests the player correctly left alone auto-destroy once their beat
            // has elapsed; a rest that was miss-tapped persists as the failure tell.
            FinalizeElapsedRests(now);
        }
        #endregion

        #region Event Handlers
        private void HandleCardRevealStart(int cardIndex)
        {
            _timing       = controller.CurrentTiming;
            // Derive from the timing itself (per-card tempo makes the window shorter
            // on Double Time cards; PhraseLength keeps this correct if N ever changes).
            _beatInterval = (_timing.ResponseStartTime - _timing.CallStartTime)
                            / _timing.PhraseLength;

            // A pending post-completion hide belongs to a previous encounter — cancel it.
            _hideDelayTween?.Kill();
            _hideDelayTween = null;

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
            if (slot < 0 || slot >= squares.Length) return;
            var sq = squares[slot];
            if (sq?.root == null) return;

            double expected = _timing.CallStartTime + slot * _beatInterval;
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
            _state = ViewState.Performing;
            SetBorder(borderYourTurnColor, animate: true);
            SetCursor(0);
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

        private void SetAllWobbles(bool on)
        {
            for (int i = 0; i < _wobbles.Length; i++)
                SetWobble(i, on);
        }

        private void HideCard()
        {
            for (int i = 0; i < _revealTweens.Length; i++)
                KillSquareTweens(i);
            SetAllWobbles(false);
            _borderTween?.Kill();
            _borderTween = null;
            _hideDelayTween?.Kill();
            _hideDelayTween   = null;
            SetDoubleTimeBanner(false);
            _gradeFlashActive = false;
            _state       = ViewState.Hidden;
            _cursorIndex = -1;
            if (cardRoot != null) cardRoot.gameObject.SetActive(false);
        }
        #endregion
    }
}
