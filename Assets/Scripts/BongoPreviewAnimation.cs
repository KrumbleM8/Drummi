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
    ///                (BongoAnimator), a 96-unit disc rises 70 above that drum
    ///                (FlashPlayer), a gold star spins on the last (Perfect)
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
        private static readonly float PlayerFlashX = 200f, PlayerFlashY = -283f;

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
                _playerBase[i] = new Vector2(TapLeft[i % TapLeft.Length] ? -PlayerFlashX : PlayerFlashX, PlayerFlashY);
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
                RectTransform rt = callFlashes[k];
                if (rt == null) continue;
                float age = t - k * Beat;
                bool left = CallLeft[k % CallLeft.Length];
                Color tint = left ? leftColor : rightColor;

                if (age < 0f || age >= 0.37f)
                {
                    SetFlash(rt, callFlashImages, callOverlayImages, k, 0f, 1f, tint);
                    continue;
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
                SetFlash(rt, callFlashImages, callOverlayImages, k, alpha, scale, tint);
            }
        }

        private void EvaluatePlayerFlashes(float t)
        {
            for (int k = 0; k < playerFlashes.Length; k++)
            {
                RectTransform rt = playerFlashes[k];
                if (rt == null) continue;
                float age = t - (4f * Beat + k * Beat);
                bool left = TapLeft[k % TapLeft.Length];
                Image img = playerFlashImages.Length > k ? playerFlashImages[k] : null;
                Color tint = left ? leftColor : rightColor;

                if (age < 0f || age >= 0.36f)
                {
                    tint.a = 0f; if (img != null) img.color = tint;
                    continue;
                }
                float scale, alpha, rise;
                if (age < 0.06f) { scale = Mathf.Lerp(0.75f, 1f, OutBack(age / 0.06f)); alpha = 1f; rise = 0f; }
                else
                {
                    float fp = (age - 0.06f) / 0.3f;
                    scale = 1f; alpha = 1f - fp; rise = 70f * fp;
                }
                rt.anchoredPosition = _playerBase[k] + new Vector2(0f, rise);
                SetScale(rt, scale);
                tint.a = alpha; if (img != null) img.color = tint;
            }

            // Perfect star on the last tap: 60 above the flash; spin+grow 0.17 s → settle 0.12 s → fade 0.45 s.
            if (star != null && starImage != null)
            {
                int last = TapLeft.Length - 1;
                float age = t - (4f * Beat + last * Beat);
                if (age < 0f || age >= 0.74f) { SetAlpha(starImage, 0f); }
                else
                {
                    float scale, alpha = 1f, rot = 0f;
                    if (age < 0.17f)      { float p = age / 0.17f; scale = 1.2f * OutBack(p); rot = 360f * p; }
                    else if (age < 0.29f) { scale = Mathf.Lerp(1.2f, 0.8f, (age - 0.17f) / 0.12f); }
                    else                  { scale = 0.8f; alpha = 1f - (age - 0.29f) / 0.45f; }
                    star.anchoredPosition = _playerBase[last] + new Vector2(0f, 60f);
                    star.localRotation = Quaternion.Euler(0f, 0f, rot);
                    SetScale(star, scale);
                    SetAlpha(starImage, alpha);
                }
            }
        }

        private static void SetFlash(RectTransform rt, Image[] images, Image[] overlays, int k, float alpha, float scale, Color tint)
        {
            SetScale(rt, scale);
            if (images.Length > k && images[k] != null) { tint.a = alpha; images[k].color = tint; }
            if (overlays.Length > k && overlays[k] != null) SetAlpha(overlays[k], alpha);
        }
    }
}
