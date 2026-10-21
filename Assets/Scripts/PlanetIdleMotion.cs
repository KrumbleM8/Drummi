using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Gentle idle float + wobble for the planet while it hangs in the title
    /// background. Lives on PlanetPivot next to PlanetSpinner; disabled in the
    /// scene and switched on only by HomeIntroDirector for the title state.
    ///
    /// LateUpdate recomposes the pivot's rotation every frame from the
    /// spinner's current roll (Euler(−angle,0,0), the same thing
    /// PlanetSpinner.ApplyAngle writes) times a small Y/Z wobble, so nothing
    /// accumulates and the roll tween can run underneath. Weight scales both
    /// motions (1 = full idle, 0 = exactly the spinner's pose); the director
    /// fades it to 0 as the zoom begins and then calls Deactivate, which
    /// restores the base position/rotation and disables the component so the
    /// menu has no per-frame cost. Horizon sorting in PlanetSpinner ignores
    /// the wobble (it reads CurrentAngle) — fine for a few degrees.
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

        /// <summary>0..1 scale on both motions (1 = full idle, 0 = spinner pose only).</summary>
        public float Weight { get; set; } = 1f;

        private Vector3 _basePos;
        private float   _t0;

        private void Awake()
        {
            _basePos = transform.localPosition;
        }

        private void OnEnable()
        {
            _t0 = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime - _t0;
            float w = Mathf.Clamp01(Weight);
            const float tau = Mathf.PI * 2f;

            float bob   = Mathf.Sin(t * tau / floatPeriod) * floatAmplitude * w;
            float tilt  = Mathf.Sin(t * tau / tiltPeriod) * tiltDegrees * w;
            float twist = Mathf.Sin(t * tau / twistPeriod + 1.3f) * twistDegrees * w;

            transform.localPosition = _basePos + Vector3.up * bob;
            transform.localRotation = Quaternion.Euler(-CurrentRoll(), 0f, 0f) * Quaternion.Euler(0f, twist, tilt);
        }

        /// <summary>Snap back to the spinner's exact pose and stop running.</summary>
        public void Deactivate()
        {
            Weight = 0f;
            transform.localPosition = _basePos;
            transform.localRotation = Quaternion.Euler(-CurrentRoll(), 0f, 0f);
            enabled = false;
        }

        private float CurrentRoll() => spinner != null ? spinner.CurrentAngle : 0f;
    }
}
