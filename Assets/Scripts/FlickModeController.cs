using TMPro;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

namespace KrumbleHut.Drummi.Flick
{
    /// <summary>
    /// Standalone Flick mode — ENDLESS survival. Hosts the shared flick gameplay core
    /// (FlickInputReader / FlickBeatManager / FlickTargetSweep / FlickBallController /
    /// FlickTargetVisual) without forking any evaluation logic: this controller only
    /// ARMS the stack, keeps score, and ends the run when HP depletes.
    ///
    /// Run rules:
    ///   - Endless bar cycle (half-time required beats) until DungeonHealth depletes.
    ///   - Each bar's fizzles land as the enemy seam attack (FlickBeatManager); this
    ///     controller never applies damage itself.
    ///   - Score v1: consumed beats weighted by grade (Perfect/Good — dungeon
    ///     constants 200/100 as serialized defaults). High score via PlayerPrefs,
    ///     following the DungeonEvaluator pattern with a Flick-specific key.
    ///
    /// Music: serialized AudioManager track index + BPM pair (stand-in until dedicated
    /// tracks exist). BGM loops from the shared DSP start; the accent tick is mixed
    /// over it (FlickBeatManager.accentEnabled/accentVolume) so the half-time grid
    /// stays audible. Long-term the music itself will carry the accent. BGM-only audio
    /// means OnPause/OnResume have no rescheduling obligations (PauseHandler +
    /// AudioManager handle the BGM centrally); the accent is fire-and-forget PlayOneShot.
    ///
    /// INSPECTOR SETUP (Flick scene):
    ///   metronome, beatManager, flickReader, targetSweep, ball, targetVisual, health —
    ///   scene instances. scoreText — gameplay HUD. Play button → StartFromMenu().
    ///   Register in GameManager.modeControllers.
    /// </summary>
    public class FlickModeController : ModeController
    {
        #region Inspector
        [Header("Flick Systems")]
        [SerializeField] private Metronome           metronome;
        [SerializeField] private FlickBeatManager    beatManager;
        [SerializeField] private FlickInputReader    flickReader;
        [SerializeField] private FlickTargetSweep    targetSweep;
        [SerializeField] private FlickBallController ball;
        [SerializeField] private FlickTargetVisual   targetVisual;
        [SerializeField] private DungeonHealth       health;

        [Header("Music (stand-in until dedicated tracks)")]
        [Tooltip("Index into AudioManager.musicTracks.")]
        [SerializeField] private int musicTrackIndex = 1;

        [Tooltip("Tempo of the selected track. Applied to the Metronome before StartGame.")]
        [SerializeField] private int musicBpm = 105;

        [Tooltip("Bars (8 beats) of music before grading starts. Sweep and ball are live " +
                 "during the lead-in; nothing is graded or damaged.")]
        [SerializeField, Min(0)] private int leadInBars = 1;

        [Header("Scoring (dungeon constants)")]
        [Tooltip("Points per Perfect consumed beat.")]
        [SerializeField] private int perfectPoints = 200;

        [Tooltip("Points per Good consumed beat.")]
        [SerializeField] private int goodPoints = 100;

        [Tooltip("PlayerPrefs key for this mode's high score — separate from the dungeon record.")]
        [SerializeField] private string highScoreKey = "FlickHS";

        [Header("HUD")]
        [SerializeField] private TMP_Text scoreText;
        #endregion

        #region State
        // Endless: no song end. TimingCoordinator just needs a horizon far beyond
        // any plausible run (~100k beats ≈ 16 h at 105 BPM) — the FlashCards pattern.
        private const int EndlessTotalBeats = 100_000;

        private bool _runActive;
        private int  _score;
        private int  _perfectHits;
        private int  _barsSurvived;
        private int  _previousHighScore;
        #endregion

        #region ModeController — Identity & Scoring
        /// <inheritdoc />
        public override string ModeId                   => "Flick";
        /// <inheritdoc />
        public override int    BarsBeforeEndForFinalBar => 1;
        /// <inheritdoc />
        public override int    Score                    => _score;
        /// <inheritdoc />
        public override int    TotalPerfectHits         => _perfectHits;
        /// <inheritdoc />
        public override bool   IsNewHighScore           => _score > _previousHighScore;
        #endregion

        #region Menu Entry
        /// <summary>
        /// Play-button entry point: applies the mode's music selection (track + BPM)
        /// BEFORE StartGame so GameManager reads the right metronome.bpm — the same
        /// set-music-then-start contract Flash Cards follows.
        /// </summary>
        public void StartFromMenu()
        {
            if (metronome != null) metronome.bpm = musicBpm;
            if (AudioManager.instance != null) AudioManager.instance.selectedSongIndex = musicTrackIndex;
            GameManager.instance.StartGame();
        }
        #endregion

        #region ModeController — Lifecycle
        /// <inheritdoc />
        public override int CalculateTotalBeats(int bpm) => EndlessTotalBeats;

        /// <inheritdoc />
        public override void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
        {
            Debug.Log("[FlickMode] Starting endless run");

            if (beatManager == null || health == null || targetSweep == null)
            {
                Debug.LogError("[FlickMode] beatManager, health, or targetSweep not assigned — cannot start.");
                return;
            }

            ResetRunState();

            // BGM: loop from the shared DSP start (dungeon floor pattern).
            if (AudioManager.instance != null)
            {
                AudioManager.instance.scheduledStartTime = realDspStartTime;
                AudioManager.instance.PlayMusic(loop: true);
            }

            double beatInterval = 60.0 / bpm;
            double gradingStart = virtualStartTime + leadInBars * GameConstants.BEATS_PER_LOOP * beatInterval;

            targetSweep.StartSweep(virtualStartTime, beatInterval);

            beatManager.Initialize(bpm);
            beatManager.AimCheck = r => targetSweep.IsThrowOnTarget(r.DragVector, r.StartPosition);
            beatManager.OnReleaseResolved += HandleReleaseResolved;
            beatManager.OnBarResolved     += HandleBarResolved;
            health.OnHealthDepleted       += HandleHealthDepleted;

            _runActive = true;
            beatManager.Arm(gradingStart);
        }

        /// <inheritdoc />
        public override void Cleanup()
        {
            SaveHighScore();
            TearDownStack();
            _runActive = false;
        }

        /// <inheritdoc />
        public override void ResetToInitialState()
        {
            TearDownStack();
            ResetRunState();
            _runActive = false;
            Debug.Log("[FlickMode] Reset to initial state");
        }

        /// <inheritdoc />
        public override void OnPause()
        {
            SaveHighScore();
            // BGM-only audio: no scheduled per-beat audio to cancel/reschedule — the
            // accent tick is fire-and-forget and BGM pause is handled centrally by
            // PauseHandler → AudioManager.PauseAllAudio.
        }

        // OnResume: intentionally not overridden — sweep, respawn polling, accent cues,
        // and the bar cycle are all GameClock-polled and freeze/resume automatically;
        // BGM resume is central (accumulatedPauseDuration).
        #endregion

        #region Run Logic
        private void HandleReleaseResolved(FlickReleaseOutcome outcome)
        {
            if (!_runActive || !outcome.Consumed) return;

            switch (outcome.Grade)
            {
                case InputMatch.MatchQuality.Perfect:
                    _score += perfectPoints;
                    _perfectHits++;
                    break;
                case InputMatch.MatchQuality.Good:
                    _score += goodPoints;
                    break;
                default:
                    return; // Consumed as fizzle — no points.
            }
            UpdateHud();
        }

        private void HandleBarResolved(int fizzleCount)
        {
            if (!_runActive) return;
            _barsSurvived++;
        }

        private void HandleHealthDepleted()
        {
            if (!_runActive) return;
            Debug.Log($"[FlickMode] RUN OVER — score={_score} perfects={_perfectHits} bars={_barsSurvived}");
            _runActive = false;

            // Same end mechanism as Flash Cards: CompletMode → GameManager results.
            CompletMode();
        }
        #endregion

        #region Teardown / Reset
        // Unsubscribes and disarms everything — safe to call repeatedly, from any
        // state, including mid-Held (reader track dropped, ball tweens killed).
        private void TearDownStack()
        {
            if (beatManager != null)
            {
                beatManager.OnReleaseResolved -= HandleReleaseResolved;
                beatManager.OnBarResolved     -= HandleBarResolved;
                beatManager.StopEncounter();
                beatManager.AimCheck = null;
            }
            if (health != null) health.OnHealthDepleted -= HandleHealthDepleted;
            if (targetSweep != null) targetSweep.StopSweep();
            if (flickReader != null) flickReader.CancelActiveTracking();
            if (ball != null) ball.ResetToHome();
            if (targetVisual != null) targetVisual.ResetVisual();
        }

        private void ResetRunState()
        {
            _previousHighScore = PlayerPrefs.GetInt(highScoreKey, 0);
            _score        = 0;
            _perfectHits  = 0;
            _barsSurvived = 0;
            if (health != null) health.ResetHealth();
            UpdateHud();
        }

        private void SaveHighScore()
        {
            int saved = PlayerPrefs.GetInt(highScoreKey, 0);
            if (_score > saved) { PlayerPrefs.SetInt(highScoreKey, _score); PlayerPrefs.Save(); }
        }

        private void UpdateHud()
        {
            if (scoreText != null) scoreText.text = _score.ToString();
        }
        #endregion
    }
}
