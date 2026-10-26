using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Drop-in "bop to the menu music" for anything in the Home globe scene:
    /// signs, bushes, the on-globe mountain, future decor. Menu twin of the
    /// gameplay BounceOnBeat with the same feel — on every beat the Y scale
    /// snaps up to stretchY and SmoothDamps back to rest (smoothTime), with an
    /// optional sideways squash so the stretch reads as a bounce rather than a
    /// grow. Driven by MenuBeatClock.Instance (polled, no wiring); idles at the
    /// rest scale when no clock is active.
    ///
    /// Scale is the ONLY thing written (localScale, every frame while enabled),
    /// so it coexists with TreeAnimationEffect (position/rotation shake) on the
    /// bushes. Anything else that wants to scale the same transform must go
    /// through ScaleMultiplier instead of writing localScale — MenuSignNode
    /// routes its focus pop through it (same pattern as DrumPadStateVisual →
    /// BounceOnBeat.ScaleMultiplier).
    ///
    /// Beat selection: everyNBeats / beatOffset pick which beats bop (e.g.
    /// 2 / 1 = the off-beats), so a scene can be layered rather than having
    /// everything jump at once.
    /// </summary>
    public class MenuBeatBopper : MonoBehaviour
    {
        [Header("Bounce")]
        [Tooltip("Y scale (relative to rest) the object snaps to on the beat. BounceOnBeat pads use 1.3; decor wants 1.03–1.1.")]
        [SerializeField, Min(0f)] private float stretchY = 1.06f;

        [Tooltip("Fraction of the Y growth taken off X on the beat (0 = pure stretch, 0.5 = cartoon squash-and-stretch).")]
        [SerializeField, Range(0f, 1f)] private float squashX = 0.35f;

        [Tooltip("SmoothDamp time back to rest — BounceOnBeat uses 0.15.")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        [Header("Which Beats")]
        [Tooltip("Bop every N beats (1 = every beat).")]
        [SerializeField, Min(1)] private int everyNBeats = 1;

        [Tooltip("Which beat within the group (0 = the downbeat of each group).")]
        [SerializeField, Min(0)] private int beatOffset = 0;

        /// <summary>
        /// Runtime-only uniform multiplier for other systems that need to scale
        /// this transform (sign focus pop). Composed into the final write so
        /// neither side ever overwrites the other. 1 = no extra scaling.
        /// </summary>
        [System.NonSerialized] public float ScaleMultiplier = 1f;

        /// <summary>Rest scale captured in Awake (before anything animates it).</summary>
        public Vector3 BaseScale => _baseScale;

        private Vector3 _baseScale;
        private float   _pulse;          // 1 on the beat, SmoothDamps to 0
        private float   _pulseVelocity;
        private long    _lastBeatIndex = -1;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            // Fresh start: the clock is re-anchored whenever the menu comes back.
            _lastBeatIndex = -1;
            _pulse = 0f;
            _pulseVelocity = 0f;
        }

        private void OnDisable()
        {
            // Leave the transform exactly at rest so a disabled object never
            // keeps a half-bounce (and the sign focus pop still shows).
            transform.localScale = _baseScale * ScaleMultiplier;
        }

        private void Update()
        {
            DetectBeat();

            // SmoothDamp divides by deltaTime — skip the frame when time is frozen.
            if (Time.deltaTime > 0f)
                _pulse = Mathf.SmoothDamp(_pulse, 0f, ref _pulseVelocity, smoothTime);

            float growY = (stretchY - 1f) * _pulse;
            float x = 1f - growY * squashX;
            float y = 1f + growY;
            transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z) * ScaleMultiplier;
        }

        private void DetectBeat()
        {
            MenuBeatClock clock = MenuBeatClock.Instance;
            if (clock == null) return;

            long index = clock.BeatIndex;
            if (index < 0 || index <= _lastBeatIndex)
            {
                // Clock restarted (new loop anchor) — resync without a spurious bop.
                if (index < _lastBeatIndex) _lastBeatIndex = index;
                return;
            }

            // First poll after enabling mid-track: adopt the current beat
            // instead of firing for one that already passed while we were off
            // (index 0 is a real first beat — bop it).
            bool missedStart = _lastBeatIndex < 0 && index > 0;
            _lastBeatIndex = index;
            if (missedStart) return;

            int n = Mathf.Max(1, everyNBeats);
            if (index % n == beatOffset % n) Bop();
        }

        /// <summary>Trigger the bounce now (also handy for tap feedback).</summary>
        public void Bop()
        {
            _pulse = 1f;
            _pulseVelocity = 0f;
        }
    }
}
