using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Play / Snake (column stream) how-to-play loop, 3.2 s = 8 steps × 0.4 s:
    /// a column of cards slides down one slot per step into a gold hit zone;
    /// the card in the zone is tapped (ring + bump) and shrinks away, a rest
    /// just fades, and a new card pops in at the top. The 8-step rhythm
    /// pattern is the loop length, so the seam is invisible. Five squares
    /// form a ring buffer — each frame assigns pattern index p to square
    /// p mod 5 and sets its glyph sprite, so no square owns a glyph.
    /// Wired by CoplayScripts/AddSignPreviews.cs.
    /// </summary>
    public class SnakePreviewAnimation : SignPreviewAnimation
    {
        [SerializeField] private RectTransform[] squares     = new RectTransform[5];
        [SerializeField] private Image[]         backImages  = new Image[5];
        [SerializeField] private Image[]         glyphImages = new Image[5];
        [SerializeField] private RectTransform   ring;
        [SerializeField] private Image           ringImage;

        [Tooltip("Glyph per pattern step (8). Rests are detected by sprite name.")]
        [SerializeField] private Sprite[] pattern = new Sprite[8];

        [Tooltip("Slot y positions, bottom (hit zone) first; the last entry is the spawn slot.")]
        [SerializeField] private float[] slotY = { -75f, -25f, 25f, 75f, 125f };

        private const float StepDur = 0.4f;
        private const int   Steps   = 8;

        protected override float LoopLength => StepDur * Steps;

        protected override void Evaluate(float t)
        {
            int   step = Mathf.Min(Mathf.FloorToInt(t / StepDur), Steps - 1);
            float u    = t - step * StepDur;
            int   live = slotY.Length; // cards alive this step: slots 0..live-1 (last = spawning)

            for (int j = 0; j < live && j < squares.Length; j++)
            {
                int    p      = step + j;                 // pattern index (unbounded)
                int    sq     = p % squares.Length;       // ring-buffer square
                Sprite glyph  = pattern.Length > 0 ? pattern[p % pattern.Length] : null;
                bool   isRest = glyph != null && glyph.name.Contains("Rest");

                RectTransform rt = squares[sq];
                if (rt == null) continue;
                Image back = backImages.Length > sq ? backImages[sq] : null;
                Image gl   = glyphImages.Length > sq ? glyphImages[sq] : null;
                if (gl != null && gl.sprite != glyph) gl.sprite = glyph;

                float y, scale = 1f, alpha = 1f;
                if (j == 0)
                {
                    // In the hit zone: note → tap + bump then shrink; rest → fade.
                    y = slotY[0];
                    if (isRest)
                        alpha = 1f - Progress(u, 0.05f, 0.2f);
                    else
                        scale = Punch(u, 0f, 0.18f) * (1f - InQuad(Progress(u, 0.2f, 0.15f)));
                }
                else
                {
                    float slide = OutCubic(Progress(u, 0f, 0.2f));
                    y = Mathf.Lerp(slotY[j], slotY[j - 1], slide);
                    if (j == live - 1) scale = OutBack(Progress(u, 0f, 0.25f)); // spawning
                }

                rt.anchoredPosition = new Vector2(0f, y);
                SetScale(rt, scale);
                SetAlpha(back, alpha);
                SetAlpha(gl, alpha);
            }

            // Tap ring in the hit zone on note steps only.
            bool noteNow = pattern.Length > 0 && pattern[step % pattern.Length] != null
                           && !pattern[step % pattern.Length].name.Contains("Rest");
            if (ring != null) ring.anchoredPosition = new Vector2(0f, slotY[0]);
            if (noteNow) TapRing(ring, ringImage, u, 0f, 0.3f);
            else SetAlpha(ringImage, 0f);
        }
    }
}
