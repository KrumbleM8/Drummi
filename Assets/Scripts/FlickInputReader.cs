using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Classification of a completed flick gesture at release time. Throws are fully
    /// free-form (360°) — aiming is manual, judged downstream against the target hitbox.
    /// </summary>
    public enum FlickClassification
    {
        /// <summary>Flick exceeded the minimum distance — a legal throw in any direction.</summary>
        Valid,
        /// <summary>Flick distance was below the minimum screen-height fraction.</summary>
        TooShort,
    }

    /// <summary>
    /// Payload fired on flick release. The release timestamp is GameClock virtual time
    /// captured the same frame the touch's Ended phase was observed, before any gesture math.
    /// </summary>
    public readonly struct FlickRelease
    {
        /// <summary>GameClock virtual time at the moment the release was observed.</summary>
        public double ReleaseVirtualTime { get; }

        /// <summary>
        /// Flick vector in screen pixels, measured over the trailing motion window
        /// (release position − position ~flickWindowSeconds before release), so a
        /// wind-up drag before the flick does not affect it.
        /// </summary>
        public Vector2 DragVector { get; }

        /// <summary>
        /// Screen position where the measured flick motion began (the trailing-window
        /// anchor, NOT the original press-down position) — the origin for aim checks.
        /// </summary>
        public Vector2 StartPosition { get; }

        /// <summary>How the gesture was classified at release.</summary>
        public FlickClassification Classification { get; }

        /// <summary>Creates a release payload.</summary>
        public FlickRelease(double releaseVirtualTime, Vector2 dragVector, Vector2 startPosition, FlickClassification classification)
        {
            ReleaseVirtualTime = releaseVirtualTime;
            DragVector = dragVector;
            StartPosition = startPosition;
            Classification = classification;
        }
    }

    /// <summary>
    /// Reads upward flick gestures (touch drag-release) via EnhancedTouch polling.
    /// Sibling of DungeonInputReader — does not touch the drum pad input stack.
    ///
    /// Tracks a single active flick at a time: the first finger to touch down is followed
    /// until it ends; other fingers are ignored while it is tracked. On release the
    /// GameClock virtual time is captured before any vector/classification math so the
    /// timestamp is as close as possible to the frame the Ended phase was observed.
    ///
    /// The flick vector is measured over a trailing MOTION WINDOW (flickWindowSeconds),
    /// not from the original press-down position: players naturally wind up by dragging
    /// downward before flicking, so only the final stretch of motion counts.
    ///
    /// Flick input is live for the whole bar — there is deliberately no allowInput-style
    /// response-window gating, only the encounter-level <see cref="EncounterActive"/> gate.
    /// </summary>
    public class FlickInputReader : MonoBehaviour
    {
        [Header("Gesture Thresholds")]
        [Tooltip("Minimum flick distance (measured over the motion window) as a fraction of screen height (0.1 = 10%).")]
        [SerializeField] private float minDragScreenHeightFraction = 0.1f;

        [Tooltip("Seconds of trailing motion used to compute the flick vector at release. " +
                 "Only movement within this window counts, so a downward wind-up before the flick is ignored.")]
        [SerializeField] private float flickWindowSeconds = 0.1f;

        [Header("Gating")]
        [Tooltip("Encounter-level input gate. When false, touches are silently dropped.")]
        [SerializeField] private bool encounterActive = true;

        /// <summary>
        /// Encounter-level gate, mirroring DungeonInputReader.EncounterActive.
        /// When false, new touches are ignored and any in-flight flick is dropped on release.
        /// </summary>
        public bool EncounterActive
        {
            get => encounterActive;
            set => encounterActive = value;
        }

        /// <summary>Fired once per flick release with timestamp, drag vector, and classification.</summary>
        public event Action<FlickRelease> OnFlickReleased;

        /// <summary>
        /// Abandons any in-progress flick track (touch or mouse) without firing a
        /// release. Host cleanup hook — lets a mode quit mid-Held without a stray
        /// release event landing after the encounter is torn down.
        /// </summary>
        public void CancelActiveTracking()
        {
            if (!_tracking) return;
            _tracking = false;
            _mouseDragging = false;
            _samples.Clear();
            Debug.Log("[FlickInputReader] Active tracking cancelled by host");
        }

        /// <summary>Fired when a flick hold begins (touch or mouse), with the press screen position.</summary>
        public event Action<Vector2> OnFlickBegan;

        /// <summary>
        /// True while a flick hold is being tracked; outputs the pointer's current screen
        /// position. Used by the visual layer (ball follows the finger) — grading never
        /// reads this.
        /// </summary>
        public bool TryGetActivePointer(out Vector2 screenPos)
        {
            screenPos = default;
            if (!_tracking) return false;

            if (_mouseDragging)
            {
                if (Mouse.current == null) return false;
                screenPos = Mouse.current.position.ReadValue();
                return true;
            }

            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.finger.index == _trackedFingerIndex)
                {
                    screenPos = touch.screenPosition;
                    return true;
                }
            }
            return false;
        }

        // ── Active flick state ────────────────────────────────────────────────

        private struct DragSample
        {
            public Vector2 Position;
            public double VirtualTime;
        }

        private bool _tracking;
        private bool _mouseDragging;
        private int _trackedFingerIndex;
        private Vector2 _startPosition;
        private readonly List<DragSample> _samples = new();

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Matches DrumPadTouch / GardenInputHandler: paired Enable/Disable is safe
            // because EnhancedTouchSupport ref-counts internally.
            // NOTE: deliberately no TouchSimulation — it disables the Mouse device and
            // its mouse→touch delivery proved unreliable in-editor. Editor/PC input uses
            // the explicit mouse fallback below (CameraDrag / GardenInputHandler pattern).
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _tracking = false;
            _mouseDragging = false;
            _samples.Clear();
        }

        private void Update()
        {
            if (GameClock.Instance != null && GameClock.Instance.IsPaused) return;

            foreach (Touch touch in Touch.activeTouches)
            {
                switch (touch.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        HandleBegan(touch);
                        break;

                    case UnityEngine.InputSystem.TouchPhase.Ended:
                        HandleEnded(touch);
                        break;

                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        HandleCanceled(touch);
                        break;
                }
            }

            HandleMouse();
            SampleActiveDrag();
        }

        // Records the tracked pointer's position each frame while a drag is live, so the
        // release can measure the flick over the trailing window instead of from the
        // press-down position. Timestamps are GameClock virtual time (freezes on pause).
        private void SampleActiveDrag()
        {
            if (!_tracking || GameClock.Instance == null) return;

            Vector2 pos;
            if (_mouseDragging)
            {
                if (Mouse.current == null) return;
                pos = Mouse.current.position.ReadValue();
            }
            else
            {
                bool found = false;
                pos = default;
                foreach (Touch touch in Touch.activeTouches)
                {
                    if (touch.finger.index == _trackedFingerIndex)
                    {
                        pos = touch.screenPosition;
                        found = true;
                        break;
                    }
                }
                if (!found) return;
            }

            double now = GameClock.Instance.GameTime;
            _samples.Add(new DragSample { Position = pos, VirtualTime = now });

            // Bound the buffer: drop samples far older than the window.
            double cutoff = now - flickWindowSeconds - 0.25;
            while (_samples.Count > 0 && _samples[0].VirtualTime < cutoff)
                _samples.RemoveAt(0);
        }

        // ── Gesture tracking ──────────────────────────────────────────────────

        private void HandleBegan(Touch touch)
        {
            if (!encounterActive) return; // Silent drop.
            if (_tracking) return;        // Single active flick — ignore extra fingers.
            if (IsPointerOverUI(touch.touchId)) return; // UI (pause button etc.) owns this touch.

            _tracking = true;
            _trackedFingerIndex = touch.finger.index;
            _startPosition = touch.screenPosition;
            _samples.Clear();

            Debug.Log($"[FlickInputReader] Began — finger {_trackedFingerIndex} at {_startPosition}");
            OnFlickBegan?.Invoke(_startPosition);
        }

        private void HandleEnded(Touch touch)
        {
            if (!_tracking || _mouseDragging || touch.finger.index != _trackedFingerIndex) return;

            // Capture the release timestamp FIRST — same frame Ended is observed,
            // before any vector/classification math (Perfect window is ~25ms).
            double releaseVirtualTime = GameClock.Instance != null ? GameClock.Instance.GameTime : 0.0;

            CompleteFlick(releaseVirtualTime, touch.screenPosition);
        }

        private void HandleCanceled(Touch touch)
        {
            if (!_tracking || _mouseDragging || touch.finger.index != _trackedFingerIndex) return;

            _tracking = false;
            _samples.Clear();
            Debug.Log("[FlickInputReader] Touch canceled — flick abandoned");
        }

        /// <summary>
        /// Editor/PC fallback: left mouse button drag-release maps to a flick, mirroring
        /// the CameraDrag / GardenInputHandler convention. TouchSimulation is deliberately
        /// not used — it disables the Mouse device and proved unreliable in-editor.
        /// </summary>
        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!encounterActive) return; // Silent drop.
                if (_tracking) return;        // A touch flick is already in progress.
                if (IsPointerOverUI()) return; // UI (pause button etc.) owns this click.

                _tracking = true;
                _mouseDragging = true;
                _startPosition = mouse.position.ReadValue();
                _samples.Clear();

                Debug.Log($"[FlickInputReader] Began (mouse) at {_startPosition}");
                OnFlickBegan?.Invoke(_startPosition);
            }
            else if (mouse.leftButton.wasReleasedThisFrame && _mouseDragging)
            {
                // Timestamp FIRST, same as the touch path.
                double releaseVirtualTime = GameClock.Instance != null ? GameClock.Instance.GameTime : 0.0;

                _mouseDragging = false;
                CompleteFlick(releaseVirtualTime, mouse.position.ReadValue());
            }
        }

        /// <summary>
        /// Shared release path for touch and mouse: anchor the flick to the trailing
        /// motion window, classify, log, fire.
        /// </summary>
        private void CompleteFlick(double releaseVirtualTime, Vector2 endPosition)
        {
            _tracking = false;

            if (!encounterActive)
            {
                _samples.Clear();
                Debug.Log("[FlickInputReader] Release dropped — EncounterActive is false");
                return;
            }

            // Flick origin = earliest sample inside the trailing window. Falls back to
            // the press position for drags shorter than the window.
            Vector2 flickOrigin = _startPosition;
            double windowStart = releaseVirtualTime - flickWindowSeconds;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].VirtualTime >= windowStart)
                {
                    flickOrigin = _samples[i].Position;
                    break;
                }
            }
            _samples.Clear();

            Vector2 dragVector = endPosition - flickOrigin;
            FlickClassification classification = Classify(dragVector);

            Debug.Log($"[FlickInputReader] Released — vt={releaseVirtualTime:F4} flick={dragVector} " +
                      $"from window anchor {flickOrigin} (press was {_startPosition}) " +
                      $"dist={dragVector.magnitude:F1}px angle={Vector2.Angle(dragVector, Vector2.up):F1}° " +
                      $"→ {classification}");

            OnFlickReleased?.Invoke(new FlickRelease(releaseVirtualTime, dragVector, flickOrigin, classification));
        }

        // UI filter (TapInvoker2D convention): touches/clicks that land on raycastable
        // UI — pause button, menus — never start a flick track.
        private static bool IsPointerOverUI(int pointerId = -1)
        {
            if (EventSystem.current == null) return false;
            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private FlickClassification Classify(Vector2 dragVector)
        {
            float minDistance = minDragScreenHeightFraction * Screen.height;
            return dragVector.magnitude < minDistance
                ? FlickClassification.TooShort
                : FlickClassification.Valid;
        }
    }
}
