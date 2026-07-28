using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Generates a single Flash Card: 8 beat slots, each either a quarter note or a rest.
    /// Tier 1 — quarters and rests only; no eighth notes, no pad encoding
    /// (every note is retagged to a constant type by the caller, grading is timing-only).
    ///
    /// Rules:
    ///   - Slot 0 (beat 1) is always a note — the readability anchor.
    ///   - Total note count is random in [minNotes, maxNotes] (clamped to [1, 8]).
    ///   - Remaining notes are distributed uniformly over slots 1–7.
    ///
    /// Output shape matches DungeonPatternGenerator: a list of DungeonBeat with
    /// duration = 1f and integer timeSlots, so scheduling and evaluation are unchanged.
    /// Rest slots are simply absent from the list; the parallel noteMask reports
    /// all 8 slots for the reveal visuals.
    /// </summary>
    public class FlashCardPatternGenerator
    {
        /// <summary>Number of beat slots on a card (2 rows x 4 squares).</summary>
        public const int SlotCount = 8;

        /// <summary>
        /// Generates one card.
        /// </summary>
        /// <param name="minNotes">Minimum notes on the card (clamped to [1, 8]).</param>
        /// <param name="maxNotes">Maximum notes on the card (clamped to [minNotes, 8]).</param>
        /// <param name="constantType">Type stamped on every note (timing-only grading).</param>
        /// <param name="noteMask">
        /// Out: 8 entries, true = note in that slot, false = rest. Drives the reveal visuals.
        /// </param>
        public List<DungeonBeat> GenerateCard(
            int minNotes, int maxNotes, DungeonEnemyType constantType, out bool[] noteMask)
        {
            minNotes = Mathf.Clamp(minNotes, 1, SlotCount);
            maxNotes = Mathf.Clamp(maxNotes, minNotes, SlotCount);
            int noteCount = Random.Range(minNotes, maxNotes + 1);

            noteMask    = new bool[SlotCount];
            noteMask[0] = true; // beat 1 anchor

            // Fill the remaining (noteCount - 1) notes over slots 1–7 via partial shuffle.
            var freeSlots = new List<int>(SlotCount - 1);
            for (int s = 1; s < SlotCount; s++) freeSlots.Add(s);
            for (int i = 0; i < noteCount - 1 && freeSlots.Count > 0; i++)
            {
                int pick = Random.Range(0, freeSlots.Count);
                noteMask[freeSlots[pick]] = true;
                freeSlots.RemoveAt(pick);
            }

            var beats = new List<DungeonBeat>(noteCount);
            for (int s = 0; s < SlotCount; s++)
            {
                if (noteMask[s])
                    beats.Add(new DungeonBeat(1f, s, constantType));
            }
            return beats;
        }
    }
}
