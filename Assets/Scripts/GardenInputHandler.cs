// GardenInputHandler.cs
// Single-raycaster input for the Garden drum machine grid.
// One Physics2D.OverlapPoint per tap instead of one EventSystem handler per cell,
// which is significantly cheaper on mobile.
//
// TapOrDragFilter components on individual cells are no longer needed — remove them
// from the GardenCell prefab.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class GardenInputHandler : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [Tooltip("Pixels the pointer must move before the gesture is treated as a drag, not a tap.")]
    [SerializeField] private float dragThresholdPx = 10f;

    // Touch state (separate from mouse to avoid cross-contamination)
    private Vector2 _touchDownPos;
    private bool _touchIsDrag;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();

    private void Update()
    {
        HandleTouch();
        HandleMouse();
    }

    // ── Touch ─────────────────────────────────────────────────────────────

    private void HandleTouch()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchDownPos = touch.screenPosition;
                    _touchIsDrag = false;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                    if (!_touchIsDrag && ExceedsDragThreshold(touch.screenPosition, _touchDownPos))
                        _touchIsDrag = true;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                    if (!_touchIsDrag) TryHitCell(touch.screenPosition);
                    break;
            }
        }
    }

    // ── Mouse (editor / desktop) ───────────────────────────────────────────
    // Fires on press rather than release — precise clicking in the editor
    // needs no drag filtering.

    private void HandleMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
            TryHitCell(mouse.position.ReadValue());
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool ExceedsDragThreshold(Vector2 currentPos, Vector2 downPos) =>
        (currentPos - downPos).sqrMagnitude >= dragThresholdPx * dragThresholdPx;

    private void TryHitCell(Vector2 screenPos)
    {
        Vector3 worldPos = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        hit?.GetComponent<DrumGridCell>()?.Tap();
    }
}
