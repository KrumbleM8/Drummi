using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Config for one Flash Cards variant, selected from the FlashCardsMenu
    /// globe. A MenuSignNode carries one of these; on selection the menu hands
    /// it to FlashCardsModeController.StartRun, which starts the run in-scene.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFlashCardsMode", menuName = "Drummi/Flash Cards Mode Definition")]
    public class FlashCardsModeDefinition : MenuSignDefinition
    {
        [Tooltip("Beat-grid multiplier (1 = normal, 2 = Double Time). Keep musicBpm * multiplier a whole number — GameManager plumbs bpm as int.")]
        [Min(1f)] public float tempoMultiplier = 1f;

        [Tooltip("Card presentation. Grid = classic memorize-then-perform card. ColumnScroll " +
                 "(Snake) = single vertical column of beat squares stepping down one slot per " +
                 "beat onto an enlarged bottom hit zone — same mechanics, scrolling look.")]
        public FlashCardLayout layout = FlashCardLayout.Grid;

        [Header("Rule Overrides")]
        [Tooltip("ON: this variant replaces the scene-authored escalation ladder and reveal rules with " +
                 "the values below (used by Classic). OFF: everything below is ignored and the scene's " +
                 "FlashCardsModeController / FlashCardController / FlashCardView settings apply.")]
        public bool overrideRules;

        [Tooltip("Escalation ladder for this variant (ascending minBlocksCleared; step 0 at 0). " +
                 "Only used when overrideRules is ON and the array is non-empty.")]
        public FlashCardsModeController.EscalationStep[] escalationSteps;

        [Tooltip("ON: the next card reveals in the on-deck zone while the current one performs (zero-gap " +
                 "continuous play). OFF: classic cycle — watch the full reveal, then perform, then the " +
                 "next card reveals.")]
        public bool pipelineReveal = true;

        [Tooltip("ON: reveal capped at revealBars bars (bigger cards pop faster). OFF: symmetric reveal " +
                 "= perform = one beat per slot.")]
        public bool compressReveal = true;

        [Tooltip("Reveal length in bars when compressReveal is ON.")]
        [Min(1)] public int revealBars = 1;

        [Tooltip("ON: classic reveal bars present in the on-deck zone and promote at perform start " +
                 "(GET READY telegraph). OFF: in-place reveal on the main card.")]
        public bool onDeckReveal = true;

        [Tooltip("ON: one grade (sound / score / streak) per CARD, like the original. OFF: one grade " +
                 "per 4-block row. Per-block HP damage and block visuals are the same either way.")]
        public bool wholeCardGrading;

        [Tooltip("ON: rests may sit on consecutive slots (original 2x4 placement — without this an " +
                 "8-slot card with 3–4 notes always lays out note-rest-note-rest). OFF: rests never touch.")]
        public bool allowAdjacentRests;
    }
}
