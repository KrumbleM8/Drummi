using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Endless-scrolling UI background: drifts a RawImage's uvRect offset over time
    /// so a tileable pattern appears to scroll forever. The texture's wrap mode must
    /// be Repeat. Purely cosmetic — uses unscaled time so the drift never freezes
    /// with timescale changes, and wraps the offset so it never loses float precision.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ScrollingPatternBackground : MonoBehaviour
    {
        [Tooltip("Scroll speed in UV units per second (1 = one full texture repeat).")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.05f, 0.05f);

        private RawImage _image;
        private Vector2 _offset;

        private void Awake() => _image = GetComponent<RawImage>();

        private void Update()
        {
            _offset += scrollSpeed * Time.unscaledDeltaTime;
            _offset.x = Mathf.Repeat(_offset.x, 1f);
            _offset.y = Mathf.Repeat(_offset.y, 1f);

            Rect uv = _image.uvRect;
            uv.position = _offset;
            _image.uvRect = uv;
        }
    }
}
