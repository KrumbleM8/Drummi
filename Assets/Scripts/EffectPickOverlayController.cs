using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Placeholder pick overlay for dungeon effects: shows up to 3 cards, the player
    /// must tap exactly one (no skip/dismiss). Purely presentational — the caller
    /// (DungeonRunner) applies the picked effect and resumes room sequencing.
    /// Gameplay input is NOT touched here; it re-enables via the room-start path.
    /// </summary>
    public class EffectPickOverlayController : MonoBehaviour
    {
        /// <summary>One pre-built card in the overlay, wired in the Inspector.</summary>
        [Serializable]
        public class Card
        {
            [Tooltip("Card root — deactivated when the offer has fewer effects than cards.")]
            public GameObject root;
            public Button button;
            public TMP_Text nameText;
            public TMP_Text descriptionText;
            public TMP_Text durationText;
        }

        [Tooltip("Overlay root panel. Inactive except during a pick.")]
        [SerializeField] private GameObject overlayRoot;

        [Tooltip("Pre-built cards, max offer size. Populated left to right.")]
        [SerializeField] private Card[] cards = new Card[3];

        [Header("Animation")]
        [Tooltip("Pop-in duration per card, seconds. Purely cosmetic (UI tween, not gameplay timing).")]
        [SerializeField] private float cardPopDuration = 0.25f;

        [Tooltip("Delay between successive card pop-ins, seconds.")]
        [SerializeField] private float cardPopStagger = 0.08f;

        private Action<EffectDefinition> _onPicked;
        private bool _selectionMade;

        /// <summary>True while the overlay is waiting for a selection.</summary>
        public bool IsShowing => overlayRoot != null && overlayRoot.activeSelf;

        /// <summary>
        /// Shows the overlay with <paramref name="offer"/> (1–3 effects) and invokes
        /// <paramref name="onPicked"/> exactly once with the chosen effect, after the
        /// overlay has been dismissed. Caller must ensure the offer is non-empty.
        /// </summary>
        public void Show(List<EffectDefinition> offer, Action<EffectDefinition> onPicked)
        {
            if (overlayRoot == null)
            {
                Debug.LogError("[EffectPickOverlay] overlayRoot not assigned — auto-picking first effect");
                onPicked?.Invoke(offer[0]);
                return;
            }

            _onPicked      = onPicked;
            _selectionMade = false;
            overlayRoot.SetActive(true);

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card?.root == null) continue;

                if (i >= offer.Count)
                {
                    card.root.SetActive(false);
                    continue;
                }

                var effect = offer[i];
                card.root.SetActive(true);
                if (card.nameText        != null) card.nameText.text        = effect.DisplayName;
                if (card.descriptionText != null) card.descriptionText.text = effect.Description;
                if (card.durationText    != null) card.durationText.text    = effect.DurationText;

                if (card.button != null)
                {
                    card.button.onClick.RemoveAllListeners();
                    card.button.onClick.AddListener(() => HandleCardTapped(effect));
                    card.button.interactable = true;
                }

                // Cosmetic pop-in. Cards are reused across picks — kill stale tweens first.
                var t = card.root.transform;
                t.DOKill();
                t.localScale = Vector3.zero;
                t.DOScale(1f, cardPopDuration)
                    .SetDelay(i * cardPopStagger)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }

        private void HandleCardTapped(EffectDefinition effect)
        {
            // Guard double-taps during the same pick.
            if (_selectionMade) return;
            _selectionMade = true;

            foreach (var card in cards)
                if (card?.button != null) card.button.interactable = false;

            overlayRoot.SetActive(false);

            // Fire after dismissal so room sequencing resumes with the overlay gone.
            var cb = _onPicked;
            _onPicked = null;
            cb?.Invoke(effect);
        }
    }
}
