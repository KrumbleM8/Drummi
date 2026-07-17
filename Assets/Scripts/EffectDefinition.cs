using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>How long a run effect stays active once picked.</summary>
    public enum EffectDuration
    {
        /// <summary>Active until the run ends.</summary>
        Run,
        /// <summary>Active until the current floor is left.</summary>
        Floor,
        /// <summary>Active for <see cref="EffectDefinition.RoomCount"/> room starts.</summary>
        NRooms
    }

    /// <summary>
    /// The gameplay parameter a single modifier entry tweaks. Systems read the
    /// aggregate of all active modifiers via DungeonEffectsController queries.
    /// </summary>
    public enum EffectModifierType
    {
        /// <summary>Scales the Perfect/Good timing windows (multipliers multiply across effects).</summary>
        TimingWindowMultiplier,
        /// <summary>Hides slider spawn markers while active (value ignored; bools OR across effects).</summary>
        HideSliderMarkers
    }

    /// <summary>One parameter tweak inside an effect. Effects may carry several.</summary>
    [System.Serializable]
    public class EffectModifier
    {
        [Tooltip("Which gameplay parameter this entry tweaks.")]
        [SerializeField] private EffectModifierType type;

        [Tooltip("Modifier value. TimingWindowMultiplier: window scale (e.g. 0.75). HideSliderMarkers: ignored.")]
        [SerializeField] private float value = 1f;

        public EffectModifierType Type => type;
        public float Value => value;
    }

    /// <summary>
    /// A pickable run modifier ("effect") for Drummi Dungeons. Effects are
    /// challenges, not buffs: after every Nth completed room the player must pick
    /// one from a random offer. Applied and queried via DungeonEffectsController.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEffect", menuName = "Drummi Dungeons/Effect Definition")]
    public class EffectDefinition : ScriptableObject
    {
        [Header("Presentation")]
        [Tooltip("Name shown on the pick card.")]
        [SerializeField] private string displayName;

        [Tooltip("Short description shown on the pick card.")]
        [SerializeField][TextArea] private string description;

        [Header("Duration")]
        [Tooltip("How long the effect stays active once picked.")]
        [SerializeField] private EffectDuration duration = EffectDuration.Run;

        [Tooltip("Number of rooms the effect lasts. Only used when Duration is NRooms. Counts the rooms in which the effect is active, starting with the next room.")]
        [SerializeField] private int roomCount = 3;

        [Header("Modifiers")]
        [Tooltip("Parameter tweaks this effect applies while active.")]
        [SerializeField] private List<EffectModifier> modifiers = new List<EffectModifier>();

        /// <summary>Name shown on the pick card.</summary>
        public string DisplayName => displayName;

        /// <summary>Short description shown on the pick card.</summary>
        public string Description => description;

        /// <summary>How long the effect stays active once picked.</summary>
        public EffectDuration Duration => duration;

        /// <summary>Rooms the effect lasts when <see cref="Duration"/> is NRooms.</summary>
        public int RoomCount => roomCount;

        /// <summary>Parameter tweaks this effect applies while active.</summary>
        public IReadOnlyList<EffectModifier> Modifiers => modifiers;

        /// <summary>Human-readable duration for the pick card ("Whole run", "This floor", "3 rooms").</summary>
        public string DurationText => duration switch
        {
            EffectDuration.Run    => "Whole run",
            EffectDuration.Floor  => "This floor",
            EffectDuration.NRooms => roomCount == 1 ? "1 room" : $"{roomCount} rooms",
            _                     => string.Empty
        };
    }
}
