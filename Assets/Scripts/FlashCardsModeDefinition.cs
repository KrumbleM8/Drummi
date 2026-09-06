using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Config for one Flash Cards variant, selected from the FlashCardsMenu
    /// signpost scene. A MenuSignNode carries one of these; on selection it is
    /// handed to FlashCardsModeController.PendingDefinition, which auto-starts
    /// the run in the FlashCards scene with these values.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFlashCardsMode", menuName = "Drummi/Flash Cards Mode Definition")]
    public class FlashCardsModeDefinition : ScriptableObject
    {
        [Tooltip("Name shown on the menu sign.")]
        public string displayName = "Flash Cards";

        [Tooltip("Beat-grid multiplier (1 = normal, 2 = Double Time). Keep musicBpm * multiplier a whole number — GameManager plumbs bpm as int.")]
        [Min(1f)] public float tempoMultiplier = 1f;

        [Tooltip("PlayerPrefs key for this variant's high score.")]
        public string highScoreKey = "FlashCardsHS";
    }
}
