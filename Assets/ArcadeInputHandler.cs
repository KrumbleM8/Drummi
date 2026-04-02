using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Detects player input for Left and Right lanes via keyboard or screen touch
/// and forwards taps to HitJudge. Uses Unity's new Input System + EnhancedTouch,
/// consistent with Drummi's existing DrumInputHandler.
///
/// KEYBOARD: Configurable keys per lane (default A / L).
/// TOUCH:    Left half of screen = Left lane. Right half = Right lane.
///
/// Place on the same GameObject as HitJudge.
/// </summary>
[RequireComponent(typeof(HitJudge))]
public class ArcadeInputHandler : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Keyboard Bindings")]
    [Tooltip("Key for the Left lane.")]
    public Key leftLaneKey = Key.A;

    [Tooltip("Key for the Right lane.")]
    public Key rightLaneKey = Key.L;

    [Header("Touch Settings")]
    [Tooltip("Normalised X screen position used as the Left/Right boundary (0–1). Default 0.5 = screen centre.")]
    [Range(0f, 1f)]
    public float touchSplitNormalisedX = 0.5f;

    // ── Private ───────────────────────────────────────────────────────────

    private HitJudge _hitJudge;
    private HitZoneManager _hitZoneManager;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _hitJudge = GetComponent<HitJudge>();
        _hitZoneManager = FindFirstObjectByType<HitZoneManager>(); // optional
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (GameClock.Instance.IsPaused) return;

        HandleKeyboard();
        HandleTouch();
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void HandleKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb[leftLaneKey].wasPressedThisFrame) FireLane(Lane.Left);
        if (kb[rightLaneKey].wasPressedThisFrame) FireLane(Lane.Right);
    }

    private void HandleTouch()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            // Only register the initial tap frame
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

            float splitX = Screen.width * touchSplitNormalisedX;
            Lane lane = touch.screenPosition.x < splitX ? Lane.Left : Lane.Right;
            FireLane(lane);
        }
    }

    private void FireLane(Lane lane)
    {
        // Instant press flash — before judgement so it feels responsive
        _hitZoneManager?.NotifyPress(lane);
        if (lane == Lane.Left)
        {
            AudioManager.instance.PlayBongoLeft();
            BongoAnimator.instance.PlayBongoAnimation(true);
        }
        else
        {
            AudioManager.instance.PlayBongoRight();
            BongoAnimator.instance.PlayBongoAnimation(false);
        }

        HitJudgement result = _hitJudge.Judge(lane);
        Debug.Log($"[RhythmInput] {lane} tapped → {result}");
    }
}