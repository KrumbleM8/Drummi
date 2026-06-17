using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Full-screen transparent tap target that collapses the expanded album view
    /// when the player presses anywhere outside the vinyl row.
    /// Fires on <c>IPointerDownHandler.OnPointerDown</c> (not click-up) for
    /// immediate response.
    ///
    /// <para><b>Scene setup (required):</b></para>
    /// <list type="bullet">
    ///   <item>Attach alongside a raycast-enabled <see cref="Image"/> with alpha = 0
    ///         and Raycast Target = true so Unity's EventSystem can hit-test it.</item>
    ///   <item>Place as a sibling of the vinyl-row panel at a <b>lower</b> sibling index
    ///         so vinyls are rendered on top and receive raycasts first.</item>
    ///   <item>Place at a <b>lower</b> sibling index than the popup panel so the popup
    ///         is above the catcher (the catcher is deactivated during Popup anyway).</item>
    ///   <item>SetActive is managed entirely by <see cref="SongSelectController"/>;
    ///         do not toggle it elsewhere.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TapCatcher : MonoBehaviour, IPointerDownHandler
    {
        private SongSelectController _controller;

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Establishes the back-reference to the owning controller.
        /// Called by <see cref="SongSelectController"/> in <c>Start()</c>.
        /// </summary>
        public void Initialize(SongSelectController controller)
        {
            _controller = controller;
        }

        // ── IPointerDownHandler ───────────────────────────────────────────────

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.Collapse();
        }
    }
}
