using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// One signpost on the planet rim in the FlashCardsMenu scene. Holds the
    /// variant definition it represents. The sign face carries a world-space
    /// Canvas Button that is ALWAYS tappable: FlashCardsMenuController spins a
    /// tapped distant sign into focus and launches on a focused-sign tap. A
    /// TapOrDragFilter alongside the button lets drags that start on the sign
    /// spin the planet instead of clicking. SetFocused drives a scale-up visual
    /// on the focused sign.
    /// </summary>
    public class MenuSignNode : MonoBehaviour
    {
        [Tooltip("The Flash Cards variant this sign launches.")]
        [SerializeField] private FlashCardsModeDefinition definition;

        [Tooltip("World-space button on the sign face.")]
        [SerializeField] private Button signButton;

        [Tooltip("Label on the sign face — set from the definition's displayName on Awake.")]
        [SerializeField] private TMP_Text label;

        [Header("Focus Visual (optional)")]
        [Tooltip("Transform scaled up while this sign is focused (defaults to this transform).")]
        [SerializeField] private Transform focusScaleTarget;

        [SerializeField, Min(1f)]    private float focusedScale = 1.15f;
        [SerializeField, Min(0.01f)] private float focusTweenDuration = 0.2f;

        /// <summary>Raised when the sign's button is pressed.</summary>
        public event Action<MenuSignNode> Selected;

        public FlashCardsModeDefinition Definition => definition;

        private Vector3 _baseScale;
        private bool    _focused;

        private void Awake()
        {
            if (label != null && definition != null)
                label.text = definition.displayName;

            if (signButton != null)
                signButton.onClick.AddListener(HandleClick);

            if (focusScaleTarget == null) focusScaleTarget = transform;
            _baseScale = focusScaleTarget.localScale;
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
            focusScaleTarget
                .DOScale(_baseScale * (focused ? focusedScale : 1f), focusTweenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
