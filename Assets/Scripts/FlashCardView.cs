using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Renders the Flash Card: a 2x4 grid of squares (beats 1–4 top row, 5–8 bottom,
    /// reading order top-left → bottom-right) that flip face-up on their beats during
    /// the reveal bar and stay visible through the perform bar.
    ///
    /// TIMING CONTRACT: which square reveals, and when, is driven entirely by
    /// FlashCardController's GameClock-polled events (OnCardRevealStart /
    /// OnSquareReveal / OnPerformStart / OnCardEvaluated) — beat accuracy lives in
    /// the controller, matching the CRIndicator convention. DOTween here is cosmetic
    /// only (flip/pop), with SetLink + kill-before-reuse. The perform-bar beat cursor
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
    ///   noteSprite / restSprite — notation glyphs (placeholder art, drop-in swap later)
    /// </summary>
    public class FlashCardView : MonoBehaviour
    {
        [System.Serializable]
        public class Square
        {
            [Tooltip("Square root — flip tween scales this on X.")]
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

        [Header("Colors")]
        [SerializeField] private Color faceDownColor     = new Color(0.16f, 0.13f, 0.22f, 1f);
        [SerializeField] private Color noteFaceColor     = new Color(0.96f, 0.93f, 0.82f, 1f);
        [SerializeField] private Color restFaceColor     = new Color(0.62f, 0.60f, 0.68f, 1f);
        [SerializeField] private Color borderIdleColor   = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color borderYourTurnColor = new Color(1f, 0.82f, 0.25f, 0.9f);
        [SerializeField] private Color cursorColor       = new Color(1f, 1f, 1f, 0.35f);

        [Header("Flip (cosmetic)")]
        [SerializeField] private float flipHalfDuration = 0.07f;
        #endregion

        #region State
        private enum ViewState { Hidden, Revealing, Performing, Held }

        private ViewState    _state = ViewState.Hidden;
        private PhraseTiming _timing;
        private double       _beatInterval;
        private int          _cursorIndex = -1;

        private readonly Tween[] _flipTweens = new Tween[8];
        private Tween            _borderTween;
        #endregion

        #region Unity
        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnCardRevealStart   += HandleCardRevealStart;
            controller.OnSquareReveal      += HandleSquareReveal;
            controller.OnPerformStart      += HandlePerformStart;
            controller.OnCardEvaluated     += HandleCardEvaluated;
            controller.OnEncounterComplete += HandleEncounterEnd;
            controller.OnEncounterStopped  += HandleEncounterEnd;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCardRevealStart   -= HandleCardRevealStart;
                controller.OnSquareReveal      -= HandleSquareReveal;
                controller.OnPerformStart      -= HandlePerformStart;
                controller.OnCardEvaluated     -= HandleCardEvaluated;
                controller.OnEncounterComplete -= HandleEncounterEnd;
                controller.OnEncounterStopped  -= HandleEncounterEnd;
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
        }
        #endregion

        #region Event Handlers
        private void HandleCardRevealStart(int cardIndex)
        {
            _timing       = controller.CurrentTiming;
            _beatInterval = (_timing.ResponseStartTime - _timing.CallStartTime)
                            / FlashCardPatternGenerator.SlotCount;

            if (cardRoot != null && !cardRoot.gameObject.activeSelf)
                cardRoot.gameObject.SetActive(true);

            // New card: everything back to face-down, border idle, cursor off.
            for (int i = 0; i < squares.Length; i++) ResetSquare(i);
            SetCursor(-1);
            SetBorder(borderIdleColor, animate: false);
            _state = ViewState.Revealing;
        }

        private void HandleSquareReveal(int cardIndex, int slot, bool isNote)
        {
            if (slot < 0 || slot >= squares.Length) return;
            var sq = squares[slot];
            if (sq?.root == null) return;

            double expected = _timing.CallStartTime + slot * _beatInterval;
            double delta    = GameClock.Instance != null ? GameClock.Instance.GameTime - expected : 0.0;
            Debug.Log($"[FlashCardView] Flip | card={cardIndex} slot={slot} {(isNote ? "NOTE" : "rest")} " +
                      $"delta=+{delta * 1000.0:F3}ms");

            // Cosmetic flip: scaleX 1→0, swap face at the midpoint, 0→1.
            // The reveal MOMENT was beat-accurate (controller event); the tween just dresses it.
            _flipTweens[slot]?.Kill();
            sq.root.localScale = Vector3.one;
            var seq = DOTween.Sequence().SetLink(sq.root.gameObject);
            seq.Append(sq.root.DOScaleX(0f, flipHalfDuration).SetEase(Ease.InQuad));
            seq.AppendCallback(() => ShowFace(sq, isNote));
            seq.Append(sq.root.DOScaleX(1f, flipHalfDuration * 1.6f).SetEase(Ease.OutBack));
            _flipTweens[slot] = seq;
        }

        private void HandlePerformStart(int cardIndex)
        {
            _state = ViewState.Performing;
            SetBorder(borderYourTurnColor, animate: true);
            SetCursor(0);
        }

        private void HandleCardEvaluated(int cardIndex, EvaluationResult result)
        {
            // Card stays visible (Held) until the next card's reveal resets it —
            // cycles chain zero-gap, so this state is typically one frame long.
            _state = ViewState.Held;
            SetCursor(-1);
            SetBorder(borderIdleColor, animate: false);
        }

        private void HandleEncounterEnd() => HideCard();
        #endregion

        #region Private — Visual State
        private void ResetSquare(int i)
        {
            var sq = squares[i];
            if (sq?.root == null) return;
            _flipTweens[i]?.Kill();
            _flipTweens[i] = null;
            sq.root.localScale = Vector3.one;
            if (sq.back   != null) sq.back.color = faceDownColor;
            if (sq.glyph  != null) sq.glyph.enabled = false;
            if (sq.cursor != null) sq.cursor.enabled = false;
        }

        private void ShowFace(Square sq, bool isNote)
        {
            if (sq.back != null) sq.back.color = isNote ? noteFaceColor : restFaceColor;
            if (sq.glyph != null)
            {
                sq.glyph.sprite  = isNote ? noteSprite : restSprite;
                sq.glyph.enabled = sq.glyph.sprite != null;
                // Rests read quieter: dim the glyph slightly.
                sq.glyph.color = isNote ? Color.black : new Color(0.15f, 0.15f, 0.2f, 0.8f);
            }
        }

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

        private void HideCard()
        {
            for (int i = 0; i < _flipTweens.Length; i++)
            {
                _flipTweens[i]?.Kill();
                _flipTweens[i] = null;
            }
            _borderTween?.Kill();
            _borderTween = null;
            _state       = ViewState.Hidden;
            _cursorIndex = -1;
            if (cardRoot != null) cardRoot.gameObject.SetActive(false);
        }
        #endregion
    }
}
