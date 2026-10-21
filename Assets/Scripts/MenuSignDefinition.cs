using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Base config for one sign on the Home globe. A MenuSignNode
    /// carries one of these; FlashCardsMenuController decides what a tap does
    /// from the concrete type (FlashCardsModeDefinition = in-scene run,
    /// SceneLaunchDefinition = load another scene).
    /// </summary>
    public abstract class MenuSignDefinition : ScriptableObject
    {
        [Tooltip("Name shown on the menu sign.")]
        public string displayName = "Flash Cards";

        [Tooltip("PlayerPrefs key for this sign's high score. Also the root of the sign's unlock " +
                 "flag (highScoreKey + \"_SignUnlocked\") — keep it unique per sign.")]
        public string highScoreKey = "FlashCardsHS";
    }
}
