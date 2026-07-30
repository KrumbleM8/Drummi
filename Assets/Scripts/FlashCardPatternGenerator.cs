using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Generates a single Flash Card: 8 beat slots, each stamped with one
    /// SquareTemplate from the authored vocabulary. A square is always exactly one
    /// beat; eighth notes sit at slot + 0.5 with duration 0.5f. No pad encoding —
    /// every note is retagged to a constant type by the caller, grading is timing-only.
    ///
    /// Rules:
    ///   - Slot 0 anchor: always a downbeat-bearing template
    ///     (Quarter | EighthPair | DownbeatEighth) — the readability anchor.
    ///   - noteRange [minNotes, maxNotes] counts NOTE-BEARING SQUARES (not taps),
    ///     preserving the meaning of existing authored assets. Clamped to [1, 8].
    ///   - When the vocabulary excludes Rest, every square is note-bearing (range
    ///     collapses to 8).
    ///   - Note-bearing squares beyond slot 0 are distributed uniformly over slots
    ///     1–7; each draws its template uniformly from the vocabulary's note pool.
    ///
    /// Output shape matches DungeonPatternGenerator: a list of DungeonBeat whose
    /// timeSlots may be half-integral (offbeat eighths), so scheduling and
    /// evaluation are unchanged (both are float/double-clean). Rest positions are
    /// simply absent from the list; the parallel templates array reports all 8
    /// slots for the reveal visuals.
    /// </summary>
    public class FlashCardPatternGenerator
    {
        /// <summary>Number of beat slots on a card (2 rows x 4 squares).</summary>
        public const int SlotCount = 8;

        // Reused scratch pools — generator is single-threaded, one card at a time.
        private readonly List<SquareTemplate> _notePool   = new(4);
        private readonly List<SquareTemplate> _anchorPool = new(3);
        private readonly List<int>            _freeSlots  = new(SlotCount - 1);

        /// <summary>
        /// Generates one card.
        /// </summary>
        /// <param name="minNotes">Minimum note-bearing squares (clamped to [1, 8]).</param>
        /// <param name="maxNotes">Maximum note-bearing squares (clamped to [minNotes, 8]).</param>
        /// <param name="constantType">Type stamped on every note (timing-only grading).</param>
        /// <param name="vocab">
        /// Template vocabulary. Must contain at least one note-bearing flag;
        /// warns and falls back to Quarter otherwise. Callers resolve the None
        /// sentinel to their Inspector default BEFORE calling.
        /// </param>
        /// <param name="templates">
        /// Out: 8 entries, the template of each square. Drives the reveal visuals.
        /// </param>
        public List<DungeonBeat> GenerateCard(
            int minNotes, int maxNotes, DungeonEnemyType constantType,
            FlashCardVocab vocab, out SquareTemplate[] templates)
        {
            BuildPools(vocab);

            bool hasRest = (vocab & FlashCardVocab.Rest) != 0;
            minNotes = Mathf.Clamp(minNotes, 1, SlotCount);
            maxNotes = Mathf.Clamp(maxNotes, minNotes, SlotCount);
            if (!hasRest) { minNotes = SlotCount; maxNotes = SlotCount; }
            int noteCount = Random.Range(minNotes, maxNotes + 1);

            templates = new SquareTemplate[SlotCount];
            for (int s = 0; s < SlotCount; s++) templates[s] = SquareTemplate.Rest;

            // Slot 0 anchor: downbeat-bearing template only.
            templates[0] = _anchorPool[Random.Range(0, _anchorPool.Count)];

            // Distribute the remaining (noteCount - 1) note-bearing squares over
            // slots 1–7 via partial shuffle; each draws uniformly from the note pool.
            _freeSlots.Clear();
            for (int s = 1; s < SlotCount; s++) _freeSlots.Add(s);
            for (int i = 0; i < noteCount - 1 && _freeSlots.Count > 0; i++)
            {
                int pick = Random.Range(0, _freeSlots.Count);
                templates[_freeSlots[pick]] = _notePool[Random.Range(0, _notePool.Count)];
                _freeSlots.RemoveAt(pick);
            }

            // Emit beats. Eighths carry duration 0.5f (cosmetic — evaluation uses
            // only the scheduled time) and offbeats sit at slot + 0.5.
            var beats = new List<DungeonBeat>(noteCount * 2);
            for (int s = 0; s < SlotCount; s++)
            {
                switch (templates[s])
                {
                    case SquareTemplate.Quarter:
                        beats.Add(new DungeonBeat(1f, s, constantType));
                        break;
                    case SquareTemplate.EighthPair:
                        beats.Add(new DungeonBeat(0.5f, s,        constantType));
                        beats.Add(new DungeonBeat(0.5f, s + 0.5f, constantType));
                        break;
                    case SquareTemplate.DownbeatEighth:
                        beats.Add(new DungeonBeat(0.5f, s, constantType));
                        break;
                    case SquareTemplate.OffbeatEighth:
                        beats.Add(new DungeonBeat(0.5f, s + 0.5f, constantType));
                        break;
                    // SquareTemplate.Rest: no beats.
                }
            }
            return beats;
        }

        /// <summary>
        /// Resolves the vocabulary into the note-template pool and the slot-0
        /// anchor pool (downbeat-bearing subset). Warns and falls back to Quarter
        /// when a pool would be empty — a card must always be performable and must
        /// always open with a downbeat note.
        /// </summary>
        private void BuildPools(FlashCardVocab vocab)
        {
            _notePool.Clear();
            if ((vocab & FlashCardVocab.Quarter)        != 0) _notePool.Add(SquareTemplate.Quarter);
            if ((vocab & FlashCardVocab.EighthPair)     != 0) _notePool.Add(SquareTemplate.EighthPair);
            if ((vocab & FlashCardVocab.DownbeatEighth) != 0) _notePool.Add(SquareTemplate.DownbeatEighth);
            if ((vocab & FlashCardVocab.OffbeatEighth)  != 0) _notePool.Add(SquareTemplate.OffbeatEighth);

            if (_notePool.Count == 0)
            {
                Debug.LogWarning($"[FlashCardGen] Vocabulary '{vocab}' has no note-bearing template — falling back to Quarter.");
                _notePool.Add(SquareTemplate.Quarter);
            }

            _anchorPool.Clear();
            foreach (var t in _notePool)
                if (t.HasDownbeatNote()) _anchorPool.Add(t);

            if (_anchorPool.Count == 0)
            {
                Debug.LogWarning($"[FlashCardGen] Vocabulary '{vocab}' has no downbeat-bearing template for the " +
                                 "slot-0 anchor — falling back to Quarter for slot 0.");
                _anchorPool.Add(SquareTemplate.Quarter);
            }
        }
    }
}
