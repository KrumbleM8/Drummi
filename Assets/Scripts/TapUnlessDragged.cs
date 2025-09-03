using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class TapUnlessDragged : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Tooltip("Pixels finger must move before a tap becomes a drag.")]
    public float dragThreshold = 10f;

    public UnityEvent onTap; // or keep your existing EventTrigger and just don’t bind PointerClick when dragging

    private Vector2 downPos;
    private bool dragged;

    public void OnPointerDown(PointerEventData eventData)
    {
        downPos = eventData.position;
        dragged = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if ((eventData.position - downPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            dragged = true;
            // Cancel any pending click on this object
            eventData.pointerPress = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragged && (eventData.position - downPos).sqrMagnitude >= dragThreshold * dragThreshold)
        {
            dragged = true;
            eventData.pointerPress = null;
        }
        // Do NOT Use() here; let the parent carousel consume drag if it wants.
    }

    public void OnEndDrag(PointerEventData eventData) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragged) return; // suppress click if we dragged
        onTap?.Invoke();
    }
}
