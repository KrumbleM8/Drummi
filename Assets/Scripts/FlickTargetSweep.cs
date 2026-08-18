using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Deterministic left-right ping-pong sweep for the flick target.
    ///
    /// Position is a PURE FUNCTION of GameClock virtual time — no tween drives the
    /// sweep, so pause safety comes free from GameClock and grading, aim checks, and
    /// lock-on flights all sample identical math via <see cref="PositionAt"/>.
    ///
    /// Coordinate space: WORLD space (Transform anchors + SpriteRenderer visuals),
    /// matching the dungeon enemy visuals (DungeonVisualController positions enemies
    /// in world units). The aim cone converts the target's world position to screen
    /// space internally, since drags are measured in screen pixels.
    /// </summary>
    public class FlickTargetSweep : MonoBehaviour
    {
        [Header("Sweep")]
        [Tooltip("World-space anchor at the left extreme of the sweep.")]
        [SerializeField] private Transform leftAnchor;

        [Tooltip("World-space anchor at the right extreme of the sweep.")]
        [SerializeField] private Transform rightAnchor;

        [Tooltip("Beats per full crossing (left→right). Default 4 = one crossing per half bar.")]
        [SerializeField] private float beatsPerCrossing = 4f;

        [Header("Hitbox")]
        [Tooltip("Generous hitbox radius around the target, in world units. A throw whose ray passes within this radius counts as on-target.")]
        [SerializeField] private float hitboxRadiusWorld = 1.2f;

        [Tooltip("Camera used to project the target into screen space for hitbox checks. Falls back to Camera.main.")]
        [SerializeField] private Camera aimCamera;

        private double _sweepOriginTime;
        private double _secondsPerCrossing;
        private bool _running;

        // Authored (scene-serialized) values captured at Awake so per-room overrides
        // can be reverted by sentinel (see ApplyRoomOverrides).
        private float _authoredBeatsPerCrossing;
        private float _authoredHitboxRadius;

        private void Awake()
        {
            _authoredBeatsPerCrossing = beatsPerCrossing;
            _authoredHitboxRadius     = hitboxRadiusWorld;
        }

        /// <summary>
        /// Per-room tuning hook (dungeon host). Values &lt;= 0 restore the authored
        /// scene defaults, so hosts call this unconditionally each room. Call before
        /// StartSweep — the crossing duration is derived there.
        /// </summary>
        public void ApplyRoomOverrides(float newBeatsPerCrossing, float newHitboxRadiusWorld)
        {
            beatsPerCrossing  = newBeatsPerCrossing  > 0f ? newBeatsPerCrossing  : _authoredBeatsPerCrossing;
            hitboxRadiusWorld = newHitboxRadiusWorld > 0f ? newHitboxRadiusWorld : _authoredHitboxRadius;
            Debug.Log($"[FlickTargetSweep] Room overrides — beatsPerCrossing: {beatsPerCrossing}, " +
                      $"hitboxRadius: {hitboxRadiusWorld}wu");
        }

        /// <summary>
        /// Starts the sweep on the session's virtual-time grid.
        /// </summary>
        /// <param name="sweepOriginVirtual">Virtual time of phase 0 (target at the left anchor).</param>
        /// <param name="beatInterval">Seconds per beat (60 / bpm).</param>
        public void StartSweep(double sweepOriginVirtual, double beatInterval)
        {
            _sweepOriginTime = sweepOriginVirtual;
            _secondsPerCrossing = beatsPerCrossing * beatInterval;
            _running = true;
            Debug.Log($"[FlickTargetSweep] Started — origin vt={sweepOriginVirtual:F4}, " +
                      $"{beatsPerCrossing} beats/crossing = {_secondsPerCrossing:F3}s");
        }

        /// <summary>Stops the sweep (position freezes where it is).</summary>
        public void StopSweep() => _running = false;

        /// <summary>
        /// The target's world position at the given virtual time — the single source of
        /// truth sampled by the sweep itself, the aim cone, and lock-on flights.
        /// </summary>
        public Vector3 PositionAt(double virtualTime)
        {
            if (!_running || _secondsPerCrossing <= 0 || leftAnchor == null || rightAnchor == null)
                return transform.position;

            // Before the origin the sweep HOLDS at phase 0 (left anchor) — hosts may
            // start the sweep with a future origin (dungeon lead-in), and PingPong of a
            // negative phase would otherwise mirror into backwards motion.
            double elapsed = virtualTime - _sweepOriginTime;
            if (elapsed < 0) elapsed = 0;

            float phase = Mathf.PingPong((float)(elapsed / _secondsPerCrossing), 1f);
            return Vector3.Lerp(leftAnchor.position, rightAnchor.position, phase);
        }

        /// <summary>
        /// True when the throw's ray (from the flick origin along the drag direction)
        /// passes within the target's generous hitbox, sampled at the target's CURRENT
        /// position — what the player sees at release is what they aim at. Screen-space
        /// ray-vs-circle test; the world-space hitbox radius is projected to pixels.
        /// Logs the decision per call.
        /// </summary>
        /// <param name="dragVector">Flick vector in screen pixels (any direction is legal).</param>
        /// <param name="flickOrigin">Screen position where the measured flick motion began.</param>
        public bool IsThrowOnTarget(Vector2 dragVector, Vector2 flickOrigin)
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null || GameClock.Instance == null)
            {
                Debug.LogWarning("[FlickTargetSweep] No camera/clock for hitbox check — passing by default.");
                return true;
            }

            Vector3 targetWorld = PositionAt(GameClock.Instance.GameTime);
            Vector2 targetScreen = cam.WorldToScreenPoint(targetWorld);

            // Project the world-space radius into pixels at the target's position.
            Vector2 edgeScreen = cam.WorldToScreenPoint(targetWorld + cam.transform.right * hitboxRadiusWorld);
            float radiusPx = (edgeScreen - targetScreen).magnitude;

            Vector2 dragDir = dragVector.normalized;
            Vector2 toTarget = targetScreen - flickOrigin;

            // The target must be in front of the throw, not behind it.
            float along = Vector2.Dot(toTarget, dragDir);
            bool hit;
            float perpDistance;
            if (along <= 0f)
            {
                hit = false;
                perpDistance = toTarget.magnitude;
            }
            else
            {
                perpDistance = (toTarget - dragDir * along).magnitude;
                hit = perpDistance <= radiusPx;
            }

            Debug.Log($"[FlickTargetSweep] THROW {(hit ? "HIT" : "MISS")} — " +
                      $"perp distance {perpDistance:F0}px vs hitbox {radiusPx:F0}px " +
                      $"(r={hitboxRadiusWorld}wu), target screen={targetScreen}, origin={flickOrigin}" +
                      (along <= 0f ? " [target behind throw]" : ""));
            return hit;
        }

        private void Update()
        {
            if (!_running || GameClock.Instance == null) return;
            // BounceOnBeat-style polling: snap to f(GameTime) every frame.
            transform.position = PositionAt(GameClock.Instance.GameTime);
        }
    }
}
