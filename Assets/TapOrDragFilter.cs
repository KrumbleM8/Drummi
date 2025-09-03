using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TapOrDragFilter : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Tooltip("Pixels the pointer can move and still count as a tap.")]
    public float dragThreshold = 10f;

    [Tooltip("Invoked only when the gesture is a TAP (not a drag).")]
    public UnityEvent onTap;

    private Vector2 downPos;
    private bool dragged;

    public void OnPointerDown(PointerEventData eventData)
    {
        downPos = eventData.position;
        dragged = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!dragged && (eventData.position - downPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            dragged = true;
            // Cancel Unity's pending click on this object so it won't fire on pointer up.
            eventData.pointerPress = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragged && (eventData.position - downPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            dragged = true;
            eventData.pointerPress = null; // belt-and-braces: also cancel if threshold crossed here
        }
        // Do NOT eventData.Use() here; let the parent/global system handle scrolling/dragging.
    }

    public void OnEndDrag(PointerEventData eventData) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragged) return; // suppress click if it was a drag
        onTap?.Invoke();
    }
}
