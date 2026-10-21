using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Tiny-planet menu navigation: this component lives on the globe pivot
    /// (a 3D sphere viewed Mario-Galaxy style — static perspective camera just
    /// above the surface looking forward) and rolls it around the world X axis
    /// so signposts rise over the far horizon and travel toward the camera.
    /// Vertical drag anywhere spins the globe; on release the projected stop
    /// angle is snapped to the nearest sign and tweened there with DOTween.
    /// Tapping a distant sign calls FocusOn to roll it into focus (driven by
    /// FlashCardsMenuController).
    ///
    /// Sign mounts sit on the forward great circle (YZ plane) standing
    /// radially, so signs lean back over the horizon and straighten up as they
    /// arrive — no counter-rotation, mounts simply ride the globe.
    ///
    /// Input mirrors CarouselController's proven pattern: global InputActions
    /// on &lt;Pointer&gt;/press|delta|position (covers mouse in editor — touch
    /// simulation is unreliable), with drag-promotion + a short grace window
    /// (SuppressChildTap) so sign buttons don't fire on drag release.
    ///
    /// Rotation is applied only when the angle changes — the component does no
    /// per-frame work at rest and allocates nothing while spinning.
    ///
    /// HORIZON SORTING: the project uses URP's 2D renderer, where the sphere
    /// mesh gets no depth occlusion over sprites/canvases — far-side signs
    /// would bleed through the globe. So every pivot child (signs + decor) is
    /// painter-sorted against the globe: inside the visible arc → sorting
    /// order in front of the globe mesh, beyond it → behind (the globe's
    /// silhouette then clips it per-pixel, so signs still crest the horizon
    /// gradually). Flipping right at the ridge makes the swap invisible.
    /// With sortingCamera assigned the visible arc is derived from the camera
    /// each update (centre = camera's ring direction, half-width =
    /// acos(R/ρ)), so it stays correct while HomeIntroDirector flies the
    /// camera in from far away and for decor on the lower half of the globe;
    /// for the menu camera (0,13.5,−8) it is (−81°, +19.8°) — the same ridge
    /// as the fixed horizonFlipAngle fallback. Caches renderers in Awake — no
    /// per-spin allocations.
    ///
    /// INSPECTOR SETUP: signMounts — one child mount per sign, in index order
    /// (index 0 = start focus). Use the ArrangeSigns context menu to lay the
    /// mounts out along the rim at anglePerSign spacing. The sphere child must
    /// be named "GlobeMesh" (excluded from horizon sorting, order 0).
    /// </summary>
    public class PlanetSpinner : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Sign mount transforms around the rim, in focus order (0 = top at start).")]
        [SerializeField] private List<Transform> signMounts = new();

        [Tooltip("Degrees between adjacent signs along the rim.")]
        [SerializeField, Min(1f)] private float anglePerSign = 35f;

        [Tooltip("Distance from the globe centre to each sign mount — the sphere's surface radius (ArrangeSigns).")]
        [SerializeField, Min(0.01f)] private float radius = 10f;

        [Header("Feel")]
        [Tooltip("Drag gain — 1 means a quarter-screen vertical swipe moves one sign.")]
        [SerializeField, Min(0.1f)] private float dragSensitivity = 1f;

        [Tooltip("Exponential decay rate of release velocity (higher = shorter glide).")]
        [SerializeField, Min(0.1f)] private float deceleration = 5f;

        [Tooltip("Duration of the settle/focus tween.")]
        [SerializeField, Min(0.01f)] private float snapDuration = 0.35f;

        [Tooltip("Pixels the pointer must move before the gesture counts as a drag (tap otherwise).")]
        [SerializeField, Min(1f)] private float dragThreshold = 10f;

        [Header("Horizon Sorting (2D renderer)")]
        [Tooltip("Ring angle (deg from the focus point) past which a pivot child sorts behind the globe. Set to the angle where a sign BASE crosses the visible horizon silhouette (≈20° for camera at (0,13.5,-8), R=10) — flipping exactly there makes the front/behind swap pixel-identical. Too high and far-side signs float over the planet face.")]
        [SerializeField, Range(10f, 90f)] private float horizonFlipAngle = 20f;

        [Tooltip("Optional. When set, the visible horizon arc is derived from this camera's position every update (needed while the intro camera flies in). Null = fixed horizonFlipAngle.")]
        [SerializeField] private Transform sortingCamera;

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

        /// <summary>Globe surface radius (sphere centre = this transform's position).</summary>
        public float Radius => radius;

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

        // Horizon sorting cache — one entry per pivot child riding the globe.
        private struct RimItem
        {
            public float      BaseAngle;  // ring angle at currentAngle 0
            public Renderer[] Renderers;
            public Canvas[]   Canvases;
            public bool       InFront;
        }

        private const int FrontOrder = 10;   // sprites in front of the globe (canvases +1)
        private const int BehindOrder = -12; // sprites behind the globe (canvases +1)

        private RimItem[] _rimItems;

        private void Awake()
        {
            _maxAngle = Mathf.Max(0, signMounts.Count - 1) * anglePerSign;

            _pressAction    = new InputAction("PlanetPress",    binding: "<Pointer>/press");
            _deltaAction    = new InputAction("PlanetDelta",    binding: "<Pointer>/delta");
            _positionAction = new InputAction("PlanetPosition", binding: "<Pointer>/position");

            _pressAction.started  += OnPointerDown;
            _pressAction.canceled += OnPointerUp;
            _deltaAction.performed += OnDragPerformed;

            BuildRimCache();
        }

        private void BuildRimCache()
        {
            var items = new List<RimItem>();
            foreach (Transform child in transform)
            {
                if (child.name == "GlobeMesh") continue;
                items.Add(new RimItem
                {
                    BaseAngle = Mathf.Atan2(child.localPosition.z, child.localPosition.y) * Mathf.Rad2Deg,
                    Renderers = child.GetComponentsInChildren<Renderer>(true),
                    Canvases  = child.GetComponentsInChildren<Canvas>(true),
                    InFront   = false // force first ApplyAngle to assign orders
                });
            }
            _rimItems = items.ToArray();
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
        /// Set the planet angle directly, UNCLAMPED — for the title intro, which
        /// starts the globe rolled past the sign range and rolls it back to 0.
        /// Input must be disabled while this drives the planet. force re-fires
        /// AngleChanged/FocusedIndexChanged even when the angle is unchanged.
        /// </summary>
        public void SetAngle(float angle, bool force = false)
        {
            KillSnapTween();
            _angularVelocity = 0f;
            ApplyAngle(angle, force);
        }

        /// <summary>
        /// Re-evaluate horizon sorting without changing the angle — call after
        /// moving sortingCamera while the planet is still.
        /// </summary>
        public void RefreshRimSorting() => UpdateRimSorting();

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
        /// Single write point for rotation: rolls the globe around X so the
        /// mount at currentAngle sits at the focus point, then events fire.
        /// Mounts are plain children standing radially — no per-sign work.
        /// </summary>
        private void ApplyAngle(float angle, bool forceEvents = false)
        {
            if (!forceEvents && Mathf.Approximately(angle, _currentAngle)) return;
            _currentAngle = angle;

            transform.localEulerAngles = new Vector3(-_currentAngle, 0f, 0f);
            UpdateRimSorting(forceEvents);

            AngleChanged?.Invoke(_currentAngle);

            int nearest = NearestIndex(_currentAngle);
            if (forceEvents || nearest != _focusedIndex)
            {
                _focusedIndex = nearest;
                FocusedIndexChanged?.Invoke(_focusedIndex);
            }
        }

        /// <summary>
        /// Painter-sorts each rim child against the globe mesh for the 2D
        /// renderer: near side of the horizon ridge → in front, beyond →
        /// behind (globe silhouette clips it). Only touches renderers whose
        /// side actually flipped — no work, no allocs on most frames.
        /// </summary>
        private void UpdateRimSorting(bool force = false)
        {
            if (_rimItems == null) return;

            // Camera-derived horizon: a point on the forward great circle is on
            // the camera's visible hemisphere iff its ring angle lies within
            // acos(R/ρ) of the camera's own ring direction (ρ = camera distance
            // from the globe centre in the YZ plane). Two-sided, so lower-half
            // decor sorts correctly too.
            bool useCamera = sortingCamera != null;
            float arcCentre = 0f, arcHalfWidth = 0f;
            if (useCamera)
            {
                Vector3 c = sortingCamera.position - transform.position;
                float rho = Mathf.Sqrt(c.y * c.y + c.z * c.z);
                arcCentre = Mathf.Atan2(c.z, c.y) * Mathf.Rad2Deg;
                arcHalfWidth = rho > radius ? Mathf.Acos(radius / rho) * Mathf.Rad2Deg : 0f;
            }

            for (int i = 0; i < _rimItems.Length; i++)
            {
                float ringAngle = _rimItems[i].BaseAngle - _currentAngle;
                bool inFront = useCamera
                    ? Mathf.Abs(Mathf.DeltaAngle(ringAngle, arcCentre)) < arcHalfWidth
                    : ringAngle < horizonFlipAngle;
                if (!force && inFront == _rimItems[i].InFront) continue;
                _rimItems[i].InFront = inFront;

                int order = inFront ? FrontOrder : BehindOrder;
                Renderer[] renderers = _rimItems[i].Renderers;
                for (int r = 0; r < renderers.Length; r++)
                    renderers[r].sortingOrder = order;
                Canvas[] canvases = _rimItems[i].Canvases;
                for (int c = 0; c < canvases.Length; c++)
                    canvases[c].sortingOrder = order + 1; // labels/buttons above their board
            }
        }

        private int NearestIndex(float angle) =>
            Mathf.Clamp(Mathf.RoundToInt(angle / anglePerSign), 0, Mathf.Max(0, signMounts.Count - 1));

        // ── Authoring ─────────────────────────────────────────────────────────

        /// <summary>
        /// Editor helper: place mount i on the forward great circle at
        /// radius·(0, cos(i·a), sin(i·a)) — sign 0 at the top (focus point),
        /// later signs ahead over the horizon — standing radially so the
        /// globe's X-roll brings each one upright at the top.
        /// </summary>
        [ContextMenu("Arrange Signs Along Rim")]
        private void ArrangeSigns()
        {
            for (int i = 0; i < signMounts.Count; i++)
            {
                if (signMounts[i] == null) continue;
                float a = i * anglePerSign;
                float rad = a * Mathf.Deg2Rad;
                signMounts[i].localPosition = new Vector3(0f, Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                signMounts[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        }
    }
}
