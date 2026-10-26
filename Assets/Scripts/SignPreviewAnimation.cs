using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Base class for the looping "how to play" mini-animations shown on a
    /// MenuSignPreview panel. Subclasses describe ONE loop as a pure function
    /// of loop time — Evaluate(t) sets every Image/RectTransform from t alone
    /// — so the loop is deterministic, seam-free (Evaluate(LoopLength) must
    /// equal Evaluate(0)) and needs no tween bookkeeping. Runs on unscaled
    /// time only while playing (one panel plays at a time). Pure visuals,
    /// no audio, no gameplay dependencies.
    ///
    /// speed scales loop time (Double Time reuses the Classic animation at 2).
    /// </summary>
    public abstract class SignPreviewAnimation : MonoBehaviour
    {
        [Tooltip("Loop time multiplier (2 = twice as fast).")]
        [SerializeField, Min(0.1f)] protected float speed = 1f;

        /// <summary>True while the loop is being advanced each frame.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Current loop time in seconds (probes).</summary>
        public float LoopTime => _time;

        /// <summary>Length of one loop in (unscaled, unsped) seconds.</summary>
        protected abstract float LoopLength { get; }

        /// <summary>Set all visuals for loop time t ∈ [0, LoopLength).</summary>
        protected abstract void Evaluate(float t);

        private float _time;

        public void Play()
        {
            if (IsPlaying) return;
            IsPlaying = true;
            _time = 0f;
            Evaluate(0f);
        }

        /// <summary>Stop advancing and rest at the loop's first frame.</summary>
        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            _time = 0f;
            Evaluate(0f);
        }

        protected virtual void OnDisable() => Stop();

        private void Update()
        {
            if (!IsPlaying) return;
            float len = LoopLength;
            if (len <= 0f) return;
            _time = Mathf.Repeat(_time + Time.unscaledDeltaTime * speed, len);
            Evaluate(_time);
        }

        // ── Timing / easing helpers ───────────────────────────────────────────

        /// <summary>0 before start, 1 after start+duration, linear between.</summary>
        protected static float Progress(float t, float start, float duration) =>
            duration <= 0f ? (t >= start ? 1f : 0f) : Mathf.Clamp01((t - start) / duration);

        protected static float OutCubic(float p) { p = 1f - p; return 1f - p * p * p; }
        protected static float OutQuad(float p)  => 1f - (1f - p) * (1f - p);
        protected static float InQuad(float p)   => p * p;

        protected static float OutBack(float p)
        {
            const float s = 1.70158f;
            p -= 1f;
            return p * p * ((s + 1f) * p + s) + 1f;
        }

        /// <summary>Scale of an element popping in (0 → 1, OutBack) from start.</summary>
        protected static float PopIn(float t, float start, float duration) =>
            t < start ? 0f : OutBack(Progress(t, start, duration));

        /// <summary>Scale multiplier for a hit bump (1 → 1+amount → 1) over duration.</summary>
        protected static float Punch(float t, float start, float duration, float amount = 0.18f)
        {
            if (t < start || t > start + duration) return 1f;
            return 1f + amount * Mathf.Sin((t - start) / duration * Mathf.PI);
        }

        // ── Visual helpers ────────────────────────────────────────────────────

        protected static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        protected static void SetScale(RectTransform rt, float s)
        {
            if (rt != null) rt.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>
        /// The shared "tap finger": a ring that expands 0.5 → 1.4 while fading
        /// 0.9 → 0 over duration from each time in tapTimes; hidden otherwise.
        /// </summary>
        protected static void TapRing(RectTransform ring, Image ringImage, float t, float[] tapTimes, float duration = 0.3f)
        {
            if (ring == null || ringImage == null) return;
            for (int i = 0; i < tapTimes.Length; i++)
            {
                float start = tapTimes[i];
                if (t < start || t > start + duration) continue;
                float p = OutQuad((t - start) / duration);
                SetScale(ring, Mathf.Lerp(0.5f, 1.4f, p));
                SetAlpha(ringImage, 0.9f * (1f - p));
                return;
            }
            SetAlpha(ringImage, 0f);
        }

        protected static void TapRing(RectTransform ring, Image ringImage, float t, float tapTime, float duration = 0.3f)
        {
            if (ring == null || ringImage == null) return;
            if (t < tapTime || t > tapTime + duration) { SetAlpha(ringImage, 0f); return; }
            float p = OutQuad((t - tapTime) / duration);
            SetScale(ring, Mathf.Lerp(0.5f, 1.4f, p));
            SetAlpha(ringImage, 0.9f * (1f - p));
        }
    }
}
