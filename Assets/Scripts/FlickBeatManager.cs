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

        /// <summary>True when the consumed beat was an optional off-beat bonus slot (half points).</summary>
        public bool IsOffBeat { get; }

        /// <summary>
        /// Clean-streak position of this hit BEFORE it landed: 0 for the first clean
        /// on-beat hit of the current enemy, 1 for the next, etc. Hosts use it as the
        /// combo-damage exponent. Always 0 for off-beat hits and fizzles.
        /// </summary>
        public int ComboIndex { get; }

        /// <summary>The originating release payload.</summary>
        public FlickRelease Release { get; }

        /// <summary>Creates an outcome payload.</summary>
        public FlickReleaseOutcome(bool consumed, InputMatch.MatchQuality grade, FlickFizzleCause? fizzleCause, FlickRelease release, bool isOffBeat = false, int comboIndex = 0)
        {
            Consumed = consumed;
            Grade = grade;
            FizzleCause = fizzleCause;
            Release = release;
            IsOffBeat = isOffBeat;
            ComboIndex = comboIndex;
        }
    }

    /// <summary>
    /// Per-bar seam payload published at bar resolution.
    /// </summary>
    public readonly struct FlickBarResult
    {
        /// <summary>Fizzled releases + missed required beats this bar. 0 during a grace seam.</summary>
        public int FizzleCount { get; }

        /// <summary>True when the enemy died mid-bar — the seam resolved with no attack.</summary>
        public bool Grace { get; }

        /// <summary>Virtual time the next bar starts — hosts schedule the replacement enemy spawn here.</summary>
        public double NextBarStartVirtual { get; }

        /// <summary>Creates a bar-result payload.</summary>
        public FlickBarResult(int fizzleCount, bool grace, double nextBarStartVirtual)
        {
            FizzleCount = fizzleCount;
            Grace = grace;
            NextBarStartVirtual = nextBarStartVirtual;
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
    ///   1/3/5/7, half-time), plus OPTIONAL off-beat bonus slots (default 1/3/5/7 —
    ///   the in-between beats). Flick input is live for the whole bar; there is no
    ///   response-window gating, only the encounter-level gate driven onto
    ///   FlickInputReader.EncounterActive.
    ///
    /// ATTRIBUTION RULE (manual-aiming model):
    ///   An in-window Valid throw at an unconsumed OFF-BEAT slot claims that slot
    ///   first: hitbox hit → graded bonus (Perfect/Good, IsOffBeat — hosts award half
    ///   points), hitbox miss → the off-beat is consumed as a BadAim fizzle. Off-beats
    ///   are pure bonus otherwise: they never absorb out-of-window throws and are NOT
    ///   misses when left unconsumed at bar close.
    ///   Every other Valid throw is attributed to the nearest unconsumed REQUIRED beat
    ///   in the current bar and CONSUMES it:
    ///     in Good window + hitbox hit  → graded quality (Perfect/Good)
    ///     in Good window + hitbox miss → consumed as fizzle (BadAim)
    ///     out of window                → consumed as fizzle (OutOfWindow)
    ///   TooShort releases and Valid throws with no unconsumed beat left (ExtraRelease)
    ///   are additive fizzles that consume nothing. Required beats never consumed by
    ///   bar close are misses. Worst case: one fizzle per required beat, plus spam.
    ///
    /// ATTACK RESOLUTION (at beat 7.5, mirroring DungeonBeatManager's evaluation point):
    ///   A SURVIVING enemy always attacks: DungeonHealth.TakeMissDamage(
    ///   survivorAttackUnits + fizzleCount), where fizzleCount = fizzled releases +
    ///   missed required beats. This seam attack is the ONLY flick damage channel —
    ///   TakeOutOfWindowPenalty is never called on the flick path.
    ///   If the host reports the enemy dead mid-bar (NotifyEnemyDefeated), the rest of
    ///   the bar is a GRACE period: releases ignored, no cues, and the seam resolves
    ///   with NO attack (FlickBarResult.Grace = true) before the next bar arms normally.
    ///
    /// COMBO (host damage boost): consecutive cleanly consumed REQUIRED beats build a
    /// streak, published per-hit as FlickReleaseOutcome.ComboIndex. The streak resets
    /// on any fizzle, on a required beat whose window expires unconsumed, on clean
    /// off-beat hits, and at every bar start (one enemy per bar).
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

        [Tooltip("Flat attack strength (in damagePerMiss units) a SURVIVING enemy always lands at the " +
                 "seam, on top of the per-fizzle scaling. 0 restores the old whiff-on-clean-bar behavior.")]
        [SerializeField, Min(0)] private int survivorAttackUnits = 1;

        [Header("Bar Structure")]
        [Tooltip("Required release slots within the 8-beat bar. 0-based: slot 0 = musical beat 1. " +
                 "Beat time = bar start + slot * beatInterval. Default 0/2/4/6 = beats 1/3/5/7 (half-time).")]
        [SerializeField] private float[] requiredBeatSlots = { 0f, 2f, 4f, 6f };

        [Tooltip("Optional bonus slots within the 8-beat bar (default 1/3/5/7 — the off-beats). " +
                 "An in-window, on-target throw here is a half-point bonus hit. Off-beats are never " +
                 "misses when skipped and never absorb out-of-window throws. Empty = disabled.")]
        [SerializeField] private float[] offBeatSlots = { 1f, 3f, 5f, 7f };

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
        /// Fired at each bar's seam resolution. Grace = true means the enemy died
        /// mid-bar and no attack resolved; otherwise the surviving enemy's attack
        /// landed (always), scaled by FizzleCount.
        /// </summary>
        public event Action<FlickBarResult> OnBarResolved;

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
            public bool   IsOffBeat; // optional bonus slot — never a miss, no accent cue
            public bool   Consumed;
            public bool   CuePlayed;
            public bool   ExpiryStreakCounted; // this beat's unconsumed expiry already broke the streak
            public FlickFizzleCause? FizzleCause; // non-null when consumed by a fizzled throw
            public InputMatch.MatchQuality Grade;
            public double ErrorMs;
        }

        private State _state = State.Uninitialized;
        private double _beatInterval;
        private double _gradingStartVirtual;
        private int _cleanStreak;   // consecutive cleanly consumed required beats (combo exponent)
        private bool _graceActive;  // enemy died mid-bar — no grading/cues/attack until next bar
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

        /// <summary>
        /// Host hook: the enemy died mid-bar. The remainder of the bar becomes a grace
        /// period — releases ignored, no required-beat cues, and the seam resolves with
        /// no attack (FlickBarResult.Grace). The next bar arms and grades normally.
        /// </summary>
        public void NotifyEnemyDefeated()
        {
            if (_state != State.Running) return;
            _graceActive = true;
            _cleanStreak = 0;
            Debug.Log("[FlickBeatManager] Enemy defeated — grace period until next bar");
        }

        /// <summary>Stops the bar cycle and gates off the flick reader.</summary>
        public void StopEncounter()
        {
            _state = State.Uninitialized;
            _graceActive = false;
            _cleanStreak = 0;
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
            if (_graceActive) return; // dead enemy — no parity anchor to cue

            double now = GameClock.Instance.GameTime;
            foreach (var rb in _requiredBeats)
            {
                if (rb.IsOffBeat || rb.CuePlayed || now < rb.VirtualTime) continue;
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
            _cleanStreak = 0; // one enemy per bar — the combo never carries over
            _requiredBeats.Clear();
            foreach (float slot in requiredBeatSlots)
            {
                _requiredBeats.Add(new RequiredBeat
                {
                    Slot        = slot,
                    VirtualTime = bar.BarStartTime + slot * _beatInterval,
                });
            }
            foreach (float slot in offBeatSlots)
            {
                _requiredBeats.Add(new RequiredBeat
                {
                    Slot        = slot,
                    VirtualTime = bar.BarStartTime + slot * _beatInterval,
                    IsOffBeat   = true,
                });
            }
            Debug.Log($"[FlickBeatManager] Bar {bar.BarIndex} armed — {requiredBeatSlots.Length} required beats " +
                      $"+ {offBeatSlots.Length} off-beat bonus slots");
        }

        // ── Release attribution ───────────────────────────────────────────────

        private void HandleFlickReleased(FlickRelease release)
        {
            if (_state != State.Running || _graceActive) return;

            // A required beat whose window expired unconsumed is a miss in the making —
            // it breaks the clean streak now (lazy check; the beat itself still resolves
            // as a miss at bar close).
            BreakStreakOnExpiredBeats(release.ReleaseVirtualTime);

            // Below-minimum flicks are additive fizzles outright.
            if (release.Classification == FlickClassification.TooShort)
            {
                RecordFizzle(FlickFizzleCause.TooShort, release);
                return;
            }

            // Off-beat bonus first: an in-window throw at an unconsumed off-beat slot
            // claims that slot and never touches the required beats. Out-of-window
            // throws fall through to the required-beat punishment path.
            RequiredBeat nearestAny = FindNearestUnconsumed(release.ReleaseVirtualTime, requiredOnly: false);
            if (nearestAny != null && nearestAny.IsOffBeat)
            {
                double offError = release.ReleaseVirtualTime - nearestAny.VirtualTime;
                if (Math.Abs(offError) <= GoodThresholdSeconds)
                {
                    ResolveInWindowThrow(nearestAny, offError, release);
                    return;
                }
            }

            // Attribute to the nearest unconsumed required beat in the current bar.
            RequiredBeat target = FindNearestUnconsumed(release.ReleaseVirtualTime, requiredOnly: true);
            if (target == null)
            {
                RecordFizzle(FlickFizzleCause.ExtraRelease, release);
                return;
            }

            double error = release.ReleaseVirtualTime - target.VirtualTime;

            // Out-of-window: the throw consumes its beat as one fizzle, no hitbox test needed.
            if (Math.Abs(error) > GoodThresholdSeconds)
            {
                ConsumeAsFizzle(target, FlickFizzleCause.OutOfWindow, error, release,
                    $"error {error * 1000:+0.0;-0.0}ms > ±{GoodThresholdSeconds * 1000:F1}ms");
                return;
            }

            ResolveInWindowThrow(target, error, release);
        }

        private void BreakStreakOnExpiredBeats(double releaseVirtualTime)
        {
            foreach (var rb in _requiredBeats)
            {
                if (rb.IsOffBeat || rb.Consumed || rb.ExpiryStreakCounted) continue;
                if (rb.VirtualTime + GoodThresholdSeconds >= releaseVirtualTime) continue;
                rb.ExpiryStreakCounted = true;
                if (_cleanStreak > 0)
                    Debug.Log($"[FlickBeatManager] Streak broken — slot {rb.Slot} expired unconsumed " +
                              $"(was {_cleanStreak})");
                _cleanStreak = 0;
            }
        }

        private RequiredBeat FindNearestUnconsumed(double releaseVirtualTime, bool requiredOnly)
        {
            RequiredBeat best = null;
            double bestDelta = double.MaxValue;
            foreach (var rb in _requiredBeats)
            {
                if (rb.Consumed || (requiredOnly && rb.IsOffBeat)) continue;
                double d = Math.Abs(releaseVirtualTime - rb.VirtualTime);
                if (d < bestDelta) { bestDelta = d; best = rb; }
            }
            return best;
        }

        // In-window throw at its attributed beat (required or off-beat): manual aim —
        // the hitbox hook decides hit vs air-ball.
        private void ResolveInWindowThrow(RequiredBeat target, double error, FlickRelease release)
        {
            bool onTarget = AimCheck == null || AimCheck(release);
            if (!onTarget)
            {
                ConsumeAsFizzle(target, FlickFizzleCause.BadAim, error, release,
                    target.IsOffBeat ? "air-ball (off-beat)" : "air-ball");
                return;
            }

            target.Consumed = true;
            target.Grade    = Math.Abs(error) <= PerfectThresholdSeconds
                ? InputMatch.MatchQuality.Perfect
                : InputMatch.MatchQuality.Good;
            target.ErrorMs  = error * 1000.0;

            // Combo: off-beat hits publish 0 and break the streak; clean required hits
            // publish their streak position, then extend it.
            int comboIndex;
            if (target.IsOffBeat)
            {
                comboIndex   = 0;
                _cleanStreak = 0;
            }
            else
            {
                comboIndex = _cleanStreak;
                _cleanStreak++;
            }

            Debug.Log($"[FlickBeatManager] CONSUMED slot {target.Slot}{(target.IsOffBeat ? " (off-beat bonus)" : "")} — " +
                      $"{target.Grade} ({target.ErrorMs:+0.0;-0.0}ms, combo {comboIndex}, " +
                      $"windows P±{PerfectThresholdSeconds * 1000:F1}ms G±{GoodThresholdSeconds * 1000:F1}ms)");

            OnReleaseResolved?.Invoke(new FlickReleaseOutcome(true, target.Grade, null, release, target.IsOffBeat, comboIndex));
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
            _cleanStreak = 0; // any fizzle breaks the combo
            _barFizzles.Add(cause);
            Debug.Log($"[FlickBeatManager] FIZZLE ({cause}) — vt={release.ReleaseVirtualTime:F4}" +
                      (detail != null ? $" [{detail}]" : ""));
            OnReleaseResolved?.Invoke(new FlickReleaseOutcome(consumedBeat, InputMatch.MatchQuality.Miss, cause, release));
        }

        // ── Bar resolution — enemy attack at the seam ─────────────────────────

        private void ResolveBar()
        {
            int barIdx = TimingCoordinator.Instance.GetCurrentBarIndex();
            double nextBarStart = TimingCoordinator.Instance.CurrentBar.BarStartTime
                                + GameConstants.BEATS_PER_LOOP * _beatInterval;

            // Grace seam: the enemy died mid-bar — nothing counts, nobody attacks.
            if (_graceActive)
            {
                _graceActive = false;
                Debug.Log($"[FlickBeatManager] === BAR {barIdx} RESOLVED (GRACE) === enemy died mid-bar — no attack.");
                OnBarResolved?.Invoke(new FlickBarResult(0, true, nextBarStart));

                _barFizzles.Clear();
                TimingCoordinator.Instance.AdvanceToNextBar();
                SetupBar(TimingCoordinator.Instance.CurrentBar);
                return;
            }

            var consumed = new StringBuilder();
            int missedCount = 0;
            var missed = new StringBuilder();

            foreach (var rb in _requiredBeats)
            {
                if (rb.Consumed)
                {
                    if (consumed.Length > 0) consumed.Append(", ");
                    string tag = rb.IsOffBeat ? " [off-beat]" : "";
                    consumed.Append(rb.FizzleCause != null
                        ? $"slot {rb.Slot}{tag}=Fizzled/{rb.FizzleCause}({rb.ErrorMs:+0.0;-0.0}ms)"
                        : $"slot {rb.Slot}{tag}={rb.Grade}({rb.ErrorMs:+0.0;-0.0}ms)");
                }
                else if (!rb.IsOffBeat) // Skipped off-beats are free — bonus only.
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

            // A surviving enemy ALWAYS attacks: flat survivor units + per-fizzle scaling.
            // The seam attack is the only flick damage channel (damagePerMiss inside
            // TakeMissDamage). survivorAttackUnits = 0 restores whiff-on-clean-bar.
            int attackUnits = survivorAttackUnits + fizzleCount;
            if (attackUnits > 0)
            {
                health?.TakeMissDamage(attackUnits);
                Debug.Log($"[FlickBeatManager] ENEMY ATTACK LANDS — {survivorAttackUnits} survivor + " +
                          $"{fizzleCount} fizzle unit(s). HP: {(health != null ? health.CurrentHealth : -1)}");
            }
            else
            {
                Debug.Log($"[FlickBeatManager] ENEMY ATTACK WHIFFS — perfect bar. " +
                          $"HP: {(health != null ? health.CurrentHealth : -1)}");
            }

            OnBarResolved?.Invoke(new FlickBarResult(fizzleCount, false, nextBarStart));

            // Advance to the next bar and arm its required beats.
            _barFizzles.Clear();
            TimingCoordinator.Instance.AdvanceToNextBar();
            SetupBar(TimingCoordinator.Instance.CurrentBar);
        }
    }
}
