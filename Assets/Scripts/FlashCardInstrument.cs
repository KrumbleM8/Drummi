using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// An instrument "skin" for the Flash Cards drum button: unique tap sound and
    /// button look, identical functionality. Selected on the menu (cycle button),
    /// persisted via PlayerPrefs, applied by FlashCardsModeController.
    /// </summary>
    [CreateAssetMenu(menuName = "Drummi/Flash Card Instrument")]
    public class FlashCardInstrument : ScriptableObject
    {
        [Tooltip("Shown on the instrument cycle button.")]
        public string displayName = "Bongo";

        [Tooltip("Tap-echo one-shot (misc of AudioManager speakers[1], same channel as the pad sounds). " +
                 "Null = AudioManager's default pad sound — use for the stock Bongo instrument.")]
        public AudioClip tapClip;

        [Tooltip("Optional sprite for the drum button Image. Null = keep the scene's sprite.")]
        public Sprite buttonSprite;

        [Tooltip("Tint for the drum button Image (the stock button is this tan).")]
        public Color buttonTint = new(0.85f, 0.68f, 0.42f, 1f);
    }
}
