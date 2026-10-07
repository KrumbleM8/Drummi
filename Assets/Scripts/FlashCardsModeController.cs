using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Standalone Flash Cards mode — ENDLESS free play. Continuous 2-bar
    /// reveal/perform card cycles (driven by FlashCardController.StartEndless)
    /// until the player collects <see cref="maxStrikes"/> Failed cards.
    ///
    /// Run rules:
    ///   - Strike  = card graded Failed. 3 strikes = run over → results.
    ///   - Cleared = card graded above Failed. Drives the escalation ladder.
    ///   - Streak  = consecutive AllPerfect cards. AllPerfect requires
    ///     Inputs == Beats (DungeonEvaluator.DetermineGrade), so spam breaks
    ///     streaks by definition — no extra evaluator logic.
    ///   - Score accumulates via DungeonEvaluator's existing per-card scoring.
    ///
    /// Escalation ladder: serialized steps, applied via SetCardConfig from the
    /// OnCardEvaluated handler — config lands on the NEXT card, never mid-card
    /// (BuildCard reads config at each reveal-bar start).
    ///
    /// Music: serialized AudioManager track index + BPM pair (stand-in until
    /// dedicated tracks exist). BGM loops (dungeon floor pattern); the reveal
    /// is silent, so there is no per-beat scheduled audio and OnPause/OnResume
    /// have no rescheduling obligations (PauseHandler + AudioManager handle
    /// the BGM centrally).
    ///
    /// INSPECTOR SETUP (FlashCardsMenu scene):
    ///   metronome, cardController, evaluator — scene instances.
    ///   strikePips (3 Images), streakText — HUD. bestStreakText,
    ///   cardsClearedText — texts on the Score page (written at run end,
    ///   before GameManager shows results; DisplayScore does not touch them).
    ///   Play button → StartFromMenu(). Double Time button → StartFromMenuDoubleTime().
    ///   (Both are legacy paths — the signpost menu in this scene starts runs
    ///   via StartRun(definition) and never shows the in-scene menu page.)
    ///   Again button (results) → RestartFromResults() so retries keep the variant.
    ///   Register in GameManager.modeControllers.
    /// </summary>
    public class FlashCardsModeController : ModeController
    {
        #region Types
        [Serializable]
        public class EscalationStep
        {
            [Tooltip("Ladder step activates once this many cards have been CLEARED (graded above Failed).")]
            public int minCardsCleared;

            [Tooltip("Template vocabulary from this step onward.")]
            public FlashCardVocab vocab = FlashCardVocab.Quarter | FlashCardVocab.Rest;

            [Tooltip("Note-bearing-square range from this step onward (x = min, y = max).")]
            public Vector2Int noteRange = new(3, 4);

            [Tooltip("Chance [0,1] that a card is an in-run DOUBLE TIME card (whole reveal/perform cycle " +
                     "at 2x the grid) from this step onward. Forced to 0 in the Double Time menu variant — " +
                     "stacking to 4x is not playable.")]
            [Range(0f, 1f)] public float doubleTimeChance = 0f;

            [Tooltip("BASE rows per card (4 beats each) from this step onward. 1 = quick single-bar cards. " +
                     "noteRange is authored for THIS size; expanded cards scale it proportionally.")]
            [Min(1)] public int rows = 1;

            [Tooltip("Rows of an EXPANDED card at this step (0 or <= rows = never expands). Occasional " +
                     "bigger cards: 2 early, 3/5 as late-run rares.")]
            [Min(0)] public int expandRows = 2;

            [Tooltip("RandomChance trigger only: chance [0,1] that a card expands to expandRows.")]
            [Range(0f, 1f)] public float expandChance = 0.2f;

            [Tooltip("STAGE tempo from this step onward: beat-grid multiplier relative to the run-start BPM " +
                     "(absolute, not compounding). When it increases between steps, a two-bar telegraphed " +
                     "break (sting + banner, then count-in clicks at the new tempo) precedes the next card.")]
            [Min(1f)] public float tempoMultiplier = 1f;
        }

        /// <summary>How expanded (bigger) cards are triggered during a run.</summary>
        public enum ExpandTrigger
        {
            [Tooltip("Each card rolls expandChance independently.")]
            RandomChance,
            [Tooltip("Every Nth card expands (expandEveryNCards), no randomness.")]
            FixedCadence,
        }
        #endregion

        #region Inspector
        [Header("Flash Cards Systems")]
        [SerializeField] private Metronome           metronome;
        [SerializeField] private FlashCardController cardController;
        [SerializeField] private DungeonEvaluator    evaluator;

        [Header("Music (stand-in until dedicated tracks)")]
        [Tooltip("Index into AudioManager.musicTracks.")]
        [SerializeField] private int musicTrackIndex = 1;

        [Tooltip("Tempo of the selected track. Applied to the Metronome before StartGame.")]
        [SerializeField] private int musicBpm = 105;

        [Tooltip("Bars (8 beats) of music before the first reveal bar — same idea as the dungeon lead-in.")]
        [SerializeField, Min(0)] private int leadInBars = 1;

        [Tooltip("Beat-grid multiplier for Double Time. The BGM plays unchanged; only the metronome/card grid speeds up. Keep musicBpm * multiplier a whole number (GameManager plumbs bpm as int).")]
        [SerializeField, Min(1f)] private float doubleTimeMultiplier = 2f;

        [Header("Run Rules")]
        [Tooltip("Failed cards allowed before the run ends.")]
        [SerializeField, Min(1)] private int maxStrikes = 3;

        [Tooltip("Escalation ladder, in ascending minCardsCleared order. Step 0 should have minCardsCleared = 0.")]
        [SerializeField] private EscalationStep[] escalationSteps =
        {
            new() { minCardsCleared = 0,  vocab = FlashCardVocab.Quarter | FlashCardVocab.Rest,                                                                                             noteRange = new Vector2Int(2, 3), rows = 1, expandRows = 2, expandChance = 0.20f },
            new() { minCardsCleared = 4,  vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.Rest,                                                                noteRange = new Vector2Int(2, 4), rows = 1, expandRows = 2, expandChance = 0.30f },
            // doubleTimeChance intentionally 0 everywhere for now — the in-run DT
            // mechanic is built and verified but disabled pending tuning; re-enable
            // per step when wanted.
            new() { minCardsCleared = 10, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Rest,                                 noteRange = new Vector2Int(3, 4), rows = 1, expandRows = 2, expandChance = 0.35f, tempoMultiplier = 1.1f },
            new() { minCardsCleared = 16, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.Rest,        noteRange = new Vector2Int(3, 4), rows = 1, expandRows = 3, expandChance = 0.30f, tempoMultiplier = 1.2f },
            new() { minCardsCleared = 22, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.SixteenthQuad | FlashCardVocab.Rest, noteRange = new Vector2Int(3, 4), rows = 1, expandRows = 5, expandChance = 0.25f, tempoMultiplier = 1.3f },
            new() { minCardsCleared = 28, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.SixteenthQuad | FlashCardVocab.Rest, noteRange = new Vector2Int(3, 4), rows = 1, expandRows = 5, expandChance = 0.25f, tempoMultiplier = 1.4f },
            new() { minCardsCleared = 35, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.SixteenthQuad | FlashCardVocab.Rest, noteRange = new Vector2Int(3, 4), rows = 1, expandRows = 5, expandChance = 0.25f, tempoMultiplier = 1.5f },
        };

        [Header("Card Expansion")]
        [Tooltip("How expanded (bigger) cards trigger: independent per-card chance, or a strict every-Nth-card cadence.")]
        [SerializeField] private ExpandTrigger expandTrigger = ExpandTrigger.RandomChance;

        [Tooltip("FixedCadence only: every Nth card is an expanded card.")]
        [SerializeField, Min(2)] private int expandEveryNCards = 4;

        [Header("Stages")]
        [Tooltip("Beats of telegraphed break at a stage-up, counted at the NEW tempo. With 8 (two 4-beat " +
                 "bars): bar 1 = sting + banner, bar 2 = four count-in clicks at the new speed.")]
        [SerializeField, Min(0)] private int stageBreakBeats = 8;

        [Tooltip("One-shot sting at the stage-up break start (WarioWare speed-up moment).")]
        [SerializeField] private AudioClip stageUpStingClip;

        [Tooltip("Count-in click, played on each of the break's last four beats at the new tempo.")]
        [SerializeField] private AudioClip stageCountInClip;

        [Tooltip("Banner shown during the stage-up break (e.g. 'SPEED UP! x1.2'). Inactive otherwise.")]
        [SerializeField] private TMP_Text stageBannerText;

        [Tooltip("WarioWare-style: pitch the BGM (AudioManager speakers[0]) by the stage multiplier so the " +
                 "music speeds up (and rises in key) with the grid. Reset to 1 at run start/end.")]
        [SerializeField] private bool pitchMusicWithStages = true;

        [Header("HUD")]
        [Tooltip("Strike pips, left to right. Filled as strikes accumulate.")]
        [SerializeField] private Image[] strikePips = new Image[3];
        [SerializeField] private Color   pipEmptyColor  = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color   pipStrikeColor = new(0.9f, 0.25f, 0.25f, 1f);
        [SerializeField] private TMP_Text streakText;

        [Header("Results (texts on the Score page)")]
        [SerializeField] private TMP_Text bestStreakText;
        [SerializeField] private TMP_Text cardsClearedText;

        [Tooltip("The Score page root. Hidden at run start — UIMenuManager never deactivates it, so without this it lingers behind GameplayUI on restarts.")]
        [SerializeField] private GameObject scorePage;
        #endregion

        #region State
        // Endless: no song end. TimingCoordinator just needs a horizon far beyond
        // any plausible run (~100k beats ≈ 16 h at 105 BPM).
        private const int EndlessTotalBeats = 100_000;

        // Active beat-grid multiplier for the current/last run (1 = normal,
        // doubleTimeMultiplier = Double Time). Set by BeginRun; read by
        // RestartFromResults so a retry preserves the chosen variant.
        private float _activeTempoMultiplier = 1f;

        // High-score key of the current/last run — kept alongside the multiplier
        // so RestartFromResults preserves the full variant identity.
        private string _activeHighScoreKey = "FlashCardsHS";

        private bool _runActive;
        private int  _cardsCleared;
        private int  _strikes;
        private int  _streak;
        private int  _bestStreak;
        private int  _appliedStep = -1;

        // Stage tempo currently applied/queued (relative to run-start BPM).
        // Serialized steps authored before tempoMultiplier existed deserialize 0 —
        // TempoFor floors to 1.
        private float     _appliedTempo = 1f;
        private Coroutine _stageBreakRoutine;
        #endregion

        #region ModeController — Identity & Scoring
        public override string ModeId                   => "FlashCards";
        public override int    BarsBeforeEndForFinalBar => 1;
        public override int    Score                    => evaluator != null ? evaluator.Score : 0;
        public override int    TotalPerfectHits         => evaluator != null ? evaluator.TotalPerfectHits : 0;
        public override bool   IsNewHighScore           => evaluator != null && evaluator.IsNewHighScore;
        #endregion

        #region Menu Entry
        /// <summary>
        /// Signpost-menu entry point: starts a run with the given variant.
        /// Called by FlashCardsMenuController after the menu objects are hidden
        /// and the screen is covered (same scene — no load, no static handoff).
        /// </summary>
        public void StartRun(FlashCardsModeDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[FlashCardsMode] StartRun called with a null definition.");
                return;
            }

            // Defensive — the scene's GameManager should already default to this
            // mode, but SetMode is idempotent and cheap.
            GameManager.instance.SetMode(ModeId);
            BeginRun(definition.tempoMultiplier, definition.highScoreKey);
        }

        /// <summary>Play-button entry point: normal speed (1x grid).</summary>
        public void StartFromMenu() => BeginRun(1f, "FlashCardsHS");

        /// <summary>Double Time button entry point: 2x beat grid over the same music.</summary>
        public void StartFromMenuDoubleTime() => BeginRun(doubleTimeMultiplier, "FlashCardsDoubleHS");

        /// <summary>Again-button entry point: restarts at whatever speed the last run used.</summary>
        public void RestartFromResults() => BeginRun(_activeTempoMultiplier, _activeHighScoreKey);

        /// <summary>
        /// Applies the mode's music selection (track + BPM, scaled by the beat-grid
        /// multiplier) BEFORE StartGame so GameManager reads the right metronome.bpm —
        /// the same set-music-then-start contract Bongo's carousel follows. The BGM
        /// track itself is untouched; an integer multiplier keeps the faster grid
        /// aligned with the music (e.g. 210 vs 105).
        /// </summary>
        private void BeginRun(float tempoMultiplier, string highScoreKey)
        {
            _activeTempoMultiplier = tempoMultiplier;
            _activeHighScoreKey    = highScoreKey;
            if (metronome != null) metronome.bpm = musicBpm * tempoMultiplier;
            if (evaluator != null) evaluator.SetHighScoreKey(highScoreKey);
            if (AudioManager.instance != null) AudioManager.instance.selectedSongIndex = musicTrackIndex;
            GameManager.instance.StartGame();
        }
        #endregion

        #region ModeController — Lifecycle
        public override int CalculateTotalBeats(int bpm) => EndlessTotalBeats;

        public override void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
        {
            Debug.Log("[FlashCardsMode] Starting endless run");

            if (cardController == null || evaluator == null)
            {
                Debug.LogError("[FlashCardsMode] cardController or evaluator not assigned — cannot start.");
                return;
            }

            // Hide the previous run's results page (happens behind the cover).
            // GameManager's ShowPageImmediate reactivates it at the next run end.
            if (scorePage != null) scorePage.SetActive(false);

            evaluator.ResetScore();
            _runActive    = true;
            _cardsCleared = 0;
            _strikes      = 0;
            _streak       = 0;
            _bestStreak   = 0;
            _appliedStep  = -1;
            EndStageBreakPresentation();
            UpdateHud();

            // BGM: loop from the shared DSP start (dungeon floor pattern).
            if (AudioManager.instance != null)
            {
                AudioManager.instance.scheduledStartTime = realDspStartTime;
                AudioManager.instance.PlayMusic(loop: true);
            }

            cardController.OnCardEvaluated += HandleCardEvaluated;
            cardController.OnStageUp      += HandleStageUp;

            // First reveal after the lead-in; grid origin = music start, so the
            // whole-bar lead-in keeps the start on the grid (snap is a no-op).
            var step = ResolveStep(0);
            _appliedStep  = StepIndexFor(0);
            _appliedTempo = TempoFor(step);
            // Scale lead-in bars by the tempo multiplier so the real-time lead-in
            // matches 1x and the first reveal still lands on a real BGM bar
            // boundary (2 doubled bars = 1 real bar). Integer multipliers only.
            int effectiveLeadInBars = leadInBars * Mathf.Max(1, Mathf.RoundToInt(_activeTempoMultiplier));
            double barDuration = GameConstants.BEATS_PER_LOOP * (60.0 / bpm);
            double start       = virtualStartTime + effectiveLeadInBars * barDuration;
            cardController.StartEndless(start, virtualStartTime, step.vocab, step.noteRange, EffectiveDoubleTimeChance(step), SlotCountFor(step),
                                        ExpandSlotsFor(step), step.expandChance, ExpandCadence());
        }

        public override void Cleanup()
        {
            if (evaluator != null) evaluator.SaveHighScore();
            if (cardController != null)
            {
                cardController.OnCardEvaluated -= HandleCardEvaluated;
                cardController.OnStageUp      -= HandleStageUp;
                if (cardController.IsRunning) cardController.StopEncounter();
            }
            EndStageBreakPresentation();
            RestoreRunBpm();
            _runActive = false;
        }

        public override void ResetToInitialState()
        {
            if (cardController != null)
            {
                cardController.OnCardEvaluated -= HandleCardEvaluated;
                cardController.OnStageUp      -= HandleStageUp;
                if (cardController.IsRunning) cardController.StopEncounter();
            }
            if (evaluator != null) evaluator.ResetScore();

            EndStageBreakPresentation();
            RestoreRunBpm();
            _runActive    = false;
            _cardsCleared = 0;
            _strikes      = 0;
            _streak       = 0;
            _bestStreak   = 0;
            _appliedStep  = -1;
            UpdateHud();
            Debug.Log("[FlashCardsMode] Reset to initial state");
        }

        public override void OnPause()
        {
            if (evaluator != null) evaluator.SaveHighScore();
            // No scheduled per-beat audio exists (silent reveal); BGM pause is
            // handled centrally by PauseHandler → AudioManager.PauseAllAudio.
        }

        // OnResume: intentionally not overridden — the card cycle is GameClock-polled
        // and freezes/resumes automatically; BGM resume is central (accumulatedPauseDuration).
        #endregion

        #region Run Logic
        private void HandleCardEvaluated(int cardIndex, EvaluationResult result)
        {
            if (!_runActive) return;
            if (result == null) return; // evaluation skipped (defensive) — no strike, no clear

            if (result.Grade == EvaluationGrade.Failed)
            {
                _strikes++;
                _streak = 0;
                Debug.Log($"[FlashCardsMode] STRIKE {_strikes}/{maxStrikes} (card {cardIndex})");
            }
            else
            {
                _cardsCleared++;
                if (result.Grade == EvaluationGrade.AllPerfect)
                {
                    _streak++;
                    if (_streak > _bestStreak) _bestStreak = _streak;
                }
                else
                {
                    _streak = 0;
                }
            }

            UpdateHud();

            if (_strikes >= maxStrikes)
            {
                EndRun();
                return;
            }

            ApplyLadder();
        }

        private void EndRun()
        {
            Debug.Log($"[FlashCardsMode] RUN OVER — score={Score} cleared={_cardsCleared} bestStreak={_bestStreak}");
            _runActive = false;
            cardController.OnCardEvaluated -= HandleCardEvaluated;
            cardController.OnStageUp      -= HandleStageUp;
            cardController.StopEncounter();
            EndStageBreakPresentation();

            // Write the extra results lines BEFORE CompletMode — GameManager's
            // results sequence shows the Score page but never touches these texts.
            if (bestStreakText   != null) bestStreakText.text   = $"Best Streak: {_bestStreak}";
            if (cardsClearedText != null) cardsClearedText.text = $"Cards Cleared: {_cardsCleared}";

            CompletMode();
        }

        /// <summary>Applies the highest ladder step reached, once, when it changes.</summary>
        private void ApplyLadder()
        {
            int stepIndex = StepIndexFor(_cardsCleared);
            if (stepIndex == _appliedStep || stepIndex < 0) return;

            _appliedStep = stepIndex;
            var step = escalationSteps[stepIndex];
            float dtChance = EffectiveDoubleTimeChance(step);
            Debug.Log($"[FlashCardsMode] Escalation step {stepIndex} at {_cardsCleared} cleared → " +
                      $"vocab={step.vocab} notes={step.noteRange.x}-{step.noteRange.y} dtChance={dtChance:F2} " +
                      $"rows={step.rows} expandRows={step.expandRows} expandChance={step.expandChance:F2} " +
                      $"tempo=x{TempoFor(step):F2}");
            cardController.SetCardConfig(step.vocab, step.noteRange, dtChance, SlotCountFor(step),
                                         ExpandSlotsFor(step), step.expandChance, ExpandCadence());

            // STAGE UP: the step carries a higher tempo — queue the base-grid retune
            // (lands at the same next-card boundary as the config above, with the
            // telegraphed break in between).
            float tempo = TempoFor(step);
            if (tempo > _appliedTempo + 0.001f)
            {
                _appliedTempo = tempo;
                cardController.QueueStageTempo(tempo, stageBreakBeats);
            }
        }

        /// <summary>
        /// In-run Double Time only applies to the normal-speed variant — in the
        /// Double Time menu variant the whole grid already runs at 2x, and stacking
        /// to an effective 4x (Perfect window ~±7 ms) is not playable.
        /// </summary>
        private float EffectiveDoubleTimeChance(EscalationStep step)
            => _activeTempoMultiplier > 1f ? 0f : step.doubleTimeChance;

        /// <summary>
        /// Beat slots for a step's BASE cards: rows x 4. Defensive floor of 1 row —
        /// scene-serialized steps authored before this field existed deserialize
        /// rows = 0.
        /// </summary>
        private static int SlotCountFor(EscalationStep step)
            => Mathf.Max(1, step.rows) * 4;

        /// <summary>
        /// Beat slots for a step's EXPANDED cards; 0 = this step never expands
        /// (expandRows unset or not larger than the base).
        /// </summary>
        private static int ExpandSlotsFor(EscalationStep step)
            => step.expandRows > Mathf.Max(1, step.rows) ? step.expandRows * 4 : 0;

        /// <summary>Cards-per-expansion in FixedCadence mode; 0 = RandomChance mode.</summary>
        private int ExpandCadence()
            => expandTrigger == ExpandTrigger.FixedCadence ? Mathf.Max(2, expandEveryNCards) : 0;

        /// <summary>
        /// Step tempo with a defensive floor of 1 — scene-serialized steps authored
        /// before tempoMultiplier existed deserialize 0 (same gotcha as rows).
        /// </summary>
        private static float TempoFor(EscalationStep step)
            => Mathf.Max(1f, step.tempoMultiplier);

        private int StepIndexFor(int cardsCleared)
        {
            int best = -1;
            for (int i = 0; i < escalationSteps.Length; i++)
                if (escalationSteps[i].minCardsCleared <= cardsCleared) best = i;
            return best;
        }

        private EscalationStep ResolveStep(int cardsCleared)
        {
            int i = StepIndexFor(cardsCleared);
            if (i >= 0) return escalationSteps[i];
            Debug.LogWarning("[FlashCardsMode] No escalation step with minCardsCleared <= 0 — using built-in Quarter|Rest default.");
            return new EscalationStep();
        }
        #endregion

        #region Stage Break Presentation
        /// <summary>
        /// WarioWare-style stage-up moment, driven by FlashCardController.OnStageUp
        /// at the break start. Bar 1: sting + banner; bar 2 (last four beats):
        /// count-in clicks at the NEW tempo so the player hears the speed before
        /// performing at it. GameClock-polled, so pausing freezes it for free.
        /// </summary>
        private void HandleStageUp(float multiplier, double breakStart, int breakBeats, double beatInterval)
        {
            if (_stageBreakRoutine != null) StopCoroutine(_stageBreakRoutine);
            _stageBreakRoutine = StartCoroutine(StageBreakRoutine(multiplier, breakStart, breakBeats, beatInterval));
        }

        private IEnumerator StageBreakRoutine(float multiplier, double breakStart, int breakBeats, double beatInterval)
        {
            PlayStageOneShot(stageUpStingClip);
            if (pitchMusicWithStages) SetMusicPitch(multiplier);
            if (stageBannerText != null)
            {
                stageBannerText.text = $"SPEED UP!\n<size=60%>x{multiplier:0.0#}</size>";
                stageBannerText.gameObject.SetActive(true);
            }

            // Count-in clicks on the break's last four beats (the whole break when shorter).
            int countIn = Mathf.Min(4, breakBeats);
            for (int i = breakBeats - countIn; i < breakBeats; i++)
            {
                double beatTime = breakStart + i * beatInterval;
                while (GameClock.Instance != null && GameClock.Instance.GameTime < beatTime) yield return null;
                PlayStageOneShot(stageCountInClip);
            }

            double breakEnd = breakStart + breakBeats * beatInterval;
            while (GameClock.Instance != null && GameClock.Instance.GameTime < breakEnd) yield return null;

            if (stageBannerText != null) stageBannerText.gameObject.SetActive(false);
            _stageBreakRoutine = null;
        }

        /// <summary>
        /// Stops any in-flight break presentation, hides the banner, and returns the
        /// BGM to normal pitch (run start/end/reset). In StartMode this runs BEFORE
        /// PlayMusic, so a new run never inherits the previous run's staged pitch.
        /// </summary>
        private void EndStageBreakPresentation()
        {
            if (_stageBreakRoutine != null)
            {
                StopCoroutine(_stageBreakRoutine);
                _stageBreakRoutine = null;
            }
            if (stageBannerText != null) stageBannerText.gameObject.SetActive(false);
            SetMusicPitch(1f);
        }

        /// <summary>BGM pitch — speakers[0] is the music source (AudioManager.PlayMusic).</summary>
        private static void SetMusicPitch(float pitch)
        {
            var audio = AudioManager.instance;
            if (audio == null || audio.speakers == null || audio.speakers.Length < 1) return;
            if (audio.speakers[0] == null) return;
            audio.speakers[0].pitch = pitch;
        }

        /// <summary>
        /// Returns the metronome to the run's variant baseline so a run that ended
        /// at stage 5 never leaks a staged bpm to menus/results (BeginRun re-tunes
        /// every run anyway — belt and braces, mirroring RestoreBaseBpm's intent).
        /// </summary>
        private void RestoreRunBpm()
        {
            if (metronome != null) metronome.bpm = musicBpm * _activeTempoMultiplier;
            _appliedTempo = 1f;
        }

        /// <summary>Misc-speaker one-shot — same channel as the Double Time cue.</summary>
        private static void PlayStageOneShot(AudioClip clip)
        {
            if (clip == null) return;
            var audio = AudioManager.instance;
            if (audio == null || audio.speakers == null || audio.speakers.Length < 3) return;
            if (audio.speakers[2] == null) return;
            audio.speakers[2].PlayOneShot(clip);
        }
        #endregion

        #region HUD
        private void UpdateHud()
        {
            if (strikePips != null)
                for (int i = 0; i < strikePips.Length; i++)
                    if (strikePips[i] != null)
                        strikePips[i].color = i < _strikes ? pipStrikeColor : pipEmptyColor;

            if (streakText != null)
                streakText.text = $"Streak: {_streak}  Best: {_bestStreak}";
        }
        #endregion
    }
}
