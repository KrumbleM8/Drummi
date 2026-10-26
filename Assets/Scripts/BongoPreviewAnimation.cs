using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Bongo how-to-play loop: a faithful miniature of the real Bongo.unity
    /// gameplay screen (built by CoplayScripts/AddSignPreviews.cs in the
    /// game's own 800-wide canvas units, scaled into the portrait panel) that
    /// plays ONE 8-beat call-and-response bar at 0.5 s/beat = 4.0 s:
    ///   beats 0,1,2  Custard's call — he flips to the green/red pose and the
    ///                270-unit Indicator disc pops near centre (±135, −93),
    ///                tilted ±8°, then fades 0.25 s (BongoBeatFlash.FlashCall)
    ///   beat 3.33    Custard listens; the dimmed drums grow + brighten 0.3 s
    ///   beats 4,5,6  player taps L,R,L — drums kick ±11° / ×1.13 for 0.08 s
    ///                (BongoAnimator) and the SAME 270-unit disc as the call pops
    ///                near centre (FlashPlayer = call style); on the last tap
    ///                (Perfect) a big gold star pops in BEHIND the disc, centred
    ///                on it (1.6× = background stamp), turns 60° and fades first
    ///   beat 7.5     drums dim again, Custard goes Happy until the next bar
    /// Every beat also pulses the hills / Custard / drums (BounceOnBeat) and
    /// the clouds scroll. Pure visuals from the game's own sprites.
    /// </summary>
    public class BongoPreviewAnimation : SignPreviewAnimation
    {
        [Header("Custard")]
        [SerializeField] private RectTransform custard;
        [SerializeField] private Image custardImage;
        [SerializeField] private Sprite spritePlayNeutral;
        [SerializeField] private Sprite spriteRed;
        [SerializeField] private Sprite spriteGreen;
        [SerializeField] private Sprite spriteListen;
        [SerializeField] private Sprite spriteHappy;
        [Tooltip("Canvas units per sprite pixel for Custard (0.4215 world scale × 158.3 canvas/unit ÷ 100 ppu).")]
        [SerializeField] private float custardUnitsPerPixel = 0.667f;

        [Header("Drums")]
        [SerializeField] private RectTransform bongos;        // kick pivot (BongoAnimator)
        [SerializeField] private RectTransform bongoVisual;   // dim scale (DrumPadStateVisual)
        [SerializeField] private Image bongoImage;
        [SerializeField] private Image bongoShadow;

        [Header("Scenery")]
        [SerializeField] private RectTransform[] hills = new RectTransform[0];
        [Tooltip("Per-hill Y-scale bounce amplitude on each beat (BounceOnBeat: back +0.33, mid −0.18, front −0.05).")]
        [SerializeField] private float[] hillBounce = new float[0];
        [SerializeField] private RawImage clouds;
        [SerializeField, Range(0f, 1f)] private float cloudUvWidth = 0.926f;

        [Header("Beat flashes")]
        [SerializeField] private RectTransform[] callFlashes = new RectTransform[3];
        [SerializeField] private Image[] callFlashImages = new Image[3];
        [SerializeField] private Image[] callOverlayImages = new Image[3];
        [SerializeField] private RectTransform[] playerFlashes = new RectTransform[3];
        [SerializeField] private Image[] playerFlashImages = new Image[3];
        [SerializeField] private Image[] playerOverlayImages = new Image[3];
        [SerializeField] private RectTransform star;
        [SerializeField] private Image starImage;

        [SerializeField] private Color leftColor  = new Color(0.25f, 1f, 0.38f, 1f);
        [SerializeField] private Color rightColor = new Color(1f, 0.2f, 0.296f, 1f);

        // ── Timeline (one bar, 8 beats × 0.5 s) ─────────────────────────────
        private const float Beat        = 0.5f;
        private const float Loop        = 8f * Beat;
        private const float ListenAt    = 3.333f * Beat;  // 1.667 s
        private const float EvalAt      = 7.5f * Beat;    // 3.75 s
        private const float NextBarPrep = Loop - 0.1f;    // PlayNeutral 0.1 s before the next first beat
        private const float CallHold    = 0.45f;          // max(0.3 s, 0.9 beat)

        private static readonly bool[] CallLeft = { true, false, true };   // L R L
        private static readonly bool[] TapLeft  = { true, false, true };

        private static readonly float CallFlashX = 135f, CallFlashY = -93f;   // (±135, −60) in a layer 33 below centre
        private const float StarOffsetY = 0f;                                  // BongoBeatFlash.starOffsetY (centred behind the disc)

        protected override float LoopLength => Loop;

        private Vector3[] _hillBaseScale;
        private Vector2[] _playerBase;

        private void Awake()
        {
            _hillBaseScale = new Vector3[hills.Length];
            for (int i = 0; i < hills.Length; i++)
                _hillBaseScale[i] = hills[i] != null ? hills[i].localScale : Vector3.one;
            _playerBase = new Vector2[playerFlashes.Length];
            for (int i = 0; i < playerFlashes.Length; i++)
                _playerBase[i] = new Vector2(TapLeft[i % TapLeft.Length] ? -CallFlashX : CallFlashX, CallFlashY);
        }

        protected override void Evaluate(float t)
        {
            // Beat pulse: fast decay after every beat (BounceOnBeat SmoothDamp back).
            float beatP = Mathf.Repeat(t, Beat) / Beat;
            float pulse = Mathf.Pow(1f - beatP, 3f);

            EvaluateScenery(t, pulse);
            EvaluateCustard(t, pulse);
            EvaluateDrums(t, pulse);
            EvaluateCallFlashes(t);
            EvaluatePlayerFlashes(t);
        }

        private void EvaluateScenery(float t, float pulse)
        {
            for (int i = 0; i < hills.Length; i++)
            {
                if (hills[i] == null) continue;
                float amp = hillBounce.Length > i ? hillBounce[i] : 0f;
                Vector3 s = _hillBaseScale[i];
                s.y *= 1f + amp * pulse;
                hills[i].localScale = s;
            }
            if (clouds != null)
                clouds.uvRect = new Rect(t / Loop, 0f, cloudUvWidth, 1f); // exactly 1 uv per loop → seamless
        }

        private void EvaluateCustard(float t, float pulse)
        {
            if (custardImage == null) return;

            Sprite s = spritePlayNeutral;
            if (t >= NextBarPrep)      s = spritePlayNeutral;
            else if (t >= EvalAt)      s = spriteHappy;
            else if (t >= ListenAt)    s = spriteListen;
            else
            {
                for (int k = 0; k < CallLeft.Length; k++)
                {
                    float tk = k * Beat;
                    if (t >= tk && t < tk + CallHold) { s = CallLeft[k] ? spriteGreen : spriteRed; break; }
                }
            }

            if (s != null && custardImage.sprite != s)
            {
                custardImage.sprite = s;
                custard.sizeDelta = s.rect.size * custardUnitsPerPixel;
            }
            // BounceOnBeat on Custard: +1.8 % Y per beat.
            custard.localScale = new Vector3(1f, 1f + 0.018f * pulse, 1f);
        }

        private void EvaluateDrums(float t, float pulse)
        {
            // Bright during the response window only (DrumPadStateVisual 0.3 s in / 0.2 s out, OutQuad).
            float bright;
            if (t < ListenAt)              bright = 0f;
            else if (t < ListenAt + 0.3f)  bright = OutQuad((t - ListenAt) / 0.3f);
            else if (t < EvalAt)           bright = 1f;
            else if (t < EvalAt + 0.2f)    bright = 1f - OutQuad((t - EvalAt) / 0.2f);
            else                           bright = 0f;

            if (bongoVisual != null)
                bongoVisual.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, bright);
            float alpha = Mathf.Lerp(0.5f, 1f, bright);
            SetAlpha(bongoImage, alpha);
            SetAlpha(bongoShadow, alpha);

            // Tap kick (BongoAnimator): ×1.13 / ±11° up over 0.043 s, back over 0.04 s. Left = +Z.
            float kick = 0f, sign = 0f;
            for (int k = 0; k < TapLeft.Length; k++)
            {
                float age = t - (4f * Beat + k * Beat);
                if (age < 0f || age > 0.083f) continue;
                kick = age < 0.043f ? age / 0.043f : 1f - (age - 0.043f) / 0.04f;
                sign = TapLeft[k] ? 1f : -1f;
            }
            if (bongos != null)
            {
                float s = Mathf.Lerp(1f, 1.13f, kick);
                bongos.localScale = new Vector3(s, s * (1f + 0.036f * pulse), 1f); // + BounceOnBeat Y 1.036
                bongos.localRotation = Quaternion.Euler(0f, 0f, 11f * kick * sign);
            }
        }

        private void EvaluateCallFlashes(float t)
        {
            for (int k = 0; k < callFlashes.Length; k++)
            {
                bool left = CallLeft[k % CallLeft.Length];
                EvaluateBeatFlash(callFlashes[k], callFlashImages, callOverlayImages, k,
                                  t - k * Beat, left, left ? leftColor : rightColor);
            }
        }

        private void EvaluatePlayerFlashes(float t)
        {
            // Player taps use the exact same flash as the call (BongoBeatFlash.FlashPlayer → AnimateCall).
            for (int k = 0; k < playerFlashes.Length; k++)
            {
                bool left = TapLeft[k % TapLeft.Length];
                EvaluateBeatFlash(playerFlashes[k], playerFlashImages, playerOverlayImages, k,
                                  t - (4f * Beat + k * Beat), left, left ? leftColor : rightColor);
            }

            // Perfect star on the last tap: centred behind the flash (drawn earlier in the hierarchy).
            // BongoBeatFlash.AnimateStar: pop 0.7→1 over 0.08 s, fade 0.2 s drifting to ×1.05, 60° eased turn over
            // the whole 0.28 s life — so it is gone before the disc's 0.37 s.
            if (star != null && starImage != null)
            {
                int last = TapLeft.Length - 1;
                float age = t - (4f * Beat + last * Beat);
                const float grow = 0.08f, fade = 0.2f, life = grow + fade;
                if (age < 0f || age >= life) { SetAlpha(starImage, 0f); }
                else
                {
                    float scale, alpha;
                    if (age < grow) { scale = Mathf.Lerp(0.7f, 1f, OutCubic(age / grow)); alpha = 1f; }
                    else
                    {
                        float fp = (age - grow) / fade;
                        alpha = 1f - fp * fp;
                        scale = 1f + 0.05f * fp;
                    }
                    star.anchoredPosition = _playerBase[last] + new Vector2(0f, StarOffsetY);
                    star.localRotation = Quaternion.Euler(0f, 0f, 60f * OutCubic(age / life));
                    SetScale(star, scale);
                    SetAlpha(starImage, alpha);
                }
            }
        }

        /// <summary>One BongoBeatFlash.AnimateCall flash: pop 1.25→1 over 0.08 s, hold 0.04 s, fade 0.25 s drifting to ×1.08.</summary>
        private static void EvaluateBeatFlash(RectTransform rt, Image[] images, Image[] overlays, int k, float age, bool left, Color tint)
        {
            if (rt == null) return;

            if (age < 0f || age >= 0.37f)
            {
                SetFlash(rt, images, overlays, k, 0f, 1f, tint);
                return;
            }
            float scale, alpha;
            if (age < 0.08f)       { scale = Mathf.Lerp(1.25f, 1f, OutCubic(age / 0.08f)); alpha = 1f; }
            else if (age < 0.12f)  { scale = 1f; alpha = 1f; }
            else
            {
                float fp = (age - 0.12f) / 0.25f;
                alpha = 1f - fp * fp;
                scale = 1f + 0.08f * fp;
            }
            rt.anchoredPosition = new Vector2(left ? -CallFlashX : CallFlashX, CallFlashY);
            rt.localRotation = Quaternion.Euler(0f, 0f, left ? 8f : -8f);
            SetFlash(rt, images, overlays, k, alpha, scale, tint);
        }

        private static void SetFlash(RectTransform rt, Image[] images, Image[] overlays, int k, float alpha, float scale, Color tint)
        {
            SetScale(rt, scale);
            if (images.Length > k && images[k] != null) { tint.a = alpha; images[k].color = tint; }
            if (overlays.Length > k && overlays[k] != null) SetAlpha(overlays[k], alpha);
        }
    }
}
