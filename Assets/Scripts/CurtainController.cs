using System.Collections;
using UnityEngine;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// On Dungeon scene load, a full-screen curtain panel covers all layers below
    /// DecorativeOverlayCanvas. Each drum-pad tap applies an upward impulse; gravity
    /// continuously pulls the curtain back down. Once the curtain rises past
    /// <see cref="revealThreshold"/> the IntroMenu GameObject is revealed and the
    /// curtain is dismissed.
    /// </summary>
    public class CurtainController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The RectTransform of the curtain panel (child of DecorativeOverlayCanvas).")]
        [SerializeField] private RectTransform curtainRect;

        [Tooltip("DrumPadTouch that fires the hit events.")]
        [SerializeField] private DrumPadTouch drumPadTouch;

        [Tooltip("The ObjectToSpawn root GameObject to reveal when threshold is reached.")]
        [SerializeField] private GameObject ObjectToSpawn;

        [Header("Physics")]
        [Tooltip("Upward impulse added per drum hit (canvas units / second).")]
        [SerializeField] private float pushForce = 200f;

        [Tooltip("Downward acceleration (canvas units / second²).")]
        [SerializeField] private float gravity = 350f;

        [Tooltip("Maximum upward speed cap (canvas units / second).")]
        [SerializeField] private float maxUpSpeed = 1000f;

        [Header("Reveal")]
        [Tooltip("How far up the curtain must travel (canvas units) before ObjectToSpawn is shown.")]
        [SerializeField] private float revealThreshold = 650f;

        // ── State ─────────────────────────────────────────────────────────────

        private float _velocity;   // positive = upward
        private float _curtainY;   // current anchoredPosition.y (0 = fully covering)
        private bool  _active;

        // ── Unity ─────────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Yield one frame so UIMenuManager.Start() has already activated the Intro page,
            // then immediately hide it behind the curtain.
            yield return null;

            if (ObjectToSpawn != null)
                ObjectToSpawn.SetActive(false);

            SubscribeInput();
            _active = true;
        }

        private void OnDestroy() => UnsubscribeInput();

        private void Update()
        {
            if (!_active) return;

            // Apply gravity
            _velocity -= gravity * Time.deltaTime;
            _velocity  = Mathf.Min(_velocity, maxUpSpeed);   // cap upward speed

            // Integrate position
            _curtainY += _velocity * Time.deltaTime;

            // Floor: curtain cannot drop below its starting (fully covering) position
            if (_curtainY <= 0f)
            {
                _curtainY = 0f;
                if (_velocity < 0f) _velocity = 0f;
            }

            curtainRect.anchoredPosition = new Vector2(0f, _curtainY);

            // Reveal check
            if (_curtainY >= revealThreshold)
                ActivateReveal();
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private void SubscribeInput()
        {
            if (drumPadTouch == null) return;
            drumPadTouch.OnLeftHit   += OnHit;
            drumPadTouch.OnCenterHit += OnHit;
            drumPadTouch.OnRightHit  += OnHit;
        }

        private void UnsubscribeInput()
        {
            if (drumPadTouch == null) return;
            drumPadTouch.OnLeftHit   -= OnHit;
            drumPadTouch.OnCenterHit -= OnHit;
            drumPadTouch.OnRightHit  -= OnHit;
        }

        private void OnHit()
        {
            if (!_active) return;
            _velocity += pushForce;
        }

        // ── Reveal ────────────────────────────────────────────────────────────

        private void ActivateReveal()
        {
            _active = false;
            UnsubscribeInput();

            if (ObjectToSpawn != null)
                ObjectToSpawn.SetActive(true);

            // Disable the curtain object — no further rendering or Update cost.
            curtainRect.gameObject.SetActive(false);
        }
    }
}
