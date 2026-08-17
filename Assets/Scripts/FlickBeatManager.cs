using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Why a release was counted as a fizzle instead of consuming a required beat.
    /// </summary>
    public enum FlickFizzleCause
    {
        /// <summary>Drag was below the minimum flick distance.</summary>
        TooShort,
        /// <summary>Valid throw, but timing fell outside the attributed beat's Good window.</summary>
        OutOfWindow,
        /// <summary>On-beat throw that missed the target's hitbox (air-ball).</summary>
        BadAim,
        /// <summary>Valid throw, but every required beat in the bar was already consumed.</summary>
        ExtraRelease,
    }

    /// <summary>
    /// Per-release outcome published for the visual layer (ball lock-on vs fizzle).
    /// </summary>
    public readonly struct FlickReleaseOutcome
    {
        /// <summary>True when the release consumed a required beat — cleanly (Perfect/Good) or as a fizzled throw (OutOfWindow/BadAim).</summary>
        public bool Consumed { get; }

        /// <summary>Timing grade for a cleanly consumed beat; Miss when the release fizzled.</summary>
        public InputMatch.MatchQuality Grade { get; }

        /// <summary>Fizzle cause, or null when the beat was consumed cleanly (Perfect/Good).</summary>
        public FlickFizzleCause? FizzleCause { get; }

        /// <summary>The originating release payload.</summary>
        public FlickRelease Release { get; }

        /// <summary>Creates an outcome payload.</summary>
        public FlickReleaseOutcome(bool consumed, InputMatch.MatchQuality grade, FlickFizzleCause? fizzleCause, FlickRelease release)
        {
            Consumed = consumed;
            Grade = grade;
            FizzleCause = fizzleCause;
            Release = release;
        }
    }

    /// <summary>
    /// Orchestrates the Flick encounter bar cycle. Sibling of DungeonBeatManager —
    /// same Update()-polling shape, all timing from GameClock virtual time via
    /// TimingCoordinator's bar structures.
    ///
    /// LIFECYCLE (host-driven — FlickModeController now, RoomController in Phase 5):
    ///   Initialize(bpm) → Arm(gradingStartVirtual) → [lead-in bars: cues + input live,
    ///   nothing graded or damaged] → grading live at gradingStartVirtual →
    ///   StopEncounter(). Unarmed = no bar evaluation and no HP drain, ever.
    ///
    /// BAR CYCLE (8 beats):
    ///   Required release beats at serialized slots (default 0/2/4/6 — musical beats
    ///   1/3/5/7, half-time). Flick input is live for the whole bar; there is no
    ///   response-window gating, only the encounter-level gate driven onto
    ///   FlickInputReader.EncounterActive.
    ///
    /// ATTRIBUTION RULE (manual-aiming model):
    ///   Each Valid throw is attributed to the nearest unconsumed required beat in the
    ///   current bar and CONSUMES it:
    ///     in Good window + hitbox hit  → graded quality (Perfect/Good)
    ///     in Good window + hitbox miss → consumed as fizzle (BadAim)
    ///     out of window                → consumed as fizzle (OutOfWindow)
    ///   TooShort releases and Valid throws with no unconsumed beat left (ExtraRelease)
    ///   are additive fizzles that consume nothing. Required beats never consumed by
    ///   bar close are misses. Worst case: one fizzle per required beat, plus spam.
    ///
    /// ATTACK RESOLUTION (at beat 7.5, mirroring DungeonBeatManager's evaluation point):
    ///   fizzleCount = fizzled releases + missed required beats.
    ///   0 → the enemy attack whiffs. &gt;0 → DungeonHealth.TakeMissDamage(fizzleCount),
    ///   scaling the attack by damagePerMiss. This seam attack is the ONLY flick damage
    ///   channel — TakeOutOfWindowPenalty is never called on the flick path.
    ///
    /// Timing windows are read from DungeonEvaluator's serialized fractions via its
    /// public EffectivePerfectWindowBeats / EffectiveGoodWindowBeats (includes any
    /// active effect scaling), so flick grading stays in tune with pad grading.
    ///
    /// INSPECTOR SETUP:
    ///   metronome   — shared Metronome (BPM source)
    ///   evaluator   — DungeonEvaluator (window fractions only; EvaluateBar is never called)
    ///   flickReader — FlickInputReader whose releases drive the bar
    ///   health      — DungeonHealth receiving seam attack damage
    /// </summary>
    public class FlickBeatManager : MonoBehaviour
    {
        [Header("Core Systems")]
        [Tooltip("Shared Metronome — BPM source for the beat interval.")]
        [SerializeField] private Metronome metronome;

        [Tooltip("DungeonEvaluator whose serialized window fractions are reused for grading. EvaluateBar is never called.")]
        [SerializeField] private DungeonEvaluator evaluator;

        [Tooltip("FlickInputReader whose release events drive the bar. Its EncounterActive gate is owned by this manager.")]
        [SerializeField] private FlickInputReader flickReader;

        [Tooltip("DungeonHealth that receives seam attack damage (TakeMissDamage per fizzle).")]
        [SerializeField] private DungeonHealth health;

        [Header("Bar Structure")]
        [Tooltip("Required release slots within the 8-beat bar. 0-based: slot 0 = musical beat 1. " +
                 "Beat time = bar start + slot * beatInterval. Default 0/2/4/6 = beats 1/3/5/7 (half-time).")]
        [SerializeField] private float[] requiredBeatSlots = { 0f, 2f, 4f, 6f };

        [Header("Required-Beat Cue")]
        [Tooltip("Accent tick played on each required beat so the player can tell required beats from " +
                 "in-between metronome ticks (parity anchor). When unassigned, a source is created at " +
                 "runtime from the metronome speaker's clip at accentPitch.")]
        [SerializeField] private AudioSource accentSource;

        [Tooltip("Pitch of the runtime-created fallback accent tick, relative to the metronome tick.")]
        [SerializeField] private float accentPitch = 1.5f;

        [Tooltip("Master enable for the accent tick. The OnRequiredBeatCue event fires regardless.")]
        [SerializeField] private bool accentEnabled = true;

        [Tooltip("Accent tick volume — mixed OVER any BGM, so keep it high enough to anchor the half-time grid.")]
        [SerializeField, Range(0f, 1f)] private float accentVolume = 1f;

        /// <summary>
        /// Hitbox hook invoked once per IN-WINDOW Valid throw, receiving the release
        /// payload (drag vector + flick origin). Return false to consume the beat as a
        /// BadAim fizzle (air-ball). Null (the default) means every throw is on-target.
        /// Off-beat throws never invoke this — they fizzle on timing alone.
        /// </summary>
        public Func<FlickRelease, bool> AimCheck { get; set; }

        /// <summary>
        /// Fired after each release is attributed — consumed (with grade) or fizzled
        /// (with cause). Visual layer only; grading state never reads back from this.
        /// </summary>
        public event Action<FlickReleaseOutcome> OnReleaseResolved;

        /// <summary>
        /// Fired at each bar's seam resolution with the bar's fizzle count.
        /// 0 = the enemy attack whiffed; &gt;0 = the attack landed.
        /// </summary>
        public event Action<int> OnBarResolved;

        /// <summary>
        /// Fired once as each required beat's moment passes (frame-quantized, like the
        /// metronome tick). Visual layer cue only — grading never reads from this.
        /// </summary>
        public event Action OnRequiredBeatCue;

        // ── State ─────────────────────────────────────────────────────────────

        private enum State { Uninitialized, Armed, Running }

        private class RequiredBeat
        {
            public float  Slot;
            public double VirtualTime;
            public bool   Consumed;
            public bool   CuePlayed;
            public FlickFizzleCause? FizzleCause; // non-null when consumed by a fizzled throw
            public InputMatch.MatchQuality Grade;
            public double ErrorMs;
        }

        private State _state = State.Uninitialized;
        private double _beatInterval;
        private double _gradingStartVirtual;
        private readonly List<RequiredBeat> _requiredBeats = new();
        private readonly List<FlickFizzleCause> _barFizzles = new();

        // Windows in seconds, recomputed per read so BPM / effect-scale changes apply live.
        private double GoodThresholdSeconds    => evaluator.EffectiveGoodWindowBeats    * (60.0 / metronome.bpm);
        private double PerfectThresholdSeconds => evaluator.EffectivePerfectWindowBeats * (60.0 / metronome.bpm);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Prepares the manager for a new session. Mirrors DungeonBeatManager.Initialize:
        /// sets the shared metronome BPM and derives the beat interval.
        /// </summary>
        /// <param name="bpm">Beats per minute for this session.</param>
        public void Initialize(int bpm)
        {
            metronome.bpm = bpm;
            _beatInterval = 60.0 / bpm;
            _state = State.Uninitialized;
            Debug.Log($"[FlickBeatManager] Initialized — BPM: {bpm}, beat: {_beatInterval:F4}s, " +
                      $"slots: [{string.Join(", ", requiredBeatSlots)}]");
        }

        /// <summary>
        /// Arms the manager, driven by its host (mode controller now, RoomController in
        /// the dungeon). TimingCoordinator must already be initialized with the same
        /// start time and BPM. Arming opens the flick reader's encounter gate and starts
        /// the bar cycle + required-beat cues immediately, but NOTHING is graded or
        /// damaged until <paramref name="gradingStartVirtual"/> — bars before it are
        /// lead-in: the sweep and ball are live, releases are silently ignored, and the
        /// seam resolves without an enemy attack. Unarmed (Uninitialized) means no bar
        /// evaluation and no HP drain at all.
        /// </summary>
        /// <param name="gradingStartVirtual">Virtual time grading begins — normally a bar boundary at or after bar 0's start.</param>
        public void Arm(double gradingStartVirtual)
        {
            if (TimingCoordinator.Instance == null)
            {
                Debug.LogError("[FlickBeatManager] No TimingCoordinator — cannot arm.");
                return;
            }

            _gradingStartVirtual = gradingStartVirtual;
            _barFizzles.Clear();
            SetupBar(TimingCoordinator.Instance.CurrentBar);

            if (flickReader != null) flickReader.EncounterActive = true;
            _state = State.Armed;
            TryBeginGrading();

            Debug.Log($"[FlickBeatManager] Armed — grading from vt={gradingStartVirtual:F4} " +
                      $"(state: {_state})");
        }

        /// <summary>Stops the bar cycle and gates off the flick reader.</summary>
        public void StopEncounter()
        {
            _state = State.Uninitialized;
            if (flickReader != null) flickReader.EncounterActive = false;
            _requiredBeats.Clear();
            _barFizzles.Clear();
            Debug.Log("[FlickBeatManager] Encounter stopped");
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (flickReader != null) flickReader.OnFlickReleased += HandleFlickReleased;
        }

        private void OnDisable()
        {
            if (flickReader != null) flickReader.OnFlickReleased -= HandleFlickReleased;
        }

        private void Update()
        {
            if (_state == State.Uninitialized) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            FireRequiredBeatCues();

            // Bar evaluation at beat 7.5 — before the seam, mirroring DungeonBeatManager.
            if (TimingCoordinator.Instance.ShouldEvaluateNow())
            {
                if (_state == State.Running) ResolveBar();
                else AdvanceLeadInBar();
            }
        }

        // Lead-in seam: advance the bar cycle without grading or damage. Once the new
        // bar starts at/after the grading start, the manager goes live.
        private void AdvanceLeadInBar()
        {
            TimingCoordinator.Instance.AdvanceToNextBar();
            SetupBar(TimingCoordinator.Instance.CurrentBar);
            _barFizzles.Clear(); // Nothing recorded during lead-in counts.
            TryBeginGrading();
        }

        private void TryBeginGrading()
        {
            if (_state != State.Armed) return;
            if (TimingCoordinator.Instance.CurrentBar.BarStartTime < _gradingStartVirtual - 1e-4) return;

            _state = State.Running;
            Debug.Log($"[FlickBeatManager] Lead-in over — grading LIVE from bar " +
                      $"{TimingCoordinator.Instance.CurrentBar.BarIndex}");
        }

        // Accent + event as each required beat's moment passes — the parity anchor that
        // distinguishes required beats from the in-between metronome ticks. Same
        // frame-quantized polling as Metronome's own tick.
        private void FireRequiredBeatCues()
        {
            double now = GameClock.Instance.GameTime;
            foreach (var rb in _requiredBeats)
            {
                if (rb.CuePlayed || now < rb.VirtualTime) continue;
                rb.CuePlayed = true;
                PlayAccent();
                OnRequiredBeatCue?.Invoke();
            }
        }

        private void PlayAccent()
        {
            if (!accentEnabled) return;

            if (accentSource == null && metronome != null && metronome.speaker != null && metronome.speaker.clip != null)
            {
                // Runtime fallback: the metronome tick at a higher pitch.
                accentSource = gameObject.AddComponent<AudioSource>();
                accentSource.playOnAwake = false;
                accentSource.clip = metronome.speaker.clip;
                accentSource.pitch = accentPitch;
            }

            if (accentSource != null && accentSource.clip != null)
                accentSource.PlayOneShot(accentSource.clip, accentVolume);
        }

        // ── Bar setup ─────────────────────────────────────────────────────────

        private void SetupBar(TimingCoordinator.BarTiming bar)
        {
            _requiredBeats.Clear();
            foreach (float slot in requiredBeatSlots)
            {
                _requiredBeats.Add(new RequiredBeat
                {
                    Slot        = slot,
                    VirtualTime = bar.BarStartTime + slot * _beatInterval,
                });
            }
            Debug.Log($"[FlickBeatManager] Bar {bar.BarIndex} armed — {_requiredBeats.Count} required beats, " +
                      $"first at {(_requiredBeats.Count > 0 ? _requiredBeats[0].VirtualTime : 0):F4}");
        }

        // ── Release attribution ───────────────────────────────────────────────

        private void HandleFlickReleased(FlickRelease release)
        {
            if (_state != State.Running) return;

            // Below-minimum flicks are additive fizzles outright.
            if (release.Classification == FlickClassification.TooShort)
            {
                RecordFizzle(FlickFizzleCause.TooShort, release);
                return;
            }

            // Attribute to the nearest unconsumed required beat in the current bar.
            RequiredBeat target = null;
            double bestDelta = double.MaxValue;
            foreach (var rb in _requiredBeats)
            {
                if (rb.Consumed) continue;
                double d = Math.Abs(release.ReleaseVirtualTime - rb.VirtualTime);
                if (d < bestDelta) { bestDelta = d; target = rb; }
            }

            if (target == null)
            {
                RecordFizzle(FlickFizzleCause.ExtraRelease, release);
                return;
            }

            double error = release.ReleaseVirtualTime - target.VirtualTime;

            // Off-beat: the throw consumes its beat as one fizzle, no hitbox test needed.
            if (Math.Abs(error) > GoodThresholdSeconds)
            {
                ConsumeAsFizzle(target, FlickFizzleCause.OutOfWindow, error, release,
                    $"error {error * 1000:+0.0;-0.0}ms > ±{GoodThresholdSeconds * 1000:F1}ms");
                return;
            }

            // On-beat: manual aim — the hitbox hook decides hit vs air-ball.
            bool onTarget = AimCheck == null || AimCheck(release);
            if (!onTarget)
            {
                ConsumeAsFizzle(target, FlickFizzleCause.BadAim, error, release, "air-ball");
                return;
            }

            target.Consumed = true;
            target.Grade    = Math.Abs(error) <= PerfectThresholdSeconds
                ? InputMatch.MatchQuality.Perfect
                : InputMatch.MatchQuality.Good;
            target.ErrorMs  = error * 1000.0;

            Debug.Log($"[FlickBeatManager] CONSUMED slot {target.Slot} — {target.Grade} " +
                      $"({target.ErrorMs:+0.0;-0.0}ms, windows P±{PerfectThresholdSeconds * 1000:F1}ms " +
                      $"G±{GoodThresholdSeconds * 1000:F1}ms)");

            OnReleaseResolved?.Invoke(new FlickReleaseOutcome(true, target.Grade, null, release));
        }

        // Consumes a required beat with a fizzled throw (off-beat or air-ball): the beat
        // is spent — one fizzle now, no additional miss at bar close.
        private void ConsumeAsFizzle(RequiredBeat target, FlickFizzleCause cause, double error, FlickRelease release, string detail)
        {
            target.Consumed    = true;
            target.FizzleCause = cause;
            target.Grade       = InputMatch.MatchQuality.Miss;
            target.ErrorMs     = error * 1000.0;
            RecordFizzle(cause, release, $"slot {target.Slot} CONSUMED, {detail}", consumedBeat: true);
        }

        private void RecordFizzle(FlickFizzleCause cause, FlickRelease release, string detail = null, bool consumedBeat = false)
        {
            _barFizzles.Add(cause);
            Debug.Log($"[FlickBeatManager] FIZZLE ({cause}) — vt={release.ReleaseVirtualTime:F4}" +
                      (detail != null ? $" [{detail}]" : ""));
            OnReleaseResolved?.Invoke(new FlickReleaseOutcome(consumedBeat, InputMatch.MatchQuality.Miss, cause, release));
        }

        // ── Bar resolution — enemy attack at the seam ─────────────────────────

        private void ResolveBar()
        {
            int barIdx = TimingCoordinator.Instance.GetCurrentBarIndex();

            var consumed = new StringBuilder();
            int missedCount = 0;
            var missed = new StringBuilder();

            foreach (var rb in _requiredBeats)
            {
                if (rb.Consumed)
                {
                    if (consumed.Length > 0) consumed.Append(", ");
                    consumed.Append(rb.FizzleCause != null
                        ? $"slot {rb.Slot}=Fizzled/{rb.FizzleCause}({rb.ErrorMs:+0.0;-0.0}ms)"
                        : $"slot {rb.Slot}={rb.Grade}({rb.ErrorMs:+0.0;-0.0}ms)");
                }
                else
                {
                    missedCount++;
                    if (missed.Length > 0) missed.Append(", ");
                    missed.Append($"slot {rb.Slot}");
                }
            }

            // Fizzle breakdown by cause.
            var causeCounts = new Dictionary<FlickFizzleCause, int>();
            foreach (var c in _barFizzles)
                causeCounts[c] = causeCounts.TryGetValue(c, out int n) ? n + 1 : 1;
            var breakdown = new StringBuilder();
            foreach (var kv in causeCounts)
            {
                if (breakdown.Length > 0) breakdown.Append(", ");
                breakdown.Append($"{kv.Key} x{kv.Value}");
            }

            int fizzleCount = _barFizzles.Count + missedCount;

            Debug.Log($"[FlickBeatManager] === BAR {barIdx} RESOLVED ===\n" +
                      $"  Consumed: {(consumed.Length > 0 ? consumed.ToString() : "none")}\n" +
                      $"  Missed:   {(missedCount > 0 ? missed.ToString() : "none")}\n" +
                      $"  Fizzled releases: {_barFizzles.Count}" +
                      (breakdown.Length > 0 ? $" ({breakdown})" : "") +
                      $"  → fizzleCount = {_barFizzles.Count} + {missedCount} missed = {fizzleCount}");

            if (fizzleCount == 0)
            {
                Debug.Log($"[FlickBeatManager] ENEMY ATTACK WHIFFS — perfect bar. " +
                          $"HP: {(health != null ? health.CurrentHealth : -1)}");
            }
            else
            {
                // The seam attack is the only flick damage channel; damage scales with
                // fizzles via damagePerMiss inside TakeMissDamage.
                health?.TakeMissDamage(fizzleCount);
                Debug.Log($"[FlickBeatManager] ENEMY ATTACK LANDS — {fizzleCount} fizzle(s). " +
                          $"HP: {(health != null ? health.CurrentHealth : -1)}");
            }

            OnBarResolved?.Invoke(fizzleCount);

            // Advance to the next bar and arm its required beats.
            _barFizzles.Clear();
            TimingCoordinator.Instance.AdvanceToNextBar();
            SetupBar(TimingCoordinator.Instance.CurrentBar);
        }
    }
}
