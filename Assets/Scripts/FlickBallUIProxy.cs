using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Dungeon-only UI mirror of the world-space flick ball. The dungeon's decorative
    /// Frame lives on a Screen Space - Overlay canvas, which ALWAYS renders above
    /// world sprites — so the real ball disappears behind it near the screen edges.
    /// This proxy re-draws the ball as a UI Image on the same overlay canvas, placed
    /// ABOVE the Frame in sibling order.
    ///
    /// Pure mirror, every frame in LateUpdate (after FlickBallController's Update and
    /// any DOTween writes): position, projected size (so cosmetic scale tweens carry
    /// over), and renderer visibility. Nothing reads from this component; gameplay
    /// still runs entirely on the world ball. The world ball keeps rendering — the
    /// proxy draws over the identical pixels, so no double image is visible.
    ///
    /// INSPECTOR SETUP (Dungeon scene, child of OverlayCanvas, after Frame):
    ///   ballRenderer — the world FlickBall's SpriteRenderer
    ///   worldCamera  — the camera rendering the flick rig (Main Camera)
    ///   image        — this object's Image (same sprite/color as the ball,
    ///                  raycastTarget OFF so flick presses pass through)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FlickBallUIProxy : MonoBehaviour
    {
        [Tooltip("World-space ball renderer this proxy mirrors (position, size, visibility).")]
        [SerializeField] private SpriteRenderer ballRenderer;

        [Tooltip("Camera rendering the world ball — used to project it into canvas space.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("UI Image drawn in the ball's place. Keep raycastTarget OFF.")]
        [SerializeField] private Image image;

        private RectTransform _rt;
        private RectTransform _parentRect;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _parentRect = transform.parent as RectTransform;
            if (image != null) image.enabled = false; // hidden until the ball shows
        }

        private void LateUpdate()
        {
            if (image == null || ballRenderer == null || worldCamera == null || _parentRect == null) return;

            bool visible = ballRenderer.enabled && ballRenderer.gameObject.activeInHierarchy;
            if (image.enabled != visible) image.enabled = visible;
            if (!visible) return;

            Vector3 worldPos = ballRenderer.transform.position;
            float worldRadius = ballRenderer.sprite != null
                ? ballRenderer.sprite.bounds.extents.x * Mathf.Abs(ballRenderer.transform.lossyScale.x)
                : Mathf.Abs(ballRenderer.transform.lossyScale.x) * 0.5f;

            // Project center + one radius-offset point, convert both into the parent
            // rect's local space (overlay canvas → no camera), and size from the gap so
            // CanvasScaler resolution differences are handled for free.
            Vector2 centerScreen = worldCamera.WorldToScreenPoint(worldPos);
            Vector2 edgeScreen   = worldCamera.WorldToScreenPoint(worldPos + worldCamera.transform.right * worldRadius);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, centerScreen, null, out Vector2 centerLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, edgeScreen,   null, out Vector2 edgeLocal);

            float radiusLocal = Mathf.Abs(edgeLocal.x - centerLocal.x);
            _rt.anchoredPosition = centerLocal;
            _rt.sizeDelta = new Vector2(radiusLocal * 2f, radiusLocal * 2f);
        }
    }
}
