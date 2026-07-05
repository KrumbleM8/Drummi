using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Manages the locked/unlocked visual and interaction state of a menu bubble button.
    ///
    /// Locked state: smaller scale + locked sprite on the Icon child.
    /// Unlock state is persisted via PlayerPrefs using <see cref="unlockKey"/>.
    ///
    /// SCENE SETUP:
    ///   1. Add this component alongside UIBubbleFloat on the bubble root.
    ///   2. Assign sprites and configure unlockKey / startUnlocked in the Inspector.
    ///   3. Wire your EventTrigger PointerClick → BubbleButton.OnButtonTapped().
    ///   4. Wire onPress to whatever previously fired (e.g. UIMenuManager.GoToBongoScene).
    /// </summary>
    [RequireComponent(typeof(UIBubbleFloat))]
    public class BubbleButton : MonoBehaviour
    {
        [Header("Unlock")]
        [Tooltip("PlayerPrefs key used to persist unlock state. Leave empty to treat as always unlocked.")]
        [SerializeField] private string unlockKey;

        [Tooltip("If true, this button starts unlocked regardless of PlayerPrefs (e.g. the default game mode).")]
        [SerializeField] private bool startUnlocked = false;

        [Header("Visuals")]
        [Tooltip("Icon Image child. Auto-resolved from a child named 'Icon' if left empty.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Sprite displayed when the button is unlocked.")]
        [SerializeField] private Sprite unlockedSprite;

        [Tooltip("Sprite displayed when the button is locked.")]
        [SerializeField] private Sprite lockedSprite;

        [Tooltip("Scale multiplier applied to the bubble while locked (e.g. 0.75 = 75% of normal size).")]
        [Range(0.3f, 1f)]
        [SerializeField] private float lockedScale = 0.75f;

        [Header("Events")]
        [Tooltip("Fired when the button is tapped and is unlocked. Wire navigation here.")]
        [SerializeField] private UnityEvent onPress;

        // ── Private ───────────────────────────────────────────────────────────

        private UIBubbleFloat _bubbleFloat;
        private RectTransform _rt;
        private Vector3 _unlockedScale;
        private bool _isLocked;

        /// <summary>Whether this button is currently locked.</summary>
        public bool IsLocked => _isLocked;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _bubbleFloat = GetComponent<UIBubbleFloat>();

            // Capture the designer-set scale as the unlocked reference.
            _unlockedScale = _rt.localScale;

            if (iconImage == null)
            {
                var iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                    iconImage = iconTransform.GetComponent<Image>();
            }
        }

        private void Start()
        {
            bool unlocked = startUnlocked
                         || (!string.IsNullOrEmpty(unlockKey) && PlayerPrefs.GetInt(unlockKey, 0) == 1);

            ApplyState(locked: !unlocked);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Wire this to your EventTrigger PointerClick (or Button OnClick).
        /// Blocks the press when locked; fires <see cref="onPress"/> when unlocked.
        /// </summary>
        public void OnButtonTapped()
        {
            if (_isLocked)
            {
                // TODO: play locked feedback (e.g. wobble / locked sound)
                return;
            }

            onPress?.Invoke();
        }

        /// <summary>
        /// Unlocks this button and saves the state to PlayerPrefs.
        /// Call this from your progression system when the player earns access.
        /// </summary>
        public void Unlock()
        {
            if (!_isLocked) return;

            if (!string.IsNullOrEmpty(unlockKey))
            {
                PlayerPrefs.SetInt(unlockKey, 1);
                PlayerPrefs.Save();
            }

            ApplyState(locked: false);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyState(bool locked)
        {
            _isLocked = locked;

            Vector3 targetScale = locked ? _unlockedScale * lockedScale : _unlockedScale;
            _rt.localScale = targetScale;

            // Keep UIBubbleFloat's rest-scale in sync so the breathe animation
            // oscillates around the correct base rather than fighting the lock scale.
            _bubbleFloat.SetRestScale(targetScale);

            if (iconImage == null) return;

            Sprite sprite = locked ? lockedSprite : unlockedSprite;
            if (sprite != null)
                iconImage.sprite = sprite;
        }
    }
}
