using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Prefab-side wiring for one flash-card square. FlashCardView instantiates
    /// these at runtime (the grid size is dynamic — 2/3/5 rows) and maps the
    /// references onto its internal Square entries. All Images must keep
    /// raycastTarget = false so pad input passes through the card.
    /// </summary>
    public class FlashCardSquareWidget : MonoBehaviour
    {
        [Tooltip("Square root — reveal pop tween scales this from 0.")]
        public RectTransform root;
        [Tooltip("Card-back / face background image on the root.")]
        public Image back;
        [Tooltip("Glyph image child — shows the note or rest sprite when face-up.")]
        public Image glyph;
        [Tooltip("Cursor overlay child — enabled while this square's beat is current in the perform bar.")]
        public Image cursor;
    }
}
