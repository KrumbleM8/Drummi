using System;
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
    /// INSPECTOR SETUP (FlashCards scene):
    ///   metronome, cardController, evaluator — scene instances.
    ///   strikePips (3 Images), streakText — HUD. bestStreakText,
    ///   cardsClearedText — texts on the Score page (written at run end,
    ///   before GameManager shows results; DisplayScore does not touch them).
    ///   Play button → StartFromMenu(). Register in GameManager.modeControllers.
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

        [Header("Run Rules")]
        [Tooltip("Failed cards allowed before the run ends.")]
        [SerializeField, Min(1)] private int maxStrikes = 3;

        [Tooltip("Escalation ladder, in ascending minCardsCleared order. Step 0 should have minCardsCleared = 0.")]
        [SerializeField] private EscalationStep[] escalationSteps =
        {
            new() { minCardsCleared = 0,  vocab = FlashCardVocab.Quarter | FlashCardVocab.Rest,                                                                                             noteRange = new Vector2Int(3, 4) },
            new() { minCardsCleared = 4,  vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.Rest,                                                                noteRange = new Vector2Int(3, 5) },
            new() { minCardsCleared = 10, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Rest,                                 noteRange = new Vector2Int(4, 6) },
            new() { minCardsCleared = 16, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.Rest,        noteRange = new Vector2Int(4, 6) },
            new() { minCardsCleared = 22, vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.SixteenthQuad | FlashCardVocab.Rest, noteRange = new Vector2Int(4, 6) },
        };

        [Header("HUD")]
        [Tooltip("Strike pips, left to right. Filled as strikes accumulate.")]
        [SerializeField] private Image[] strikePips = new Image[3];
        [SerializeField] private Color   pipEmptyColor  = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color   pipStrikeColor = new(0.9f, 0.25f, 0.25f, 1f);
        [SerializeField] private TMP_Text streakText;

        [Header("Results (texts on the Score page)")]
        [SerializeField] private TMP_Text bestStreakText;
        [SerializeField] private TMP_Text cardsClearedText;
        #endregion

        #region State
        // Endless: no song end. TimingCoordinator just needs a horizon far beyond
        // any plausible run (~100k beats ≈ 16 h at 105 BPM).
        private const int EndlessTotalBeats = 100_000;

        private bool _runActive;
        private int  _cardsCleared;
        private int  _strikes;
        private int  _streak;
        private int  _bestStreak;
        private int  _appliedStep = -1;
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
        /// Play-button entry point: applies the mode's music selection (track +
        /// BPM) BEFORE StartGame so GameManager reads the right metronome.bpm —
        /// the same set-music-then-start contract Bongo's carousel follows.
        /// </summary>
        public void StartFromMenu()
        {
            if (metronome != null) metronome.bpm = musicBpm;
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

            evaluator.ResetScore();
            _runActive    = true;
            _cardsCleared = 0;
            _strikes      = 0;
            _streak       = 0;
            _bestStreak   = 0;
            _appliedStep  = -1;
            UpdateHud();

            // BGM: loop from the shared DSP start (dungeon floor pattern).
            if (AudioManager.instance != null)
            {
                AudioManager.instance.scheduledStartTime = realDspStartTime;
                AudioManager.instance.PlayMusic(loop: true);
            }

            cardController.OnCardEvaluated += HandleCardEvaluated;

            // First reveal after the lead-in; grid origin = music start, so the
            // whole-bar lead-in keeps the start on the grid (snap is a no-op).
            var step = ResolveStep(0);
            _appliedStep = StepIndexFor(0);
            double barDuration = GameConstants.BEATS_PER_LOOP * (60.0 / bpm);
            double start       = virtualStartTime + leadInBars * barDuration;
            cardController.StartEndless(start, virtualStartTime, step.vocab, step.noteRange);
        }

        public override void Cleanup()
        {
            if (evaluator != null) evaluator.SaveHighScore();
            if (cardController != null)
            {
                cardController.OnCardEvaluated -= HandleCardEvaluated;
                if (cardController.IsRunning) cardController.StopEncounter();
            }
            _runActive = false;
        }

        public override void ResetToInitialState()
        {
            if (cardController != null)
            {
                cardController.OnCardEvaluated -= HandleCardEvaluated;
                if (cardController.IsRunning) cardController.StopEncounter();
            }
            if (evaluator != null) evaluator.ResetScore();

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
            cardController.StopEncounter();

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
            Debug.Log($"[FlashCardsMode] Escalation step {stepIndex} at {_cardsCleared} cleared → " +
                      $"vocab={step.vocab} notes={step.noteRange.x}-{step.noteRange.y}");
            cardController.SetCardConfig(step.vocab, step.noteRange);
        }

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
