using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// One signpost in the FlashCardsMenu scene. Holds the variant definition it
    /// represents and the authored camera anchor the menu camera tweens to when
    /// this sign is focused. The sign face carries a world-space Canvas Button;
    /// only the focused sign's button is interactable (SetFocused).
    /// </summary>
    public class MenuSignNode : MonoBehaviour
    {
        [Tooltip("Child transform whose position + rotation the menu camera adopts when this sign is focused.")]
        [SerializeField] private Transform cameraAnchor;

        [Tooltip("The Flash Cards variant this sign launches.")]
        [SerializeField] private FlashCardsModeDefinition definition;

        [Tooltip("World-space button on the sign face.")]
        [SerializeField] private Button signButton;

        [Tooltip("Label on the sign face — set from the definition's displayName on Awake.")]
        [SerializeField] private TMP_Text label;

        /// <summary>Raised when the sign's button is pressed.</summary>
        public event Action<MenuSignNode> Selected;

        public Transform CameraAnchor => cameraAnchor;
        public FlashCardsModeDefinition Definition => definition;

        private void Awake()
        {
            if (label != null && definition != null)
                label.text = definition.displayName;

            if (signButton != null)
                signButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (signButton != null)
                signButton.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick() => Selected?.Invoke(this);

        /// <summary>Only the focused sign accepts taps.</summary>
        public void SetFocused(bool focused)
        {
            if (signButton != null)
                signButton.interactable = focused;
        }
    }
}
