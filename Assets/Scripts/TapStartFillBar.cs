using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// "Tap to Start" fill bar: drum pad hits fill it, idle time drains it.
    /// Fires <see cref="onFilled"/> once when full, then disables itself.
    ///
    /// SCENE SETUP:
    ///   1. Assign the scene's DrumPadTouch and a filled-type Image.
    ///   2. Wire onFilled → HomeFlowController.StartGame.
    /// </summary>
    public class TapStartFillBar : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("DrumPadTouch that fires the hit events.")]
        [SerializeField] private DrumPadTouch drumPadTouch;

        [Tooltip("Image (type Filled) whose fillAmount displays the progress.")]
        [SerializeField] private Image fillImage;

        [Header("Tuning")]
        [Tooltip("Fill added per drum hit (0-1).")]
        [SerializeField] private float fillPerHit = 0.15f;

        [Tooltip("Fill drained per second while idle (0-1).")]
        [SerializeField] private float drainPerSecond = 0.1f;

        [Header("Events")]
        [Tooltip("Fired once when the bar reaches full.")]
        [SerializeField] private UnityEvent onFilled;

        // ── State ─────────────────────────────────────────────────────────────

        private float _fill;
        private bool  _done;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (drumPadTouch == null) return;
            drumPadTouch.OnLeftHit  += OnHit;
            drumPadTouch.OnRightHit += OnHit;
        }

        private void OnDisable()
        {
            if (drumPadTouch == null) return;
            drumPadTouch.OnLeftHit  -= OnHit;
            drumPadTouch.OnRightHit -= OnHit;
        }

        private void Update()
        {
            if (_done) return;

            _fill = Mathf.Max(0f, _fill - drainPerSecond * Time.deltaTime);
            UpdateVisual();
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private void OnHit()
        {
            if (_done) return;

            _fill = Mathf.Min(1f, _fill + fillPerHit);
            UpdateVisual();

            if (_fill >= 1f)
            {
                _done = true;
                onFilled?.Invoke();
                enabled = false;
            }
        }

        private void UpdateVisual()
        {
            if (fillImage != null)
                fillImage.fillAmount = _fill;
        }
    }
}
