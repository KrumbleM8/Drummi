using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates Dungeon mode gameplay: pattern generation, spawn audio scheduling,
/// input-window gating, and bar evaluation.
/// Parallel to BeatGenerator — mirrors the same Update()-polling state machine and
/// pause/resume audio-rescheduling approach.
///
/// BAR CYCLE (8 beats, same as Bongo):
///   Beats 1-4  (spawn phase)   : enemies spawn with scheduled audio + slider markers
///   Beats 5-7.5 (response phase): player input window open
///   Beat  7.5  (evaluation)    : inputs matched against pattern, score updated
///   New pattern generated and scheduled for the next bar immediately after.
///
/// INSPECTOR SETUP:
///   metronome          — shared Metronome (BPM source for beat interval)
///   evaluator          — DungeonEvaluator on this or a sibling GameObject
///   inputReader        — DungeonInputReader
///   visualController   — DungeonVisualController
///   enemyAudioSources  — 3 AudioSources (index 0=Left, 1=Center, 2=Right)
///   enemySoundClips    — 3 AudioClips   (index 0=Left, 1=Center, 2=Right)
/// </summary>
public class DungeonBeatManager : MonoBehaviour
{
    #region Inspector
    [Header("Core Systems")]
    [SerializeField] public  Metronome              metronome;
    [SerializeField] private DungeonEvaluator        evaluator;
    [SerializeField] private DungeonInputReader      inputReader;
    [SerializeField] private DungeonVisualController visualController;

    [Header("Spawn Audio (index 0=Left, 1=Center, 2=Right)")]
    [SerializeField] private AudioSource[] enemyAudioSources;
    [SerializeField] private AudioClip[]   enemySoundClips;

    [Header("Song Progression")]
    [SerializeField] private float delayBeforeResults  = 2f;
    [SerializeField] private int   maxBarsPerEncounter = 8;
    #endregion

    #region Events
    public System.Action OnSongComplete;
    public System.Action OnFinalBarComplete;
    #endregion

    #region State
    private GameState              currentState = GameState.Uninitialized;
    private DungeonPatternGenerator patternGenerator;
    private List<DungeonBeat>       currentPattern = new();
    private List<DungeonScheduledBeat> scheduledBeats = new();
    private double beatInterval;
    private bool   hasScheduledFirstPattern = false;
    private bool   isFinalBar               = false;
    private int    _barsPlayed              = 0;
    private bool   _skipGracePeriod         = false;

    // Minimum audio lookahead (seconds) used when scheduling the first bar for
    // skip-grace rooms. Must be large enough to survive initialization overhead
    // and Unity's audio-thread scheduling latency. Bar 2+ naturally gets ~0.25s
    // (half a beat at 120 BPM); 0.3s gives the first bar a consistent match.
    private const double FirstBarScheduleLookahead = 0.3;

    // Tracks scheduled audio for pause/resume (virtual times only)
    private class ScheduledSpawnAudio
    {
        public AudioSource      source;
        public double           virtualTime;
        public DungeonEnemyType enemyType;
    }
    private readonly List<ScheduledSpawnAudio> _scheduledAudio = new();

    // Tracks turn signals for pause/resume
    private class ScheduledTurnSignal
    {
        public double virtualTime;
        public int    barIndex;
    }
    private readonly List<ScheduledTurnSignal> _scheduledTurnSignals = new();
    #endregion

    #region Public Properties
    public double PatternStartTime { get; private set; }
    public double InputStartTime   { get; private set; }
    public List<DungeonScheduledBeat> ScheduledBeats => scheduledBeats;
    #endregion

    #region Public API — Initialization
    /// <summary>
    /// Prepares the beat manager for a new session.
    /// </summary>
    /// <param name="bpm">Beats per minute for this session.</param>
    /// <param name="patternDurations">
    /// Duration weights passed to DungeonPatternGenerator.
    /// Pass null to use the default weights { 1f, 0.5f }.
    /// </param>
    /// <param name="skipGracePeriod">
    /// When true, the bar-0 grace period is skipped and the first pattern spawns
    /// immediately on <see cref="StartGameplay"/>. Pass true for all rooms after
    /// the first so the player sees enemies straight away.
    /// </param>
    public void Initialize(int bpm, float[] patternDurations = null, bool skipGracePeriod = false)
    {
        metronome.bpm  = bpm;
        beatInterval   = 60.0 / bpm;

        float[] durations = (patternDurations != null && patternDurations.Length > 0)
            ? patternDurations
            : new float[] { 1f, 0.5f };
        patternGenerator = new DungeonPatternGenerator(durations);

        currentState             = GameState.WaitingForFirstBar;
        hasScheduledFirstPattern = false;
        isFinalBar               = false;
        _barsPlayed              = 0;
        _skipGracePeriod         = skipGracePeriod;

        Debug.Log($"[DungeonBeatManager] Initialized — BPM: {bpm}, beat: {beatInterval:F4}s, " +
                  $"durations: [{string.Join(", ", durations)}], skipGrace: {skipGracePeriod}");
    }

    public void StartGameplay(double startTimeVirtual)
    {
        ClearState();
        currentState = GameState.WaitingForFirstBar;
        if (inputReader != null) inputReader.EncounterActive = true;

        if (_skipGracePeriod)
        {
            // Scheduling is deferred to Update() so the first bar's audio always
            // has at least FirstBarScheduleLookahead seconds of DSP headroom.
            // Calling ScheduleNewPattern() here (synchronously after all the
            // Initialize() work) can leave < 80 ms of real lookahead when the
            // seam is close — enough to cause missed or glitchy PlayScheduled
            // calls on mobile. Update() handles it cleanly once we are close.
            Debug.Log($"[DungeonBeatManager] Grace period skipped — awaiting seam (Virtual: {startTimeVirtual:F4})");
        }
        else
        {
            // Bar 0 grace period: schedule the turn signal cue so the player
            // knows the first pattern is coming, then wait for bar 1.
            double graceTurnSignal = TimingCoordinator.Instance.CurrentBar.TurnSignalTime;
            if (graceTurnSignal > GameClock.Instance.GameTime)
                ScheduleTurnSignal(graceTurnSignal, 0);

            Debug.Log($"[DungeonBeatManager] Gameplay started with grace period (Virtual: {startTimeVirtual:F4})");
        }
    }
    #endregion

    #region Unity — Update
    private void Update()
    {
        if (GameClock.Instance.IsPaused || currentState == GameState.Uninitialized) return;

        var coordinator = TimingCoordinator.Instance;

        // ── Grace period → first pattern ────────────────────────────────
        if (!hasScheduledFirstPattern && currentState == GameState.WaitingForFirstBar)
        {
            double now = GameClock.Instance.GameTime;

            if (_skipGracePeriod)
            {
                // Room 2+: schedule bar 0 directly once we are within
                // FirstBarScheduleLookahead of its pattern start. This gives
                // PlayScheduled the same comfortable window that bar 2+ naturally
                // gets (~0.25 s) instead of the < 80 ms worst case from calling
                // ScheduleNewPattern immediately after all initialization work.
                if (coordinator.CurrentBar.PatternStartTime - now <= FirstBarScheduleLookahead)
                {
                    ScheduleNewPattern(coordinator.CurrentBar);
                    hasScheduledFirstPattern = true;
                    currentState = GameState.Playing;
                    Debug.Log($"[DungeonBeatManager] First pattern scheduled (skip-grace, " +
                              $"lookahead: {coordinator.CurrentBar.PatternStartTime - now:F3}s)");
                }
            }
            else if (coordinator.GetCurrentBarIndex() == 0)
            {
                double timeUntilNextBar = coordinator.NextBar.BarStartTime - now;
                if (timeUntilNextBar <= 0.1)
                {
                    coordinator.AdvanceToNextBar();
                    ScheduleNewPattern(coordinator.CurrentBar);
                    hasScheduledFirstPattern = true;
                    currentState = GameState.Playing;
                    Debug.Log("[DungeonBeatManager] Grace period done — gameplay begins at bar 1");
                }
            }
            else
            {
                // Fallback: past bar 0, schedule immediately
                ScheduleNewPattern(coordinator.CurrentBar);
                hasScheduledFirstPattern = true;
                currentState = GameState.Playing;
            }
        }

        // ── Evaluation trigger ───────────────────────────────────────────
        if (currentState == GameState.Playing || currentState == GameState.GeneratingFinalPattern)
        {
            if (coordinator.ShouldEvaluateNow())
                EvaluateCurrentBar();
        }

        // ── Final pattern detection ──────────────────────────────────────
        if (currentState == GameState.Playing && coordinator.ShouldGenerateFinalPattern())
        {
            currentState = GameState.GeneratingFinalPattern;
            isFinalBar   = true;
            Debug.Log("[DungeonBeatManager] *** NEXT EVALUATION WILL BE FINAL ***");
        }

        // ── Song timeout fallback ────────────────────────────────────────
        if (coordinator.IsSongComplete() &&
            currentState != GameState.EvaluatingFinalBar &&
            currentState != GameState.GameComplete)
        {
            Debug.LogWarning("[DungeonBeatManager] Song time exceeded — forcing completion");
            HandleGameComplete();
        }

        // ── Open input window at response phase start ────────────────────
        if (currentState == GameState.Playing || currentState == GameState.GeneratingFinalPattern)
            CheckAndOpenInputWindow();
    }
    #endregion

    #region Pattern Scheduling
    private void ScheduleNewPattern(TimingCoordinator.BarTiming timing)
    {
        scheduledBeats.Clear();
        _scheduledAudio.Clear();
        visualController.ResetVisuals();

        PatternStartTime = timing.PatternStartTime;
        InputStartTime   = timing.InputWindowStart;

        currentPattern = patternGenerator.GeneratePattern();

        // Turn signal for this bar
        if (timing.TurnSignalTime > GameClock.Instance.GameTime)
            ScheduleTurnSignal(timing.TurnSignalTime, timing.BarIndex);

        Debug.Log($"[DungeonBeatManager] Scheduling {currentPattern.Count} beats for bar {timing.BarIndex}");

        foreach (var beat in currentPattern)
        {
            double virtualTime   = PatternStartTime + (beat.timeSlot * beatInterval);
            var    scheduledBeat = new DungeonScheduledBeat(virtualTime, beat.enemyType);
            scheduledBeats.Add(scheduledBeat);
            ScheduleSpawnAudio(virtualTime, beat.enemyType);
            visualController.ScheduleSpawnMarker(virtualTime, beat.enemyType, scheduledBeat);
        }
        visualController.FinalizeBarEnemyPositions();
    }

    private void ScheduleSpawnAudio(double virtualTime, DungeonEnemyType type)
    {
        int idx = (int)type;

        // Resolve clip — fall back to null (guard in AudioManager handles missing clips)
        AudioClip clip = (enemySoundClips != null && idx < enemySoundClips.Length)
            ? enemySoundClips[idx]
            : null;

        if (clip == null) return;

        // Create a one-shot voice per beat so multiple spawns of the same enemy
        // type in one bar don't cancel each other's PlayScheduled call.
        // Mirrors AudioManager.PlayTurnSignal exactly.
        var go  = new GameObject("SpawnVoice");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.clip        = clip;
        src.playOnAwake = false;

        double realDsp = GameClock.Instance.VirtualToRealDsp(virtualTime);
        src.PlayScheduled(realDsp);
        Destroy(go, clip.length + 3f);

        _scheduledAudio.Add(new ScheduledSpawnAudio
        {
            source      = src,
            virtualTime = virtualTime,
            enemyType   = type
        });
    }

    private void ScheduleTurnSignal(double virtualTime, int barIndex)
    {
        double realDsp = GameClock.Instance.VirtualToRealDsp(virtualTime);
        AudioManager.instance.PlayTurnSignal(realDsp);

        _scheduledTurnSignals.Add(new ScheduledTurnSignal
        {
            virtualTime = virtualTime,
            barIndex    = barIndex
        });
    }
    #endregion

    #region Input Window
    private void CheckAndOpenInputWindow()
    {
        var    bar = TimingCoordinator.Instance.CurrentBar;
        double now = GameClock.Instance.GameTime;

        if (!inputReader.allowInput && now >= bar.InputWindowStart && now < bar.EvaluationTime)
        {
            inputReader.allowInput = true;
            inputReader.SetScheduledBeats(scheduledBeats);
            inputReader.SetTimeOffset(InputStartTime - PatternStartTime);
            Debug.Log("[DungeonBeatManager] Input window open");
        }
    }
    #endregion

    #region Evaluation
    private void EvaluateCurrentBar()
    {
        int barIdx = TimingCoordinator.Instance.GetCurrentBarIndex();
        Debug.Log($"[DungeonBeatManager] Evaluating bar {barIdx}");

        double timeOffset = InputStartTime - PatternStartTime;
        evaluator.EvaluateBar(inputReader.playerInputData, scheduledBeats, timeOffset);

        inputReader.allowInput = false;
        inputReader.ResetInputs();

        _barsPlayed++;
        bool barLimitReached = maxBarsPerEncounter > 0 && _barsPlayed >= maxBarsPerEncounter;

        if (isFinalBar || barLimitReached)
        {
            currentState = GameState.EvaluatingFinalBar;
            Debug.Log(barLimitReached
                ? $"[DungeonBeatManager] *** BAR LIMIT ({_barsPlayed}/{maxBarsPerEncounter}) — ending encounter ***"
                : "[DungeonBeatManager] *** FINAL BAR EVALUATED ***");
            OnFinalBarComplete?.Invoke();
            Invoke(nameof(HandleGameComplete), 0.5f);
        }
        else
        {
            TimingCoordinator.Instance.AdvanceToNextBar();
            ScheduleNewPattern(TimingCoordinator.Instance.CurrentBar);
        }
    }
    #endregion

    #region Game Completion
    private void HandleGameComplete()
    {
        currentState = GameState.GameComplete;
        StopAllCoroutines();
        CancelInvoke();
        inputReader.allowInput = false;
        if (inputReader != null) inputReader.EncounterActive = false;

        Debug.Log($"[DungeonBeatManager] *** GAME COMPLETE — Score: {evaluator.Score} ***");
        Invoke(nameof(TriggerSongComplete), delayBeforeResults);
    }

    private void TriggerSongComplete()
    {
        scheduledBeats.Clear();
        OnSongComplete?.Invoke();
    }
    #endregion

    #region Pause / Resume
    public void OnPause()
    {
        Debug.Log("[DungeonBeatManager] Pausing");

        // Cancel all scheduled spawn audio
        foreach (var a in _scheduledAudio)
            if (a.source != null) a.source.Stop();

        // Cancel all turn signal GameObjects
        CancelAllTurnSignals();

        StopAllCoroutines();
    }

    public void OnResume()
    {
        Debug.Log("[DungeonBeatManager] Resuming");
        double now = GameClock.Instance.GameTime;
        int    rescheduled = 0;

        // Reschedule spawn audio that hasn't played yet
        foreach (var a in _scheduledAudio)
        {
            if (a.virtualTime > now && a.source != null)
            {
                double realDsp = GameClock.Instance.VirtualToRealDsp(a.virtualTime);
                a.source.PlayScheduled(realDsp);
                rescheduled++;
            }
        }

        // Reschedule turn signals that haven't played yet
        var pending = _scheduledTurnSignals.FindAll(s => s.virtualTime > now);
        _scheduledTurnSignals.Clear();
        foreach (var s in pending)
        {
            double realDsp = GameClock.Instance.VirtualToRealDsp(s.virtualTime);
            AudioManager.instance.PlayTurnSignal(realDsp);
            _scheduledTurnSignals.Add(s);
            rescheduled++;
        }

        Debug.Log($"[DungeonBeatManager] Resumed — {rescheduled} events rescheduled");
    }

    private void CancelAllTurnSignals()
    {
        if (AudioManager.instance == null) return;
        var parent  = AudioManager.instance.transform;
        var toKill  = new List<GameObject>();
        foreach (Transform child in parent)
            if (child.gameObject.name == "TurnSignalVoice")
                toKill.Add(child.gameObject);
        foreach (var go in toKill) Destroy(go);
    }
    #endregion

    #region Reset / Cleanup
    private void ClearState()
    {
        StopAllCoroutines();
        CancelInvoke();

        // Stop and destroy any live SpawnVoice one-shot objects
        foreach (var a in _scheduledAudio)
        {
            if (a.source != null)
            {
                a.source.Stop();
                Destroy(a.source.gameObject);
            }
        }

        scheduledBeats.Clear();
        currentPattern.Clear();
        _scheduledAudio.Clear();
        _scheduledTurnSignals.Clear();
        hasScheduledFirstPattern = false;
        isFinalBar               = false;

        // Ensure the input reader is gated off — covers cleanup paths that bypass
        // HandleGameComplete (e.g. direct Cleanup() call on health depletion).
        if (inputReader != null)
        {
            inputReader.EncounterActive = false;
            inputReader.allowInput      = false;
        }
    }

    public void ResetToInitialState()
    {
        ClearState();
        currentState = GameState.Uninitialized;
        Debug.Log("[DungeonBeatManager] Reset");
    }
    #endregion
}
