using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Bongo (call &amp; response) how-to-play loop, 3.2 s: three rhythm glyphs
    /// pop in across the top while the left/right pads blink in turn (the
    /// "call"), then a tap ring + bump lands on each pad in the same order and
    /// the matching glyph turns green (the "response"); everything fades and
    /// the loop restarts. Wired by CoplayScripts/AddSignPreviews.cs.
    /// </summary>
    public class BongoPreviewAnimation : SignPreviewAnimation
    {
        [SerializeField] private RectTransform[] pads      = new RectTransform[2];
        [SerializeField] private Image[]         padImages = new Image[2];
        [SerializeField] private RectTransform[] glyphs    = new RectTransform[3];
        [SerializeField] private Image[]         glyphImages = new Image[3];
        [SerializeField] private RectTransform[] rings      = new RectTransform[2];
        [SerializeField] private Image[]         ringImages = new Image[2];

        [SerializeField] private Color padBaseColor  = new Color(0.86f, 0.62f, 0.38f, 1f);
        [SerializeField] private Color padLitColor   = new Color(1f, 0.92f, 0.70f, 1f);
        [SerializeField] private Color glyphColor    = new Color(0.15f, 0.13f, 0.11f, 1f);
        [SerializeField] private Color glyphHitColor = new Color(0.25f, 0.90f, 0.40f, 1f);

        private const float Loop        = 3.2f;
        private const float CallStart   = 0.0f;
        private const float Step        = 0.3f;
        private const float AnswerStart = 1.3f;
        private const float FadeStart   = 2.5f;
        private const float FadeDur     = 0.4f;

        // Pad order for the three beats: L, R, L.
        private static readonly int[] PadOrder = { 0, 1, 0 };

        protected override float LoopLength => Loop;

        protected override void Evaluate(float t)
        {
            float fadeOut = 1f - Progress(t, FadeStart, FadeDur);

            // Glyphs: pop on the call, turn green when answered, fade at the end.
            for (int k = 0; k < glyphs.Length; k++)
            {
                float callT = CallStart + k * Step;
                float hitT  = AnswerStart + k * Step;
                SetScale(glyphs[k], PopIn(t, callT, 0.25f) * Punch(t, hitT, 0.18f));
                if (glyphImages.Length > k && glyphImages[k] != null)
                {
                    Color c = Color.Lerp(glyphColor, glyphHitColor, Progress(t, hitT, 0.1f));
                    c.a = fadeOut;
                    glyphImages[k].color = c;
                }
            }

            // Pads: blink on the call beat, bump on the answer tap.
            for (int p = 0; p < pads.Length; p++)
            {
                float lit = 0f, bump = 1f;
                for (int k = 0; k < PadOrder.Length; k++)
                {
                    if (PadOrder[k] != p) continue;
                    float callT = CallStart + k * Step;
                    float hitT  = AnswerStart + k * Step;
                    lit  = Mathf.Max(lit, Progress(t, callT, 0.06f) * (1f - Progress(t, callT + 0.06f, 0.2f)));
                    bump *= Punch(t, hitT, 0.18f, 0.12f);
                }
                SetScale(pads[p], bump);
                if (padImages.Length > p && padImages[p] != null)
                    padImages[p].color = Color.Lerp(padBaseColor, padLitColor, lit);
            }

            // Tap rings per pad on the answer beats.
            for (int p = 0; p < rings.Length; p++)
            {
                float best = -1f;
                for (int k = 0; k < PadOrder.Length; k++)
                {
                    if (PadOrder[k] != p) continue;
                    float hitT = AnswerStart + k * Step;
                    if (t >= hitT && t <= hitT + 0.3f) best = hitT;
                }
                if (best >= 0f) TapRing(rings[p], ringImages.Length > p ? ringImages[p] : null, t, best);
                else if (ringImages.Length > p) SetAlpha(ringImages[p], 0f);
            }
        }
    }
}
