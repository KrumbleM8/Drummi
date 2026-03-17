// GardenInputHandler.cs — no structural changes, just updated to match
// DrumGridCell.Tap() now passing itself rather than row/col.

using UnityEngine;
using UnityEngine.InputSystem;

public class GardenInputHandler : MonoBehaviour
{
    [SerializeField] private Camera cam;

    private InputAction tapAction;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        tapAction = new InputAction(
            name: "Tap",
            type: InputActionType.Button,
            binding: "<Pointer>/press"
        );
    }

    private void OnEnable()
    {
        //tapAction.performed += OnTap;
        //tapAction.Enable();
    }

    private void OnDisable()
    {
        //tapAction.performed -= OnTap;
        //tapAction.Disable();
    }

    //private void OnTap(InputAction.CallbackContext ctx)
    //{
    //    Vector2 screenPos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
    //    Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

    //    Collider2D hit = Physics2D.OverlapPoint(worldPos);
    //    if (hit == null) return;

    //    // Works for any plot — DrumMachine figures out which one owns this cell
    //    hit.GetComponent<DrumGridCell>()?.Tap();

    //    // TODO: handle animal sprite taps here too
    //}
}