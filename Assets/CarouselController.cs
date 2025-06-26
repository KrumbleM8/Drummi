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

    private float currentOffset = 0f;
    private float velocity = 0f;
    private bool isDragging = false;

    // InputActions for pointer input using the new Input System.
    private InputAction dragAction;
    private InputAction pointerDownAction;
    private InputAction pointerUpAction;

    private Vector2 previousPointerPos;

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
        // Calculate half the total width for looping logic.
        float totalWidth = count * spacing;
        float halfWidth = totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            // Calculate the base x-position for the item.
            float targetX = (i - (count - 1) / 2f) * spacing + currentOffset;

            // Loop the item by adding/subtracting the total width.
            while (targetX < -halfWidth)
            {
                targetX += totalWidth;
            }
            while (targetX > halfWidth)
            {
                targetX -= totalWidth;
            }

            // Update the position.
            child.anchoredPosition = new Vector2(targetX, child.anchoredPosition.y);

            // Compute scale based on how close the item is to the center.
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
        previousPointerPos = Mouse.current.position.ReadValue();
    }

    void OnPointerUp(InputAction.CallbackContext context)
    {
        isDragging = false;
        Vector2 lastDelta = dragAction.ReadValue<Vector2>();
        velocity = lastDelta.x / Time.deltaTime;
    }

    void OnDragPerformed(InputAction.CallbackContext context)
    {
        if (isDragging)
        {
            Vector2 delta = context.ReadValue<Vector2>();
            currentOffset += delta.x;
        }
    }
}
