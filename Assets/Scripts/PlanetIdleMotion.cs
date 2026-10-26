using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Gentle idle float + wobble for the planet while it hangs in the title
    /// background, plus a beat-synced bounce of the whole globe to the menu
    /// music. Lives on PlanetPivot next to PlanetSpinner; disabled in the
    /// scene and switched on only by HomeIntroDirector for the title state.
    ///
    /// LateUpdate recomposes the pivot's rotation every frame from the
    /// spinner's current roll (Euler(−angle,0,0), the same thing
    /// PlanetSpinner.ApplyAngle writes) times a small Y/Z wobble, so nothing
    /// accumulates and the roll tween can run underneath. Weight scales the
    /// slow float/wobble (1 = full idle, 0 = exactly the spinner's pose);
    /// BeatWeight scales the beat bounce separately so the director can let
    /// the bounce die away more slowly than the wobble as the camera flies
    /// in. When both are faded it calls Deactivate, which restores the base
    /// position/rotation/scale and disables the component so the menu has no
    /// per-frame cost. Horizon sorting in PlanetSpinner ignores the wobble
    /// (it reads CurrentAngle) — fine for a few degrees.
    ///
    /// BEAT BOUNCE: a bouncing-ball hop — the globe is on the "ground" on
    /// each beat and arcs up between beats (parabola, peak mid-period) —
    /// plus a tiny uniform scale pump that peaks on the beat so the impact
    /// reads. Position-based rather than a Y-stretch because the pivot is
    /// rolled: a local-axis stretch would squash the sphere along a tilted
    /// axis. Phase comes from MenuBeatClock via MenuBeatBopper's shared
    /// helpers, so it runs from the first frame like the decor.
    /// </summary>
    public class PlanetIdleMotion : MonoBehaviour
    {
        [SerializeField] private PlanetSpinner spinner;

        [Header("Float")]
        [Tooltip("World units of vertical bob.")]
        [SerializeField, Min(0f)] private float floatAmplitude = 0.8f;
        [SerializeField, Min(0.1f)] private float floatPeriod = 3.6f;

        [Header("Wobble")]
        [Tooltip("Degrees of side tilt (Z).")]
        [SerializeField, Min(0f)] private float tiltDegrees = 1.5f;
        [SerializeField, Min(0.1f)] private float tiltPeriod = 6.5f;
        [Tooltip("Degrees of slow twist (Y).")]
        [SerializeField, Min(0f)] private float twistDegrees = 1.2f;
        [SerializeField, Min(0.1f)] private float twistPeriod = 9f;

        [Header("Beat Bounce")]
        [Tooltip("World units the globe hops between beats (0 = off).")]
        [SerializeField, Min(0f)] private float hopHeight = 0.6f;

        [Tooltip("Uniform scale added at the beat impact (0.02 = 2 % pump), relaxing between beats.")]
        [SerializeField, Range(0f, 0.2f)] private float pumpScale = 0.02f;

        [Tooltip("One hop every N beats (1 = every beat).")]
        [SerializeField, Min(1)] private int everyNBeats = 1;

        [Tooltip("Which beat within the group carries the impact (0 = the group's downbeat).")]
        [SerializeField, Min(0)] private int beatOffset = 0;

        [Tooltip("How fast the pump relaxes after the impact (see MenuBeatBopper.relaxPower).")]
        [SerializeField, Range(0.5f, 6f)] private float pumpRelaxPower = 2.2f;

        [Tooltip("Tempo used when no MenuBeatClock is active.")]
        [SerializeField, Min(1f)] private float fallbackBpm = 110f;

        /// <summary>0..1 scale on the slow float/wobble (1 = full idle, 0 = spinner pose only).</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>0..1 scale on the beat hop/pump (faded separately by the director).</summary>
        public float BeatWeight { get; set; } = 1f;

        private Vector3 _basePos;
        private Vector3 _baseScale;
        private float   _t0;

        private void Awake()
        {
            _basePos   = transform.localPosition;
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _t0 = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime - _t0;
            float w = Mathf.Clamp01(Weight);
            float bw = Mathf.Clamp01(BeatWeight);
            const float tau = Mathf.PI * 2f;

            float bob   = Mathf.Sin(t * tau / floatPeriod) * floatAmplitude * w;
            float tilt  = Mathf.Sin(t * tau / tiltPeriod) * tiltDegrees * w;
            float twist = Mathf.Sin(t * tau / twistPeriod + 1.3f) * twistDegrees * w;

            // Beat bounce: ball-in-flight arc between beats, impact pump on the beat.
            float phase = MenuBeatBopper.PeriodPhase(everyNBeats, beatOffset, fallbackBpm);
            float hop   = 4f * phase * (1f - phase) * hopHeight * bw;
            float pump  = MenuBeatBopper.Envelope(phase, pumpRelaxPower, 0f) * pumpScale * bw;

            transform.localPosition = _basePos + Vector3.up * (bob + hop);
            transform.localRotation = Quaternion.Euler(-CurrentRoll(), 0f, 0f) * Quaternion.Euler(0f, twist, tilt);
            transform.localScale    = _baseScale * (1f + pump);
        }

        /// <summary>Snap back to the spinner's exact pose and stop running.</summary>
        public void Deactivate()
        {
            Weight = 0f;
            BeatWeight = 0f;
            transform.localPosition = _basePos;
            transform.localRotation = Quaternion.Euler(-CurrentRoll(), 0f, 0f);
            transform.localScale    = _baseScale;
            enabled = false;
        }

        private float CurrentRoll() => spinner != null ? spinner.CurrentAngle : 0f;
    }
}
