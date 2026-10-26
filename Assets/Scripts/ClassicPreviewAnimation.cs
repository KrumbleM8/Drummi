using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Classic (2x4 grid) how-to-play loop, 4.0 s: the two rows of cards pop
    /// in with their rhythm glyphs, a gold cursor sweeps the eight slots while
    /// a tap ring + bump marks every note slot (rests are skipped), then the
    /// cards shrink away and the loop restarts. Double Time reuses this at
    /// speed 2. Wired by CoplayScripts/AddSignPreviews.cs: squares[8] +
    /// glyphs[8] row-major (rest glyphs detected by sprite name).
    /// </summary>
    public class ClassicPreviewAnimation : SignPreviewAnimation
    {
        [SerializeField] private RectTransform[] squares = new RectTransform[8];
        [SerializeField] private Image[]         glyphs  = new Image[8];
        [SerializeField] private RectTransform   cursor;
        [SerializeField] private Image           cursorImage;
        [SerializeField] private RectTransform   ring;
        [SerializeField] private Image           ringImage;

        private const float Loop        = 4.0f;
        private const float RowGap      = 0.8f;   // row 2 starts popping here
        private const float PopStep     = 0.15f;
        private const float PopDur      = 0.25f;
        private const float CursorIn    = 1.7f;
        private const float SweepStart  = 1.8f;
        private const float SweepStep   = 0.2f;
        private const float CursorOut   = 3.45f;
        private const float ClearStart  = 3.5f;
        private const float ClearStep   = 0.03f;
        private const float ClearDur    = 0.2f;

        protected override float LoopLength => Loop;

        private bool[]    _isRest;
        private Vector2[] _slotPos;
        private float[]   _tapTimes;

        private void Awake()
        {
            int n = squares.Length;
            _isRest  = new bool[n];
            _slotPos = new Vector2[n];
            int notes = 0;
            for (int i = 0; i < n; i++)
            {
                _isRest[i]  = glyphs.Length > i && glyphs[i] != null && glyphs[i].sprite != null
                              && glyphs[i].sprite.name.Contains("Rest");
                _slotPos[i] = squares[i] != null ? squares[i].anchoredPosition : Vector2.zero;
                if (!_isRest[i]) notes++;
            }
            _tapTimes = new float[notes];
            for (int i = 0, k = 0; i < n; i++)
                if (!_isRest[i]) _tapTimes[k++] = SweepStart + i * SweepStep;
        }

        protected override void Evaluate(float t)
        {
            int n = squares.Length;
            for (int i = 0; i < n; i++)
            {
                if (squares[i] == null) continue;
                float popStart = i < 4 ? i * PopStep : RowGap + (i - 4) * PopStep;
                float scale = PopIn(t, popStart, PopDur);
                scale *= 1f - InQuad(Progress(t, ClearStart + i * ClearStep, ClearDur));
                if (!_isRest[i]) scale *= Punch(t, SweepStart + i * SweepStep, 0.18f);
                SetScale(squares[i], scale);

                if (glyphs.Length > i)
                    SetAlpha(glyphs[i], Progress(t, popStart + 0.05f, 0.15f) * (1f - Progress(t, ClearStart, 0.15f)));
            }

            // Cursor: fades in, hops slot to slot (0.08 s OutCubic per hop), fades out.
            if (cursor != null)
            {
                float alpha = Progress(t, CursorIn, 0.1f) * (1f - Progress(t, CursorOut, 0.1f));
                SetAlpha(cursorImage, alpha * 0.9f);

                Vector2 pos = _slotPos[0];
                if (t >= SweepStart)
                {
                    int k = Mathf.Clamp(Mathf.FloorToInt((t - SweepStart) / SweepStep), 0, n - 1);
                    float hop = OutCubic(Progress(t, SweepStart + k * SweepStep, 0.08f));
                    pos = k == 0 ? _slotPos[0] : Vector2.Lerp(_slotPos[k - 1], _slotPos[k], hop);
                }
                cursor.anchoredPosition = pos;
            }

            // Tap ring follows the cursor's note slots.
            if (ring != null && t >= SweepStart)
            {
                int k = Mathf.Clamp(Mathf.FloorToInt((t - SweepStart) / SweepStep), 0, n - 1);
                ring.anchoredPosition = _slotPos[k];
            }
            TapRing(ring, ringImage, t, _tapTimes, 0.25f);
        }
    }
}
