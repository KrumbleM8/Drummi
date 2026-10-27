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
    ///                on it (1.6× = background stamp), turns 60° and fades first;
    ///                the bar is clean, so the same tap also fires the phrase-
    ///                clear burst: a white pulse swells out from behind the disc
    ///                and a fan of small discs + stars arcs up and fades
    ///                (BongoBeatFlash.FlashPhraseClear, pieces built in Awake)
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

        // Phrase-clear burst — mirrors BongoBeatFlash's clear* defaults exactly.
        private const float ClearPulseAlpha    = 0.55f;
        private const float ClearPulseEndScale = 2.6f;
        private const float ClearPulseDuration = 0.4f;
        private const int   ClearPieceCount    = 14;
        private const float ClearPieceSize     = 54f;
        private const float ClearStarFactor    = 1.3f;
        private const float ClearSpeedMin      = 650f, ClearSpeedMax = 1150f;
        private const float ClearSpread        = 150f;
        private const float ClearGravity       = 2400f;
        private const float ClearSpin          = 540f;
        private const float ClearLife          = 0.75f;
        private const float ClearFadeFraction  = 0.45f;
        private const float ClearPopScale      = 0.4f;
        private const float ClearPopDuration   = 0.07f;
        private const float CallSize           = 270f;

        private RectTransform _clearPulse;
        private Image _clearPulseImage;
        private RectTransform[] _clearPieces = new RectTransform[0];
        private Image[] _clearPieceImages = new Image[0];
        private Vector2[] _clearVelocity = new Vector2[0];
        private float[] _clearSpin = new float[0];
        private float[] _clearAngle = new float[0];

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

            BuildClearBurst();
        }

        /// <summary>
        /// Builds the phrase-clear burst objects at runtime (no scene rebuild needed): a pulse disc just
        /// behind the star and a fixed, seeded fan of small discs/stars on top of everything. The real
        /// burst randomises per phrase; the loop uses one seeded pattern so it is deterministic.
        /// </summary>
        private void BuildClearBurst()
        {
            if (star == null || starImage == null) return;
            Sprite disc = playerFlashImages.Length > 0 && playerFlashImages[0] != null ? playerFlashImages[0].sprite : null;
            if (disc == null) return;

            RectTransform parent = star.parent as RectTransform;
            if (parent == null) return;

            _clearPulse = MakePiece("ClearPulse", parent, disc, CallSize, Color.white, out _clearPulseImage);
            _clearPulse.SetSiblingIndex(star.GetSiblingIndex());   // behind the star (and so behind the disc)

            var rng = new System.Random(2026);
            float halfSpread = ClearSpread * 0.5f;
            const float sideBias = -8f;                              // last tap is left (TapLeft[2])

            _clearPieces      = new RectTransform[ClearPieceCount];
            _clearPieceImages = new Image[ClearPieceCount];
            _clearVelocity    = new Vector2[ClearPieceCount];
            _clearSpin        = new float[ClearPieceCount];
            _clearAngle       = new float[ClearPieceCount];

            for (int i = 0; i < ClearPieceCount; i++)
            {
                bool isStar = i % 3 == 2;
                Sprite sprite = isStar ? starImage.sprite : disc;
                Color tint = isStar ? Color.white : (i % 2 == 0 ? leftColor : rightColor);
                float size = ClearPieceSize * (isStar ? ClearStarFactor : 1f);

                _clearPieces[i] = MakePiece("ClearPiece" + i, parent, sprite, size, tint, out _clearPieceImages[i]);
                _clearPieces[i].SetAsLastSibling();

                float slot = (float)i / (ClearPieceCount - 1);
                float angle = 90f + sideBias + Mathf.Lerp(-halfSpread, halfSpread, slot) + Range(rng, -6f, 6f);
                float speed = Range(rng, ClearSpeedMin, ClearSpeedMax);
                _clearVelocity[i] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                _clearSpin[i]  = Range(rng, 0.35f, 1f) * ClearSpin * (rng.NextDouble() < 0.5 ? -1f : 1f);
                _clearAngle[i] = Range(rng, 0f, 360f);
            }
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private RectTransform MakePiece(string name, RectTransform parent, Sprite sprite, float size, Color tint, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = star.anchorMin;
            rt.anchorMax = star.anchorMax;
            rt.pivot = star.pivot;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = _playerBase.Length > 0 ? _playerBase[_playerBase.Length - 1] : Vector2.zero;
            rt.localScale = Vector3.one;

            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            tint.a = 0f;
            image.color = tint;
            return rt;
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
            EvaluateClearBurst(t);
        }

        /// <summary>
        /// Phrase-clear burst on the last tap (BongoBeatFlash.FlashPhraseClear): pulse 1→2.6× over 0.4 s
        /// (OutCubic) fading from 0.55 (InQuad); pieces pop 0.4→1 over 0.07 s, fly a closed-form ballistic
        /// arc, spin, and fade over the last 45 % of a 0.75 s life.
        /// </summary>
        private void EvaluateClearBurst(float t)
        {
            if (_clearPulse == null) return;

            int last = TapLeft.Length - 1;
            float age = t - (4f * Beat + last * Beat);
            Vector2 origin = _playerBase[last];

            if (age < 0f || age >= ClearPulseDuration) SetAlpha(_clearPulseImage, 0f);
            else
            {
                float p = age / ClearPulseDuration;
                _clearPulse.anchoredPosition = origin;
                SetScale(_clearPulse, Mathf.Lerp(1f, ClearPulseEndScale, OutCubic(p)));
                SetAlpha(_clearPulseImage, ClearPulseAlpha * (1f - InQuad(p)));
            }

            bool live = age >= 0f && age < ClearLife;
            float fadeStart = ClearLife * (1f - ClearFadeFraction);
            float scale = live ? Mathf.Lerp(ClearPopScale, 1f, OutCubic(Mathf.Clamp01(age / ClearPopDuration))) : 0f;
            float alpha = !live ? 0f : (age < fadeStart ? 1f : 1f - InQuad((age - fadeStart) / (ClearLife - fadeStart)));

            for (int i = 0; i < _clearPieces.Length; i++)
            {
                if (_clearPieces[i] == null) continue;
                SetAlpha(_clearPieceImages[i], alpha);
                if (!live) continue;
                _clearPieces[i].anchoredPosition = origin + _clearVelocity[i] * age + Vector2.down * (0.5f * ClearGravity * age * age);
                _clearPieces[i].localRotation = Quaternion.Euler(0f, 0f, _clearAngle[i] + _clearSpin[i] * age);
                SetScale(_clearPieces[i], scale);
            }
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
