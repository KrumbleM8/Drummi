using UnityEngine;
using UnityEngine.InputSystem;

public class CarouselController : MonoBehaviour
{
    [Header("Carousel Settings")]
    [Tooltip("Spacing in pixels between items.")]
    public float spacing = 200f;
    [Tooltip("How much items scale down at the edges (0 = no scaling, 1 = high scaling effect).")]
    [Range(0f, 1f)]
    public float scaleFactor = 0.5f;
    [Tooltip("Deceleration rate when drag is released.")]
    public float deceleration = 5f;

    [Header("Gesture")]
    [Tooltip("Pixels the pointer must move before we treat the gesture as a drag (tap otherwise).")]
    public float dragThreshold = 10f;

    private float currentOffset = 0f;
    private float velocity = 0f;
    private bool isDragging = false;

    // InputActions for pointer input using the new Input System.
    private InputAction dragAction;
    private InputAction pointerDownAction;
    private InputAction pointerUpAction;

    private Vector2 previousPointerPos;

    // Drag promotion & click suppression
    private Vector2 pointerDownPos;
    private bool promotedToDrag = false;
    private float suppressClickUntil = 0f; // small grace window after release

    /// <summary>
    /// True when a drag was recognized (moved past threshold) OR within a short window after release.
    /// Children should not treat the gesture as a tap when this is true.
    /// </summary>
    public bool SuppressChildTap => promotedToDrag || Time.unscaledTime < suppressClickUntil;

    void Awake()
    {
        // Create and bind the InputActions.
        dragAction = new InputAction("Drag", binding: "<Pointer>/delta");
        pointerDownAction = new InputAction("PointerDown", binding: "<Pointer>/press");
        pointerUpAction = new InputAction("PointerUp", binding: "<Pointer>/press");

        // Subscribe to events.
        dragAction.performed += OnDragPerformed;
        pointerDownAction.started += OnPointerDown;
        pointerUpAction.canceled += OnPointerUp;
    }

    void OnEnable()
    {
        dragAction.Enable();
        pointerDownAction.Enable();
        pointerUpAction.Enable();
    }

    void OnDisable()
    {
        dragAction.Disable();
        pointerDownAction.Disable();
        pointerUpAction.Disable();
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        // If not dragging, let inertia carry the movement.
        if (!isDragging)
        {
            if (Mathf.Abs(velocity) > 0.01f)
            {
                currentOffset += velocity * Time.deltaTime;
                velocity = Mathf.Lerp(velocity, 0f, deceleration * Time.deltaTime);
            }
            else
            {
                velocity = 0f;
            }
        }

        UpdateCarouselItems();
    }

    void UpdateCarouselItems()
    {
        int count = transform.childCount;
        if (count == 0) return;

        float totalWidth = count * spacing;
        float halfWidth = totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (!child) continue;

            float targetX = (i - (count - 1) / 2f) * spacing + currentOffset;

            while (targetX < -halfWidth) targetX += totalWidth;
            while (targetX > halfWidth) targetX -= totalWidth;

            child.anchoredPosition = new Vector2(targetX, child.anchoredPosition.y);

            float distance = Mathf.Abs(targetX);
            float scale = Mathf.Lerp(1f, 1f - scaleFactor, distance / halfWidth);
            scale = Mathf.Clamp(scale, 0.5f, 1f);
            child.localScale = Vector3.one * scale;
        }
    }

    void OnPointerDown(InputAction.CallbackContext context)
    {
        isDragging = true;
        velocity = 0f;

        previousPointerPos = Mouse.current != null ? Mouse.current.position.ReadValue() : previousPointerPos;
        pointerDownPos = previousPointerPos;

        promotedToDrag = false;
        suppressClickUntil = 0f; // clear suppression at new gesture start
    }

    void OnPointerUp(InputAction.CallbackContext context)
    {
        isDragging = false;

        Vector2 lastDelta = dragAction.ReadValue<Vector2>();
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        velocity = lastDelta.x / dt;

        // If this gesture was promoted to a drag, suppress taps briefly to avoid click firing on release frame.
        if (promotedToDrag)
            suppressClickUntil = Time.unscaledTime + 0.05f;

        promotedToDrag = false;
    }

    void OnDragPerformed(InputAction.CallbackContext context)
    {
        if (!isDragging) return;

        Vector2 delta = context.ReadValue<Vector2>();
        currentOffset += delta.x;

        // Promote to drag if moved beyond threshold (so children should not tap)
        Vector2 posNow = Mouse.current != null ? Mouse.current.position.ReadValue() : previousPointerPos + delta;
        if (!promotedToDrag && (posNow - pointerDownPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            promotedToDrag = true;
        }

        previousPointerPos = posNow;
    }
}
