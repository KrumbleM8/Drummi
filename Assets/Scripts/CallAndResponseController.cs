using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Standalone phrase-clock and encounter coordinator for Call-and-Response encounters.
    ///
    /// Computes variable-length call/response phrase windows directly from
    /// GameClock.GameTime — does NOT route through TimingCoordinator or BarTiming,
    /// which are hardcoded to 8-beat bars and cannot be parameterised at runtime.
    ///
    /// BPM is read from Metronome.bpm (the same source DungeonBeatManager uses)
    /// and snapshotted at StartEncounter time. BPM is assumed constant for the
    /// duration of the encounter; a mid-encounter BPM change would cause drift
    /// relative to the music and is not supported.
    ///
    /// Phase 1: beat-anchored start; phrase generated and stored per round.
    /// Phase 2: drives CallPerformer — directional strikes during call window,
    ///          handoff cue at response boundary, allowInput gated false throughout call.
    /// Phase 3: opens the echo window at the response boundary (EncounterActive +
    ///          allowInput = true), grades the player's echo via DungeonEvaluator.EvaluateBar
    ///          at the evaluation boundary, routes per-tap feedback to CallPerformer.OnEchoHit
    ///          via DungeonInputReader.OnHit.
    /// Phase 4: room integration — StartEncounter(double, int[]) overload lets RoomController
    ///          pass the room's authored phraseLengths; T-key debug path uses Inspector defaults.
    ///
    /// INSPECTOR SETUP:
    ///   metronome        — shared Metronome (Metronome.bpm)
    ///   modeController   — DungeonModeController (session beat-grid origin)
    ///   performer        — CallPerformer in the scene (drives call visuals + audio)
    ///   inputReader      — DungeonInputReader (allowInput + EncounterActive managed per-phase)
    ///   evaluator        — DungeonEvaluator (grades the echo at each evaluation boundary)
    ///   phraseLengths    — Inspector default phrase lengths; used when not overridden by a room
    ///   patternDurations — beat subdivision weights for DungeonPatternGenerator
    ///   (debug) press T at runtime to start a test encounter using Inspector phraseLengths
    /// </summary>
    public class CallAndResponseController : MonoBehaviour
    {
        #region Inspector
        [Header("Timing Source")]
        [Tooltip("Shared Metronome — provides Metronome.bpm (same source as DungeonBeatManager).")]
        [SerializeField] private Metronome metronome;

        [Tooltip("DungeonModeController — provides SessionStartVirtualTime for beat-grid snap.")]
        [SerializeField] private DungeonModeController modeController;

        [Header("Encounter Systems")]
        [Tooltip("CallPerformer in the scene — drives directional strikes and handoff cue.")]
        [SerializeField] private CallPerformer performer;

        [Tooltip("DungeonInputReader — allowInput and EncounterActive are managed per-phase; SuppressSpawnMarkers is held true for the whole encounter.")]
        [SerializeField] private DungeonInputReader inputReader;

        [Tooltip("DungeonEvaluator — grades the player's echo at each round's evaluation boundary.")]
        [SerializeField] private DungeonEvaluator evaluator;

        [Header("Phrase Sequence (Inspector defaults — overridden by RoomDefinition.PhraseLengths when started via a room)")]
        [Tooltip("Phrase length in beats for each round. Used by the T-key debug path; rooms supply their own via StartEncounter(double, int[]).")]
        [SerializeField] private int[] phraseLengths = { 2, 4, 6 };

        [Tooltip("Beat subdivision weights passed to DungeonPatternGenerator.")]
        [SerializeField] private float[] patternDurations = { 1f, 0.5f };

        #endregion

        #region Events
        /// <summary>
        /// Fired when the call (enemy phrase) window begins.
        /// Parameters: roundIndex, phraseLengthN.
        /// </summary>
        public event Action<int, int> OnCallStart;

        /// <summary>
        /// Fired when the response (player echo) window begins.
        /// Parameters: roundIndex, phraseLengthN.
        /// </summary>
        public event Action<int, int> OnResponseStart;

        /// <summary>
        /// Fired at the end of the response window — the evaluation point.
        /// Parameters: roundIndex, phraseLengthN.
        /// </summary>
        public event Action<int, int> OnEvaluate;

        /// <summary>
        /// Fired once, after the final round's evaluation completes.
        /// </summary>
        public event Action OnEncounterComplete;
        #endregion

        #region State
        private bool         _running;
        private int          _roundIndex;
        private PhraseTiming _current;
        private bool         _callFired;
        private bool         _responseFired;
        private bool         _audioPrepared;

        // Minimum real-DSP lead time enforced for the first PlayScheduled call of each
        // call phrase. 0.15 s gives comfortable margin over the largest common mobile audio
        // buffer (~4096 samples @ 44100 Hz ≈ 0.093 s). Must not exceed the shortest
        // possible phrase window (1 beat at max BPM); at 240 BPM that's 0.25 s — fine.
        private const double CALL_AUDIO_HEADROOM_SECS = 0.15;

        // Snapshotted at StartEncounter — held constant for the full encounter.
        // Assumption: BPM does not change mid-encounter.
        private double _beatInterval;

        // Created once per encounter from patternDurations.
        private DungeonPatternGenerator _phraseGenerator;

        // The call phrase for the current round, stored as absolute virtual beat times.
        private List<DungeonScheduledBeat> _currentCallPhrase;

        // Beats not yet triggered by Strike() — consumed forward in time during the call window.
        private List<DungeonScheduledBeat> _pendingCallBeats = new();

        // Resolved at encounter start: room-authored overrides Inspector default.
        private int[] _activePhraseLengths;

        // Constant enemy type forced on all call beats and echo inputs — C&R is
        // single-input only (any pad counts, grading is timing-only).
        private const DungeonEnemyType SingleDrumConstantType = DungeonEnemyType.Center;
        #endregion

        #region Public Properties
        /// <summary>True while a sequence is running.</summary>
        public bool IsRunning => _running;

        /// <summary>
        /// The call phrase stored for the current round.
        /// Null before the first round starts.
        /// </summary>
        public IReadOnlyList<DungeonScheduledBeat> CurrentCallPhrase => _currentCallPhrase;
        #endregion

        #region Public API
        /// <summary>
        /// Starts a full encounter using the Inspector default phraseLengths.
        /// Used by the T-key debug trigger for isolated testing.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Raw virtual time (GameClock.GameTime) near which the first call window should start.
        /// Snapped forward to the next session beat before use.
        /// </param>
        public void StartEncounter(double startVirtualTime)
            => StartEncounterCore(startVirtualTime, null);

        /// <summary>
        /// Starts a full encounter using the room's authored phrase configuration.
        /// Called by RoomController when a RoomType.CallAndResponse room begins.
        /// Falls back to Inspector defaults if <paramref name="roomPhraseLengths"/> is null or empty.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Current virtual time (GameClock.GameTime). StartEncounter snaps this forward
        /// to the next beat on the session grid — pass GameClock.Instance.GameTime directly.
        /// </param>
        /// <param name="roomPhraseLengths">
        /// Authored phrase lengths from RoomDefinition.PhraseLengths. Takes priority over
        /// the Inspector phraseLengths field for this encounter.
        /// </param>
        public void StartEncounter(double startVirtualTime, int[] roomPhraseLengths)
            => StartEncounterCore(startVirtualTime, roomPhraseLengths);

        /// <summary>
        /// Stops the encounter immediately. OnEncounterComplete is NOT fired.
        /// </summary>
        public void StopEncounter()
        {
            _running = false;
            _pendingCallBeats.Clear();
            TerminateEncounter();
            Debug.Log("[C&R] Encounter stopped externally.");
        }
        #endregion

        #region Unity
        private void Update()
        {
            // Debug trigger: start a test encounter on key press using Inspector phraseLengths.
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !_running)
            {
                if (GameClock.Instance != null && metronome != null)
                {
                    const double LOOKAHEAD = 0.05;
                    StartEncounter(GameClock.Instance.GameTime + LOOKAHEAD);
                }
                else
                {
                    Debug.LogWarning("[C&R] Debug trigger: GameClock or Metronome not ready.");
                }
            }

            if (!_running) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;
            int    n   = _current.PhraseLength;

            // ── Pre-schedule call audio ──────────────────────────────────────
            // Build the phrase and schedule all strike audio CALL_AUDIO_HEADROOM_SECS
            // before callStartTime so beat-0's PlayScheduled has ≥0.15 s of real DSP lead
            // on every round — including rounds whose callStart immediately follows the
            // previous evaluation, where the frame-late OnCallStart path yields ~0 lead.
            if (!_callFired && !_audioPrepared && now >= _current.CallStartTime - CALL_AUDIO_HEADROOM_SECS)
            {
                _currentCallPhrase = BuildCallPhrase(_current);
                performer?.PrepareCallPhrase(_currentCallPhrase, _current.ResponseStartTime - 0.5 * _beatInterval);
                _audioPrepared = true;
            }

            // ── OnCallStart ───────────────────────────────────────────────────
            if (!_callFired && now >= _current.CallStartTime)
            {
                _callFired = true;

                // Phrase and audio were pre-built/scheduled by the headroom block above.
                // Safety-net: if callStartTime was already in the past when the encounter
                // started (e.g. T-key debug path, or very short lead-in), build and schedule
                // here instead. This path has ~0 DSP lead — acceptable for editor/desktop.
                if (_currentCallPhrase == null)
                {
                    _currentCallPhrase = BuildCallPhrase(_current);
                    performer?.PrepareCallPhrase(_currentCallPhrase, _current.ResponseStartTime - 0.5 * _beatInterval);
                }
                LogCallPhrase(_roundIndex, _current, _currentCallPhrase);

                // First call: jump the performer in. Subsequent rounds: restore from the
                // dimmed handoff state. Audio was pre-scheduled above — no PrepareCallPhrase here.
                if (_roundIndex == 0)
                    performer?.JumpIn();
                else
                    performer?.ResetToActiveState();

                // Queue beats for per-beat strike polling during the call window.
                _pendingCallBeats = new List<DungeonScheduledBeat>(_currentCallPhrase);

                // Arm the coyote buffer so near-boundary early inputs carry into the echo window.
                // EncounterActive=true lets TriggerInput reach the coyote path; allowInput stays
                // false so actual recording is blocked until the response boundary.
                if (inputReader != null)
                {
                    inputReader.allowInput                = false;
                    inputReader.EncounterActive           = true;
                    inputReader.CoyoteWindowStartOverride = _current.ResponseStartTime;
                }

                double delta = now - _current.CallStartTime;
                Debug.Log($"[C&R] OnCallStart     | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.CallStartTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");
                OnCallStart?.Invoke(_roundIndex, n);
            }

            // ── Strike polling (call window only) ─────────────────────────────
            // Consumes _pendingCallBeats in time order. Beats are sorted ascending
            // by virtualTime (GeneratePhrase builds them with increasing timeSlot),
            // so once the front beat hasn't arrived, none of the later ones have.
            if (_callFired && !_responseFired)
            {
                while (_pendingCallBeats.Count > 0 &&
                       now >= _pendingCallBeats[0].scheduledTime)
                {
                    var beat = _pendingCallBeats[0];
                    _pendingCallBeats.RemoveAt(0);

                    performer?.Strike(beat.enemyType);

                    double strikeDelta = now - beat.scheduledTime;
                    Debug.Log($"[C&R] Strike poll     | round={_roundIndex} " +
                              $"type={beat.enemyType}  " +
                              $"expected={beat.scheduledTime:F6} actual={now:F6} " +
                              $"delta=+{strikeDelta * 1000.0:F3}ms");
                }
            }

            // ── OnResponseStart ───────────────────────────────────────────────
            if (_callFired && !_responseFired && now >= _current.ResponseStartTime)
            {
                _responseFired = true;
                _pendingCallBeats.Clear(); // discard any beats that didn't trigger

                // Handoff cue: performer dims visually; audio was pre-scheduled.
                performer?.ShowHandoffCue();

                // Open the echo window: arm DungeonInputReader so the player's taps
                // are recorded and evaluated against the call phrase.
                // Setters (beats + offset) are applied before gates (EncounterActive + allowInput).
                OpenEchoWindow(n);

                double delta = now - _current.ResponseStartTime;
                Debug.Log($"[C&R] OnResponseStart | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.ResponseStartTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");
                OnResponseStart?.Invoke(_roundIndex, n);
            }

            // ── OnEvaluate ────────────────────────────────────────────────────
            if (_responseFired && now >= _current.EvaluationTime)
            {
                // Close the window before evaluation so no stray inputs sneak in.
                CloseEchoWindow();

                double delta = now - _current.EvaluationTime;
                Debug.Log($"[C&R] OnEvaluate      | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.EvaluationTime:F6} actual={now:F6} delta=+{delta * 1000.0:F3}ms");

                if (evaluator != null && _currentCallPhrase != null && inputReader != null)
                {
                    // timeOffset maps call beats into response-window time coordinates,
                    // identical to DungeonBeatManager's usage (N * beatInterval = window shift).
                    double timeOffset = n * _beatInterval;
                    var result = evaluator.EvaluateBar(inputReader.playerInputData, _currentCallPhrase, timeOffset);
                    if (result != null)
                        Debug.Log($"[C&R]   Grade={result.Grade}  points={result.PointsAwarded}  " +
                                  $"correct={result.CorrectHits}/{_currentCallPhrase.Count}  " +
                                  $"perfect={result.PerfectHits}");
                }
                else
                {
                    Debug.Log("[C&R]   (no evaluator or phrase — echo skipped)");
                }

                OnEvaluate?.Invoke(_roundIndex, n);
                AdvanceRound();
            }
        }
        #endregion

        #region Private — Encounter Start
        private void StartEncounterCore(double startVirtualTime, int[] overridePhraseLengths)
        {
            // Resolve active phrase lengths: room-authored takes priority over Inspector defaults.
            _activePhraseLengths = (overridePhraseLengths != null && overridePhraseLengths.Length > 0)
                ? overridePhraseLengths
                : phraseLengths;

            if (metronome == null)
            {
                Debug.LogError("[C&R] Metronome not assigned — cannot start encounter.");
                return;
            }
            if (_activePhraseLengths == null || _activePhraseLengths.Length == 0)
            {
                Debug.LogError("[C&R] phraseLengths is empty — cannot start encounter.");
                return;
            }

            // Stop any previous encounter cleanly before initialising the new one.
            if (_running) TerminateEncounter();

            _beatInterval    = 60.0 / metronome.bpm;
            _phraseGenerator = new DungeonPatternGenerator(patternDurations);
            _roundIndex        = 0;
            _running           = true;
            _callFired         = false;
            _responseFired     = false;
            _audioPrepared     = false;
            _currentCallPhrase = null;
            _pendingCallBeats.Clear();

            performer?.PreloadClips();

            // Gate input off for the call window. allowInput is managed per-phase;
            // the controller owns this and does not rely on TimingCoordinator.CurrentBar.
            // SuppressSpawnMarkers is held true for the whole encounter — the standard bar
            // slider is BarTiming-coupled and would place markers at wrong positions.
            if (inputReader != null)
            {
                inputReader.allowInput                = false;
                inputReader.EncounterActive           = false;
                inputReader.CoyoteWindowStartOverride = null;
                inputReader.SuppressSpawnMarkers      = true;
                inputReader.OnHit                    += EchoHitCallback;
            }

            // ── Beat-grid snap ────────────────────────────────────────────────
            double raw = startVirtualTime;
            double snapped;

            if (modeController != null)
            {
                double sessionOrigin = modeController.SessionStartVirtualTime;
                double offset        = raw - sessionOrigin;
                snapped = sessionOrigin + SnapToBeat(offset, _beatInterval);
                Debug.Log($"[C&R] Beat-snap: raw={raw:F6}  snapped={snapped:F6}  " +
                          $"grid-offset=+{(snapped - raw) * 1000.0:F3}ms  " +
                          $"sessionOrigin={sessionOrigin:F6}");
            }
            else
            {
                snapped = SnapToBeat(raw, _beatInterval);
                Debug.LogWarning($"[C&R] No DungeonModeController — snapping relative to t=0. " +
                                 $"raw={raw:F6} snapped={snapped:F6}");
            }

            _current = PhraseTiming.Compute(0, _activePhraseLengths[0], snapped, _beatInterval);

            Debug.Log($"[C&R] === ENCOUNTER START ===");
            Debug.Log($"[C&R]   BPM={metronome.bpm:F2}  beatInterval={_beatInterval:F6}s");
            Debug.Log($"[C&R]   Rounds={_activePhraseLengths.Length}  sequence=[{string.Join(", ", _activePhraseLengths)}]");
            Debug.Log($"[C&R]   {_current}");
        }
        #endregion

        #region Private — Round Sequencing
        private void AdvanceRound()
        {
            _roundIndex++;

            if (_roundIndex >= _activePhraseLengths.Length)
            {
                _running = false;
                _pendingCallBeats.Clear();
                if (inputReader != null)
                {
                    inputReader.allowInput                = false;
                    inputReader.SuppressSpawnMarkers      = false;
                    inputReader.OnHit                    -= EchoHitCallback;
                    inputReader.CoyoteWindowStartOverride = null;
                }
                performer?.StopAndReset();
                Debug.Log("[C&R] === ENCOUNTER COMPLETE ===");
                OnEncounterComplete?.Invoke();
                return;
            }

            // Chain: next phrase starts exactly where this one ended — no gap, no drift.
            double nextStart = _current.NextPhraseStart;
            int    nextN     = _activePhraseLengths[_roundIndex];
            _current           = PhraseTiming.Compute(_roundIndex, nextN, nextStart, _beatInterval);
            _callFired         = false;
            _responseFired     = false;
            _audioPrepared     = false;
            _currentCallPhrase = null;
            _pendingCallBeats.Clear();

            Debug.Log($"[C&R]   {_current}");
        }
        #endregion

        #region Private — Echo Window
        /// <summary>
        /// Arms DungeonInputReader to capture and evaluate the player's echo response.
        /// Setters (beats + time offset) are applied before the gates (EncounterActive +
        /// allowInput) so the evaluator is fully configured before any tap can arrive.
        /// </summary>
        private void OpenEchoWindow(int n)
        {
            if (inputReader == null) return;
            // ClearRecordedInputs (not ResetInputs): a coyote-buffered early tap for the
            // first echo beat must survive window-open so ReplayBuffer can fire it.
            inputReader.ClearRecordedInputs();
            inputReader.SetScheduledBeats(_currentCallPhrase);
            inputReader.SetTimeOffset(n * _beatInterval);
            inputReader.CoyoteWindowStartOverride = null;  // clear before opening so replay goes through normally
            // Arm the type override so any pad tap is tagged to the constant type
            // (grading is timing-only; pad identity is irrelevant).
            inputReader.SingleDrumEchoConstant = SingleDrumConstantType;
            inputReader.EncounterActive = true;
            inputReader.allowInput      = true;
        }

        /// <summary>
        /// Disarms DungeonInputReader. Called immediately before evaluation and
        /// on encounter stop/interrupt so no inputs arrive during or after grading.
        /// </summary>
        private void CloseEchoWindow()
        {
            if (inputReader == null) return;
            inputReader.allowInput                = false;
            inputReader.EncounterActive           = false;
            inputReader.CoyoteWindowStartOverride = null;
            inputReader.SingleDrumEchoConstant    = null;
        }

        /// <summary>
        /// Full teardown: closes the echo window, resets SuppressSpawnMarkers,
        /// flushes buffered inputs, unsubscribes from OnHit, and resets the performer.
        /// Called on interrupt (StopEncounter) and when a new encounter supersedes a running one.
        /// </summary>
        private void TerminateEncounter()
        {
            _audioPrepared = false;
            CloseEchoWindow();
            if (inputReader != null)
            {
                inputReader.SuppressSpawnMarkers      = false;
                inputReader.ResetInputs();
                inputReader.OnHit                    -= EchoHitCallback;
                inputReader.CoyoteWindowStartOverride = null;
            }
            performer?.StopAndReset();
        }

        // Routes per-tap feedback from DungeonInputReader.OnHit to CallPerformer.OnEchoHit.
        private void EchoHitCallback(DungeonEnemyType type, InputMatch.MatchQuality quality)
            => performer?.OnEchoHit(type, quality);
        #endregion

        #region Private — Beat Snap
        /// <summary>
        /// Snaps <paramref name="offset"/> forward to the next multiple of
        /// <paramref name="beatInterval"/>, returning the snapped offset.
        ///
        /// Uses a tolerance before applying Math.Ceiling so that offsets already
        /// exactly on a beat boundary (e.g. a room-seam passed from StartRoom) are
        /// not pushed one beat forward by floating-point error.
        /// Math.Ceiling(24.0000000001) would otherwise advance by a whole extra beat.
        /// </summary>
        private static double SnapToBeat(double offset, double beatInterval)
        {
            // Tolerance: sub-microsecond — larger than any double rounding error for
            // values computed from the same beatInterval, smaller than any real beat gap.
            const double TOLERANCE = 1e-9;
            double exactBeats  = offset / beatInterval;
            double floored     = Math.Floor(exactBeats);
            double fractional  = exactBeats - floored;
            double beatCount   = fractional < TOLERANCE ? floored : Math.Ceiling(exactBeats);
            return beatCount * beatInterval;
        }
        #endregion

        #region Private — Phrase Generation
        private List<DungeonScheduledBeat> BuildCallPhrase(PhraseTiming timing)
        {
            var beats = _phraseGenerator.GeneratePhraseWithConstantType(timing.PhraseLength, SingleDrumConstantType);
            var result = new List<DungeonScheduledBeat>(beats.Count);

            foreach (var b in beats)
            {
                // b.timeSlot is in beats from phrase start; multiply by _beatInterval for seconds.
                double virtualTime = timing.CallStartTime + b.timeSlot * _beatInterval;
                result.Add(new DungeonScheduledBeat(virtualTime, b.enemyType));
            }

            return result;
        }

        private void LogCallPhrase(int roundIndex, PhraseTiming timing, List<DungeonScheduledBeat> phrase)
        {
            Debug.Log($"[C&R] Call phrase stored | round={roundIndex} N={timing.PhraseLength} " +
                      $"beats={phrase.Count} " +
                      $"window=[{timing.CallStartTime:F6}, {timing.ResponseStartTime:F6})");

            for (int i = 0; i < phrase.Count; i++)
            {
                var b         = phrase[i];
                double offset = b.scheduledTime - timing.CallStartTime;
                Debug.Log($"[C&R]   beat[{i}] {b.enemyType,-6}  " +
                          $"virtualTime={b.scheduledTime:F6}  " +
                          $"offset={offset * 1000.0:F1}ms from call start");
            }
        }
        #endregion
    }

    /// <summary>
    /// All virtual-time boundaries for a single call-and-response round.
    /// All times are in GameClock virtual coordinates (pause-excluded DSP time).
    ///
    /// Computed from a phrase start time and phrase length N (in beats):
    ///   CallStartTime     = phraseStart
    ///   ResponseStartTime = phraseStart + N * beatInterval
    ///   EvaluationTime    = phraseStart + 2N * beatInterval
    ///   NextPhraseStart   = EvaluationTime   (zero-gap chaining, no drift)
    /// </summary>
    public readonly struct PhraseTiming
    {
        /// <summary>Zero-based index of this round within the encounter.</summary>
        public int RoundIndex { get; }

        /// <summary>Phrase length in beats (N).</summary>
        public int PhraseLength { get; }

        /// <summary>Virtual time the call window opens.</summary>
        public double CallStartTime { get; }

        /// <summary>Virtual time the response window opens (CallStartTime + N * beatInterval).</summary>
        public double ResponseStartTime { get; }

        /// <summary>Virtual time of evaluation — end of the response window (CallStartTime + 2N * beatInterval).</summary>
        public double EvaluationTime { get; }

        /// <summary>
        /// Virtual start time of the next phrase.
        /// Equals EvaluationTime so phrases chain with zero gap and no accumulated drift.
        /// </summary>
        public double NextPhraseStart { get; }

        private PhraseTiming(
            int round, int n,
            double callStart, double responseStart, double evalTime)
        {
            RoundIndex        = round;
            PhraseLength      = n;
            CallStartTime     = callStart;
            ResponseStartTime = responseStart;
            EvaluationTime    = evalTime;
            NextPhraseStart   = evalTime;
        }

        /// <summary>
        /// Compute a PhraseTiming from a phrase start virtual time and beat interval.
        /// Symmetric: the call window is as long as the response window (N beats each).
        /// </summary>
        /// <param name="round">Zero-based round index.</param>
        /// <param name="n">Phrase length in beats.</param>
        /// <param name="phraseStart">Virtual time (GameClock.GameTime) at which the call begins.</param>
        /// <param name="beatInterval">Seconds per beat (60.0 / bpm), snapshotted at encounter start.</param>
        public static PhraseTiming Compute(int round, int n, double phraseStart, double beatInterval)
            => Compute(round, n, phraseStart, beatInterval, n);

        /// <summary>
        /// Asymmetric variant: the call window lasts <paramref name="callBeats"/>
        /// beats while the response window keeps the full N. Used by FlashCards'
        /// compressed reveal (big cards reveal in a fixed 2 bars); callBeats = n
        /// reproduces the symmetric behavior exactly.
        /// </summary>
        public static PhraseTiming Compute(int round, int n, double phraseStart, double beatInterval, int callBeats)
            => Compute(round, n, phraseStart, beatInterval, callBeats, n);

        /// <summary>
        /// Variable-length response variant: the response window lasts
        /// <paramref name="performBeats"/> beats instead of N. Used by FlashCards'
        /// HalfRest cards (a half-beat rest shortens the card to N − 0.5 beats, and
        /// NextPhraseStart = EvaluationTime then phase-shifts every later card onto
        /// the off-beats). performBeats = n reproduces the integer overload exactly.
        /// </summary>
        public static PhraseTiming Compute(int round, int n, double phraseStart, double beatInterval, int callBeats, double performBeats)
        {
            double responseStart = phraseStart + callBeats * beatInterval;
            double evalTime      = responseStart + performBeats * beatInterval;
            return new PhraseTiming(round, n, phraseStart, responseStart, evalTime);
        }

        public override string ToString() =>
            $"[Round {RoundIndex} N={PhraseLength}] " +
            $"Call={CallStartTime:F6}  Response={ResponseStartTime:F6}  Eval={EvaluationTime:F6}";
    }
}
