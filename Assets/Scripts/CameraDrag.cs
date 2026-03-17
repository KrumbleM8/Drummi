using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class CameraDrag : MonoBehaviour
{
    [Header("Drag Settings")]
    [Tooltip("How quickly the camera follows the drag (1 = instant, lower = smoother)")]
    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.25f;

    [Tooltip("Restrict camera movement within these world-space bounds")]
    public Bounds cameraBounds;
    public bool useBounds = false;

    [Header("Zoom Settings")]
    [Tooltip("Minimum orthographic size (zoomed in)")]
    public float minZoom = 2f;

    [Tooltip("Maximum orthographic size (zoomed out)")]
    public float maxZoom = 12f;

    [Tooltip("How quickly the zoom smooths out (1 = instant, lower = smoother)")]
    [Range(0.01f, 1f)]
    public float zoomSmoothSpeed = 0.15f;

    [Tooltip("Zoom speed for mouse scroll wheel")]
    public float scrollZoomSpeed = 2f;

    private Vector3 _dragOrigin;
    private Vector3 _targetPosition;
    private bool _isDragging = false;

    private float _targetZoom;

    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetPosition = transform.position;
        _targetZoom = _cam.orthographicSize;

        EnhancedTouchSupport.Enable();
    }

    void OnDestroy()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        HandleTouchInput();
        HandleMouseInput();
        ApplySmoothMovement();
        ApplySmoothZoom();
    }

    // ── Touch (mobile) ───────────────────────────────────────────────────────

    private bool _dragStartedWithTouch = false;
    private float _lastPinchDistance = 0f;

    private void HandleTouchInput()
    {
        var activeTouches = Touch.activeTouches;

        if (activeTouches.Count == 0)
        {
            _isDragging = false;
            return;
        }

        // Two fingers — pinch zoom (drag is suspended during pinch)
        if (activeTouches.Count == 2)
        {
            _isDragging = false;

            Touch t0 = activeTouches[0];
            Touch t1 = activeTouches[1];

            float currentDistance = Vector2.Distance(t0.screenPosition, t1.screenPosition);

            // On the first frame both fingers are down, just record the distance
            if (t0.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                t1.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                _lastPinchDistance = currentDistance;
                return;
            }

            float delta = _lastPinchDistance - currentDistance;

            // Convert pixel delta to world-space zoom delta.
            // Dividing by Screen.height makes it resolution-independent.
            _targetZoom += delta * (_targetZoom / Screen.height);
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);

            _lastPinchDistance = currentDistance;
            return;
        }

        // One finger — normal drag
        if (activeTouches.Count == 1)
        {
            Touch touch = activeTouches[0];

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _dragStartedWithTouch = true;
                    BeginDrag(touch.screenPosition);
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    if (_dragStartedWithTouch)
                        ContinueDrag(touch.screenPosition);
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _dragStartedWithTouch = false;
                    _isDragging = false;
                    break;
            }
        }
    }

    private void HandleMouseInput()
    {
        if (_dragStartedWithTouch) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Scroll wheel zoom (editor / desktop)
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            _targetZoom -= scroll * scrollZoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }

        if (mouse.leftButton.wasPressedThisFrame)
            BeginDrag(mouse.position.ReadValue());
        else if (mouse.leftButton.isPressed)
            ContinueDrag(mouse.position.ReadValue());
        else if (mouse.leftButton.wasReleasedThisFrame)
            _isDragging = false;
    }

    // ── Shared drag logic ────────────────────────────────────────────────────

    private void BeginDrag(Vector2 screenPosition)
    {
        _dragOrigin = ScreenToWorldPoint(screenPosition);
        _isDragging = true;
    }

    private void ContinueDrag(Vector2 screenPosition)
    {
        if (!_isDragging) return;

        Vector3 currentWorldPoint = ScreenToWorldPoint(screenPosition);
        Vector3 delta = _dragOrigin - currentWorldPoint;

        _targetPosition = transform.position + delta;

        if (useBounds)
            ClampTargetToBounds();
    }

    private void ApplySmoothMovement()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            new Vector3(_targetPosition.x, _targetPosition.y, transform.position.z),
            smoothSpeed
        );
    }

    private void ApplySmoothZoom()
    {
        _cam.orthographicSize = Mathf.Lerp(
            _cam.orthographicSize,
            _targetZoom,
            zoomSmoothSpeed
        );
    }

    private void ClampTargetToBounds()
    {
        _targetPosition.x = Mathf.Clamp(_targetPosition.x, cameraBounds.min.x, cameraBounds.max.x);
        _targetPosition.y = Mathf.Clamp(_targetPosition.y, cameraBounds.min.y, cameraBounds.max.y);
    }

    private Vector3 ScreenToWorldPoint(Vector2 screenPosition)
    {
        Vector3 point = _cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _cam.nearClipPlane));
        point.z = 0f;
        return point;
    }
}