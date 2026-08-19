using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Persistent flick ball. State machine:
    ///   Idle (at home) → Held (press summons the ball to the finger; follows the
    ///   pointer until release) → Resolving (on release outcome: aim-driven flight or
    ///   fizzle drop) → AwaitingRespawn → Idle (reappears at home after a SHORT FIXED
    ///   delay — deliberately not beat-synced, players wind up the next throw well
    ///   before the next beat — polled from GameClock virtual time, never tween timing).
    ///
    /// A new press ALWAYS summons the ball — any in-flight animation is completed
    /// instantly (its OnComplete still fires) so the ball can never be invisible while
    /// the player is winding up a throw.
    ///
    /// Flight is fully aim-driven: the ball always travels along the thrown drag
    /// direction at a constant speed. A hit stops at the closest approach to the
    /// target's position sampled at release via FlickTargetSweep.PositionAt — the same
    /// f(GameTime) the aim check and grading use. DOTween here is cosmetic only
    /// (flight/fizzle/pop); nothing the evaluator reads derives from tween state. All
    /// tweens SetLink(gameObject); the active animation is tracked in _anim so it can
    /// be completed/killed explicitly (Sequences are not reachable via transform.DOKill).
    /// </summary>
    public class FlickBallController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("FlickBeatManager whose release outcomes drive the ball.")]
        [SerializeField] private FlickBeatManager beatManager;

        [Tooltip("Sweep sampled for the lock-on destination.")]
        [SerializeField] private FlickTargetSweep targetSweep;

        [Tooltip("Target visual to pop when a lock-on flight lands.")]
        [SerializeField] private FlickTargetVisual targetVisual;

        [Tooltip("World-space home position anchor.")]
        [SerializeField] private Transform homeAnchor;

        [Tooltip("FlickInputReader followed while the ball is held (press/drag visuals only).")]
        [SerializeField] private FlickInputReader flickReader;

        [Tooltip("Camera mapping the pointer to the ball's world plane. Falls back to Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Header("Flight")]
        [Tooltip("Constant flight speed in world units per second — used for both hit flights and air-balls.")]
        [SerializeField] private float flightSpeed = 34f;

        [Header("Throw Miss (air-ball)")]
        [Tooltip("World units an on-beat air-ball travels along the thrown direction before despawning.")]
        [SerializeField] private float throwMissDistance = 12f;

        [Header("Fizzle")]
        [Tooltip("World units of upward drift at the start of a fizzle.")]
        [SerializeField] private float fizzleRise = 1.2f;

        [Tooltip("Seconds for the upward drift.")]
        [SerializeField] private float fizzleRiseTime = 0.15f;

        [Tooltip("World units the ball falls off-screen after the drift.")]
        [SerializeField] private float fizzleFall = 9f;

        [Tooltip("Seconds for the fall.")]
        [SerializeField] private float fizzleFallTime = 0.35f;

        [Header("Respawn")]
        [Tooltip("Seconds (virtual time) between the ball despawning and reappearing at home. " +
                 "Kept short so the ball is back before the player winds up the next throw.")]
        [SerializeField] private float respawnDelaySeconds = 0.15f;

        private enum State { Idle, Held, Resolving, AwaitingRespawn }

        private State _state = State.Idle;
        private double _respawnAtVirtual;
        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        private float _planeZ;
        private Tween _anim; // Active flight/fizzle/pop — tracked so Sequences can be killed too.

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (homeAnchor != null) transform.position = homeAnchor.position;
            _planeZ = transform.position.z;
        }

        /// <summary>
        /// Dormancy hook (dungeon host): renderer-level show/hide, never SetActive —
        /// subscriptions and respawn polling stay alive (FlickTargetVisual convention).
        /// While the encounter is gated off (FlickInputReader.EncounterActive false)
        /// nothing re-enables the renderer, so a hidden ball stays hidden.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_sr != null) _sr.enabled = visible;
        }

        /// <summary>
        /// Host cleanup hook: hard-resets the ball to Idle at home from ANY state —
        /// including mid-Held or mid-flight. Kills the tracked animation without
        /// completing it (no OnComplete side effects), restores scale/visibility.
        /// </summary>
        public void ResetToHome()
        {
            if (_anim != null && _anim.IsActive()) _anim.Kill();
            _anim = null;

            transform.localScale = _baseScale;
            if (_sr != null) _sr.enabled = true;
            if (homeAnchor != null) transform.position = homeAnchor.position;
            _state = State.Idle;
            Debug.Log("[FlickBall] Reset to home (host)");
        }

        private void OnEnable()
        {
            if (beatManager != null) beatManager.OnReleaseResolved += HandleReleaseResolved;
            if (flickReader != null) flickReader.OnFlickBegan += HandleFlickBegan;
        }

        private void OnDisable()
        {
            if (beatManager != null) beatManager.OnReleaseResolved -= HandleReleaseResolved;
            if (flickReader != null) flickReader.OnFlickBegan -= HandleFlickBegan;
        }

        private void Update()
        {
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            if (_state == State.Held)
            {
                // Follow the pointer while held. On the release frame the outcome event
                // moves us to Resolving before/at this Update, so losing the pointer here
                // means the touch was CANCELED (no release event) — return home quietly.
                if (flickReader != null && flickReader.TryGetActivePointer(out Vector2 screenPos))
                {
                    transform.position = ScreenToBallPlane(screenPos);
                }
                else
                {
                    transform.position = homeAnchor != null ? homeAnchor.position : transform.position;
                    _state = State.Idle;
                }
                return;
            }

            if (_state != State.AwaitingRespawn) return;

            // Respawn after the short delay — polled from virtual time, not tween timing.
            if (GameClock.Instance.GameTime >= _respawnAtVirtual)
            {
                transform.position = homeAnchor != null ? homeAnchor.position : transform.position;
                transform.localScale = _baseScale;
                if (_sr != null) _sr.enabled = true;
                _state = State.Idle;

                // Cosmetic spawn pop.
                _anim = transform.DOPunchScale(_baseScale * 0.25f, 0.15f, vibrato: 1)
                                 .SetLink(gameObject);
                Debug.Log($"[FlickBall] Respawned at vt={_respawnAtVirtual:F4}");
            }
        }

        // A new press always summons the ball to the finger, whatever it was doing.
        private void HandleFlickBegan(Vector2 screenPos)
        {
            // Complete any in-flight animation instantly — its OnComplete still fires
            // (target pop / hide / respawn scheduling), then the summon overrides it.
            if (_anim != null && _anim.IsActive()) _anim.Kill(complete: true);
            _anim = null;

            transform.localScale = _baseScale;
            if (_sr != null) _sr.enabled = true;
            transform.position = ScreenToBallPlane(screenPos);
            _state = State.Held;
        }

        private Vector3 ScreenToBallPlane(Vector2 screenPos)
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null) return transform.position;

            Vector3 world = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, _planeZ - cam.transform.position.z));
            world.z = _planeZ;
            return world;
        }

        private void HandleReleaseResolved(FlickReleaseOutcome outcome)
        {
            if (_state == State.Resolving || _state == State.AwaitingRespawn)
            {
                Debug.Log($"[FlickBall] Outcome ignored — ball busy ({_state})");
                return;
            }

            _state = State.Resolving;
            if (_anim != null && _anim.IsActive()) _anim.Kill();
            _anim = null;

            if (outcome.FizzleCause == null)
                PlayHitFlight(outcome.Release.DragVector);
            else if (outcome.FizzleCause == FlickFizzleCause.BadAim)
                PlayThrowMiss(outcome.Release.DragVector);
            else
                PlayFizzle();
        }

        // Aim-driven hit: the ball flies along the thrown direction and stops at the
        // closest approach to the target's position at release — the same position the
        // aim check tested. Never auto-paths to the target.
        private void PlayHitFlight(Vector2 dragVector)
        {
            Vector3 dir = ((Vector2)dragVector.normalized);
            Vector3 targetWorld = targetSweep != null && GameClock.Instance != null
                ? targetSweep.PositionAt(GameClock.Instance.GameTime)
                : transform.position;

            float along = Mathf.Max(0f, Vector3.Dot(targetWorld - transform.position, dir));
            Vector3 impact = transform.position + dir * along;

            _anim = transform.DOMove(impact, along / flightSpeed)
                     .SetEase(Ease.Linear)
                     .SetLink(gameObject)
                     .OnComplete(() =>
                     {
                         if (targetVisual != null) targetVisual.HitPop();
                         if (_sr != null) _sr.enabled = false;
                         ScheduleRespawn();
                     });
        }

        // On-beat air-ball: the ball flies along the thrown direction and sails past.
        // Screen axes align with the axis-aligned ortho camera's world axes, so the
        // normalized drag maps directly to a world-space direction.
        private void PlayThrowMiss(Vector2 dragVector)
        {
            Vector3 dir = ((Vector2)dragVector.normalized);
            Vector3 dest = transform.position + dir * throwMissDistance;

            _anim = transform.DOMove(dest, throwMissDistance / flightSpeed)
                     .SetEase(Ease.Linear)
                     .SetLink(gameObject)
                     .OnComplete(() =>
                     {
                         if (_sr != null) _sr.enabled = false;
                         ScheduleRespawn();
                     });
        }

        private void PlayFizzle()
        {
            Vector3 p = transform.position;
            _anim = DOTween.Sequence()
                   .Append(transform.DOMoveY(p.y + fizzleRise, fizzleRiseTime).SetEase(Ease.OutQuad))
                   .Append(transform.DOMoveY(p.y - fizzleFall, fizzleFallTime).SetEase(Ease.InQuad))
                   .Join(transform.DOScale(_baseScale * 0.6f, fizzleFallTime))
                   .SetLink(gameObject)
                   .OnComplete(() =>
                   {
                       if (_sr != null) _sr.enabled = false;
                       ScheduleRespawn();
                   });
        }

        private void ScheduleRespawn()
        {
            _respawnAtVirtual = GameClock.Instance != null
                ? GameClock.Instance.GameTime + respawnDelaySeconds
                : 0;
            _state = State.AwaitingRespawn;
            Debug.Log($"[FlickBall] Respawn scheduled for vt={_respawnAtVirtual:F4}");
        }
    }
}
