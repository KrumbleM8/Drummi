using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Lifecycle phase of a <see cref="CRIndicator"/>.
    /// </summary>
    public enum CRIndicatorState
    {
        /// <summary>Waiting for its launch time (launchStartVirtual) to arrive.</summary>
        Idle,
        /// <summary>Flying from enemy origin up to its randomised hang position.</summary>
        Launching,
        /// <summary>Hovering at hang position until the attack window opens.</summary>
        Hanging,
        /// <summary>Flying from hang position toward the drum — response window active.</summary>
        Attacking,
        /// <summary>Hit-resolve animation playing (scale-pop on successful echo).</summary>
        ResolvingHit,
        /// <summary>Miss animation playing (fly-through and fade on missed note).</summary>
        ResolvingMiss,
        /// <summary>Animation complete — CRIndicatorController detects this and returns to pool.</summary>
        Done,
    }

    /// <summary>
    /// Pooled visual component for a single Call-and-Response phrase note.
    ///
    /// State machine (Idle → Launching → Hanging → Attacking → ResolvingHit/ResolvingMiss → Done):
    /// All launch and attack animations are DSP-driven — position and scale are computed each
    /// frame as a pure function of GameClock.GameTime relative to scheduled virtual times.
    /// Frame rate and tempo have no effect on the screen state at any given DSP time.
    ///
    /// Critical guarantee: at t=1.0 of the attack (exactly at hitVirtualTime),
    ///   transform.position == landingPosition  and  localScale == Vector3.one * endScale.
    ///
    /// The controller calls <see cref="Init"/> once per note, then the state machine runs
    /// automatically in Update(). Transitions to Done when resolve animation finishes;
    /// CRIndicatorController detects Done in its own Update and returns the object to the pool.
    ///
    /// SETUP: requires a SpriteRenderer on the same GameObject (use indicatorPrefab or the
    /// auto-generated fallback in CRIndicatorController).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CRIndicator : MonoBehaviour
    {
        // ── Init data ────────────────────────────────────────────────────────

        private double         _launchStartVirtual;
        private float          _launchDuration;
        private AnimationCurve _launchCurve;
        private Vector3        _spawnPosition;
        private Vector3        _hangPosition;
        private double         _hitVirtualTime;
        private double         _attackStartVirtual;
        private float          _attackDuration;
        private AnimationCurve _attackCurve;
        private float          _startScale;
        private float          _endScale;
        private Vector3        _landingPosition;
        private bool           _preAttackCueEnabled;
        private double         _preCueVirtualTime;
        private bool           _hangIdleEnabled;
        private float          _hangIdleAmplitude;
        private float          _hangIdleSpeed;
        private Color          _baseColor;

        // ── Reveal state ─────────────────────────────────────────────────────

        // Target scale for the reveal pop-in, cached from _startScale at Init() time.
        // Used to restore the correct scale when a pooled indicator is reused.
        private Vector3 _revealTargetScale;

        // Live reveal tween — killed on ResetForPool / Diffuse / TriggerMiss so a
        // pooled indicator never carries a mid-tween or zeroed scale into the next phrase.
        private Tween _revealTween;

        // Duration of the scale-up pop on reveal. OutBack overshoot reads as a snappy hit.
        private const float REVEAL_DURATION = 0.15f;

        // ── Resolve state ────────────────────────────────────────────────────

        private double  _resolveStartVirtual;
        private Vector3 _missStartPosition; // captured at TriggerMiss() time

        // Cosmetic resolve durations — not gameplay-critical, not exposed as tunables.
        private const float HIT_RESOLVE_DURATION  = 0.18f;
        private const float MISS_RESOLVE_DURATION = 0.45f;
        // World-unit distance the miss animation flies past the landing point.
        private const float MISS_OVERSHOOT = 1.5f;

        // ── Components ───────────────────────────────────────────────────────

        private SpriteRenderer _sr;

        // ── Public state ─────────────────────────────────────────────────────

        /// <summary>Current lifecycle phase — read by CRIndicatorController.</summary>
        public CRIndicatorState State { get; private set; } = CRIndicatorState.Idle;

        /// <summary>
        /// Virtual time at which this indicator's echo note must be hit
        /// (= callBeat.scheduledTime + N * beatInterval).
        /// </summary>
        public double HitVirtualTime => _hitVirtualTime;

        /// <summary>True while this indicator is in the pool and not in active use.</summary>
        public bool InPool { get; set; }

        // ── Setup data struct ─────────────────────────────────────────────────

        /// <summary>All per-note configuration passed to <see cref="Init"/>.</summary>
        public struct InitData
        {
            /// <summary>Virtual time the launch begins — equals the call beat's scheduledTime.</summary>
            public double launchStartVirtual;
            /// <summary>World-space spawn position (enemy origin). Launch starts here.</summary>
            public Vector3 spawnPosition;
            /// <summary>World-space hang position (randomised above enemy). Set by CRIndicatorController.</summary>
            public Vector3 hangPosition;
            /// <summary>World-space attack landing point (center drum button). Attack ends here.</summary>
            public Vector3 landingPosition;
            /// <summary>
            /// Virtual time the player must hit this note (= callBeat.scheduledTime + N × beatInterval).
            /// At this time position == landingPosition and scale == endScale.
            /// </summary>
            public double hitVirtualTime;
            /// <summary>Launch flight duration in seconds.</summary>
            public float launchDuration;
            /// <summary>AnimationCurve evaluating launch position (0→1). May be null (falls back to linear).</summary>
            public AnimationCurve launchCurve;
            /// <summary>Attack flight duration in seconds. Attack starts at hitVirtualTime − attackDuration.</summary>
            public float attackDuration;
            /// <summary>AnimationCurve evaluating attack position AND scale (0→1). May be null (linear).</summary>
            public AnimationCurve attackCurve;
            /// <summary>Indicator scale during hang phase and at the start of the attack.</summary>
            public float startScale;
            /// <summary>Indicator scale at the drum landing point — fixed learnable size.</summary>
            public float endScale;
            /// <summary>When true, the indicator pulses during the pre-attack window.</summary>
            public bool preAttackCueEnabled;
            /// <summary>Virtual time the pre-attack pulse begins (= attackStart − leadBeats × beatInterval).</summary>
            public double preCueVirtualTime;
            /// <summary>When true, indicator bobs gently while hanging.</summary>
            public bool hangIdleEnabled;
            /// <summary>World-unit amplitude of the idle bob.</summary>
            public float hangIdleAmplitude;
            /// <summary>Cycles-per-second of the idle bob.</summary>
            public float hangIdleSpeed;
            /// <summary>Base color applied to the SpriteRenderer.</summary>
            public Color color;
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (InPool || State == CRIndicatorState.Done) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;

            switch (State)
            {
                case CRIndicatorState.Idle:          CheckLaunch(now);       break;
                case CRIndicatorState.Launching:     UpdateLaunch(now);      break;
                case CRIndicatorState.Hanging:       UpdateHang(now);        break;
                case CRIndicatorState.Attacking:     UpdateAttack(now);      break;
                case CRIndicatorState.ResolvingHit:  UpdateResolveHit(now);  break;
                case CRIndicatorState.ResolvingMiss: UpdateResolveMiss(now); break;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Configures this indicator for a new note. Call immediately after pulling from the pool.
        /// Resets state to Idle and positions the object at the spawn point.
        /// </summary>
        public void Init(in InitData d)
        {
            _launchStartVirtual  = d.launchStartVirtual;
            _spawnPosition       = d.spawnPosition;
            _hangPosition        = d.hangPosition;
            _landingPosition     = d.landingPosition;
            _hitVirtualTime      = d.hitVirtualTime;
            _launchDuration      = Mathf.Max(d.launchDuration, 0.01f);
            _launchCurve         = d.launchCurve;
            _attackDuration      = Mathf.Max(d.attackDuration, 0.01f);
            _attackStartVirtual  = d.hitVirtualTime - (double)d.attackDuration;
            _attackCurve         = d.attackCurve;
            _startScale          = d.startScale;
            _endScale            = d.endScale;
            _preAttackCueEnabled = d.preAttackCueEnabled;
            _preCueVirtualTime   = d.preCueVirtualTime;
            _hangIdleEnabled     = d.hangIdleEnabled;
            _hangIdleAmplitude   = d.hangIdleAmplitude;
            _hangIdleSpeed       = d.hangIdleSpeed;
            _baseColor           = d.color;

            // Cache the target scale before zeroing — must survive pool reuse.
            _revealTargetScale = Vector3.one * _startScale;

            if (_sr != null)
            {
                _sr.color   = d.color;
                // Start hidden; CheckLaunch enables the renderer when the beat arrives.
                _sr.enabled = false;
            }

            State                = CRIndicatorState.Idle;
            transform.position   = _spawnPosition;
            // Start at scale zero; the reveal tween in CheckLaunch animates to _revealTargetScale.
            transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// Triggers the hit-resolve animation (scale-pop).
        /// Called by CRIndicatorController when the player successfully echoes this note.
        /// Safe to call in any state; no-ops if already resolving or done.
        /// </summary>
        public void Diffuse()
        {
            if (State == CRIndicatorState.ResolvingHit
             || State == CRIndicatorState.ResolvingMiss
             || State == CRIndicatorState.Done)
                return;

            // Kill reveal tween before entering resolve so its scale interpolation doesn't
            // conflict with UpdateResolveHit. Snap to _revealTargetScale so the hit-resolve
            // pop starts from the correct base size regardless of how early the echo arrived.
            _revealTween?.Kill(complete: false);
            _revealTween = null;
            transform.localScale = _revealTargetScale;

            _resolveStartVirtual = GameClock.Instance != null ? GameClock.Instance.GameTime : 0.0;
            State = CRIndicatorState.ResolvingHit;
        }

        /// <summary>
        /// Triggers the miss animation (fly-through and fade).
        /// Called by CRIndicatorController when this note's hit window has passed without a hit.
        /// Safe to call in any state; no-ops if already resolving or done.
        /// </summary>
        public void TriggerMiss()
        {
            if (State == CRIndicatorState.ResolvingHit
             || State == CRIndicatorState.ResolvingMiss
             || State == CRIndicatorState.Done)
                return;

            // Kill reveal tween so its scale interpolation doesn't conflict with the
            // miss fly-through. Ensure the renderer is visible even if the beat had not
            // yet revealed (an indicator can miss before its beat if it never received a hit).
            _revealTween?.Kill(complete: false);
            _revealTween = null;
            if (_sr != null) _sr.enabled = true;
            if (transform.localScale == Vector3.zero)
                transform.localScale = _revealTargetScale;

            _missStartPosition   = transform.position;
            _resolveStartVirtual = GameClock.Instance != null ? GameClock.Instance.GameTime : 0.0;
            State = CRIndicatorState.ResolvingMiss;
        }

        /// <summary>
        /// Immediately hides the indicator and resets it for return to the pool.
        /// Called by CRIndicatorController — do not call from other contexts.
        /// </summary>
        public void ResetForPool()
        {
            // Kill any in-flight reveal tween before resetting scale so a pooled indicator
            // never enters the next phrase with a live tween or a zeroed/oversized scale.
            _revealTween?.Kill(complete: false);
            _revealTween = null;

            State = CRIndicatorState.Idle;
            if (_sr != null)
            {
                Color c = _baseColor;
                c.a       = 1f;
                _sr.color   = c;
                _sr.enabled = false;
            }
            transform.localScale = Vector3.one;
        }

        // ── Private — State Updates ───────────────────────────────────────────

        private void CheckLaunch(double now)
        {
            if (now < _launchStartVirtual) return;

            // Reveal: enable the sprite and pop in with a scale tween synchronised to
            // this beat's scheduled virtual time. DOTween runs in LateUpdate, so it wins
            // over the UpdateLaunch scale-set (which runs in Update) during the 0.15 s
            // overlap, then hands off cleanly once the tween completes.
            if (_sr != null) _sr.enabled = true;
            _revealTween?.Kill(complete: false);
            _revealTween = transform
                .DOScale(_revealTargetScale, REVEAL_DURATION)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);

            State = CRIndicatorState.Launching;
            UpdateLaunch(now); // catch up immediately on the first frame
        }

        private void UpdateLaunch(double now)
        {
            double elapsed = now - _launchStartVirtual;
            float  t       = Mathf.Clamp01((float)(elapsed / _launchDuration));
            float  curveT  = _launchCurve != null ? _launchCurve.Evaluate(t) : t;

            transform.position   = Vector3.Lerp(_spawnPosition, _hangPosition, curveT);
            transform.localScale = Vector3.one * _startScale;

            if (t >= 1f)
            {
                transform.position = _hangPosition;
                State              = CRIndicatorState.Hanging;
            }
        }

        private void UpdateHang(double now)
        {
            Vector3 pos = _hangPosition;
            float   scl = _startScale;

            if (_hangIdleEnabled)
            {
                // Subtle bob — cosmetic flavour only; must not be mistaken for a timing cue.
                float bob = Mathf.Sin((float)now * _hangIdleSpeed) * _hangIdleAmplitude;
                pos.y += bob;
            }

            if (_preAttackCueEnabled && now >= _preCueVirtualTime && now < _attackStartVirtual)
            {
                // Gentle continuous pulse to alert the player this note is about to attack.
                // Non-rhythmic (10 Hz) so it does not carry beat information.
                float pulse = 1f + 0.18f * (0.5f + 0.5f * Mathf.Sin((float)now * 10f));
                scl *= pulse;
            }

            transform.position   = pos;
            transform.localScale = Vector3.one * scl;

            if (now >= _attackStartVirtual)
                State = CRIndicatorState.Attacking;
        }

        private void UpdateAttack(double now)
        {
            double elapsed = now - _attackStartVirtual;
            float  t       = Mathf.Clamp01((float)(elapsed / _attackDuration));
            float  curveT  = _attackCurve != null ? _attackCurve.Evaluate(t) : t;

            // Both position and scale driven by the same curve so acceleration reads consistently.
            transform.position   = Vector3.Lerp(_hangPosition, _landingPosition, curveT);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, curveT);
            // At t == 1.0 (hitVirtualTime): position == landingPosition, scale == endScale. Guaranteed.
        }

        private void UpdateResolveHit(double now)
        {
            double elapsed = now - _resolveStartVirtual;
            float  t       = Mathf.Clamp01((float)(elapsed / HIT_RESOLVE_DURATION));

            // Scale-pop: grow to 1.4× then collapse to 0.
            float scl;
            if (t < 0.3f)
                scl = Mathf.Lerp(_endScale, _endScale * 1.4f, t / 0.3f);
            else
                scl = Mathf.Lerp(_endScale * 1.4f, 0f,        (t - 0.3f) / 0.7f);

            transform.localScale = Vector3.one * scl;

            if (t >= 1f) MarkDone();
        }

        private void UpdateResolveMiss(double now)
        {
            double elapsed = now - _resolveStartVirtual;
            float  t       = Mathf.Clamp01((float)(elapsed / MISS_RESOLVE_DURATION));

            // Fly-through: continue in the attack direction past the landing point and fade out.
            // _missStartPosition was captured at TriggerMiss() time so this works whether
            // the indicator was hanging, mid-attack, or at the landing point.
            Vector3 attackDir = (_landingPosition - _hangPosition).normalized;
            Vector3 target    = _landingPosition + attackDir * MISS_OVERSHOOT;
            transform.position   = Vector3.Lerp(_missStartPosition, target, t);
            transform.localScale = Vector3.one * _endScale;

            if (_sr != null)
            {
                Color c = _baseColor;
                c.a       = 1f - t;
                _sr.color = c;
            }

            if (t >= 1f) MarkDone();
        }

        private void MarkDone()
        {
            State = CRIndicatorState.Done;
            if (_sr != null)
            {
                Color c = _baseColor;
                c.a       = 0f;
                _sr.color = c;
            }
        }
    }
}
