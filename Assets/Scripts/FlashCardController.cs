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
    ///                 take the out-of-window penalty (rests are >= 1 full beat from
    ///                 any note at Tier 1, far beyond the Good threshold).
    /// Cards chain zero-gap: next card's reveal starts exactly at this card's
    /// evaluation boundary (PhraseTiming.NextPhraseStart), no accumulated drift.
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
        [SerializeField, Range(1, 8)] private int minNotes = 3;

        [Tooltip("Maximum notes per card.")]
        [SerializeField, Range(1, 8)] private int maxNotes = 6;

        #endregion

        #region Events
        /// <summary>Fired when a card's reveal bar begins. Parameter: cardIndex.</summary>
        public event Action<int> OnCardRevealStart;

        /// <summary>
        /// Fired as each card square reveals, on its beat.
        /// Parameters: cardIndex, slot (0–7), isNote (false = rest).
        /// </summary>
        public event Action<int, int, bool> OnSquareReveal;

        /// <summary>Fired when a card's perform bar begins. Parameter: cardIndex.</summary>
        public event Action<int> OnPerformStart;

        /// <summary>
        /// Fired after a card's perform bar is graded.
        /// Parameters: cardIndex, result (null when evaluation was skipped, e.g. paused).
        /// </summary>
        public event Action<int, EvaluationResult> OnCardEvaluated;

        /// <summary>Fired once, after the final card's evaluation completes.</summary>
        public event Action OnEncounterComplete;

        /// <summary>
        /// Fired when the encounter is stopped externally mid-cycle (death, room interrupt).
        /// NOT fired on normal completion — subscribers use OnEncounterComplete for that.
        /// Lets the card view tear down cleanly without polling encounter state.
        /// </summary>
        public event Action OnEncounterStopped;
        #endregion

        #region State
        private const int CardBeats = FlashCardPatternGenerator.SlotCount; // N = 8

        private bool         _running;
        private int          _cardIndex;
        private PhraseTiming _current;
        private bool         _revealFired;
        private bool         _performFired;
        private int          _revealSlot;   // next slot to reveal, 0–8

        // Snapshotted at StartEncounter — held constant for the full encounter.
        private double _beatInterval;

        private FlashCardPatternGenerator _cardGenerator;

        // Current card: scheduled notes in reveal-bar virtual times + full 8-slot mask.
        private List<DungeonScheduledBeat> _cardBeats;
        private bool[]                     _noteMask;

        // Constant type stamped on all notes and echo inputs — Tier 1 is rhythm-only
        // (any pad counts; grading is timing-only), same collapse as SingleDrum C&R.
        private const DungeonEnemyType ConstantType = DungeonEnemyType.Center;

        // Resolved at encounter start: room-authored config overrides Inspector defaults
        // (same pattern as CallAndResponseController._activePhraseLengths).
        private int _activeCards;
        private int _activeMinNotes;
        private int _activeMaxNotes;
        #endregion

        #region Public Properties
        /// <summary>True while an encounter is running.</summary>
        public bool IsRunning => _running;

        /// <summary>The scheduled notes of the current card (reveal-bar virtual times). Null before the first reveal.</summary>
        public IReadOnlyList<DungeonScheduledBeat> CurrentCardBeats => _cardBeats;

        /// <summary>8-slot note/rest mask of the current card. Null before the first reveal.</summary>
        public IReadOnlyList<bool> CurrentNoteMask => _noteMask;

        /// <summary>Timing boundaries of the current card cycle.</summary>
        public PhraseTiming CurrentTiming => _current;
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
            => StartEncounter(startVirtualTime, 0, Vector2Int.zero);

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
        /// (0,0) sentinel = use the Inspector minNotes/maxNotes defaults.
        /// </param>
        public void StartEncounter(double startVirtualTime, int cardCount, Vector2Int noteRange)
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

            _beatInterval  = 60.0 / metronome.bpm;
            _cardGenerator = new FlashCardPatternGenerator();
            _cardIndex     = 0;
            _running       = true;
            _revealFired   = false;
            _performFired  = false;
            _revealSlot    = 0;
            _cardBeats     = null;
            _noteMask      = null;

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
            }

            // ── Beat-grid snap (same approach as CallAndResponseController) ──
            double raw = startVirtualTime;
            double snapped;
            if (modeController != null)
            {
                double sessionOrigin = modeController.SessionStartVirtualTime;
                snapped = sessionOrigin + SnapToBeat(raw - sessionOrigin, _beatInterval);
                Debug.Log($"[FlashCard] Beat-snap: raw={raw:F6}  snapped={snapped:F6}  " +
                          $"grid-offset=+{(snapped - raw) * 1000.0:F3}ms  sessionOrigin={sessionOrigin:F6}");
            }
            else
            {
                snapped = SnapToBeat(raw, _beatInterval);
                Debug.LogWarning($"[FlashCard] No DungeonModeController — snapping relative to t=0. " +
                                 $"raw={raw:F6} snapped={snapped:F6}");
            }

            _current = PhraseTiming.Compute(0, CardBeats, snapped, _beatInterval);

            Debug.Log($"[FlashCard] === ENCOUNTER START ===");
            Debug.Log($"[FlashCard]   BPM={metronome.bpm:F2}  beatInterval={_beatInterval:F6}s  " +
                      $"cards={_activeCards}  notes={_activeMinNotes}-{_activeMaxNotes}");
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
                _cardBeats = BuildCard(_current, out _noteMask);
                LogCard(_cardIndex, _current, _noteMask);

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

                double delta = now - _current.CallStartTime;
                Debug.Log($"[FlashCard] RevealStart  | card={_cardIndex} " +
                          $"expected={_current.CallStartTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");
                OnCardRevealStart?.Invoke(_cardIndex);
            }

            // ── Per-beat square reveals (reveal bar only) ────────────────────
            // Every slot flips on its beat — rests flip too (showing a rest glyph
            // later; a log/colored square for now).
            if (_revealFired && !_performFired)
            {
                while (_revealSlot < CardBeats &&
                       now >= _current.CallStartTime + _revealSlot * _beatInterval)
                {
                    int  slot   = _revealSlot++;
                    bool isNote = _noteMask[slot];

                    double expected = _current.CallStartTime + slot * _beatInterval;
                    Debug.Log($"[FlashCard] Reveal       | card={_cardIndex} slot={slot} " +
                              $"{(isNote ? "NOTE" : "rest")}  " +
                              $"expected={expected:F6} actual={now:F6} delta=+{(now - expected) * 1000.0:F3}ms");
                    OnSquareReveal?.Invoke(_cardIndex, slot, isNote);
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
                if (evaluator != null && _cardBeats != null && inputReader != null)
                {
                    // timeOffset maps reveal-bar beats into perform-bar time coordinates,
                    // identical to C&R's echo evaluation (N * beatInterval window shift).
                    double timeOffset = CardBeats * _beatInterval;
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

        #region Private — Card Sequencing
        private void AdvanceCard()
        {
            _cardIndex++;

            if (_cardIndex >= _activeCards)
            {
                _running = false;
                if (inputReader != null)
                {
                    inputReader.allowInput                = false;
                    inputReader.SuppressSpawnMarkers      = false;
                    inputReader.OnHit                    -= HitCallback;
                    inputReader.CoyoteWindowStartOverride = null;
                }
                Debug.Log("[FlashCard] === ENCOUNTER COMPLETE ===");
                OnEncounterComplete?.Invoke();
                return;
            }

            // Chain: next card starts exactly where this one ended — no gap, no drift.
            _current      = PhraseTiming.Compute(_cardIndex, CardBeats, _current.NextPhraseStart, _beatInterval);
            _revealFired  = false;
            _performFired = false;
            _revealSlot   = 0;
            _cardBeats    = null;
            _noteMask     = null;

            Debug.Log($"[FlashCard]   {_current}");
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
            inputReader.SetTimeOffset(CardBeats * _beatInterval);
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
            if (inputReader != null)
            {
                inputReader.SuppressSpawnMarkers      = false;
                inputReader.ResetInputs();
                inputReader.OnHit                    -= HitCallback;
                inputReader.CoyoteWindowStartOverride = null;
            }
        }

        // Per-tap feedback (Phase 1: log evidence; later phases route this to card UI).
        private void HitCallback(DungeonEnemyType type, InputMatch.MatchQuality quality)
            => Debug.Log($"[FlashCard] Hit          | card={_cardIndex} quality={quality}");
        #endregion

        #region Private — Card Generation
        private List<DungeonScheduledBeat> BuildCard(PhraseTiming timing, out bool[] noteMask)
        {
            var beats  = _cardGenerator.GenerateCard(_activeMinNotes, _activeMaxNotes, ConstantType, out noteMask);
            var result = new List<DungeonScheduledBeat>(beats.Count);
            foreach (var b in beats)
            {
                // b.timeSlot is in beats from card start; multiply by _beatInterval for seconds.
                double virtualTime = timing.CallStartTime + b.timeSlot * _beatInterval;
                result.Add(new DungeonScheduledBeat(virtualTime, b.enemyType));
            }
            return result;
        }

        private void LogCard(int cardIndex, PhraseTiming timing, bool[] noteMask)
        {
            var slots = new char[CardBeats];
            for (int i = 0; i < CardBeats; i++) slots[i] = noteMask[i] ? 'X' : '.';
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
