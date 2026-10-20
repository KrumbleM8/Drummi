using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// A globe-menu sign that leaves the FlashCardsMenu scene and loads another
    /// scene (e.g. Bongo). The target scene is responsible for returning to
    /// "FlashCardsMenu" when the player exits.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneLaunchSign", menuName = "Drummi/Scene Launch Sign")]
    public class SceneLaunchDefinition : MenuSignDefinition
    {
        [Tooltip("Scene to load when the focused sign is tapped. Must be in Build Settings.")]
        public string sceneName = "Bongo";
    }
}
