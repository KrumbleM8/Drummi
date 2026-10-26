using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Drop-in "bop to the menu music" for anything in the Home scene: signs,
    /// bushes, the on-globe mountain, the big parallax mountain, future decor.
    ///
    /// Continuous, phase-driven: every frame the scale is a pure function of
    /// the beat position (MenuBeatClock.Instance.BeatPosition), so the object
    /// is always in motion — peak stretch on the beat, relaxing through the
    /// beat, a short anticipation rise into the next one. Nothing to trigger,
    /// nothing to catch up: it moves from the first frame of the scene,
    /// including the title screen, and before the music's first beat the
    /// clock extrapolates the grid backwards. With no clock at all (component
    /// missing / disabled) it free-runs at fallbackBpm so the menu never
    /// looks dead.
    ///
    /// Scale is the ONLY thing written (localScale, every frame while
    /// enabled), so it coexists with TreeAnimationEffect (position/rotation
    /// shake) on the bushes and with HomeIntroDirector moving MountainMid.
    /// Anything else that wants to scale the same transform must go through
    /// ScaleMultiplier instead of writing localScale — MenuSignNode routes its
    /// focus pop through it (same pattern as DrumPadStateVisual →
    /// BounceOnBeat.ScaleMultiplier).
    ///
    /// Beat selection: everyNBeats / beatOffset pick the bounce period (e.g.
    /// 2 / 0 = one slow bounce per two beats from the downbeat), so a scene
    /// can be layered rather than having everything jump in unison.
    /// </summary>
    public class MenuBeatBopper : MonoBehaviour
    {
        [Header("Bounce")]
        [Tooltip("Y scale (relative to rest) at the peak of each bounce. Decor wants 1.04–1.12.")]
        [SerializeField, Min(0f)] private float stretchY = 1.08f;

        [Tooltip("Fraction of the Y growth taken off X at the peak (0 = pure stretch, 0.5 = cartoon squash-and-stretch).")]
        [SerializeField, Range(0f, 1f)] private float squashX = 0.35f;

        [Tooltip("How fast the stretch relaxes after the beat: 1 = linear sag to the next beat, 2–3 = quick drop then a long gentle settle.")]
        [SerializeField, Range(0.5f, 6f)] private float relaxPower = 2.2f;

        [Tooltip("Fraction of the period spent rising into the next peak (anticipation). 0 = hard snap on the beat.")]
        [SerializeField, Range(0f, 0.5f)] private float anticipation = 0.12f;

        [Header("Which Beats")]
        [Tooltip("One bounce every N beats (1 = every beat).")]
        [SerializeField, Min(1)] private int everyNBeats = 1;

        [Tooltip("Which beat within the group carries the peak (0 = the group's downbeat).")]
        [SerializeField, Min(0)] private int beatOffset = 0;

        [Header("Fallback")]
        [Tooltip("Tempo used when no MenuBeatClock is active, so the bounce still runs.")]
        [SerializeField, Min(1f)] private float fallbackBpm = 110f;

        /// <summary>
        /// Runtime-only uniform multiplier for other systems that need to scale
        /// this transform (sign focus pop). Composed into the final write so
        /// neither side ever overwrites the other. 1 = no extra scaling.
        /// </summary>
        [System.NonSerialized] public float ScaleMultiplier = 1f;

        /// <summary>Rest scale captured in Awake (before anything animates it).</summary>
        public Vector3 BaseScale => _baseScale;

        /// <summary>Current bounce amount, 1 at the peak, 0 fully relaxed (read-only, for debugging).</summary>
        public float Pulse { get; private set; }

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            // Leave the transform exactly at rest so a hidden object never
            // keeps a half-bounce (the sign focus pop is preserved).
            transform.localScale = _baseScale * ScaleMultiplier;
        }

        private void Update()
        {
            Pulse = Shape(PeriodPhase());

            float growY = (stretchY - 1f) * Pulse;
            float x = 1f - growY * squashX;
            float y = 1f + growY;
            transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z) * ScaleMultiplier;
        }

        /// <summary>0..1 position within this bopper's bounce period (0 = the peak beat).</summary>
        private float PeriodPhase()
        {
            int n = Mathf.Max(1, everyNBeats);
            MenuBeatClock clock = MenuBeatClock.Instance;
            double beats = clock != null
                ? clock.BeatPosition
                : Time.time * fallbackBpm / 60.0;
            double p = (beats - beatOffset) / n;
            p -= System.Math.Floor(p);
            return (float)p;
        }

        /// <summary>
        /// Bounce envelope over one period: 1 on the beat, relaxing toward 0
        /// with relaxPower, then an eased rise over the last `anticipation`
        /// of the period back up to the next peak. Continuous everywhere, so
        /// the object never sits still.
        /// </summary>
        private float Shape(float phase)
        {
            float relaxSpan = 1f - anticipation;
            if (phase < relaxSpan || relaxSpan <= 0f)
            {
                float t = relaxSpan > 0f ? phase / relaxSpan : 1f;
                return Mathf.Pow(1f - t, relaxPower);
            }
            float u = (phase - relaxSpan) / anticipation;
            return u * u; // ease-in to the hit
        }
    }
}
