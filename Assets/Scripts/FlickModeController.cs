using TMPro;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

namespace KrumbleHut.Drummi.Flick
{
    /// <summary>
    /// Standalone Flick mode — ENDLESS survival. THIN HOST over the shared flick core:
    /// this controller only initializes/arms FlickBeatManager, drives the shared
    /// FlickEncounterDirector (which owns the whole enemy loop), keeps score, and ends
    /// the run when HP depletes. It never touches the enemy or applies damage itself —
    /// the dungeon host (RoomController, Phase 5) drives the exact same director.
    ///
    /// Run rules:
    ///   - Endless bar cycle (half-time required beats) until DungeonHealth depletes.
    ///   - ONE ENEMY PER BAR: enemy damage/retreat/respawn is FlickEncounterDirector's
    ///     job; survivor seam attacks are FlickBeatManager's. This controller never
    ///     applies PLAYER damage itself.
    ///   - Score v1: consumed beats weighted by grade (Perfect/Good — dungeon
    ///     constants 200/100 as serialized defaults); off-beat bonus hits score at
    ///     half value. High score via PlayerPrefs, following the DungeonEvaluator
    ///     pattern with a Flick-specific key.
    ///
    /// Scoring subscribes to the SAME beat-manager events as the director (parallel
    /// subscription): outcome payloads are immutable structs, so ordering is free.
    ///
    /// Music: serialized AudioManager track index + BPM pair (stand-in until dedicated
    /// tracks exist). BGM loops from the shared DSP start; the accent tick is mixed
    /// over it (FlickBeatManager.accentEnabled/accentVolume) so the half-time grid
    /// stays audible. Long-term the music itself will carry the accent. BGM-only audio
    /// means OnPause/OnResume have no rescheduling obligations (PauseHandler +
    /// AudioManager handle the BGM centrally); the accent is fire-and-forget PlayOneShot.
    ///
    /// INSPECTOR SETUP (Flick scene):
    ///   metronome, beatManager, health, director — scene instances (the director
    ///   carries the rest of the flick stack). scoreText — gameplay HUD. Play button →
    ///   StartFromMenu(). Register in GameManager.modeControllers.
    /// </summary>
    public class FlickModeController : ModeController
    {
        #region Inspector
        [Header("Flick Systems")]
        [SerializeField] private Metronome              metronome;
        [SerializeField] private FlickBeatManager       beatManager;
        [SerializeField] private DungeonHealth          health;

        [Tooltip("Shared enemy-loop director — owns the rest of the flick stack " +
                 "(reader, sweep, ball, visual, enemy) and all enemy damage tuning.")]
        [SerializeField] private FlickEncounterDirector director;

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

        [Tooltip("Score page root, closed when a run starts. UIMenuManager.ShowPageImmediate never " +
                 "hides the previous page, so after a game over the Score page would otherwise stay " +
                 "active and its opaque panel would cover the world-space flick rig. FlashCards gets " +
                 "away with this because its gameplay UI is opaque; Flick cannot.")]
        [SerializeField] private GameObject scorePage;
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

            if (beatManager == null || health == null || director == null)
            {
                Debug.LogError("[FlickMode] beatManager, health, or director not assigned — cannot start.");
                return;
            }

            ResetRunState();

            // Runs after the transition cover, so this never pops on screen. See the
            // scorePage tooltip for why the mode must close it itself.
            if (scorePage != null) scorePage.SetActive(false);

            // BGM: loop from the shared DSP start (dungeon floor pattern).
            if (AudioManager.instance != null)
            {
                AudioManager.instance.scheduledStartTime = realDspStartTime;
                AudioManager.instance.PlayMusic(loop: true);
            }

            double beatInterval = 60.0 / bpm;
            double gradingStart = virtualStartTime + leadInBars * GameConstants.BEATS_PER_LOOP * beatInterval;

            beatManager.Initialize(bpm);

            // Scoring first, director second — preserves the pre-extraction handler
            // order (score, then damage) within each event dispatch.
            beatManager.OnReleaseResolved += HandleReleaseResolved;
            beatManager.OnBarResolved     += HandleBarResolved;
            health.OnHealthDepleted       += HandleHealthDepleted;
            director.BeginEncounter(virtualStartTime, beatInterval);

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

            // Off-beat bonus slots score at half value.
            switch (outcome.Grade)
            {
                case InputMatch.MatchQuality.Perfect:
                    _score += outcome.IsOffBeat ? perfectPoints / 2 : perfectPoints;
                    _perfectHits++;
                    break;
                case InputMatch.MatchQuality.Good:
                    _score += outcome.IsOffBeat ? goodPoints / 2 : goodPoints;
                    break;
                default:
                    return; // Consumed as fizzle — no points, no damage.
            }
            UpdateHud();
            // Enemy damage for this outcome is FlickEncounterDirector's parallel handler.
        }

        private void HandleBarResolved(FlickBarResult result)
        {
            if (!_runActive) return;
            _barsSurvived++;
            // Enemy retreat/respawn at the seam is FlickEncounterDirector's parallel handler.
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
        // Unsubscribes the mode's own handlers, then routes the stack shutdown through
        // the director's single teardown entry point — safe to call repeatedly, from
        // any state, including mid-Held (reader track dropped, ball tweens killed).
        private void TearDownStack()
        {
            if (beatManager != null)
            {
                beatManager.OnReleaseResolved -= HandleReleaseResolved;
                beatManager.OnBarResolved     -= HandleBarResolved;
            }
            if (health != null) health.OnHealthDepleted -= HandleHealthDepleted;
            if (director != null) director.TeardownEncounter();
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
