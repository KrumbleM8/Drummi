using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Generates a single Flash Card: slotCount beat slots (multiple of 4 — one
    /// visual row per 4 beats; default 8), each stamped with one
    /// SquareTemplate from the authored vocabulary. A square is exactly one beat
    /// (HalfRest: half a beat); subdivided notes sit at fractional slots (eighths
    /// +0.5, sixteenths +0.25 steps, triplets +1/3 steps) with matching durations.
    /// No pad encoding — every note is retagged to a constant type by the caller,
    /// grading is timing-only.
    ///
    /// Rules:
    ///   - Slot 0 anchor: always a downbeat-bearing template
    ///     (Quarter | EighthPair | SixteenthQuad | Triplet) — the readability anchor.
    ///   - noteRange [minNotes, maxNotes] counts NOTE-BEARING SQUARES (not taps),
    ///     preserving the meaning of existing authored assets. Clamped to [1, 8].
    ///   - When the vocabulary excludes Rest, every square is note-bearing (range
    ///     collapses to 8).
    ///   - Rests are never consecutive: rest slots are drawn uniformly from the
    ///     non-adjacent combinations of slots 1..slotCount-1, and noteCount is
    ///     raised when a roll would demand more rests than can fit (> slotCount/2).
    ///   - Every other square is note-bearing and draws its template uniformly
    ///     from the vocabulary's note pool.
    ///   - HalfRest (when in vocab): after placement, at most ONE placed Rest is
    ///     converted to HalfRest with probability halfRestChance. It inherits the
    ///     Rest rules (never slot 0, never adjacent to another rest, counts toward
    ///     the rest quota) and shortens the card by half a beat — all later slots
    ///     shift onto the off-beats.
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

        /// <summary>
        /// ON: rests may land on consecutive slots and the note count is not raised
        /// to keep them apart — the ORIGINAL 2x4 placement (Classic variant). With
        /// the default non-adjacent rule an 8-slot card with 3–4 notes has exactly
        /// one legal layout (note-rest alternating), which reads as "always the
        /// same card". HalfRest episodes are skipped while ON (they rely on the
        /// non-adjacent guarantee). Set by the owning controller.
        /// </summary>
        public bool allowAdjacentRests = false;

        /// <summary>
        /// Chance that a card whose vocab contains HalfRest converts one of its
        /// placed Rests into a HalfRest. Set by the owning controller.
        /// </summary>
        public float halfRestChance = 0.35f;

        /// <summary>
        /// ON-BEAT cards required after an episode resolves before a new one can
        /// start, so the player settles into the normal grid between episodes.
        /// Counted in generation order, which equals play order (preview → next →
        /// current chain). Set by the owning controller.
        /// </summary>
        public int halfRestCooldownCards = 2;

        /// <summary>
        /// Fully off-beat cards between an episode's starting half-rest card and
        /// the FORCED resolving half-rest card. 1 = flip, one off-beat card, flip
        /// back — a tight, learnable episode shape.
        /// </summary>
        public int offbeatCardsPerEpisode = 1;

        // ── Episode state machine (generation order) ──
        // Idle        → cooldown counts; an eligible card may roll the STARTING
        //               half-rest (episode begins, play skips in half a beat early).
        // Offbeat     → emit offbeatCardsPerEpisode cards with NO flip rest.
        // ResolveNext → the next card is FORCED to carry the resolving half-rest —
        //               the IDENTICAL half-beat-skip gesture as the opener (never
        //               the final slot) — plus an APPENDED plain-Rest balancing
        //               slot, making the card slotCount+1 slots / slotCount+0.5
        //               beats. Episode total is net-zero in time, so bar/BGM
        //               alignment never drifts. Vocab / rest availability
        //               notwithstanding — resolution integrity beats composition
        //               rules. Then back to Idle.
        private enum EpisodePhase { Idle, Offbeat, ResolveNext }
        private EpisodePhase _episode = EpisodePhase.Idle;
        private int _offbeatEmitted;

        /// <summary>
        /// While true, Idle never STARTS a new episode (in-flight episodes still
        /// resolve normally). Set by the controller the moment a stage-up is
        /// queued — a speed-up must never have fresh off-beat segments stacked in
        /// front of it; cleared by ResetEpisode when the stage applies.
        /// </summary>
        public bool suppressNewEpisodes;

        // Generation-order counter for the cooldown. Starts satisfied so the first
        // eligible card can roll.
        private int _cardsSinceHalfRest = int.MaxValue;

        /// <summary>
        /// Controller hook: the next generated card MUST resolve the phase (carry
        /// the resolving half-rest + balancing slot), regardless of the machine's
        /// current position. Used when a stage-up is held waiting for an on-beat
        /// boundary after the pre-generated resolver was discarded.
        /// </summary>
        public void ForceResolveNext() => _episode = EpisodePhase.ResolveNext;

        /// <summary>
        /// Controller hook: hard-reset to on-beat idle with the cooldown running —
        /// called when a stage-up applies (play is on-beat by then and any
        /// pre-generated episode cards were discarded with the preview).
        /// </summary>
        public void ResetEpisode()
        {
            _episode            = EpisodePhase.Idle;
            _offbeatEmitted     = 0;
            _cardsSinceHalfRest = 0;
            suppressNewEpisodes = false;
        }

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

            // Rests must never land on consecutive slots (default rule). Slot 0 is
            // always a note, so at most slotCount/2 rests fit among slots
            // 1..slotCount-1 without two touching — raise noteCount when the roll
            // would exceed that. allowAdjacentRests (Classic) keeps the rolled count.
            if (!allowAdjacentRests)
                noteCount = Mathf.Max(noteCount, slotCount - slotCount / 2);
            int restCount = slotCount - noteCount;

            templates = new SquareTemplate[slotCount];

            if (allowAdjacentRests)
            {
                // Original placement: rests drawn uniformly from slots 1..slotCount-1
                // with no adjacency constraint (partial Fisher–Yates over the slots).
                _freeSlots.Clear();
                for (int v = 1; v < slotCount; v++) _freeSlots.Add(v);
                for (int i = 0; i < restCount; i++)
                {
                    int pick = Random.Range(i, _freeSlots.Count);
                    (_freeSlots[i], _freeSlots[pick]) = (_freeSlots[pick], _freeSlots[i]);
                }
                for (int i = 0; i < restCount; i++)
                    templates[_freeSlots[i]] = SquareTemplate.Rest;
            }
            else
            {
                // Place the rests: draw uniformly over all non-adjacent combinations of
                // slots 1..slotCount-1 (gap method — sample a plain combination from a
                // range shrunk by the mandatory one-slot gaps, then re-expand each
                // sorted pick by its index).
                _freeSlots.Clear();
                for (int v = 0; v < slotCount - restCount; v++) _freeSlots.Add(v);
                for (int i = 0; i < restCount; i++)
                {
                    int pick = Random.Range(i, _freeSlots.Count);
                    (_freeSlots[i], _freeSlots[pick]) = (_freeSlots[pick], _freeSlots[i]);
                }
                _freeSlots.Sort(0, restCount, null);
                for (int i = 0; i < restCount; i++)
                    templates[1 + _freeSlots[i] + i] = SquareTemplate.Rest;
            }

            // Fill every non-rest slot. Slot 0 anchor: downbeat-bearing template
            // only; other row-start slots (s % 4 == 0) also draw from the anchor
            // pool so each visual row opens readably WHEN it opens with a note at
            // all; remaining slots draw uniformly from the note pool.
            templates[0] = _anchorPool[Random.Range(0, _anchorPool.Count)];
            for (int s = 1; s < slotCount; s++)
            {
                if (templates[s] == SquareTemplate.Rest) continue;
                var pool = s % SlotsPerRow == 0 ? _anchorPool : _notePool;
                templates[s] = pool[Random.Range(0, pool.Count)];
            }

            // Off-beat episode machine. The starting HALF-REST converts ONE placed
            // Rest (inheriting every Rest rule for free — never slot 0, never
            // adjacent to another rest, counts toward the rest quota, max one per
            // card). The resolver is the IDENTICAL gesture — a forced half-rest,
            // kept out of the final slot so the appended balancing Rest below
            // never fuses with it into an uncountable 1.5-beat gap.
            bool appendBalance = false;
            switch (_episode)
            {
                case EpisodePhase.Idle:
                    bool started = false;
                    if (!suppressNewEpisodes && !allowAdjacentRests &&
                        (vocab & FlashCardVocab.HalfRest) != 0 &&
                        _cardsSinceHalfRest >= halfRestCooldownCards &&
                        Random.value < halfRestChance)
                    {
                        started = ConvertRest(templates, slotCount, SquareTemplate.HalfRest);
                    }
                    if (started)
                    {
                        _episode        = EpisodePhase.Offbeat;
                        _offbeatEmitted = 0;
                    }
                    else if (_cardsSinceHalfRest != int.MaxValue)
                    {
                        _cardsSinceHalfRest++;
                    }
                    break;

                case EpisodePhase.Offbeat:
                    // Fully off-beat card — no flip rest here by design.
                    _offbeatEmitted++;
                    if (_offbeatEmitted >= offbeatCardsPerEpisode) _episode = EpisodePhase.ResolveNext;
                    break;

                case EpisodePhase.ResolveNext:
                    if (!ConvertRest(templates, slotCount - 1, SquareTemplate.HalfRest))
                    {
                        // No eligible Rest — resolution integrity wins: convert a
                        // random non-anchor, non-final slot outright (drops a note).
                        templates[Random.Range(1, slotCount - 1)] = SquareTemplate.HalfRest;
                    }
                    appendBalance       = true;
                    _episode            = EpisodePhase.Idle;
                    _offbeatEmitted     = 0;
                    _cardsSinceHalfRest = 0;
                    break;
            }

            // Balancing slot: the resolver card ends with one plain, countable Rest
            // beat — it gives back the whole beat the episode's two half-skips
            // removed, so play lands back ON the original bar grid.
            if (appendBalance)
            {
                var withBalance = new SquareTemplate[slotCount + 1];
                System.Array.Copy(templates, withBalance, slotCount);
                withBalance[slotCount] = SquareTemplate.Rest;
                templates = withBalance;
            }

            // Emit beats. Durations are cosmetic (evaluation uses only the scheduled
            // time); subdivided notes sit at fractional slots. Triplet thirds as
            // float lose ~1e-8 beat (~nanoseconds at any BPM) — far below timing
            // precision, a non-issue (Phase 6 recon).
            const float Third = 1f / 3f;
            var beats = new List<DungeonBeat>(noteCount * 4);
            float cursor = 0f; // slot start in beats — diverges from s after a HalfRest
            for (int s = 0; s < templates.Length; s++) // Length may be slotCount+1 (balancing slot)
            {
                float t = cursor;
                switch (templates[s])
                {
                    case SquareTemplate.Quarter:
                        beats.Add(new DungeonBeat(1f, t, constantType));
                        break;
                    case SquareTemplate.EighthPair:
                        beats.Add(new DungeonBeat(0.5f, t,        constantType));
                        beats.Add(new DungeonBeat(0.5f, t + 0.5f, constantType));
                        break;
                    case SquareTemplate.OffbeatEighth:
                        beats.Add(new DungeonBeat(0.5f, t + 0.5f, constantType));
                        break;
                    case SquareTemplate.SixteenthQuad:
                        beats.Add(new DungeonBeat(0.25f, t,         constantType));
                        beats.Add(new DungeonBeat(0.25f, t + 0.25f, constantType));
                        beats.Add(new DungeonBeat(0.25f, t + 0.5f,  constantType));
                        beats.Add(new DungeonBeat(0.25f, t + 0.75f, constantType));
                        break;
                    case SquareTemplate.Triplet:
                        beats.Add(new DungeonBeat(Third, t,             constantType));
                        beats.Add(new DungeonBeat(Third, t + Third,     constantType));
                        beats.Add(new DungeonBeat(Third, t + 2f * Third, constantType));
                        break;
                    // SquareTemplate.Rest / HalfRest: no beats.
                }
                cursor += templates[s].BeatLength();
            }
            return beats;
        }

        /// <summary>
        /// Converts one random placed Rest in slots [1, maxSlotExclusive) to
        /// <paramref name="to"/>. False when no eligible Rest exists.
        /// </summary>
        private static bool ConvertRest(SquareTemplate[] templates, int maxSlotExclusive, SquareTemplate to)
        {
            int eligible = 0;
            for (int s = 1; s < maxSlotExclusive; s++)
                if (templates[s] == SquareTemplate.Rest) eligible++;
            if (eligible == 0) return false;

            int target = Random.Range(0, eligible);
            for (int s = 1; s < maxSlotExclusive; s++)
            {
                if (templates[s] != SquareTemplate.Rest) continue;
                if (target-- == 0) { templates[s] = to; return true; }
            }
            return false;
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
