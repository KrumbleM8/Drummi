using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Play (column stream, FlashCardLayout.ColumnScroll) how-to-play loop: a
    /// miniature of the real stream screen (CoplayScripts/AddSignPreviews.cs
    /// builds it in canvas units). The stream never stops: every beat at
    /// 105 BPM the column slides down one 150 px pitch (0.12 s OutCubic), the
    /// arriving block grows from the 119 px trail size to the 175 px hit-zone
    /// size (OutBack), everything wobbles ±6°, a note in the hit zone is tapped
    /// (gold/green flash, punch, 5 shards) and a rest dissolves grey as its
    /// beat ends. Eight squares form a ring buffer over an 8-step pattern, so
    /// the 8-beat loop (4.57 s) is seamless. Upcoming faces are dimmed ×0.95
    /// with glyph alpha 0.85 like the on-deck stack.
    /// </summary>
    public class SnakePreviewAnimation : FlashCardsPreviewAnimation
    {
        [Header("Column")]
        [SerializeField] private RectTransform[] squares = new RectTransform[8];
        [SerializeField] private Image[] backs  = new Image[8];
        [SerializeField] private Image[] glyphs = new Image[8];
        [Tooltip("Glyph per pattern step (8). Rests are detected by sprite name.")]
        [SerializeField] private Sprite[] pattern = new Sprite[8];

        [Tooltip("Hit-zone square centre y (canvas units, origin centre).")]
        [SerializeField] private float hitZoneY = -91.5f;
        [SerializeField] private float pitch = 150f;
        [SerializeField] private float trailSize = 119f;
        [SerializeField] private float hitSize = 175f;

        private const int   Steps     = 8;
        private const float StepDur   = 0.12f;
        private const float TapOffset = 0.25f;

        protected override float LoopLength => Steps * Beat;

        private bool IsRest(int p) => pattern.Length > 0 && pattern[p % pattern.Length] != null
                                      && pattern[p % pattern.Length].name.Contains("Rest");

        protected override void Evaluate(float t)
        {
            EvaluateDrum(t);
            int   step = Mathf.Min(Mathf.FloorToInt(t / Beat), Steps - 1);
            float u    = t - step * Beat;
            float wobble = Wobble(t);
            float slide = OutCubic(Progress(u, 0f, StepDur));

            int live = squares.Length;           // j = 0 hit zone … live-1 spawning above the top
            for (int j = 0; j < live; j++)
            {
                int p = step + j;                 // pattern index (unbounded)
                int sq = p % squares.Length;      // ring-buffer square
                RectTransform rt = squares[sq];
                if (rt == null) continue;
                Sprite glyph = pattern.Length > 0 ? pattern[p % pattern.Length] : null;
                bool rest = IsRest(p);
                Image back = backs.Length > sq ? backs[sq] : null;
                Image gl   = glyphs.Length > sq ? glyphs[sq] : null;
                if (gl != null && gl.sprite != glyph) gl.sprite = glyph;

                // Slide from slot j+1 down to slot j during the first 0.12 s of the beat.
                float y = Mathf.Lerp(hitZoneY + pitch * (j + 1), hitZoneY + pitch * j, slide);
                float size = j == 0 ? Mathf.Lerp(trailSize, hitSize, OutBack(Progress(u, 0f, StepDur))) : trailSize;
                float scale = 1f;
                Color face = rest ? RestFace : NoteFace;
                Color backCol = face;
                Color glyphCol = rest ? RestGlyph : NoteGlyph;

                if (j == 0)
                {
                    // In the hit zone: note tapped at +0.25 s, rest dissolves at the beat's end.
                    float eventTime = rest ? Beat - DestroyLen - 0.02f : TapOffset;
                    Color hit = rest ? RestFace : (step % 2 == 0 ? HitGold : HitGreen);
                    scale = DestroyScale(u - eventTime, face, hit, out backCol);
                }
                else
                {
                    backCol = face * 0.95f; backCol.a = 1f;           // on-deck dim
                    if (!rest) glyphCol.a = 0.85f;
                }

                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(size, size);
                SetScale(rt, scale);
                rt.localRotation = Quaternion.Euler(0f, 0f, wobble);
                if (back != null) back.color = backCol;
                if (gl != null) gl.color = glyphCol;
            }

            // Shard bursts for this step's hit-zone event (pool = step parity) and the previous step's tail.
            BurstFor(step, u, step % 2);
            BurstFor(step - 1, u + Beat, (step + 1) % 2);
        }

        private void BurstFor(int step, float u, int pool)
        {
            if (step < 0) { EvaluateShardBurst(pool, Vector2.zero, HitGold, hitSize, -1f); return; }
            bool rest = IsRest(step);
            float eventTime = rest ? Beat - DestroyLen - 0.02f : TapOffset;
            Color col = rest ? RestFace : (step % 2 == 0 ? HitGold : HitGreen);
            EvaluateShardBurst(pool, new Vector2(0f, hitZoneY), col, hitSize, u - eventTime);
        }
    }
}
