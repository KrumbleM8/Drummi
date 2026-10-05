using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Tiny-planet menu navigation: this component lives on the planet pivot
    /// (a disc at the bottom of the portrait screen) and spins it so signposts
    /// crest over the horizon. Vertical drag anywhere spins the planet; on
    /// release the projected stop angle is snapped to the nearest sign and
    /// tweened there with DOTween. Tapping a distant sign calls FocusOn to
    /// spin it into focus (driven by FlashCardsMenuController).
    ///
    /// Input mirrors CarouselController's proven pattern: global InputActions
    /// on &lt;Pointer&gt;/press|delta|position (covers mouse in editor — touch
    /// simulation is unreliable), with drag-promotion + a short grace window
    /// (SuppressChildTap) so sign buttons don't fire on drag release.
    ///
    /// Rotation is applied only when the angle changes — the component does no
    /// per-frame work at rest and allocates nothing while spinning.
    ///
    /// INSPECTOR SETUP: signMounts — one child mount per sign, in index order
    /// (index 0 = start focus). Use the ArrangeSigns context menu to lay the
    /// mounts out along the rim at anglePerSign spacing.
    /// </summary>
    public class PlanetSpinner : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Sign mount transforms around the rim, in focus order (0 = top at start).")]
        [SerializeField] private List<Transform> signMounts = new();

        [Tooltip("Degrees between adjacent signs along the rim.")]
        [SerializeField, Min(1f)] private float anglePerSign = 35f;

        [Tooltip("Distance from the pivot centre to each sign mount (ArrangeSigns).")]
        [SerializeField, Min(0.01f)] private float radius = 6f;

        [Tooltip("Counter-rotate mounts so signs stay screen-upright; off = signs stand radially on the surface.")]
        [SerializeField] private bool keepSignsUpright = true;

        [Header("Feel")]
        [Tooltip("Drag gain — 1 means a quarter-screen vertical swipe moves one sign.")]
        [SerializeField, Min(0.1f)] private float dragSensitivity = 1f;

        [Tooltip("Exponential decay rate of release velocity (higher = shorter glide).")]
        [SerializeField, Min(0.1f)] private float deceleration = 5f;

        [Tooltip("Duration of the settle/focus tween.")]
        [SerializeField, Min(0.01f)] private float snapDuration = 0.35f;

        [Tooltip("Pixels the pointer must move before the gesture counts as a drag (tap otherwise).")]
        [SerializeField, Min(1f)] private float dragThreshold = 10f;

        /// <summary>Raised whenever the planet's angle changes (parallax layers listen).</summary>
        public event Action<float> AngleChanged;

        /// <summary>Raised when the nearest-sign index changes while spinning.</summary>
        public event Action<int> FocusedIndexChanged;

        /// <summary>
        /// True while a gesture has been promoted to a drag, plus a short grace
        /// window after release — sign buttons must not treat it as a tap.
        /// </summary>
        public bool SuppressChildTap => _promotedToDrag || Time.unscaledTime < _suppressTapUntil;

        /// <summary>Current planet angle in degrees (0 = sign 0 focused).</summary>
        public float CurrentAngle => _currentAngle;

        /// <summary>Index of the sign nearest the focus point.</summary>
        public int FocusedIndex => NearestIndex(_currentAngle);

        private float _currentAngle;
        private int   _focusedIndex;
        private float _maxAngle;

        // Gesture state
        private bool    _isDragging;
        private bool    _promotedToDrag;
        private float   _suppressTapUntil;
        private Vector2 _pointerDownPos;
        private float   _angularVelocity;   // deg/s, smoothed

        // InputActions (CarouselController pattern — new Input System).
        private InputAction _pressAction;
        private InputAction _deltaAction;
        private InputAction _positionAction;

        private Tween _snapTween;

        private void Awake()
        {
            _maxAngle = Mathf.Max(0, signMounts.Count - 1) * anglePerSign;

            _pressAction    = new InputAction("PlanetPress",    binding: "<Pointer>/press");
            _deltaAction    = new InputAction("PlanetDelta",    binding: "<Pointer>/delta");
            _positionAction = new InputAction("PlanetPosition", binding: "<Pointer>/position");

            _pressAction.started  += OnPointerDown;
            _pressAction.canceled += OnPointerUp;
            _deltaAction.performed += OnDragPerformed;
        }

        private void OnEnable()
        {
            _pressAction.Enable();
            _deltaAction.Enable();
            _positionAction.Enable();

            // Re-apply after SetMenuShown off/on toggles so signs + listeners re-sync.
            ApplyAngle(_currentAngle, forceEvents: true);
        }

        private void OnDisable()
        {
            _pressAction.Disable();
            _deltaAction.Disable();
            _positionAction.Disable();
            _isDragging = false;
            _promotedToDrag = false;
            _angularVelocity = 0f;
        }

        private void OnDestroy()
        {
            _snapTween?.Kill();
            _pressAction?.Dispose();
            _deltaAction?.Dispose();
            _positionAction?.Dispose();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Tween the planet so the sign at index is focused (interruptible).</summary>
        public void FocusOn(int index)
        {
            KillSnapTween();
            TweenToAngle(Mathf.Clamp(index, 0, signMounts.Count - 1) * anglePerSign);
        }

        /// <summary>Instantly set the planet so the sign at index is focused (menu restore).</summary>
        public void SnapTo(int index)
        {
            KillSnapTween();
            _angularVelocity = 0f;
            ApplyAngle(Mathf.Clamp(index, 0, signMounts.Count - 1) * anglePerSign, forceEvents: true);
        }

        /// <summary>
        /// Gate pointer input (same contract as CarouselController.SetInputEnabled).
        /// Disabling also stops any in-flight glide so the planet is still under
        /// the launch cover; state is cleaned so inertia can't resume on enable.
        /// </summary>
        public void SetInputEnabled(bool inputEnabled)
        {
            if (inputEnabled)
            {
                _pressAction.Enable();
                _deltaAction.Enable();
                _positionAction.Enable();
            }
            else
            {
                KillSnapTween();
                _pressAction.Disable();
                _deltaAction.Disable();
                _positionAction.Disable();
                _isDragging = false;
                _promotedToDrag = false;
                _angularVelocity = 0f;
            }
        }

        // ── Input handlers ────────────────────────────────────────────────────

        private void OnPointerDown(InputAction.CallbackContext context)
        {
            // Interruptible: grab the planet mid-tween from wherever it is.
            KillSnapTween();

            _isDragging = true;
            _promotedToDrag = false;
            _suppressTapUntil = 0f;
            _angularVelocity = 0f;
            _pointerDownPos = _positionAction.ReadValue<Vector2>();
        }

        private void OnPointerUp(InputAction.CallbackContext context)
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (_promotedToDrag)
                _suppressTapUntil = Time.unscaledTime + 0.05f;
            _promotedToDrag = false;

            // Project where the glide would stop (∫ v·e^(−kt) dt = v/k), then
            // settle on the nearest sign. Zero-velocity releases still snap.
            float projected = _currentAngle + _angularVelocity / deceleration;
            _angularVelocity = 0f;
            TweenToAngle(NearestIndex(projected) * anglePerSign);
        }

        private void OnDragPerformed(InputAction.CallbackContext context)
        {
            if (!_isDragging) return;

            Vector2 delta = context.ReadValue<Vector2>();

            // Vertical drag spins the planet: dragging down pulls the next sign
            // up over the horizon. Quarter-screen swipe ≈ one sign (resolution
            // independent).
            float angleDelta = -delta.y * dragSensitivity * anglePerSign / (Screen.height * 0.25f);
            ApplyAngle(Mathf.Clamp(_currentAngle + angleDelta, 0f, _maxAngle));

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _angularVelocity = Mathf.Lerp(_angularVelocity, angleDelta / dt, 0.5f);

            if (!_promotedToDrag)
            {
                Vector2 posNow = _positionAction.ReadValue<Vector2>();
                if ((posNow - _pointerDownPos).sqrMagnitude >= dragThreshold * dragThreshold)
                    _promotedToDrag = true;
            }
        }

        // ── Rotation ──────────────────────────────────────────────────────────

        private void TweenToAngle(float target)
        {
            target = Mathf.Clamp(target, 0f, _maxAngle);
            _snapTween = DOTween.To(() => _currentAngle, a => ApplyAngle(a), target, snapDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void KillSnapTween()
        {
            _snapTween?.Kill();
            _snapTween = null;
        }

        /// <summary>
        /// Single write point for rotation. Pivot and sign counter-rotation are
        /// applied in the same call (never across frames), then events fire.
        /// </summary>
        private void ApplyAngle(float angle, bool forceEvents = false)
        {
            if (!forceEvents && Mathf.Approximately(angle, _currentAngle)) return;
            _currentAngle = angle;

            transform.localEulerAngles = new Vector3(0f, 0f, _currentAngle);

            if (keepSignsUpright)
            {
                Quaternion counter = Quaternion.Euler(0f, 0f, -_currentAngle);
                for (int i = 0; i < signMounts.Count; i++)
                    if (signMounts[i] != null) signMounts[i].localRotation = counter;
            }

            AngleChanged?.Invoke(_currentAngle);

            int nearest = NearestIndex(_currentAngle);
            if (forceEvents || nearest != _focusedIndex)
            {
                _focusedIndex = nearest;
                FocusedIndexChanged?.Invoke(_focusedIndex);
            }
        }

        private int NearestIndex(float angle) =>
            Mathf.Clamp(Mathf.RoundToInt(angle / anglePerSign), 0, Mathf.Max(0, signMounts.Count - 1));

        // ── Authoring ─────────────────────────────────────────────────────────

        /// <summary>
        /// Editor helper: place mount i at radius·(sin(i·a), cos(i·a)) — sign 0
        /// at the top of the disc, later signs clockwise below the horizon.
        /// </summary>
        [ContextMenu("Arrange Signs Along Rim")]
        private void ArrangeSigns()
        {
            for (int i = 0; i < signMounts.Count; i++)
            {
                if (signMounts[i] == null) continue;
                float rad = i * anglePerSign * Mathf.Deg2Rad;
                signMounts[i].localPosition = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f) * radius;
                signMounts[i].localRotation = keepSignsUpright
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, -i * anglePerSign);
            }
        }
    }
}
