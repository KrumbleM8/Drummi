using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Phrase-clock coordinator for Flash Card encounters — sight-reading rooms where
    /// the "call" is visual instead of audible.
    ///
    /// Each card is one 2-bar cycle of a fixed 8-beat phrase (N = 8):
    ///   Reveal bar  — on each of the 8 beats a card square flips SILENTLY, showing
    ///                 note or rest. No call audio is ever scheduled. Input is gated
    ///                 off (allowInput = false); taps are silently ignored, except the
    ///                 coyote window just before the perform bar (same pre-arm as C&amp;R).
    ///   Perform bar — the player performs the revealed rhythm from beat 1. Graded by
    ///                 DungeonEvaluator.EvaluateBar with timeOffset = 8 * beatInterval.
    ///                 Taps landing in rests fall outside every note's Good window and
    ///                 take the out-of-window penalty. This holds down to half-beat
    ///                 (eighth) spacing: rests sit >= 0.5 beat from the nearest note,
    ///                 beyond the Good threshold (0.199 beat shipped; guard warns if
    ///                 tuning ever reaches 0.25 — see CheckSubdivisionTuningGuard).
    /// Cards chain zero-gap: next card's reveal starts exactly at this card's
    /// evaluation boundary (PhraseTiming.NextPhraseStart), no accumulated drift.
    ///
    /// PIPELINED REVEAL (endless runs only): the next card is pre-built at each
    /// perform start and reveals beat-by-beat in the view's ON-DECK zone during
    /// the current perform window's final stretch, so its ResponseStart coincides
    /// with the current EvaluationTime — the player rolls straight from one card
    /// into the next with no rest bar (OnNextCardRevealStart / OnNextSquareReveal
    /// / OnCardHandoff). The only remaining breathers are stage-up breaks, which
    /// intentionally fall back to the classic break + standalone reveal (a stage
    /// queued while the next card is already pre-built therefore lands one card
    /// late). Dungeon rooms keep the classic watch-then-play cycle unchanged.
    ///
    /// Structurally a sibling of CallAndResponseController: same GameClock virtual-time
    /// round cycle (PhraseTiming), same DungeonInputReader gating contract
    /// (EncounterActive + allowInput + CoyoteWindowStartOverride), same evaluator
    /// pipeline. Differences: N is fixed at 8, phrases are sparse (rests), the per-beat
    /// callback drives a visual reveal for ALL 8 slots (rests flip too), and no
    /// CallPerformer / call audio exists at all.
    ///
    /// Visuals: FlashCardView subscribes to the reveal/perform events and renders the
    /// 2x4 card. The controller stays visual-free — beat accuracy comes from the
    /// virtual-time polling here; the view's tweens are cosmetic only.
    ///
    /// INSPECTOR SETUP:
    ///   metronome      — shared Metronome (Metronome.bpm)
    ///   modeController — DungeonModeController (session beat-grid origin)
    ///   inputReader    — DungeonInputReader (gates managed per-phase)
    ///   evaluator      — DungeonEvaluator (grades each perform bar)
    ///   (debug) press F at runtime to start a test encounter with Inspector defaults
    /// </summary>
    public class FlashCardController : MonoBehaviour
    {
        #region Inspector
        [Header("Timing Source")]
        [Tooltip("Shared Metronome — provides Metronome.bpm (same source as DungeonBeatManager).")]
        [SerializeField] private Metronome metronome;

        [Tooltip("DungeonModeController — provides SessionStartVirtualTime for beat-grid snap.")]
        [SerializeField] private DungeonModeController modeController;

        [Header("Encounter Systems")]
        [Tooltip("DungeonInputReader — allowInput and EncounterActive are managed per-phase; SuppressSpawnMarkers is held true for the whole encounter.")]
        [SerializeField] private DungeonInputReader inputReader;

        [Tooltip("DungeonEvaluator — grades the player's performance at each card's evaluation boundary.")]
        [SerializeField] private DungeonEvaluator evaluator;

        [Header("Card Config")]
        [Tooltip("Cards per encounter. The encounter completes after this many reveal/perform cycles.")]
        [SerializeField, Min(1)] private int cardsPerEncounter = 4;

        [Tooltip("Minimum notes per card (beat 1 is always a note).")]
        [SerializeField, Range(1, 20)] private int minNotes = 3;

        [Tooltip("Maximum notes per card.")]
        [SerializeField, Range(1, 20)] private int maxNotes = 6;

        [Tooltip("Default template vocabulary when the room authors None. " +
                 "Quarter | Rest ≡ pre-eighths behavior (quarters and full rests only).")]
        [SerializeField] private FlashCardVocab vocabulary = FlashCardVocab.Quarter | FlashCardVocab.Rest;

        [Header("Reveal Compression (dev toggle)")]
        [Tooltip("ON: the reveal phase is capped at revealBars bars — bigger cards pop their squares " +
                 "proportionally faster, so the wait never grows with row count. OFF: classic symmetric " +
                 "timing (reveal = perform = N beats). Identical for 8-slot cards either way.")]
        [SerializeField] private bool compressReveal = true;

        [Tooltip("Reveal length in bars (4 beats each) when compression is ON. 1 = every card reveals in " +
                 "one bar (expanded cards pop double-time-style); 2 = the classic 8-beat reveal.")]
        [SerializeField, Min(1)] private int revealBars = 1;

        [Header("Per-Block Judging (endless)")]
        [Tooltip("Seconds past a slot's end boundary before it is judged. Covers same-frame input/Update " +
                 "ordering races only — the eighths tuning guard keeps every note window closed before the " +
                 "slot boundary, so no gameplay grace is needed here.")]
        [SerializeField, Min(0f)] private float blockJudgeGraceSeconds = 0.05f;

        #endregion

        #region Events
        /// <summary>Fired when a card's reveal bar begins. Parameter: cardIndex.</summary>
        public event Action<int> OnCardRevealStart;

        /// <summary>
        /// Fired as each card square reveals, on its beat.
        /// Parameters: cardIndex, slot (0–7), template (rhythmic content of the square).
        /// </summary>
        public event Action<int, int, SquareTemplate> OnSquareReveal;

        /// <summary>Fired when a card's perform bar begins. Parameter: cardIndex.</summary>
        public event Action<int> OnPerformStart;

        /// <summary>
        /// Pipelined reveal (endless runs): fired when the NEXT card starts revealing
        /// in the on-deck zone DURING the current card's perform window.
        /// Parameters: next cardIndex, its slot count, its tempo multiplier.
        /// </summary>
        public event Action<int, int, float> OnNextCardRevealStart;

        /// <summary>
        /// Pipelined reveal: a next-card square flips in the on-deck zone, on its beat.
        /// Parameters: next cardIndex, slot, template.
        /// </summary>
        public event Action<int, int, SquareTemplate> OnNextSquareReveal;

        /// <summary>
        /// Pipelined handoff: the pre-revealed next card just became the CURRENT card
        /// at the zero-gap boundary — its perform window is already open. Fired instead
        /// of OnCardRevealStart/OnSquareReveal; OnPerformStart follows immediately.
        /// Parameter: cardIndex.
        /// </summary>
        public event Action<int> OnCardHandoff;

        /// <summary>
        /// Fired after a card's perform bar is graded.
        /// Parameters: cardIndex, result (null when evaluation was skipped, e.g. paused).
        /// Endless runs: the result is a synthesized aggregate of the per-block
        /// judgements (PointsAwarded = 0 — points were already applied per bar group).
        /// </summary>
        public event Action<int, EvaluationResult> OnCardEvaluated;

        /// <summary>
        /// Endless only: a slot's judging window closed. Parameters: cardIndex, slot,
        /// cleared (true when every note of the slot's template was hit in-window;
        /// rests are always cleared — miss-taps on them are charged via OnPlayerMissTap).
        /// </summary>
        public event Action<int, int, bool> OnBlockJudged;

        /// <summary>
        /// Endless only: a bar group (4 consecutive blocks, plus any shorter remainder
        /// at the card end) finished judging. Carries the group grade for sounds,
        /// score, and streak — the per-block replacement for the old whole-card grade.
        /// Parameters: cardIndex, judgement.
        /// </summary>
        public event Action<int, BarJudgement> OnBarJudged;

        /// <summary>Aggregate judgement of one bar group (see OnBarJudged).</summary>
        public readonly struct BarJudgement
        {
            public readonly int GroupIndex;     // 0-based group within the card
            public readonly int BlocksInGroup;  // 4, or the remainder at card end
            public readonly int MissedBlocks;
            public readonly int NoteCount;      // scheduled notes across the group
            public readonly int PerfectNotes;
            public readonly int StrayTaps;
            public readonly EvaluationGrade Grade;

            public BarJudgement(int groupIndex, int blocksInGroup, int missedBlocks,
                                int noteCount, int perfectNotes, int strayTaps, EvaluationGrade grade)
            {
                GroupIndex    = groupIndex;
                BlocksInGroup = blocksInGroup;
                MissedBlocks  = missedBlocks;
                NoteCount     = noteCount;
                PerfectNotes  = perfectNotes;
                StrayTaps     = strayTaps;
                Grade         = grade;
            }
        }

        /// <summary>
        /// Fired on a confirmed Perfect/Good tap during the perform bar.
        /// Parameters: slot (0–7) of the matched note's square, match quality.
        /// </summary>
        public event Action<int, InputMatch.MatchQuality> OnPlayerHit;

        /// <summary>
        /// Fired on an out-of-window tap during the perform bar.
        /// Parameter: slot (0–7) nearest the tap time.
        /// </summary>
        public event Action<int> OnPlayerMissTap;

        /// <summary>Fired once, after the final card's evaluation completes.</summary>
        public event Action OnEncounterComplete;

        /// <summary>
        /// Fired at a stage-up boundary (endless ladder tempo change), when the
        /// telegraphed break begins. Parameters: new tempo multiplier (relative to
        /// run-start BPM), break start virtual time, break length in beats, new
        /// base beat interval in seconds. The next card reveals at
        /// breakStart + breakBeats * beatInterval.
        /// </summary>
        public event Action<float, double, int, double> OnStageUp;

        /// <summary>
        /// Fired when the encounter is stopped externally mid-cycle (death, room interrupt).
        /// NOT fired on normal completion — subscribers use OnEncounterComplete for that.
        /// Lets the card view tear down cleanly without polling encounter state.
        /// </summary>
        public event Action OnEncounterStopped;
        #endregion

        #region State
        // Per-card slot count (N). Config (_activeSlotCount) is applied to the card
        // at the same boundary as the tempo roll — StartCore / AdvanceCard — so a
        // ladder change lands on exactly the next card, never mid-card. Dungeon
        // rooms always run the classic 8 (2 rows x 4).
        private int _cardSlotCount   = FlashCardPatternGenerator.SlotCount;
        private int _activeSlotCount = FlashCardPatternGenerator.SlotCount;

        // Reveal phase length of the current card, in card-beats. Equals
        // _cardSlotCount when compression is off (symmetric), else capped at
        // revealBars bars. Squares pop at _revealSlotInterval spacing so all
        // _cardSlotCount pops always fit the reveal window.
        private int    _cardRevealBeats = FlashCardPatternGenerator.SlotCount;
        private double _revealSlotInterval;

        // Card expansion (endless mode): occasionally a card is bigger than the
        // step's base size. 0 expand-slots = never. expandEvery > 0 = strict
        // every-Nth cadence; otherwise each card rolls expandChance. The authored
        // noteRange describes the BASE size — expanded cards scale it by the row
        // ratio so note DENSITY stays constant (_cardMinNotes/_cardMaxNotes).
        private int   _activeExpandSlotCount;
        private float _activeExpandChance;
        private int   _activeExpandEvery;
        private int   _cardsSinceExpand;
        private int   _cardMinNotes;
        private int   _cardMaxNotes;

        private bool         _running;
        private int          _cardIndex;
        private PhraseTiming _current;
        private bool         _revealFired;
        private bool         _performFired;
        private int          _revealSlot;   // next slot to reveal, 0–8

        // The BASE grid: metronome.bpm as it was at run start (already includes any
        // run-wide variant multiplier, e.g. the Double Time menu variant's 2x).
        // Stages (endless ladder) retune the base grid at a card boundary:
        // _baseBpm = _runBpm * stageMultiplier; _runBpm itself never changes
        // during an encounter, so stage multipliers are absolute, not compounding.
        private double _runBpm;
        private double _baseBpm;
        private double _baseBeatInterval;

        // Pending stage change (QueueStageTempo): applied in AdvanceCard at the
        // next card boundary, like SetCardConfig. 0 = nothing pending.
        private float _pendingStageMultiplier;
        private int   _pendingStageBreakBeats;

        // Per-card tempo (in-run Double Time cards): rolled at each card boundary.
        // _cardBeatInterval = _baseBeatInterval / _cardTempoMultiplier. metronome.bpm
        // is retuned alongside so the evaluator/reader grading windows (which read
        // bpm live) scale with the card — without this, a 2x card's windows would
        // break the geometric rest-enforcement guard (see CheckSubdivisionTuningGuard).
        // Retuning bpm mid-run is safe: Metronome caches timePerTick only in
        // InitializeWithStartTime, so the audible tick grid and BGM are untouched.
        private double _cardBeatInterval;
        private float  _cardTempoMultiplier = 1f;
        private float  _activeDoubleTimeChance;

        private FlashCardPatternGenerator _cardGenerator;

        // Current card: scheduled notes in reveal-bar virtual times + full 8-slot templates.
        private List<DungeonScheduledBeat> _cardBeats;
        private SquareTemplate[]           _templates;

        // ── Pipelined reveal (endless): the NEXT card is rolled + built at the
        // current card's perform start and revealed in the on-deck zone during the
        // perform window's final reveal-length stretch, so play is continuous —
        // the boundary promotes it with zero gap (no standalone reveal bar).
        // Skipped while a stage-up is pending: the stage keeps its telegraphed
        // break + classic reveal bar (the breather is intentional), which also
        // means a stage queued while a card is already pre-built lands one card
        // later than in the classic flow.
        private struct CardSpec
        {
            public float  tempoMultiplier;
            public double beatInterval;
            public int    slotCount;
            public int    minNotes;
            public int    maxNotes;
            public int    revealBeats;
            public double revealSlotInterval;
        }

        private bool         _nextPrepared;
        private bool         _nextRevealStarted;
        private int          _nextRevealSlot;
        private double       _nextRevealOrigin;   // visual reveal start, clamped to the current perform start
        private CardSpec     _nextSpec;
        private PhraseTiming _nextTiming;
        private List<DungeonScheduledBeat> _nextBeats;
        private SquareTemplate[]           _nextTemplates;

        // Preview buffer: the card AFTER next, rolled+generated one boundary
        // early so the column-scroll view keeps a constant on-screen stream —
        // new bars always spawn above the screen top and only enter by
        // scrolling. Pattern only (relative beats + templates); absolute
        // scheduling waits until PrepareNextCard consumes it. Invalidated on
        // stage-ups (generated under pre-stage config) and at run start.
        private bool              _previewValid;
        private CardSpec          _previewSpec;
        private List<DungeonBeat> _previewPattern;
        private SquareTemplate[]  _previewTemplates;

        // One warning per encounter for the eighths tuning guard (see BuildCard).
        private bool _tuningGuardWarned;

        // Constant type stamped on all notes and echo inputs — Tier 1 is rhythm-only
        // (any pad counts; grading is timing-only), same collapse as SingleDrum C&R.
        private const DungeonEnemyType ConstantType = DungeonEnemyType.Center;

        // Resolved at encounter start: room-authored config overrides Inspector defaults
        // (same pattern as CallAndResponseController._activePhraseLengths).
        private int            _activeCards;
        private int            _activeMinNotes;
        private int            _activeMaxNotes;
        private FlashCardVocab _activeVocab;

        // Endless mode (standalone scene): AdvanceCard never terminates; the host
        // stops the run via StopEncounter. False for all dungeon-room encounters.
        private bool _endless;

        // ── Per-block judging (endless only) ─────────────────────────────────
        // Each slot is judged individually the moment its window closes (slot end
        // + blockJudgeGraceSeconds) instead of batch-grading the card. Hits are
        // CONSUMED against scheduled notes so a double-tap can't credit two slots.
        // Rebuilt by InitBlockJudging() whenever a card becomes current (reveal
        // branch and pipelined handoff). All arrays are null outside endless runs.
        private bool[] _slotJudged;    // window closed + OnBlockJudged fired
        private int[]  _slotExpected;  // scheduled notes per slot (rest = 0)
        private int[]  _slotHits;      // consumed in-window hits per slot
        private int[]  _slotPerfect;   // of which Perfect
        private int[]  _slotStrays;    // reader miss-taps attributed to the slot
        private bool[] _beatConsumed;  // parallel to _cardBeats
        private int[]  _beatSlot;      // parallel to _cardBeats: host slot
        private int    _nextJudgeSlot; // monotonic judging cursor
        private int    _groupStart;    // first slot of the currently judging bar group

        // Bar-group size for sounds/score/streak — one visual row.
        private const int BarGroupSize = FlashCardPatternGenerator.SlotsPerRow;
        #endregion

        #region Public Properties
        /// <summary>True while an encounter is running.</summary>
        public bool IsRunning => _running;

        /// <summary>The scheduled notes of the current card (reveal-bar virtual times). Null before the first reveal.</summary>
        public IReadOnlyList<DungeonScheduledBeat> CurrentCardBeats => _cardBeats;

        /// <summary>8-slot templates of the current card. Null before the first reveal.</summary>
        public IReadOnlyList<SquareTemplate> CurrentTemplates => _templates;

        /// <summary>Timing boundaries of the current card cycle.</summary>
        public PhraseTiming CurrentTiming => _current;

        /// <summary>
        /// Tempo multiplier of the CURRENT card (1 = normal, 2 = in-run Double Time).
        /// Valid from OnCardRevealStart; the view reads this for the telegraph.
        /// </summary>
        public float CurrentCardTempoMultiplier => _cardTempoMultiplier;

        /// <summary>
        /// Seconds between consecutive square reveals on the current card — shorter
        /// than the perform-bar beat interval when the reveal is compressed.
        /// </summary>
        public double CurrentRevealSlotInterval => _revealSlotInterval;

        /// <summary>
        /// Templates of the pre-built NEXT card (pipelined flow). Valid from
        /// OnNextCardRevealStart until the handoff; null/stale otherwise. The
        /// column-scroll view deals the whole upcoming card at once from this.
        /// </summary>
        public IReadOnlyList<SquareTemplate> NextTemplates => _nextPrepared ? _nextTemplates : null;

        /// <summary>
        /// Templates of the card AFTER next (preview buffer) — pattern-only
        /// lookahead for the column-scroll stream. Null while a stage-up is
        /// pending: the previewed card would be discarded for a fresh post-break
        /// roll, so it must never be shown to the player.
        /// </summary>
        public IReadOnlyList<SquareTemplate> PreviewTemplates
            => _previewValid && _pendingStageMultiplier <= 0f ? _previewTemplates : null;
        #endregion

        #region Public API
        /// <summary>
        /// Starts a Flash Card encounter.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Current virtual time (GameClock.GameTime) near which the first reveal bar should
        /// start. Snapped forward to the next beat on the session grid before use — pass
        /// DungeonModeController.EncounterStartVirtualTime from a room, where the snap is a no-op.
        /// </param>
        public void StartEncounter(double startVirtualTime)
            => StartEncounter(startVirtualTime, 0, Vector2Int.zero, FlashCardVocab.None);

        /// <summary>
        /// Starts a Flash Card encounter using the room's authored configuration.
        /// Called by RoomController when a RoomType.FlashCard room begins.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Current virtual time (GameClock.GameTime) near which the first reveal bar should
        /// start. Snapped forward to the next beat on the session grid before use.
        /// </param>
        /// <param name="cardCount">
        /// Number of cards for this encounter (derived from RoomDefinition.RoomLengthBars).
        /// 0 or negative = use the Inspector cardsPerEncounter default.
        /// </param>
        /// <param name="noteRange">
        /// Authored note-count range (x = min, y = max) from RoomDefinition.FlashCardNoteRange.
        /// Counts NOTE-BEARING SQUARES, not taps. (0,0) sentinel = use the Inspector
        /// minNotes/maxNotes defaults.
        /// </param>
        /// <param name="vocab">
        /// Authored template vocabulary from RoomDefinition.FlashCardVocabulary.
        /// None sentinel = use the Inspector vocabulary default (Quarter | Rest).
        /// </param>
        public void StartEncounter(double startVirtualTime, int cardCount, Vector2Int noteRange, FlashCardVocab vocab)
            => StartCore(startVirtualTime, cardCount, noteRange, vocab, gridOriginOverride: null, endless: false);

        /// <summary>
        /// Starts an ENDLESS encounter — cards chain until <see cref="StopEncounter"/>
        /// is called; OnEncounterComplete never fires. Host (FlashCardsModeController)
        /// counts strikes/streaks via OnCardEvaluated and stops the run itself.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Virtual time of the first reveal bar. Must lie ON the grid defined by
        /// <paramref name="gridOriginVirtualTime"/> (e.g. origin + whole lead-in bars) —
        /// the snap is then a no-op.
        /// </param>
        /// <param name="gridOriginVirtualTime">
        /// Beat-grid origin (the virtual time music started). Replaces
        /// DungeonModeController.SessionStartVirtualTime, which does not exist in the
        /// standalone scene.
        /// </param>
        /// <param name="vocab">Initial vocabulary (None = Inspector default).</param>
        /// <param name="noteRange">Initial note range ((0,0) = Inspector defaults).</param>
        /// <param name="doubleTimeChance">
        /// Initial probability [0,1] that a card is an in-run Double Time card (whole
        /// reveal/perform cycle at 2x the base grid). 0 = never (dungeon rooms).
        /// </param>
        /// <param name="slotCount">
        /// Initial beat slots per card (multiple of 4; one visual row per 4 beats).
        /// 0 or negative = the classic 8 (2 rows x 4).
        /// </param>
        /// <param name="expandSlotCount">
        /// Slots of an occasional EXPANDED card (0 or &lt;= slotCount = never expand).
        /// </param>
        /// <param name="expandChance">Per-card expansion probability [0,1] when expandEvery is 0.</param>
        /// <param name="expandEvery">&gt; 0 = strict cadence: every Nth card expands instead of rolling.</param>
        public void StartEndless(double startVirtualTime, double gridOriginVirtualTime, FlashCardVocab vocab, Vector2Int noteRange,
                                 float doubleTimeChance = 0f, int slotCount = 0, int expandSlotCount = 0, float expandChance = 0f, int expandEvery = 0)
            => StartCore(startVirtualTime, 0, noteRange, vocab, gridOriginVirtualTime, endless: true, doubleTimeChance, slotCount,
                         expandSlotCount, expandChance, expandEvery);

        /// <summary>
        /// Updates the generation config used for SUBSEQUENT cards. The current card
        /// (revealed or performing) is untouched — BuildCard reads these at each
        /// reveal-bar start, so changes made from an OnCardEvaluated handler land on
        /// exactly the next card. Used by the endless mode's escalation ladder.
        /// </summary>
        public void SetCardConfig(FlashCardVocab vocab, Vector2Int noteRange, float doubleTimeChance = 0f, int slotCount = 0,
                                  int expandSlotCount = 0, float expandChance = 0f, int expandEvery = 0)
        {
            _activeDoubleTimeChance = Mathf.Clamp01(doubleTimeChance);
            _activeSlotCount        = ResolveSlotCount(slotCount);
            _activeExpandSlotCount  = expandSlotCount;
            _activeExpandChance     = Mathf.Clamp01(expandChance);
            _activeExpandEvery      = Mathf.Max(0, expandEvery);
            bool hasRange   = noteRange.x > 0 && noteRange.y > 0;
            _activeMinNotes = hasRange ? noteRange.x : minNotes;
            _activeMaxNotes = hasRange ? noteRange.y : maxNotes;
            if (_activeMaxNotes < _activeMinNotes)
            {
                Debug.LogWarning($"[FlashCard] SetCardConfig: max ({_activeMaxNotes}) < min ({_activeMinNotes}) — clamping max to min.");
                _activeMaxNotes = _activeMinNotes;
            }
            _activeVocab = vocab != FlashCardVocab.None ? vocab : vocabulary;
            Debug.Log($"[FlashCard] Card config → notes={_activeMinNotes}-{_activeMaxNotes} vocab={_activeVocab} " +
                      $"dtChance={_activeDoubleTimeChance:F2} slots={_activeSlotCount} (applies from next card)");
        }

        /// <summary>
        /// Queues a stage tempo change (endless escalation ladder). At the next card
        /// boundary the BASE grid is retuned to runStartBpm * multiplier and a
        /// breakBeats-long telegraphed break — counted at the NEW tempo — is inserted
        /// before the next card (OnStageUp fires at the break start). Same next-card
        /// contract as SetCardConfig. No-op outside endless runs.
        /// </summary>
        public void QueueStageTempo(float multiplier, int breakBeats)
        {
            if (!_endless)
            {
                Debug.LogWarning("[FlashCard] QueueStageTempo ignored — not an endless run.");
                return;
            }
            _pendingStageMultiplier = Mathf.Max(1f, multiplier);
            _pendingStageBreakBeats = Mathf.Max(0, breakBeats);
            Debug.Log($"[FlashCard] Stage tempo queued → x{_pendingStageMultiplier:F2} " +
                      $"({_runBpm * _pendingStageMultiplier:F1} BPM), break={_pendingStageBreakBeats} beats (applies at next card boundary)");
        }

        /// <summary>0 or negative = classic 8; otherwise clamped up to one full row (4).</summary>
        private static int ResolveSlotCount(int slotCount)
            => slotCount <= 0 ? FlashCardPatternGenerator.SlotCount
                              : Mathf.Max(FlashCardPatternGenerator.SlotsPerRow, slotCount);

        private void StartCore(double startVirtualTime, int cardCount, Vector2Int noteRange, FlashCardVocab vocab,
                               double? gridOriginOverride, bool endless, float doubleTimeChance = 0f, int slotCount = 0,
                               int expandSlotCount = 0, float expandChance = 0f, int expandEvery = 0)
        {
            if (metronome == null)
            {
                Debug.LogError("[FlashCard] Metronome not assigned — cannot start encounter.");
                return;
            }

            // Stop any previous encounter cleanly before initialising the new one.
            if (_running) TerminateEncounter();

            // Resolve active config: room-authored overrides Inspector defaults.
            _activeCards    = cardCount > 0 ? cardCount : cardsPerEncounter;
            bool hasRange   = noteRange.x > 0 && noteRange.y > 0;
            _activeMinNotes = hasRange ? noteRange.x : minNotes;
            _activeMaxNotes = hasRange ? noteRange.y : maxNotes;
            if (_activeMaxNotes < _activeMinNotes)
            {
                Debug.LogWarning($"[FlashCard] Note range max ({_activeMaxNotes}) < min ({_activeMinNotes}) — clamping max to min.");
                _activeMaxNotes = _activeMinNotes;
            }
            _activeVocab = vocab != FlashCardVocab.None ? vocab : vocabulary;
            _endless     = endless;

            _activeDoubleTimeChance = Mathf.Clamp01(doubleTimeChance);
            _activeSlotCount        = ResolveSlotCount(slotCount);
            _activeExpandSlotCount  = expandSlotCount;
            _activeExpandChance     = Mathf.Clamp01(expandChance);
            _activeExpandEvery      = Mathf.Max(0, expandEvery);
            _cardsSinceExpand       = 0;
            _runBpm                 = metronome.bpm;
            _baseBpm                = _runBpm;
            _baseBeatInterval       = 60.0 / _baseBpm;
            _pendingStageMultiplier = 0f;
            _previewValid           = false;
            _previewPattern         = null;
            RollNextCardTempo();
            _cardGenerator     = new FlashCardPatternGenerator();
            _cardIndex         = 0;
            _running           = true;
            _revealFired       = false;
            _performFired      = false;
            _revealSlot        = 0;
            _cardBeats         = null;
            _templates         = null;
            _tuningGuardWarned = false;
            ClearNextCard();

            // Gate input off for the reveal bar. SuppressSpawnMarkers is held true for
            // the whole encounter — the standard bar slider is BarTiming-coupled and
            // would place markers at wrong positions.
            if (inputReader != null)
            {
                inputReader.allowInput                = false;
                inputReader.EncounterActive           = false;
                inputReader.CoyoteWindowStartOverride = null;
                inputReader.SuppressSpawnMarkers      = true;
                inputReader.OnHit                    += HitCallback;
                inputReader.OnMissTap                += MissCallback;
            }

            // ── Beat-grid snap (same approach as CallAndResponseController) ──
            // Origin priority: explicit override (endless host) > DungeonModeController
            // session grid (dungeon rooms) > t=0 fallback (debug trigger only).
            // Always snapped on the BASE grid — per-card tempo never moves the origin.
            double raw = startVirtualTime;
            double snapped;
            if (gridOriginOverride.HasValue)
            {
                double origin = gridOriginOverride.Value;
                snapped = origin + SnapToBeat(raw - origin, _baseBeatInterval);
                Debug.Log($"[FlashCard] Beat-snap (explicit origin): raw={raw:F6}  snapped={snapped:F6}  " +
                          $"grid-offset=+{(snapped - raw) * 1000.0:F3}ms  origin={origin:F6}");
            }
            else if (modeController != null)
            {
                double sessionOrigin = modeController.SessionStartVirtualTime;
                snapped = sessionOrigin + SnapToBeat(raw - sessionOrigin, _baseBeatInterval);
                Debug.Log($"[FlashCard] Beat-snap: raw={raw:F6}  snapped={snapped:F6}  " +
                          $"grid-offset=+{(snapped - raw) * 1000.0:F3}ms  sessionOrigin={sessionOrigin:F6}");
            }
            else
            {
                snapped = SnapToBeat(raw, _baseBeatInterval);
                Debug.LogWarning($"[FlashCard] No DungeonModeController — snapping relative to t=0. " +
                                 $"raw={raw:F6} snapped={snapped:F6}");
            }

            _current = PhraseTiming.Compute(0, _cardSlotCount, snapped, _cardBeatInterval, _cardRevealBeats);

            Debug.Log($"[FlashCard] === ENCOUNTER START ===");
            Debug.Log($"[FlashCard]   BPM={_baseBpm:F2}  beatInterval={_baseBeatInterval:F6}s  " +
                      $"cards={(_endless ? "endless" : _activeCards.ToString())}  " +
                      $"notes={_activeMinNotes}-{_activeMaxNotes}  vocab={_activeVocab}  " +
                      $"dtChance={_activeDoubleTimeChance:F2}");
            Debug.Log($"[FlashCard]   {_current}");
        }

        /// <summary>
        /// Stops the encounter immediately. OnEncounterComplete is NOT fired.
        /// </summary>
        public void StopEncounter()
        {
            _running = false;
            TerminateEncounter();
            Debug.Log("[FlashCard] Encounter stopped externally.");
            OnEncounterStopped?.Invoke();
        }
        #endregion

        #region Unity
        private void Update()
        {
            // Debug trigger: start a test encounter on key press using Inspector defaults.
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && !_running)
            {
                if (GameClock.Instance != null && metronome != null)
                {
                    const double LOOKAHEAD = 0.05;
                    StartEncounter(GameClock.Instance.GameTime + LOOKAHEAD);
                }
                else
                {
                    Debug.LogWarning("[FlashCard] Debug trigger: GameClock or Metronome not ready.");
                }
            }

            if (!_running) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;

            // ── Reveal bar start ─────────────────────────────────────────────
            if (!_revealFired && now >= _current.CallStartTime)
            {
                _revealFired = true;

                // Generate the card. No audio is scheduled anywhere — the reveal is
                // purely visual, so no pre-scheduling headroom is needed (unlike C&R's
                // PrepareCallPhrase, which exists only for PlayScheduled DSP lead).
                _cardBeats = BuildCard(_current.ResponseStartTime, _cardSlotCount, _cardMinNotes,
                                       _cardMaxNotes, _cardBeatInterval, out _templates);
                LogCard(_cardIndex, _current, _templates);
                InitBlockJudging();

                _revealSlot = 0;

                // Arm the coyote buffer so near-boundary early inputs carry into the
                // perform bar. EncounterActive=true lets TriggerInput reach the coyote
                // path; allowInput stays false so recording is blocked until then —
                // all other reveal-bar taps are silently ignored, no penalty.
                if (inputReader != null)
                {
                    inputReader.allowInput                = false;
                    inputReader.EncounterActive           = true;
                    inputReader.CoyoteWindowStartOverride = _current.ResponseStartTime;
                }

                // Pipeline the next+preview cards at REVEAL start (not perform
                // start — that call becomes a no-op) so the column-scroll stream
                // is full from the first frame: the view pulls Next/Preview
                // templates while dealing the intro stack. The on-deck reveal
                // events stay gated on the perform window (_performFired), so
                // grid-mode visuals and all timing are unchanged.
                PrepareNextCard();

                double delta = now - _current.CallStartTime;
                Debug.Log($"[FlashCard] RevealStart  | card={_cardIndex} tempo=x{_cardTempoMultiplier:F0} slots={_cardSlotCount} " +
                          $"expected={_current.CallStartTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");
                OnCardRevealStart?.Invoke(_cardIndex);
            }

            // ── Per-beat square reveals (reveal bar only) ────────────────────
            // Every slot flips on its beat — rests flip too (showing a rest glyph
            // later; a log/colored square for now).
            if (_revealFired && !_performFired)
            {
                while (_revealSlot < _cardSlotCount &&
                       now >= _current.CallStartTime + _revealSlot * _revealSlotInterval)
                {
                    int slot     = _revealSlot++;
                    var template = _templates[slot];

                    double expected = _current.CallStartTime + slot * _revealSlotInterval;
                    Debug.Log($"[FlashCard] Reveal       | card={_cardIndex} slot={slot} " +
                              $"{template}  " +
                              $"expected={expected:F6} actual={now:F6} delta=+{(now - expected) * 1000.0:F3}ms");
                    OnSquareReveal?.Invoke(_cardIndex, slot, template);
                }
            }

            // ── Perform bar start ────────────────────────────────────────────
            if (_revealFired && !_performFired && now >= _current.ResponseStartTime)
            {
                _performFired = true;
                OpenPerformWindow();

                double delta = now - _current.ResponseStartTime;
                Debug.Log($"[FlashCard] PerformStart | card={_cardIndex} " +
                          $"expected={_current.ResponseStartTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");
                OnPerformStart?.Invoke(_cardIndex);
                PrepareNextCard();
            }

            // ── Pipelined reveal: next card pops in the on-deck zone ─────────
            // During the current perform window the pre-built next card reveals
            // beat-by-beat so the player reads ahead while playing; at the
            // boundary it is promoted with zero gap (see AdvanceCard).
            if (_performFired && _nextPrepared)
            {
                if (!_nextRevealStarted && now >= _nextRevealOrigin)
                {
                    _nextRevealStarted = true;
                    // Handoff coyote: boundary-adjacent taps are ambiguous between
                    // this card's last note and the next card's first beat — arm
                    // the reader's nearest-claim redirect toward the next window.
                    if (inputReader != null)
                        inputReader.CoyoteWindowStartOverride = _nextTiming.ResponseStartTime;
                    Debug.Log($"[FlashCard] NextReveal   | card={_cardIndex + 1} tempo=x{_nextSpec.tempoMultiplier:F0} " +
                              $"slots={_nextSpec.slotCount} origin={_nextRevealOrigin:F6}");
                    OnNextCardRevealStart?.Invoke(_cardIndex + 1, _nextSpec.slotCount, _nextSpec.tempoMultiplier);
                }

                if (_nextRevealStarted)
                {
                    while (_nextRevealSlot < _nextSpec.slotCount &&
                           now >= _nextRevealOrigin + _nextRevealSlot * _nextSpec.revealSlotInterval)
                    {
                        int slot = _nextRevealSlot++;
                        OnNextSquareReveal?.Invoke(_cardIndex + 1, slot, _nextTemplates[slot]);
                    }
                }
            }

            // ── Per-block judging (endless): slots judged live as windows close ──
            if (_endless && _performFired)
            {
                JudgeElapsedSlots(now);
                if (!_running) return; // run ended inside a judgement event (health depleted)
            }

            // ── Evaluate ─────────────────────────────────────────────────────
            if (_performFired && now >= _current.EvaluationTime)
            {
                // Close the window before evaluation so no stray inputs sneak in.
                ClosePerformWindow();

                double delta = now - _current.EvaluationTime;
                Debug.Log($"[FlashCard] Evaluate     | card={_cardIndex} " +
                          $"expected={_current.EvaluationTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");

                EvaluationResult result = null;
                if (_endless)
                {
                    // Per-block path: finish any remaining slots/groups (all windows
                    // are closed at the boundary), then synthesize an aggregate for
                    // OnCardEvaluated consumers. EvaluateBar is NOT called — score
                    // and sounds were already applied per bar group.
                    JudgeRemainingSlots();
                    if (!_running) { ClearNextCard(); return; }
                    result = BuildCardResult();
                    Debug.Log($"[FlashCard]   Aggregate={result.Grade}  " +
                              $"blocks={result.CorrectHits}/{result.TotalBeats}");
                }
                else if (evaluator != null && _cardBeats != null && inputReader != null)
                {
                    // timeOffset maps reveal-bar beats into perform-bar time coordinates,
                    // identical to C&R's echo evaluation (N * beatInterval window shift).
                    double timeOffset = _cardSlotCount * _cardBeatInterval;
                    result = evaluator.EvaluateBar(inputReader.playerInputData, _cardBeats, timeOffset);
                    if (result != null)
                        Debug.Log($"[FlashCard]   Grade={result.Grade}  points={result.PointsAwarded}  " +
                                  $"correct={result.CorrectHits}/{_cardBeats.Count}  perfect={result.PerfectHits}");
                }
                else
                {
                    Debug.Log("[FlashCard]   (no evaluator or card — grading skipped)");
                }

                OnCardEvaluated?.Invoke(_cardIndex, result);
                AdvanceCard();
            }
        }
        #endregion

        #region Private — Per-Block Judging (endless)
        /// <summary>
        /// Resets the per-block judging state for the card that just became current.
        /// Must run AFTER _cardBeats/_templates/_current/_cardSlotCount are assigned
        /// and BEFORE any input can be recorded against the card (in the handoff
        /// path: before OpenPerformWindow/ReplayCoyoteBuffer).
        /// </summary>
        private void InitBlockJudging()
        {
            if (!_endless || _cardBeats == null) return;

            int n = _cardSlotCount;
            _slotJudged    = new bool[n];
            _slotExpected  = new int[n];
            _slotHits      = new int[n];
            _slotPerfect   = new int[n];
            _slotStrays    = new int[n];
            _beatConsumed  = new bool[_cardBeats.Count];
            _beatSlot      = new int[_cardBeats.Count];
            _nextJudgeSlot = 0;
            _groupStart    = 0;

            // Same floor math as HitCallback: slots measured on the uncompressed
            // reveal timeline.
            double revealOrigin = _current.ResponseStartTime - _cardSlotCount * _cardBeatInterval;
            for (int i = 0; i < _cardBeats.Count; i++)
            {
                int slot = (int)Math.Floor((_cardBeats[i].scheduledTime - revealOrigin) / _cardBeatInterval + 1e-6);
                slot = Mathf.Clamp(slot, 0, n - 1);
                _beatSlot[i] = slot;
                _slotExpected[slot]++;
            }
        }

        /// <summary>
        /// Judges every slot whose window (slot end + grace) has elapsed. Events may
        /// end the run synchronously (health depletion → StopEncounter), hence the
        /// _running re-check every iteration.
        /// </summary>
        private void JudgeElapsedSlots(double now)
        {
            if (_slotJudged == null) return;
            while (_running && _nextJudgeSlot < _cardSlotCount &&
                   now >= _current.ResponseStartTime + (_nextJudgeSlot + 1) * _cardBeatInterval + blockJudgeGraceSeconds)
            {
                JudgeSlot(_nextJudgeSlot++);
            }
        }

        /// <summary>Force-judges all remaining slots at the evaluation boundary (all windows are closed by then).</summary>
        private void JudgeRemainingSlots()
        {
            if (_slotJudged == null) return;
            while (_running && _nextJudgeSlot < _cardSlotCount)
                JudgeSlot(_nextJudgeSlot++);
        }

        private void JudgeSlot(int slot)
        {
            _slotJudged[slot] = true;
            bool cleared = _slotHits[slot] >= _slotExpected[slot];

            Debug.Log($"[FlashCard] BlockJudged  | card={_cardIndex} slot={slot} " +
                      $"{(cleared ? "CLEAR" : "MISS")} hits={_slotHits[slot]}/{_slotExpected[slot]} " +
                      $"perfect={_slotPerfect[slot]} strays={_slotStrays[slot]}");
            OnBlockJudged?.Invoke(_cardIndex, slot, cleared);
            if (!_running) return;

            // Group boundary: every BarGroupSize blocks, plus the remainder at card end.
            if (slot == _groupStart + BarGroupSize - 1 || slot == _cardSlotCount - 1)
            {
                FireBarJudgement(_groupStart, slot);
                _groupStart = slot + 1;
            }
        }

        private void FireBarJudgement(int from, int to)
        {
            int missed = 0, notes = 0, perfect = 0, strays = 0;
            for (int s = from; s <= to; s++)
            {
                if (_slotHits[s] < _slotExpected[s]) missed++;
                notes   += _slotExpected[s];
                perfect += _slotPerfect[s];
                strays  += _slotStrays[s];
            }

            var grade = GradeGroup(missed, notes, perfect, strays);
            var judgement = new BarJudgement(from / BarGroupSize, to - from + 1, missed, notes, perfect, strays, grade);

            Debug.Log($"[FlashCard] BarJudged    | card={_cardIndex} group={judgement.GroupIndex} " +
                      $"blocks={judgement.BlocksInGroup} grade={grade} missed={missed} " +
                      $"perfect={perfect}/{notes} strays={strays}");
            OnBarJudged?.Invoke(_cardIndex, judgement);
        }

        // Missed block → Failed; cleared but sprayed with stray taps → Passable
        // (preserves the old "spam breaks streaks" property); every note Perfect →
        // AllPerfect; else AllGood.
        private static EvaluationGrade GradeGroup(int missed, int notes, int perfect, int strays)
            => missed  > 0      ? EvaluationGrade.Failed
             : strays  > 0      ? EvaluationGrade.Passable
             : perfect >= notes ? EvaluationGrade.AllPerfect
             :                    EvaluationGrade.AllGood;

        /// <summary>
        /// Synthesized whole-card aggregate for OnCardEvaluated consumers (view border
        /// flash, logs). PointsAwarded is 0 — points were already applied per bar group.
        /// </summary>
        private EvaluationResult BuildCardResult()
        {
            int missed = 0, notes = 0, perfect = 0, strays = 0;
            for (int s = 0; s < _cardSlotCount; s++)
            {
                if (_slotHits[s] < _slotExpected[s]) missed++;
                notes   += _slotExpected[s];
                perfect += _slotPerfect[s];
                strays  += _slotStrays[s];
            }
            var grade = GradeGroup(missed, notes, perfect, strays);
            return new EvaluationResult(grade, _cardSlotCount - missed, perfect, _cardSlotCount, 0);
        }
        #endregion

        #region Private — Card Sequencing
        private void AdvanceCard()
        {
            _cardIndex++;

            // Run stopped during OnCardEvaluated (strike-out → host StopEncounter):
            // TerminateEncounter already closed the gates — promoting a pre-built
            // card here would reopen them on a dead run.
            if (!_running)
            {
                ClearNextCard();
                return;
            }

            if (!_endless && _cardIndex >= _activeCards)
            {
                _running = false;
                RestoreBaseBpm();
                if (inputReader != null)
                {
                    inputReader.allowInput                = false;
                    inputReader.SuppressSpawnMarkers      = false;
                    inputReader.OnHit                    -= HitCallback;
                    inputReader.OnMissTap                -= MissCallback;
                    inputReader.CoyoteWindowStartOverride = null;
                }
                Debug.Log("[FlashCard] === ENCOUNTER COMPLETE ===");
                OnEncounterComplete?.Invoke();
                return;
            }

            // ── Pipelined handoff ────────────────────────────────────────────
            // The next card was pre-revealed in the on-deck zone during this
            // card's perform window — promote it and open its perform window in
            // the SAME frame, so play continues with zero gap. A stage-up queued
            // during this evaluation stays pending: PrepareNextCard skips while
            // pending, so the stage lands at the NEXT boundary with its classic
            // break + standalone reveal.
            if (_nextPrepared)
            {
                ApplyCardSpec(_nextSpec); // boundary bpm retune — grading windows now match the new card
                _current      = _nextTiming;
                _cardBeats    = _nextBeats;
                _templates    = _nextTemplates;
                _revealFired  = true;
                _performFired = true;
                _revealSlot   = _cardSlotCount;
                ClearNextCard();

                // Fresh judging state BEFORE the window opens / the coyote buffer
                // replays — the replayed boundary tap must tally into THIS card.
                InitBlockJudging();

                OpenPerformWindow();

                Debug.Log($"[FlashCard] Handoff      | card={_cardIndex} tempo=x{_cardTempoMultiplier:F0} " +
                          $"slots={_cardSlotCount}  {_current}");
                OnCardHandoff?.Invoke(_cardIndex);
                OnPerformStart?.Invoke(_cardIndex);

                // Same-frame close→reopen: the reader's false→true transition
                // detection never sees the flip, so replay the handoff-coyote
                // buffer (a boundary-adjacent early tap for this card's first
                // beat). MUST run after OnCardHandoff/OnPerformStart — the replay
                // fires OnPlayerHit synchronously, and the view drops hit visuals
                // unless it is already in its Performing state on the new grid.
                if (inputReader != null) inputReader.ReplayCoyoteBuffer();

                PrepareNextCard();
                return;
            }

            // Chain: next card starts exactly where this one ended — no gap, no drift.
            // Per-card tempo is rolled BEFORE computing the next timing; a 2x card
            // spans 16 half-beats = 8 base beats, so every card boundary stays on
            // the base bar grid regardless of the mix of normal/DT cards.
            //
            // Stage-up (endless ladder): the pending multiplier retunes the BASE grid
            // here — chaining is relative (next card starts at NextPhraseStart), so
            // no grid re-anchor is needed — and inserts the telegraphed break, counted
            // in NEW-tempo beats, before the next card.
            double chainStart = _current.NextPhraseStart;
            if (_pendingStageMultiplier > 0f)
            {
                _baseBpm          = _runBpm * _pendingStageMultiplier;
                _baseBeatInterval = 60.0 / _baseBpm;
                chainStart       += _pendingStageBreakBeats * _baseBeatInterval;
                Debug.Log($"[FlashCard] STAGE UP → x{_pendingStageMultiplier:F2} ({_baseBpm:F1} BPM), " +
                          $"break={_pendingStageBreakBeats} beats, next card at {chainStart:F3}");
                OnStageUp?.Invoke(_pendingStageMultiplier, _current.NextPhraseStart, _pendingStageBreakBeats, _baseBeatInterval);
                _pendingStageMultiplier = 0f;
                // The previewed card was generated under pre-stage config/beat
                // interval — discard it; the post-break card rolls fresh below.
                _previewValid   = false;
                _previewPattern = null;
            }
            RollNextCardTempo();
            _current      = PhraseTiming.Compute(_cardIndex, _cardSlotCount, chainStart, _cardBeatInterval, _cardRevealBeats);
            _revealFired  = false;
            _performFired = false;
            _revealSlot   = 0;
            _cardBeats    = null;
            _templates    = null;

            Debug.Log($"[FlashCard]   {_current}");
        }

        /// <summary>
        /// Rolls the NEXT card's tempo (1x, or 2x with _activeDoubleTimeChance),
        /// retunes metronome.bpm so the live-read grading windows match the card,
        /// and applies the active slot count. Must be called before
        /// PhraseTiming.Compute for the card it applies to.
        /// </summary>
        private void RollNextCardTempo() => ApplyCardSpec(RollCardSpec());

        /// <summary>
        /// Rolls a card's tempo (1x, or 2x with _activeDoubleTimeChance), expansion,
        /// note density, and reveal pacing from the active config. Pure roll — no
        /// field/metronome side effects beyond the expansion cadence counter — so
        /// the pipelined path can roll the NEXT card while the current one is
        /// still performing.
        /// </summary>
        private CardSpec RollCardSpec()
        {
            var spec = new CardSpec
            {
                tempoMultiplier = UnityEngine.Random.value < _activeDoubleTimeChance ? 2f : 1f,
            };
            spec.beatInterval = _baseBeatInterval / spec.tempoMultiplier;

            // Expansion roll: occasionally a card is bigger than the step's base.
            bool expand = false;
            if (_activeExpandSlotCount > _activeSlotCount)
            {
                if (_activeExpandEvery > 0)
                {
                    _cardsSinceExpand++;
                    if (_cardsSinceExpand >= _activeExpandEvery) { expand = true; _cardsSinceExpand = 0; }
                }
                else
                {
                    expand = UnityEngine.Random.value < _activeExpandChance;
                }
            }
            spec.slotCount = expand ? _activeExpandSlotCount : _activeSlotCount;

            // Proportional density: the authored range describes the base size.
            float ratio   = (float)spec.slotCount / _activeSlotCount;
            spec.minNotes = Mathf.Max(1, Mathf.RoundToInt(_activeMinNotes * ratio));
            spec.maxNotes = Mathf.Max(spec.minNotes, Mathf.RoundToInt(_activeMaxNotes * ratio));

            spec.revealBeats        = compressReveal
                ? Mathf.Min(spec.slotCount, revealBars * 4)
                : spec.slotCount;
            spec.revealSlotInterval = spec.revealBeats * spec.beatInterval / spec.slotCount;
            return spec;
        }

        /// <summary>
        /// Makes <paramref name="spec"/> the CURRENT card's tempo/size config and
        /// retunes metronome.bpm so the live-read grading windows match. Must run
        /// at the card's boundary, never while the previous card is still graded —
        /// the bpm write is why the pipelined path defers this to the handoff.
        /// </summary>
        private void ApplyCardSpec(CardSpec spec)
        {
            _cardTempoMultiplier = spec.tempoMultiplier;
            _cardBeatInterval    = spec.beatInterval;
            _cardSlotCount       = spec.slotCount;
            _cardMinNotes        = spec.minNotes;
            _cardMaxNotes        = spec.maxNotes;
            _cardRevealBeats     = spec.revealBeats;
            _revealSlotInterval  = spec.revealSlotInterval;
            if (metronome != null) metronome.bpm = _baseBpm * _cardTempoMultiplier;
        }

        /// <summary>
        /// Pipelined reveal: rolls and builds the NEXT card so it can reveal in the
        /// on-deck zone during the current perform window, ending exactly at the
        /// boundary (next ResponseStart = current EvaluationTime). Called at every
        /// perform start / handoff. No-ops outside endless runs, when already
        /// prepared, or while a stage-up is pending — the stage keeps its classic
        /// telegraphed break + standalone reveal bar.
        /// </summary>
        private void PrepareNextCard()
        {
            if (!_endless || _nextPrepared || _pendingStageMultiplier > 0f) return;

            // Consume the preview buffer when valid — the card the player has
            // been reading in the stream MUST be the card they get. Scheduling
            // happens now that its timing exists. Fresh roll otherwise.
            if (_previewValid)
            {
                _nextSpec       = _previewSpec;
                _nextTemplates  = _previewTemplates;
                _previewValid   = false;
            }
            else
            {
                _nextSpec      = RollCardSpec();
                _nextTemplates = null; // generated below
            }

            double callStart = _current.EvaluationTime - _nextSpec.revealBeats * _nextSpec.beatInterval;
            _nextTiming = PhraseTiming.Compute(_cardIndex + 1, _nextSpec.slotCount, callStart,
                                               _nextSpec.beatInterval, _nextSpec.revealBeats);
            if (_previewPattern != null && _nextTemplates != null)
            {
                _nextBeats      = ScheduleCard(_previewPattern, _nextTiming.ResponseStartTime,
                                               _nextSpec.slotCount, _nextSpec.beatInterval, _nextTemplates);
                _previewPattern = null;
            }
            else
            {
                _nextBeats = BuildCard(_nextTiming.ResponseStartTime, _nextSpec.slotCount, _nextSpec.minNotes,
                                       _nextSpec.maxNotes, _nextSpec.beatInterval, out _nextTemplates);
            }
            LogCard(_cardIndex + 1, _nextTiming, _nextTemplates);

            // Refill the preview: roll + generate the card AFTER the one just
            // prepared (pattern only — scheduled when consumed next boundary).
            _previewSpec    = RollCardSpec();
            _previewPattern = _cardGenerator.GenerateCard(_previewSpec.slotCount, _previewSpec.minNotes,
                                                          _previewSpec.maxNotes, ConstantType, _activeVocab,
                                                          out _previewTemplates);
            _previewValid   = true;

            // Never reveal before the current perform window begins (exotic tempo
            // mixes could push the reveal window earlier); the pop loop catches up.
            _nextRevealOrigin  = Math.Max(callStart, _current.ResponseStartTime);
            _nextRevealStarted = false;
            _nextRevealSlot    = 0;
            _nextPrepared      = true;
        }

        /// <summary>Discards any pre-built next card (teardown, stop, stage fallback).</summary>
        private void ClearNextCard()
        {
            _nextPrepared      = false;
            _nextRevealStarted = false;
            _nextRevealSlot    = 0;
            _nextBeats         = null;
            _nextTemplates     = null;
        }

        /// <summary>
        /// Returns metronome.bpm to the run's base value. Called on every teardown
        /// path so a run ending mid-DT-card never leaks a doubled bpm to menus or
        /// the results screen. Only writes when the tempo was actually doubled —
        /// TerminateEncounter also runs when a NEW encounter supersedes an old one,
        /// where the host may have already retuned bpm for the new run.
        /// </summary>
        private void RestoreBaseBpm()
        {
            if (metronome != null && _baseBpm > 0 && _cardTempoMultiplier > 1f)
                metronome.bpm = _baseBpm;
            _cardTempoMultiplier = 1f;
            _cardBeatInterval    = _baseBeatInterval;
        }
        #endregion

        #region Private — Perform Window
        /// <summary>
        /// Arms DungeonInputReader to capture and evaluate the player's performance.
        /// Setters (beats + time offset) are applied before the gates (EncounterActive +
        /// allowInput) so the evaluator is fully configured before any tap can arrive.
        /// </summary>
        private void OpenPerformWindow()
        {
            if (inputReader == null) return;
            // ClearRecordedInputs (not ResetInputs): a coyote-buffered early tap for the
            // first note must survive window-open so ReplayBuffer can fire it.
            inputReader.ClearRecordedInputs();
            inputReader.SetScheduledBeats(_cardBeats);
            inputReader.SetTimeOffset(_cardSlotCount * _cardBeatInterval);
            inputReader.CoyoteWindowStartOverride = null;  // clear before opening so replay goes through normally
            // Any pad tap is tagged to the constant type — Tier 1 grading is timing-only.
            inputReader.SingleDrumEchoConstant = ConstantType;
            inputReader.EncounterActive = true;
            inputReader.allowInput      = true;
        }

        /// <summary>
        /// Disarms DungeonInputReader. Called immediately before evaluation and on
        /// encounter stop/interrupt so no inputs arrive during or after grading.
        /// </summary>
        private void ClosePerformWindow()
        {
            if (inputReader == null) return;
            inputReader.allowInput                = false;
            inputReader.EncounterActive           = false;
            inputReader.CoyoteWindowStartOverride = null;
            inputReader.SingleDrumEchoConstant    = null;
        }

        /// <summary>
        /// Full teardown: closes the perform window, resets SuppressSpawnMarkers,
        /// flushes buffered inputs, and unsubscribes from OnHit.
        /// Called on interrupt (StopEncounter) and when a new encounter supersedes a running one.
        /// </summary>
        private void TerminateEncounter()
        {
            ClosePerformWindow();
            RestoreBaseBpm();
            ClearNextCard();
            if (inputReader != null)
            {
                inputReader.SuppressSpawnMarkers      = false;
                inputReader.ResetInputs();
                inputReader.OnHit                    -= HitCallback;
                inputReader.OnMissTap                -= MissCallback;
                inputReader.CoyoteWindowStartOverride = null;
            }
        }

        /// <summary>
        /// Per-tap feedback: resolves the matched note's square and notifies the view.
        /// The MOMENT is the reader's confirmed hit; the view's pop is cosmetic only.
        /// </summary>
        private void HitCallback(DungeonEnemyType type, InputMatch.MatchQuality quality)
        {
            Debug.Log($"[FlashCard] Hit          | card={_cardIndex} quality={quality}");
            if (!_running || !_performFired || _cardBeats == null || _cardBeats.Count == 0) return;
            if (GameClock.Instance == null) return;

            // Find the scheduled note nearest the tap in perform-bar time coordinates
            // (reveal-bar note time + N * beatInterval — same mapping as evaluation).
            double now        = GameClock.Instance.GameTime;
            double timeOffset = _cardSlotCount * _cardBeatInterval;
            DungeonScheduledBeat nearest = null;
            double bestDelta = double.MaxValue;
            foreach (var beat in _cardBeats)
            {
                double delta = Math.Abs(now - (beat.scheduledTime + timeOffset));
                if (delta < bestDelta) { bestDelta = delta; nearest = beat; }
            }
            if (nearest == null) return;

            ConsumeHit(now, timeOffset, quality);

            // Floor (with fp epsilon) so offbeat/subdivided notes map to their host square.
            // Slots are measured on the uncompressed reveal timeline (see BuildCard).
            double revealOrigin = _current.ResponseStartTime - _cardSlotCount * _cardBeatInterval;
            int slot = (int)Math.Floor((nearest.scheduledTime - revealOrigin) / _cardBeatInterval + 1e-6);
            slot = Mathf.Clamp(slot, 0, _cardSlotCount - 1);
            OnPlayerHit?.Invoke(slot, quality);
        }

        /// <summary>
        /// Endless per-block tally: consumes the nearest UNCONSUMED scheduled note
        /// within the live Good window. Consumption prevents a double-tap on one
        /// note from crediting a neighbor. A confirmed hit with no consumable note
        /// (reader/judge disagreement on a double-tap) credits nothing — cosmetic
        /// pop only. Visual slot resolution above is intentionally untouched.
        /// </summary>
        private void ConsumeHit(double now, double timeOffset, InputMatch.MatchQuality quality)
        {
            if (!_endless || _beatConsumed == null) return;

            int    bestIdx   = -1;
            double bestDelta = double.MaxValue;
            for (int i = 0; i < _cardBeats.Count; i++)
            {
                if (_beatConsumed[i]) continue;
                double delta = Math.Abs(now - (_cardBeats[i].scheduledTime + timeOffset));
                if (delta < bestDelta) { bestDelta = delta; bestIdx = i; }
            }
            if (bestIdx < 0) return;

            double goodWindow = (60.0 / metronome.bpm) *
                                (evaluator != null ? evaluator.EffectiveGoodWindowBeats : 0.25f);
            if (bestDelta > goodWindow) return;

            _beatConsumed[bestIdx] = true;
            int s = _beatSlot[bestIdx];
            _slotHits[s]++;
            if (quality == InputMatch.MatchQuality.Perfect) _slotPerfect[s]++;
        }

        /// <summary>Out-of-window tap: resolves the square nearest the tap time and notifies the view.</summary>
        private void MissCallback(DungeonEnemyType type, InputMatch.MatchQuality quality)
        {
            Debug.Log($"[FlashCard] MissTap      | card={_cardIndex} quality={quality}");
            if (!_running || !_performFired) return;
            if (GameClock.Instance == null) return;

            double now  = GameClock.Instance.GameTime;
            int    slot = (int)Math.Floor((now - _current.ResponseStartTime) / _cardBeatInterval);
            slot = Mathf.Clamp(slot, 0, _cardSlotCount - 1);
            if (_endless && _slotStrays != null && slot < _slotStrays.Length)
                _slotStrays[slot]++;
            OnPlayerMissTap?.Invoke(slot);
        }
        #endregion

        #region Private — Card Generation
        private List<DungeonScheduledBeat> BuildCard(double responseStartTime, int slotCount, int minNotes,
                                                     int maxNotes, double beatInterval, out SquareTemplate[] templates)
        {
            var beats = _cardGenerator.GenerateCard(
                slotCount, minNotes, maxNotes, ConstantType, _activeVocab, out templates);
            return ScheduleCard(beats, responseStartTime, slotCount, beatInterval, templates);
        }

        /// <summary>
        /// Schedules a generated (relative) pattern onto absolute virtual time.
        /// Split from BuildCard so the preview buffer can GENERATE a card one
        /// boundary early and defer scheduling until its timing exists.
        /// </summary>
        private List<DungeonScheduledBeat> ScheduleCard(List<DungeonBeat> beats, double responseStartTime,
                                                        int slotCount, double beatInterval, SquareTemplate[] templates)
        {
            var result = new List<DungeonScheduledBeat>(beats.Count);
            // Beats are scheduled on the UNCOMPRESSED reveal timeline: an N-beat bar
            // ending exactly at ResponseStartTime, so scheduledTime + N*beatInterval
            // lands on the perform-bar target. With compression off this origin is
            // just CallStartTime; with it on, the visual reveal (compressed spacing)
            // and the grading timeline (full spacing) intentionally differ.
            double revealOrigin = responseStartTime - slotCount * beatInterval;
            foreach (var b in beats)
            {
                // b.timeSlot is in beats from card start (may be half-integral for
                // eighths); multiply by the card's beat interval for seconds.
                double virtualTime = revealOrigin + b.timeSlot * beatInterval;
                result.Add(new DungeonScheduledBeat(virtualTime, b.enemyType));
            }

            CheckSubdivisionTuningGuard(templates);
            return result;
        }

        /// <summary>
        /// TUNING GUARD: geometric rest enforcement relies on the effective Good
        /// window (goodFraction × any runtime window scaling) staying BELOW 0.25
        /// beat — the binding case is a rest position 0.25 beat from the nearest
        /// note (adjacent half-beat positions at the eighths tier; a rest square or
        /// offbeat rest bordering a SixteenthQuad's last note at the Phase 6 tier).
        /// At ≥ 0.25 those rests grade as sloppy hits instead of penalizing.
        /// Shipped tuning is 0.199. Additionally, Perfect must stay below half the
        /// densest spacing (0.125 for sixteenths) so Perfect zones never overlap;
        /// shipped 0.051. Warn once per encounter on a subdivided card.
        /// </summary>
        private void CheckSubdivisionTuningGuard(SquareTemplate[] templates)
        {
            if (_tuningGuardWarned || evaluator == null) return;

            bool subdivided = false;
            foreach (var t in templates)
                if (t.HasSubdivision()) { subdivided = true; break; }
            if (!subdivided) return;

            float goodBeats    = evaluator.EffectiveGoodWindowBeats;
            float perfectBeats = evaluator.EffectivePerfectWindowBeats;
            if (goodBeats >= 0.25f || perfectBeats >= 0.125f)
            {
                _tuningGuardWarned = true;
                Debug.LogWarning($"[FlashCard] TUNING GUARD: effective windows Good={goodBeats:F3} / " +
                                 $"Perfect={perfectBeats:F3} beat while generating a subdivided card — " +
                                 "rest enforcement needs Good < 0.25 and non-overlapping Perfect zones need " +
                                 "Perfect < 0.125. Reduce DungeonEvaluator fractions or the active " +
                                 "timing-window effect scale.");
            }
        }

        private void LogCard(int cardIndex, PhraseTiming timing, SquareTemplate[] templates)
        {
            var slots = new char[templates.Length];
            for (int i = 0; i < templates.Length; i++) slots[i] = templates[i].ToLogChar();
            Debug.Log($"[FlashCard] Card stored  | card={cardIndex} pattern=[{new string(slots)}] " +
                      $"reveal=[{timing.CallStartTime:F6}, {timing.ResponseStartTime:F6})");
        }
        #endregion

        #region Private — Beat Snap
        /// <summary>
        /// Snaps <paramref name="offset"/> forward to the next multiple of
        /// <paramref name="beatInterval"/>. Tolerance keeps offsets already on a beat
        /// boundary (e.g. a room-seam time) from being pushed a full beat forward by
        /// floating-point error. Same math as CallAndResponseController.SnapToBeat.
        /// </summary>
        private static double SnapToBeat(double offset, double beatInterval)
        {
            const double TOLERANCE = 1e-9;
            double exactBeats = offset / beatInterval;
            double floored    = Math.Floor(exactBeats);
            double fractional = exactBeats - floored;
            double beatCount  = fractional < TOLERANCE ? floored : Math.Ceiling(exactBeats);
            return beatCount * beatInterval;
        }
        #endregion
    }
}
