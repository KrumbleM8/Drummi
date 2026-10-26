using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Classic (2×4 grid) how-to-play loop: a miniature of the real FlashCards
    /// grid screen (CoplayScripts/AddSignPreviews.cs builds it in canvas
    /// units). One card cycle at 105 BPM = 16 beats = 9.14 s:
    ///   beats 0–7   reveal — one 140 px square pops in per beat, row-major
    ///               (scale 0 → 1, 0.12 s OutBack), note faces cream with a
    ///               black glyph, rest faces grey with a dim glyph
    ///   beats 8–15  perform — every square wobbles ±6° (2-beat cycle), the
    ///               white cursor overlay sits on the current square for one
    ///               beat; a tapped note flashes gold/green, punches ×1.15 and
    ///               collapses with 5 shards; a rest dissolves in its own grey
    ///               at the end of its beat
    /// The card ends empty, so the loop is seamless. Double Time reuses this at
    /// speed 2. Pattern (row-major): Q R Q Q / R Q R R.
    /// </summary>
    public class ClassicPreviewAnimation : FlashCardsPreviewAnimation
    {
        [Header("Grid")]
        [SerializeField] private RectTransform[] squares = new RectTransform[8];
        [SerializeField] private Image[] backs   = new Image[8];
        [SerializeField] private Image[] glyphs  = new Image[8];
        [SerializeField] private Image[] cursors = new Image[8];
        [Tooltip("true = rest slot (dissolves instead of being tapped).")]
        [SerializeField] private bool[] rests = new bool[8];

        private const float Loop        = 16f * Beat;
        private const float PerformAt   = 8f * Beat;
        private const float PopDur      = 0.12f;
        private const float TapOffset   = 0.10f;   // tap lands just after the beat
        private const float SquareSize  = 140f;

        protected override float LoopLength => Loop;

        private Vector2[] _origin;

        private void Awake()
        {
            _origin = new Vector2[squares.Length];
            for (int i = 0; i < squares.Length; i++)
                _origin[i] = squares[i] != null ? squares[i].anchoredPosition : Vector2.zero;
        }

        protected override void Evaluate(float t)
        {
            EvaluateDrum(t);
            bool performing = t >= PerformAt;
            float wobble = performing ? Wobble(t - PerformAt) : 0f;
            int cursorSlot = performing ? Mathf.Min(Mathf.FloorToInt((t - PerformAt) / Beat), squares.Length - 1) : -1;

            int lastEvent = -1; float lastAge = -1f; Color lastColor = HitGold; Vector2 lastOrigin = Vector2.zero;
            int prevEvent = -1; float prevAge = -1f; Color prevColor = HitGold; Vector2 prevOrigin = Vector2.zero;

            for (int k = 0; k < squares.Length; k++)
            {
                RectTransform rt = squares[k];
                if (rt == null) continue;
                bool rest = rests.Length > k && rests[k];
                Color face = rest ? RestFace : NoteFace;

                // Reveal pop.
                float pop = PopIn(t, k * Beat, PopDur);

                // Destroy event: note tapped shortly after its perform beat, rest dissolves at the beat's end.
                float eventTime = rest ? PerformAt + (k + 1) * Beat - 0.02f : PerformAt + k * Beat + TapOffset;
                Color hit = rest ? RestFace : (k % 2 == 0 ? HitGold : HitGreen);
                float age = t - eventTime;
                float destroy = DestroyScale(age, face, hit, out Color back);

                // Events happen in slot order, so the most recent started event is the
                // highest k with age ≥ 0; keep it and the one before for the two shard pools.
                if (age >= 0f)
                {
                    prevEvent = lastEvent; prevAge = lastAge; prevColor = lastColor; prevOrigin = lastOrigin;
                    lastEvent = k; lastAge = age; lastColor = hit; lastOrigin = _origin[k];
                }

                SetScale(rt, pop * destroy);
                rt.localRotation = Quaternion.Euler(0f, 0f, wobble);
                if (backs.Length > k && backs[k] != null) backs[k].color = back;
                if (glyphs.Length > k && glyphs[k] != null) glyphs[k].color = rest ? RestGlyph : NoteGlyph;
                if (cursors.Length > k && cursors[k] != null)
                    SetAlpha(cursors[k], cursorSlot == k ? CursorCol.a : 0f);
            }

            // Shard bursts: pool 0 for the most recent event, pool 1 for the one before.
            if (lastEvent >= 0) EvaluateShardBurst(0, lastOrigin, lastColor, SquareSize, lastAge);
            else EvaluateShardBurst(0, Vector2.zero, HitGold, SquareSize, -1f);
            if (prevEvent >= 0) EvaluateShardBurst(1, prevOrigin, prevColor, SquareSize, prevAge);
            else EvaluateShardBurst(1, Vector2.zero, HitGold, SquareSize, -1f);
        }
    }
}
