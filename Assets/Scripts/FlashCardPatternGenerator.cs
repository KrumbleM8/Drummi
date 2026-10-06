using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Generates a single Flash Card: slotCount beat slots (multiple of 4 — one
    /// visual row per 4 beats; default 8), each stamped with one
    /// SquareTemplate from the authored vocabulary. A square is always exactly one
    /// beat; subdivided notes sit at fractional slots (eighths +0.5, sixteenths
    /// +0.25 steps, triplets +1/3 steps) with matching durations. No pad encoding —
    /// every note is retagged to a constant type by the caller, grading is timing-only.
    ///
    /// Rules:
    ///   - Slot 0 anchor: always a downbeat-bearing template
    ///     (Quarter | EighthPair | SixteenthQuad | Triplet) — the readability anchor.
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
        /// <summary>Default number of beat slots on a card (2 rows x 4 squares) — dungeon rooms always use this.</summary>
        public const int SlotCount = 8;

        /// <summary>Beats per visual row — rows always stay 4/4 bars.</summary>
        public const int SlotsPerRow = 4;

        // Reused scratch pools — generator is single-threaded, one card at a time.
        private readonly List<SquareTemplate> _notePool   = new(4);
        private readonly List<SquareTemplate> _anchorPool = new(3);
        private readonly List<int>            _freeSlots  = new(SlotCount - 1);

        /// <summary>
        /// Generates one card.
        /// </summary>
        /// <param name="slotCount">
        /// Beat slots on this card (multiple of 4; 8 = classic 2x4, 12 = 3 rows,
        /// 20 = 5 rows). Endless-mode escalation grows this; dungeon rooms pass
        /// <see cref="SlotCount"/>.
        /// </param>
        /// <param name="minNotes">Minimum note-bearing squares (clamped to [1, slotCount]).</param>
        /// <param name="maxNotes">Maximum note-bearing squares (clamped to [minNotes, slotCount]).</param>
        /// <param name="constantType">Type stamped on every note (timing-only grading).</param>
        /// <param name="vocab">
        /// Template vocabulary. Must contain at least one note-bearing flag;
        /// warns and falls back to Quarter otherwise. Callers resolve the None
        /// sentinel to their Inspector default BEFORE calling.
        /// </param>
        /// <param name="templates">
        /// Out: slotCount entries, the template of each square. Drives the reveal visuals.
        /// </param>
        public List<DungeonBeat> GenerateCard(
            int slotCount, int minNotes, int maxNotes, DungeonEnemyType constantType,
            FlashCardVocab vocab, out SquareTemplate[] templates)
        {
            BuildPools(vocab);

            slotCount = Mathf.Max(SlotsPerRow, slotCount);
            bool hasRest = (vocab & FlashCardVocab.Rest) != 0;
            minNotes = Mathf.Clamp(minNotes, 1, slotCount);
            maxNotes = Mathf.Clamp(maxNotes, minNotes, slotCount);
            if (!hasRest) { minNotes = slotCount; maxNotes = slotCount; }
            int noteCount = Random.Range(minNotes, maxNotes + 1);

            templates = new SquareTemplate[slotCount];
            for (int s = 0; s < slotCount; s++) templates[s] = SquareTemplate.Rest;

            // Slot 0 anchor: downbeat-bearing template only.
            templates[0] = _anchorPool[Random.Range(0, _anchorPool.Count)];

            // Distribute the remaining (noteCount - 1) note-bearing squares over
            // slots 1..slotCount-1 via partial shuffle. Row-start slots (s % 4 == 0)
            // draw from the anchor pool so each visual row opens readably WHEN it
            // opens with a note at all; other slots draw uniformly from the note pool.
            _freeSlots.Clear();
            for (int s = 1; s < slotCount; s++) _freeSlots.Add(s);
            for (int i = 0; i < noteCount - 1 && _freeSlots.Count > 0; i++)
            {
                int pick = Random.Range(0, _freeSlots.Count);
                int slot = _freeSlots[pick];
                var pool = slot % SlotsPerRow == 0 ? _anchorPool : _notePool;
                templates[slot] = pool[Random.Range(0, pool.Count)];
                _freeSlots.RemoveAt(pick);
            }

            // Emit beats. Durations are cosmetic (evaluation uses only the scheduled
            // time); subdivided notes sit at fractional slots. Triplet thirds as
            // float lose ~1e-8 beat (~nanoseconds at any BPM) — far below timing
            // precision, a non-issue (Phase 6 recon).
            const float Third = 1f / 3f;
            var beats = new List<DungeonBeat>(noteCount * 4);
            for (int s = 0; s < slotCount; s++)
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
                    case SquareTemplate.OffbeatEighth:
                        beats.Add(new DungeonBeat(0.5f, s + 0.5f, constantType));
                        break;
                    case SquareTemplate.SixteenthQuad:
                        beats.Add(new DungeonBeat(0.25f, s,         constantType));
                        beats.Add(new DungeonBeat(0.25f, s + 0.25f, constantType));
                        beats.Add(new DungeonBeat(0.25f, s + 0.5f,  constantType));
                        beats.Add(new DungeonBeat(0.25f, s + 0.75f, constantType));
                        break;
                    case SquareTemplate.Triplet:
                        beats.Add(new DungeonBeat(Third, s,             constantType));
                        beats.Add(new DungeonBeat(Third, s + Third,     constantType));
                        beats.Add(new DungeonBeat(Third, s + 2f * Third, constantType));
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
            if ((vocab & FlashCardVocab.Quarter)       != 0) _notePool.Add(SquareTemplate.Quarter);
            if ((vocab & FlashCardVocab.EighthPair)    != 0) _notePool.Add(SquareTemplate.EighthPair);
            if ((vocab & FlashCardVocab.OffbeatEighth) != 0) _notePool.Add(SquareTemplate.OffbeatEighth);
            if ((vocab & FlashCardVocab.SixteenthQuad) != 0) _notePool.Add(SquareTemplate.SixteenthQuad);
            if ((vocab & FlashCardVocab.Triplet)       != 0) _notePool.Add(SquareTemplate.Triplet);
            // Bit 1 << 2 (was DownbeatEighth) is reserved and intentionally never read.

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
