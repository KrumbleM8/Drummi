using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// "How to play" preview panel that floats above a globe-menu sign: a
    /// world-space Canvas + CanvasGroup whose only content is uGUI Images
    /// animated by a SignPreviewAnimation. One alpha channel fades the whole
    /// panel; the Canvas is disabled while hidden so a hidden panel costs
    /// nothing. The panel has NO GraphicRaycaster and every Image has
    /// raycastTarget off, so it can never eat sign taps or planet drags.
    ///
    /// Who shows it: FlashCardsMenuController decides (focused sign + globe
    /// settled + menu interactive) and calls MenuSignNode.SetPreviewShown,
    /// which forwards here unless the sign is locked. SetShown(true) waits
    /// showDelay before fading in so a quick flick through signs never
    /// flashes a panel; SetShown(false) cancels that delay, which is what
    /// makes the delay anti-flicker.
    ///
    /// Sits on a child of the MenuSign root that EXISTS IN THE SCENE (built
    /// by CoplayScripts/AddSignPreviews.cs) so PlanetSpinner's rim cache
    /// picks up the Canvas for horizon sorting — never instantiate at runtime.
    /// </summary>
    public class MenuSignPreview : MonoBehaviour
    {
        [Tooltip("CanvasGroup on this object — its alpha fades the whole panel.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("World-space Canvas on this object — disabled while hidden.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("Optional looping mini-animation; played while shown, stopped once hidden.")]
        [SerializeField] private SignPreviewAnimation previewAnimation;

        [Header("Timing (unscaled seconds)")]
        [Tooltip("Wait after the globe settles before fading in — quick flicks never flash a panel.")]
        [SerializeField, Min(0f)] private float showDelay = 0.2f;

        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.12f;

        /// <summary>Requested state (true from SetShown(true) even while the delay/fade runs).</summary>
        public bool IsShown => _shown;

        /// <summary>Current panel alpha (probes).</summary>
        public float Alpha => group != null ? group.alpha : 0f;

        /// <summary>True while the mini-animation loop is running (probes).</summary>
        public bool IsAnimating => previewAnimation != null && previewAnimation.IsPlaying;

        private bool  _shown;
        private Tween _delayTween;
        private Tween _fadeTween;

        private TweenCallback _beginShow;
        private TweenCallback _hideImmediate;

        private void Awake()
        {
            if (group  == null) group  = GetComponent<CanvasGroup>();
            if (canvas == null) canvas = GetComponent<Canvas>();

            _beginShow     = BeginShow;
            _hideImmediate = HideImmediate;

            if (group != null)
            {
                group.interactable   = false;
                group.blocksRaycasts = false;
            }
            HideImmediate();
        }

        private void OnDisable()
        {
            // Pivot hidden for a run / scene swap — come back hidden; the
            // controller re-requests visibility when the menu returns.
            HideImmediate();
            _shown = false;
        }

        /// <summary>Request the panel shown (after showDelay) or hidden (fade out). Idempotent.</summary>
        public void SetShown(bool shown)
        {
            if (shown)
            {
                if (!isActiveAndEnabled || _shown) return;
                _shown = true;

                KillTween(ref _fadeTween);
                KillTween(ref _delayTween);
                _delayTween = DOVirtual.DelayedCall(showDelay, _beginShow, ignoreTimeScale: true)
                    .SetLink(gameObject);
            }
            else
            {
                _shown = false;
                KillTween(ref _delayTween); // cancels a pending show — the anti-flicker path

                if (group == null || group.alpha <= 0.001f)
                {
                    HideImmediate();
                    return;
                }

                // Keep the animation running under the short fade, stop it after.
                KillTween(ref _fadeTween);
                _fadeTween = group.DOFade(0f, fadeOutDuration)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(_hideImmediate);
            }
        }

        /// <summary>Kill tweens, alpha 0, animation stopped, canvas off.</summary>
        public void HideImmediate()
        {
            KillTween(ref _delayTween);
            KillTween(ref _fadeTween);

            if (previewAnimation != null) previewAnimation.Stop();
            if (group  != null) group.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        private void BeginShow()
        {
            _delayTween = null;
            if (!_shown) return; // hidden again during the delay

            if (canvas != null) canvas.enabled = true;
            if (previewAnimation != null) previewAnimation.Play();

            KillTween(ref _fadeTween);
            if (group != null)
                _fadeTween = group.DOFade(1f, fadeInDuration)
                    .SetUpdate(true)
                    .SetLink(gameObject);
        }

        private static void KillTween(ref Tween tween)
        {
            if (tween == null) return;
            tween.Kill();
            tween = null;
        }
    }
}
