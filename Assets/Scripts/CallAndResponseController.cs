using System;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Standalone phrase-clock for Call-and-Response encounters.
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
    /// Phase 0: timing spine only — no pattern generation, no enemy spawning,
    /// no input, no visuals. This class exists solely to prove that phrase
    /// boundaries fire with sample-accurate timing for N in {2, 4, 6, 8}.
    ///
    /// INSPECTOR SETUP:
    ///   metronome     — shared Metronome component (provides Metronome.bpm)
    ///   phraseLengths — ordered N values in beats for each round, e.g. {2, 4, 6, 8}
    ///   debugTriggerKey — keyboard key that starts a test encounter at runtime
    /// </summary>
    public class CallAndResponseController : MonoBehaviour
    {
        #region Inspector
        [Header("Timing Source")]
        [Tooltip("Shared Metronome — provides Metronome.bpm (same source as DungeonBeatManager).")]
        [SerializeField] private Metronome metronome;

        [Header("Phrase Sequence")]
        [Tooltip("Phrase length in beats for each round. Sequence plays in order.")]
        [SerializeField] private int[] phraseLengths = { 2, 4, 6, 8 };

        [Header("Debug")]
        [Tooltip("Press this key at runtime to start a test encounter (Phase 0 proof).")]
        [SerializeField] private KeyCode debugTriggerKey = KeyCode.T;
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

        // Snapshotted at StartEncounter — held constant for the full encounter.
        // Assumption: BPM does not change mid-encounter. A change would cause
        // phrase boundaries to drift relative to the music.
        private double _beatInterval;
        #endregion

        #region Public API
        /// <summary>
        /// Starts a full encounter sequence. The first call window begins at
        /// <paramref name="startVirtualTime"/> (a GameClock.GameTime value).
        /// BPM is read from Metronome.bpm at this moment and held constant.
        /// Safe to call when not already running; calling while running stops
        /// the previous encounter and restarts immediately.
        /// </summary>
        /// <param name="startVirtualTime">
        /// Virtual time (GameClock.GameTime) at which the first call window opens.
        /// Pass <c>GameClock.Instance.GameTime + smallLookahead</c> for a near-future start.
        /// </param>
        public void StartEncounter(double startVirtualTime)
        {
            if (metronome == null)
            {
                Debug.LogError("[C&R] Metronome not assigned — cannot start encounter.");
                return;
            }
            if (phraseLengths == null || phraseLengths.Length == 0)
            {
                Debug.LogError("[C&R] phraseLengths is empty — cannot start encounter.");
                return;
            }

            _beatInterval  = 60.0 / metronome.bpm;
            _roundIndex    = 0;
            _running       = true;
            _callFired     = false;
            _responseFired = false;

            _current = PhraseTiming.Compute(0, phraseLengths[0], startVirtualTime, _beatInterval);

            Debug.Log($"[C&R] === ENCOUNTER START ===");
            Debug.Log($"[C&R]   BPM={metronome.bpm:F2}  beatInterval={_beatInterval:F6}s");
            Debug.Log($"[C&R]   Rounds={phraseLengths.Length}  sequence=[{string.Join(", ", phraseLengths)}]");
            Debug.Log($"[C&R]   {_current}");
        }

        /// <summary>
        /// Stops the encounter immediately. OnEncounterComplete is NOT fired.
        /// </summary>
        public void StopEncounter()
        {
            _running = false;
            Debug.Log("[C&R] Encounter stopped externally.");
        }

        /// <summary>True while a sequence is running.</summary>
        public bool IsRunning => _running;
        #endregion

        #region Unity
        private void Update()
        {
            // Debug trigger: start a test encounter on key press.
            if (Input.GetKeyDown(debugTriggerKey) && !_running)
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

            // ── OnCallStart ───────────────────────────────────────────────────
            if (!_callFired && now >= _current.CallStartTime)
            {
                _callFired = true;
                double delta = now - _current.CallStartTime;
                Debug.Log($"[C&R] OnCallStart     | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.CallStartTime:F6} " +
                          $"actual={now:F6} " +
                          $"delta=+{delta * 1000.0:F3}ms");
                OnCallStart?.Invoke(_roundIndex, n);
            }

            // ── OnResponseStart ───────────────────────────────────────────────
            if (_callFired && !_responseFired && now >= _current.ResponseStartTime)
            {
                _responseFired = true;
                double delta = now - _current.ResponseStartTime;
                Debug.Log($"[C&R] OnResponseStart | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.ResponseStartTime:F6} " +
                          $"actual={now:F6} " +
                          $"delta=+{delta * 1000.0:F3}ms");
                OnResponseStart?.Invoke(_roundIndex, n);
            }

            // ── OnEvaluate ────────────────────────────────────────────────────
            if (_responseFired && now >= _current.EvaluationTime)
            {
                double delta = now - _current.EvaluationTime;
                Debug.Log($"[C&R] OnEvaluate      | round={_roundIndex} N={n:D2} " +
                          $"expected={_current.EvaluationTime:F6} " +
                          $"actual={now:F6} " +
                          $"delta=+{delta * 1000.0:F3}ms");
                OnEvaluate?.Invoke(_roundIndex, n);
                AdvanceRound();
            }
        }
        #endregion

        #region Private
        private void AdvanceRound()
        {
            _roundIndex++;

            if (_roundIndex >= phraseLengths.Length)
            {
                _running = false;
                Debug.Log("[C&R] === ENCOUNTER COMPLETE ===");
                OnEncounterComplete?.Invoke();
                return;
            }

            // Chain: next phrase starts exactly where this one ended — no gap, no drift.
            double nextStart = _current.NextPhraseStart;
            int    nextN     = phraseLengths[_roundIndex];
            _current       = PhraseTiming.Compute(_roundIndex, nextN, nextStart, _beatInterval);
            _callFired     = false;
            _responseFired = false;

            Debug.Log($"[C&R]   {_current}");
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
        /// </summary>
        /// <param name="round">Zero-based round index.</param>
        /// <param name="n">Phrase length in beats.</param>
        /// <param name="phraseStart">Virtual time (GameClock.GameTime) at which the call begins.</param>
        /// <param name="beatInterval">Seconds per beat (60.0 / bpm), snapshotted at encounter start.</param>
        public static PhraseTiming Compute(int round, int n, double phraseStart, double beatInterval)
        {
            double responseStart = phraseStart + n * beatInterval;
            double evalTime      = phraseStart + 2.0 * n * beatInterval;
            return new PhraseTiming(round, n, phraseStart, responseStart, evalTime);
        }

        public override string ToString() =>
            $"[Round {RoundIndex} N={PhraseLength}] " +
            $"Call={CallStartTime:F6}  Response={ResponseStartTime:F6}  Eval={EvaluationTime:F6}";
    }
}
