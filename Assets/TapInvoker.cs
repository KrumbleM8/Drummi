using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TapInvoker2D : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Event")]
    public UnityEvent OnTapped;

    private Camera cam;
    private Collider2D cachedCollider;

    void Awake()
    {
        cam = Camera.main;
        cachedCollider = GetComponent<Collider2D>();

        if (cachedCollider == null)
            Debug.LogError($"{name} requires a Collider2D.");
    }

    void Update()
    {
        // Mouse (Editor)
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            if (!IsPointerOverUI())
                TryInvoke(mouse.position.ReadValue());

            return;
        }

        // Touch (Mobile)
        var touch = Touchscreen.current?.primaryTouch;
        if (touch != null && touch.press.wasPressedThisFrame)
        {
            if (!IsPointerOverUI(touch.touchId.ReadValue()))
                TryInvoke(touch.position.ReadValue());
        }
    }

    private void TryInvoke(Vector2 screenPos)
    {
        if (cam == null) return;

        Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);

        // Direct overlap check (fast for 2D)
        Collider2D hit = Physics2D.OverlapPoint(worldPos, hitLayers);

        if (hit == cachedCollider)
        {
            OnTapped.Invoke();
        }
    }

    private bool IsPointerOverUI(int pointerId = -1)
    {
        if (EventSystem.current == null) return false;

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}