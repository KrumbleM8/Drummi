using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 3D counterpart of TapInvoker2D for perspective-camera scenes (e.g. the
/// FlashCardsMenu tiny-planet): fires OnTapped when a press ray hits this
/// object's own Collider. Tests only the cached collider, so overlapping
/// decor never steals the tap.
/// </summary>
public class TapInvoker3D : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private float maxRayDistance = 100f;

    [Header("Event")]
    public UnityEvent OnTapped;

    private Camera cam;
    private Collider cachedCollider;

    void Awake()
    {
        cam = Camera.main;
        cachedCollider = GetComponent<Collider>();

        if (cachedCollider == null)
            Debug.LogError($"{name} requires a Collider.");
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
        if (cam == null || cachedCollider == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (cachedCollider.Raycast(ray, out _, maxRayDistance))
            OnTapped.Invoke();
    }

    private bool IsPointerOverUI(int pointerId = -1)
    {
        if (EventSystem.current == null) return false;

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
