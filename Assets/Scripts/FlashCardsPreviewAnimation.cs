using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Shared pieces of the FlashCards gameplay replicas (Classic grid and the
    /// Play column stream): the 105 BPM beat, the drum pad's BounceOnBeat
    /// stretch, the tap-destroy animation (flash gold/green, ×1.15 in 0.05 s,
    /// → 0 in 0.10 s) and the 5-shard burst (0.22 × square, fly 60–110 px
    /// OutCubic, spin 90–270°, shrink + fade InQuad over 0.35 s), all taken
    /// from FlashCardView. Shard pools alternate between two 5-shard sets so
    /// consecutive bursts never share images.
    /// </summary>
    public abstract class FlashCardsPreviewAnimation : SignPreviewAnimation
    {
        [Header("Shared")]
        [SerializeField] protected RectTransform drum;
        [SerializeField] protected RectTransform[] shards = new RectTransform[10];
        [SerializeField] protected Image[] shardImages = new Image[10];

        protected const float Beat = 60f / 105f;   // musicBpm 105 → 0.571 s

        protected static readonly Color NoteFace  = new Color(0.96f, 0.93f, 0.82f, 1f);
        protected static readonly Color RestFace  = new Color(0.62f, 0.60f, 0.68f, 1f);
        protected static readonly Color NoteGlyph = new Color(0f, 0f, 0f, 1f);
        protected static readonly Color RestGlyph = new Color(0.15f, 0.15f, 0.20f, 0.8f);
        protected static readonly Color HitGold   = new Color(1f, 0.85f, 0.30f, 1f);
        protected static readonly Color HitGreen  = new Color(0.35f, 0.90f, 0.40f, 1f);
        protected static readonly Color CursorCol = new Color(1f, 1f, 1f, 0.35f);

        private const float DestroyUp   = 0.05f;
        private const float DestroyDown = 0.10f;
        protected const float DestroyLen = DestroyUp + DestroyDown;
        private const float ShardLife   = 0.35f;

        // Deterministic "random" shard kinematics (angle deg, distance px, spin deg, scale).
        private static readonly float[] ShardAngle = { 15f, 80f, 150f, 230f, 300f, 40f, 110f, 190f, 260f, 330f };
        private static readonly float[] ShardDist  = { 95f, 70f, 110f, 65f, 85f, 100f, 60f, 90f, 75f, 105f };
        private static readonly float[] ShardSpin  = { 180f, -120f, 250f, -90f, 150f, -200f, 120f, -270f, 100f, -160f };
        private static readonly float[] ShardScale = { 1.0f, 0.8f, 1.2f, 0.7f, 0.9f, 1.1f, 0.75f, 1.0f, 0.85f, 1.15f };

        /// <summary>Beat pulse 1 → 0 after each beat (fast decay, BounceOnBeat SmoothDamp feel).</summary>
        protected static float BeatPulse(float t)
        {
            float p = Mathf.Repeat(t, Beat) / Beat;
            return Mathf.Pow(1f - p, 3f);
        }

        /// <summary>Drum pad BounceOnBeat: Y scale to 1.3 on the beat, easing back.</summary>
        protected void EvaluateDrum(float t)
        {
            if (drum != null) drum.localScale = new Vector3(1f, 1f + 0.3f * BeatPulse(t), 1f);
        }

        /// <summary>
        /// Tap-destroy curve. age &lt; 0 → untouched (scale 1, face colour); then the
        /// back flashes to the hit colour, scale 1 → 1.15 (OutQuad, 0.05 s) → 0
        /// (InQuad, 0.10 s); after that the square is gone (scale 0).
        /// </summary>
        protected static float DestroyScale(float age, Color face, Color hit, out Color back)
        {
            if (age < 0f) { back = face; return 1f; }
            back = hit;
            if (age < DestroyUp) return Mathf.Lerp(1f, 1.15f, OutQuad(age / DestroyUp));
            if (age < DestroyLen) return 1.15f * (1f - InQuad((age - DestroyUp) / DestroyDown));
            return 0f;
        }

        /// <summary>
        /// Drive 5 pooled shards (set 0 or 1) for one burst from origin: visible
        /// while 0 ≤ age &lt; 0.35 s. squareSize is the destroyed square's size.
        /// </summary>
        protected void EvaluateShardBurst(int set, Vector2 origin, Color color, float squareSize, float age)
        {
            int first = set * 5;
            bool live = age >= 0f && age < ShardLife;
            float p = live ? age / ShardLife : 1f;
            float fly = OutCubic(p), fade = 1f - InQuad(p);
            for (int i = 0; i < 5; i++)
            {
                int idx = first + i;
                if (idx >= shards.Length || shards[idx] == null) continue;
                Image img = shardImages.Length > idx ? shardImages[idx] : null;
                if (!live) { SetAlpha(img, 0f); continue; }
                float ang = ShardAngle[idx] * Mathf.Deg2Rad;
                shards[idx].anchoredPosition = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (ShardDist[idx] * fly);
                shards[idx].localRotation = Quaternion.Euler(0f, 0f, ShardSpin[idx] * p);
                float size = squareSize * 0.22f * ShardScale[idx];
                shards[idx].sizeDelta = new Vector2(size, size);
                SetScale(shards[idx], fade);
                if (img != null) { Color c = color; c.a = fade; img.color = c; }
            }
        }

        /// <summary>Hide every shard (frame 0).</summary>
        protected void HideShards()
        {
            for (int i = 0; i < shardImages.Length; i++) SetAlpha(shardImages[i], 0f);
        }

        /// <summary>UIRotateWobble: ±6° sine, one cycle per 2 beats.</summary>
        protected static float Wobble(float t) => 6f * Mathf.Sin(2f * Mathf.PI * t / (2f * Beat));
    }
}
