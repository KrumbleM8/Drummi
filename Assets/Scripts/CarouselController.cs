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

    [Header("Visibility")]
    [Tooltip("Maximum number of items that are visible at once (closest to the center).")]
    [Min(1)]
    public int maxVisibleItems = 3;

    [Header("Initial Centering")]
    [Tooltip("Index of the child to center on when the carousel becomes active.")]
    [Min(0)]
    public int startCenteredIndex = 0;

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

    // Cached arrays to avoid per-frame allocations
    private float[] childDistances;
    private bool[] selectedVisible;

    /// <summary>
    /// True when a drag was recognized (moved past threshold) OR within a short window after release.
    /// Children should not treat the gesture as a tap when this is true.
    /// </summary>
    public bool SuppressChildTap => promotedToDrag || Time.unscaledTime < suppressClickUntil;

    /// <summary>
    /// Read/write access to the carousel's internal horizontal offset.
    /// Intended for an external coordinator (e.g. <c>SongSelectController</c>) to animate
    /// the carousel to a target position while drag input is disabled via
    /// <see cref="SetInputEnabled"/>. Setting this value immediately repositions all child
    /// items by calling <c>UpdateCarouselItems</c>, so each assigned frame renders without
    /// waiting for the next <c>Update</c> tick.
    /// <para>
    /// Do not write this property while drag input is enabled — the drag handlers also write
    /// <c>currentOffset</c> directly and will override any externally set value.
    /// </para>
    /// </summary>
    public float CurrentOffset
    {
        get => currentOffset;
        set
        {
            currentOffset = value;
            UpdateCarouselItems();
        }
    }

    private void Awake()
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

    private void OnEnable()
    {
        dragAction.Enable();
        pointerDownAction.Enable();
        pointerUpAction.Enable();

        // Ensure we always start with a child centered when this carousel becomes active.
        CenterOnChild(startCenteredIndex);
    }

    private void OnDisable()
    {
        dragAction.Disable();
        pointerDownAction.Disable();
        pointerUpAction.Disable();
    }

    /// <summary>
    /// Gates pointer drag input without stopping the <c>Update</c> loop.
    /// When <paramref name="enabled"/> is <c>false</c>, the three InputActions are disabled so
    /// no drag, pointer-down, or pointer-up callbacks fire. <c>Update</c> continues to run,
    /// so <c>currentOffset</c> can be driven externally (e.g. animated to centre an album card)
    /// and item positions / visibility continue to be refreshed every frame.
    /// <para>
    /// Note: if called while a drag gesture is in progress, <c>isDragging</c> is reset to
    /// <c>false</c> and <c>velocity</c> is zeroed so inertia does not resume on re-enable.
    /// </para>
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        if (enabled)
        {
            dragAction.Enable();
            pointerDownAction.Enable();
            pointerUpAction.Enable();
        }
        else
        {
            dragAction.Disable();
            pointerDownAction.Disable();
            pointerUpAction.Disable();

            // Clean up any in-progress gesture so inertia doesn't resume on re-enable.
            isDragging = false;
            velocity = 0f;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

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

    private void UpdateCarouselItems()
    {
        int count = transform.childCount;
        if (count == 0)
            return;

        // Ensure our caches are the right size
        if (childDistances == null || childDistances.Length != count)
        {
            childDistances = new float[count];
            selectedVisible = new bool[count];
        }

        float totalWidth = count * spacing;
        float halfWidth = totalWidth / 2f;

        // First pass: position, scale and compute distances
        for (int i = 0; i < count; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (!child)
            {
                childDistances[i] = float.MaxValue;
                continue;
            }

            // Calculate wrapped X position relative to center
            float targetX = (i - (count - 1) / 2f) * spacing + currentOffset;

            while (targetX < -halfWidth)
                targetX += totalWidth;
            while (targetX > halfWidth)
                targetX -= totalWidth;

            child.anchoredPosition = new Vector2(targetX, child.anchoredPosition.y);

            float distance = Mathf.Abs(targetX);
            childDistances[i] = distance;

            // Apply scaling effect based on distance from center
            float scale = Mathf.Lerp(1f, 1f - scaleFactor, distance / halfWidth);
            scale = Mathf.Clamp(scale, 0.5f, 1f);
            child.localScale = Vector3.one * scale;
        }

        // Second pass: decide which items are visible
        for (int i = 0; i < count; i++)
        {
            selectedVisible[i] = false;
        }

        int visibleCount = Mathf.Min(maxVisibleItems, count);

        // If we want fewer visible items than total, pick the closest ones
        for (int v = 0; v < visibleCount; v++)
        {
            float bestDist = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < count; i++)
            {
                if (selectedVisible[i])
                    continue;

                float dist = childDistances[i];
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                selectedVisible[bestIndex] = true;
            }
        }

        // Apply visibility to each child
        for (int i = 0; i < count; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (!child)
                continue;

            bool shouldBeVisible = selectedVisible[i];
            SetChildVisible(child, shouldBeVisible);
        }
    }

    private void OnPointerDown(InputAction.CallbackContext context)
    {
        isDragging = true;
        velocity = 0f;

        if (Mouse.current != null)
        {
            previousPointerPos = Mouse.current.position.ReadValue();
        }

        pointerDownPos = previousPointerPos;
        promotedToDrag = false;
        suppressClickUntil = 0f; // clear suppression at new gesture start
    }

    private void OnPointerUp(InputAction.CallbackContext context)
    {
        isDragging = false;

        Vector2 lastDelta = dragAction.ReadValue<Vector2>();
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        velocity = lastDelta.x / dt;

        // If this gesture was promoted to a drag, suppress taps briefly
        // to avoid click firing on release frame.
        if (promotedToDrag)
        {
            suppressClickUntil = Time.unscaledTime + 0.05f;
        }

        promotedToDrag = false;
    }

    private void OnDragPerformed(InputAction.CallbackContext context)
    {
        if (!isDragging)
            return;

        Vector2 delta = context.ReadValue<Vector2>();
        currentOffset += delta.x;

        // Promote to drag if moved beyond threshold (so children should not tap)
        Vector2 posNow;
        if (Mouse.current != null)
        {
            posNow = Mouse.current.position.ReadValue();
        }
        else
        {
            posNow = previousPointerPos + delta;
        }

        if (!promotedToDrag && (posNow - pointerDownPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            promotedToDrag = true;
        }

        previousPointerPos = posNow;
    }

    /// <summary>
    /// Sets <c>currentOffset</c> so that the child at <paramref name="index"/> is
    /// centered on the X axis (anchoredPosition.x == 0). Clamps out-of-range indices.
    /// Zeroes <c>velocity</c> to prevent inertia from drifting away immediately after centering.
    /// Calls <see cref="UpdateCarouselItems"/> so positions are applied in the same frame.
    /// </summary>
    public void CenterOnChild(int index)
    {
        int count = transform.childCount;
        if (count == 0)
            return;

        int clampedIndex = Mathf.Clamp(index, 0, count - 1);

        // (i - (count - 1) / 2f) * spacing + currentOffset = 0  => solve for currentOffset
        float centerIndex = (count - 1) / 2f;
        currentOffset = -(clampedIndex - centerIndex) * spacing;

        // Reset velocity so we don't instantly drift away from the centered position.
        velocity = 0f;

        // Immediately update item positions/visibility after recentering
        UpdateCarouselItems();
    }

    /// <summary>
    /// Makes a child visually visible or hidden without removing it from the hierarchy.
    /// Uses CanvasGroup so interaction and raycasts also respect visibility.
    /// </summary>
    private void SetChildVisible(RectTransform child, bool visible)
    {
        if (!child)
            return;

        CanvasGroup cg = child.GetComponent<CanvasGroup>();
        if (!cg)
        {
            cg = child.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }
}
