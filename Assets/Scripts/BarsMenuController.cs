using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Slides the horizontal game-mode bars in from off-screen, top to bottom,
    /// alternating sides (even indices from the left, odd from the right).
    /// Bars keep their designer-set positions as the settle targets.
    ///
    /// SCENE SETUP:
    ///   1. Attach to the BarsMenu root (starts inactive).
    ///   2. Add each bar RectTransform to <see cref="bars"/> in top-to-bottom order.
    /// </summary>
    public class BarsMenuController : MonoBehaviour
    {
        [Header("Bars (top to bottom)")]
        [SerializeField] private List<RectTransform> bars = new List<RectTransform>();

        [Header("Tuning")]
        [Tooltip("Slide duration per bar (seconds).")]
        [SerializeField] private float slideDuration = 0.5f;

        [Tooltip("Delay between successive bars (seconds).")]
        [SerializeField] private float stagger = 0.12f;

        [Tooltip("Extra off-screen padding so bars are fully hidden before entering.")]
        [SerializeField] private float offscreenPadding = 100f;

        /// <summary>Activates the menu root and plays the staggered slide-in.</summary>
        public void PlayEntrance()
        {
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();

            var canvas = GetComponentInParent<Canvas>();
            float canvasWidth = canvas != null
                ? ((RectTransform)canvas.transform).rect.width
                : 800f;

            for (int i = 0; i < bars.Count; i++)
            {
                RectTransform bar = bars[i];
                if (bar == null) continue;

                Vector2 target = bar.anchoredPosition;
                float side = (i % 2 == 0) ? -1f : 1f;
                float offDistance = canvasWidth + bar.rect.width * 0.5f + offscreenPadding;

                bar.anchoredPosition = new Vector2(target.x + side * offDistance, target.y);
                bar.DOAnchorPosX(target.x, slideDuration)
                   .SetEase(Ease.OutBack)
                   .SetDelay(i * stagger);
            }
        }
    }
}
