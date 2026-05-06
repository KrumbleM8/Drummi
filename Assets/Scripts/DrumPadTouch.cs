using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;

/// <summary>
/// Mode-agnostic drum pad input handler.
/// Detects left/right hits via touch (RectTransform bounds) or keyboard and fires
/// OnLeftHit / OnRightHit events. Mode-specific components subscribe to these events.
///
/// SCENE SETUP:
///   1. Assign leftPad and rightPad RectTransforms in the Inspector.
///   2. Set leftKey / rightKey as needed (default A / L).
///   3. Mode components (e.g. BongoModeInputReader) subscribe to OnLeftHit / OnRightHit.
///
/// NOTE: Garden mode has a different input style and is intentionally not supported here.
/// </summary>
public class DrumPadTouch : MonoBehaviour
{
    [Header("Pad Areas")]
    [SerializeField] private RectTransform leftPad;
    [SerializeField] private RectTransform centerPad; // Optional — Dungeon mode only
    [SerializeField] private RectTransform rightPad;

    [Header("Keyboard Bindings")]
    [Tooltip("Key that triggers the left pad.")]
    public Key leftKey = Key.A;

    [Tooltip("Key that triggers the center pad (Dungeon mode).")]
    public Key centerKey = Key.Space;

    [Tooltip("Key that triggers the right pad.")]
    public Key rightKey = Key.L;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Fired when the left pad is tapped or the left key is pressed.</summary>
    public event Action OnLeftHit;

    /// <summary>Fired when the center pad is tapped or the center key is pressed.</summary>
    public event Action OnCenterHit;

    /// <summary>Fired when the right pad is tapped or the right key is pressed.</summary>
    public event Action OnRightHit;

    // ── Private ───────────────────────────────────────────────────────────

    private readonly HashSet<int> _processedFingers = new HashSet<int>();

    // ── Unity ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        TouchSimulation.Enable();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        TouchSimulation.Disable();
#endif
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (GameClock.Instance != null && GameClock.Instance.IsPaused) return;

        _processedFingers.Clear();
        HandleTouch();
        HandleKeyboard();
    }

    // ── Input Detection ───────────────────────────────────────────────────

    private void HandleTouch()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

            int fingerId = touch.finger.index;
            if (_processedFingers.Contains(fingerId)) continue;
            _processedFingers.Add(fingerId);

            Vector2 screenPos = touch.screenPosition;

            if (leftPad != null && RectTransformUtility.RectangleContainsScreenPoint(leftPad, screenPos))
            {
                FireHit(false);
                continue;
            }

            if (centerPad != null && RectTransformUtility.RectangleContainsScreenPoint(centerPad, screenPos))
            {
                OnCenterHit?.Invoke();
                Debug.Log("[DrumPadTouch] Center hit");
                continue;
            }

            if (rightPad != null && RectTransformUtility.RectangleContainsScreenPoint(rightPad, screenPos))
            {
                FireHit(true);
            }
        }
    }

    private void HandleKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb[leftKey].wasPressedThisFrame)   FireHit(false);
        if (kb[centerKey].wasPressedThisFrame) OnCenterHit?.Invoke();
        if (kb[rightKey].wasPressedThisFrame)  FireHit(true);
    }

    private void FireHit(bool isRight)
    {
        if (isRight)
            OnRightHit?.Invoke();
        else
            OnLeftHit?.Invoke();

        Debug.Log($"[DrumPadTouch] {(isRight ? "Right" : "Left")} hit");
    }
}
