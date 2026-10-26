using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// One signpost on the planet rim in the Home scene. Holds the
    /// variant definition it represents. The sign face carries a world-space
    /// Canvas Button that is ALWAYS tappable: FlashCardsMenuController spins a
    /// tapped distant sign into focus and launches on a focused-sign tap. A
    /// TapOrDragFilter alongside the button lets drags that start on the sign
    /// spin the planet instead of clicking. SetFocused drives a scale-up visual
    /// on the focused sign.
    /// </summary>
    public class MenuSignNode : MonoBehaviour
    {
        [Tooltip("What this sign launches: a Flash Cards variant (in-scene run) or a scene launch (e.g. Bongo).")]
        [SerializeField] private MenuSignDefinition definition;

        [Tooltip("World-space button on the sign face.")]
        [SerializeField] private Button signButton;

        [Tooltip("Label on the sign face — set from the definition's displayName on Awake.")]
        [SerializeField] private TMP_Text label;

        [Header("Focus Visual (optional)")]
        [Tooltip("Transform scaled up while this sign is focused (defaults to this transform).")]
        [SerializeField] private Transform focusScaleTarget;

        [SerializeField, Min(1f)]    private float focusedScale = 1.15f;
        [SerializeField, Min(0.01f)] private float focusTweenDuration = 0.2f;

        [Header("Lock")]
        [Tooltip("Locked until the PlayerPrefs unlock flag (highScoreKey + \"_SignUnlocked\") is set.")]
        [SerializeField] private bool lockedByDefault;

        [Tooltip("Multiplied into every sprite/label color while locked.")]
        [SerializeField] private Color lockedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

        [Header("Preview (optional)")]
        [Tooltip("How-to-play panel shown while this sign is focused and the globe is settled. Never shown while locked.")]
        [SerializeField] private MenuSignPreview preview;

        /// <summary>Raised when the sign's button is pressed.</summary>
        public event Action<MenuSignNode> Selected;

        public MenuSignDefinition Definition => definition;

        /// <summary>True while this sign's variant has not been unlocked.</summary>
        public bool IsLocked { get; private set; }

        private Vector3    _baseScale;
        private Quaternion _baseRotation;
        private bool       _focused;

        private SpriteRenderer[] _sprites;
        private Color[]          _spriteBaseColors;
        private Color            _labelBaseColor;
        private Color            _buttonBaseColor;

        private string UnlockKey =>
            definition != null ? definition.highScoreKey + "_SignUnlocked" : null;

        private void Awake()
        {
            IsLocked = lockedByDefault
                    && !(UnlockKey != null && PlayerPrefs.GetInt(UnlockKey, 0) == 1);

            if (label != null && definition != null)
                label.text = IsLocked ? "???" : definition.displayName;

            if (signButton != null)
                signButton.onClick.AddListener(HandleClick);

            if (focusScaleTarget == null) focusScaleTarget = transform;
            _baseScale    = focusScaleTarget.localScale;
            _baseRotation = focusScaleTarget.localRotation;

            CacheBaseColors();
            if (IsLocked) ApplyLockVisuals(true);
        }

        private void OnDestroy()
        {
            if (signButton != null)
                signButton.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick() => Selected?.Invoke(this);

        /// <summary>Focus visual only — every sign stays tappable (tap-to-snap).</summary>
        public void SetFocused(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;

            focusScaleTarget.DOKill();
            focusScaleTarget.localRotation = _baseRotation; // clear any interrupted shake
            focusScaleTarget
                .DOScale(_baseScale * (focused ? focusedScale : 1f), focusTweenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>
        /// Show/hide this sign's how-to-play preview (FlashCardsMenuController
        /// decides when: focused + globe settled + menu interactive). Locked
        /// signs never show theirs — the panel is built so Unlock() just works.
        /// </summary>
        public void SetPreviewShown(bool shown)
        {
            if (preview != null) preview.SetShown(shown && !IsLocked);
        }

        // ── Lock ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Unlocks this sign, saves the flag to PlayerPrefs, and restores the
        /// normal visuals. Call from the progression system when earned.
        /// </summary>
        public void Unlock()
        {
            if (!IsLocked) return;
            IsLocked = false;

            if (UnlockKey != null)
            {
                PlayerPrefs.SetInt(UnlockKey, 1);
                PlayerPrefs.Save();
            }

            if (label != null && definition != null)
                label.text = definition.displayName;

            ApplyLockVisuals(false);
        }

        /// <summary>Short rotation wobble — feedback for tapping a locked focused sign.</summary>
        public void PlayLockedShake()
        {
            // Rotation only, so it never fights the focus scale tween.
            focusScaleTarget.DOKill(true);
            focusScaleTarget.localRotation = _baseRotation;
            focusScaleTarget
                .DOPunchRotation(new Vector3(0f, 0f, 8f), 0.35f, vibrato: 12)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void CacheBaseColors()
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _spriteBaseColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _spriteBaseColors[i] = _sprites[i].color;

            if (label != null) _labelBaseColor = label.color;
            if (signButton != null && signButton.image != null)
                _buttonBaseColor = signButton.image.color;
        }

        private void ApplyLockVisuals(bool locked)
        {
            Color tint = locked ? lockedTint : Color.white;

            for (int i = 0; i < _sprites.Length; i++)
                if (_sprites[i] != null) _sprites[i].color = _spriteBaseColors[i] * tint;

            if (label != null) label.color = _labelBaseColor * tint;
            if (signButton != null && signButton.image != null)
                signButton.image.color = _buttonBaseColor * tint;
        }
    }
}
