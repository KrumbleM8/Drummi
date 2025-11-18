using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates rhythm gameplay: generates beat patterns, schedules audio/visuals,
/// and manages game progression through the song.
/// FIXED: All timing now uses GameClock consistently
/// </summary>
public class BeatGenerator : MonoBehaviour
{
    #region Inspector References
    [Header("Core Systems")]
    [SerializeField] public Metronome metronome;
    [SerializeField] private BeatEvaluator evaluator;
    [SerializeField] private PlayerInputReader playerInputReader;
    [SerializeField] private CustardAnimationHandler custardAnimator;

    [Header("Visual Schedulers")]
    [SerializeField] private BeatVisualScheduler beatVisualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisual;

    [Header("Audio Sources")]
    [SerializeField] private List<AudioSource> leftBongoSources;
    [SerializeField] private List<AudioSource> rightBongoSources;

    [Header("Difficulty Settings")]
    [SerializeField] public int difficultyIndex = 0;
    [SerializeField] private int maxSameSideHits = 2;

    [Header("Song Progression")]
    [SerializeField] private int barsBeforeEndForFinalBar = 1;
    [SerializeField] private float delayBeforeResults = 2f;
    #endregion

    #region Events
    public System.Action OnSongComplete;
    public System.Action OnFinalBarComplete;
    #endregion

    #region State
    private GameState currentState = GameState.Uninitialized;
    private SongProgressionTracker progressionTracker = new SongProgressionTracker();
    private PatternGenerator patternGenerator;

    private List<Beat> currentPattern = new List<Beat>();
    private List<ScheduledBeat> scheduledBeats = new List<ScheduledBeat>();

    private double loopStartTime; // In GameClock time
    private double beatInterval;
    private float playbackOffset;

    private int leftBongoIndex = 0;
    private int rightBongoIndex = 0;

    private bool evaluationPending = false;

    // Track total beats in song
    private int totalBeatsInSong = 0;
    #endregion

    #region Difficulty Presets
    private readonly float[] starterDurations = { 1f };
    private readonly float[] standardDurations = { 1f, 0.5f };
    private readonly float[] spicyDurations = { 0.75f, 0.5f, 0.25f };
    #endregion

    #region Public Properties
    public double PatternStartTime { get; private set; } // In DSP time for audio scheduling
    public double InputStartTime { get; private set; } // In DSP time for audio scheduling
    public List<ScheduledBeat> ScheduledBeats => scheduledBeats;
    #endregion

    #region Lifecycle
    private void OnEnable()
    {
        if (metronome != null)
        {
            metronome.OnTickEvent += OnMetronomeTick;
            metronome.OnFreshBarEvent += OnMetronomeFreshBar;
        }
    }

    private void OnDisable()
    {
        if (metronome != null)
        {
            metronome.OnTickEvent -= OnMetronomeTick;
            metronome.OnFreshBarEvent -= OnMetronomeFreshBar;
        }
    }

    private void Update()
    {
        if (GameClock.Instance.IsPaused)
            return;

        switch (currentState)
        {
            case GameState.Playing:
                CheckForEvaluation();
                CheckForFinalPattern();
                break;

            case GameState.GeneratingFinalPattern:
                CheckForEvaluation();
                break;

            case GameState.Uninitialized:
            case GameState.WaitingForFirstBar:
            case GameState.EvaluatingFinalBar:
            case GameState.GameComplete:
                // No updates needed
                break;
        }
    }
    #endregion

    #region Public API - Initialization
    /// <summary>
    /// Initialize beat generator with song parameters. Call once per song selection.
    /// </summary>
    public void Initialize(int bpm, int songIndex)
    {
        // Set core parameters
        metronome.bpm = bpm;
        beatInterval = 60.0 / bpm;
        playbackOffset = (bpm == 79) ? 0.005f : 0.003f;

        // Get song clip
        AudioClip clip = AudioManager.instance.musicTracks[songIndex];
        if (clip == null)
        {
            Debug.LogError($"[BeatGenerator] Song at index {songIndex} is null!");
            return;
        }

        // Calculate song duration in beats
        totalBeatsInSong = Mathf.FloorToInt((float)(clip.length / beatInterval));

        // Setup pattern generator based on difficulty
        float[] durations = difficultyIndex switch
        {
            0 => starterDurations,
            1 => standardDurations,
            2 => spicyDurations,
            _ => spicyDurations
        };
        patternGenerator = new PatternGenerator(durations, maxSameSideHits);

        currentState = GameState.WaitingForFirstBar;

        Debug.Log($"[BeatGenerator] Initialized - BPM: {bpm}, Song: {clip.name}, Beats: {totalBeatsInSong}");
    }

    /// <summary>
    /// Start gameplay at the specified time. Called when game actually begins.
    /// FIXED: Now uses GameClock time consistently
    /// </summary>
    /// <summary>
    /// Start gameplay at the specified time. Called when game actually begins.
    /// FIXED: Uses DSP time directly, no conversion needed.
    /// </summary>
    public void StartGameplay(double startTimeDsp)
    {
        // Validate that Initialize() was called
        if (totalBeatsInSong == 0)
        {
            Debug.LogError("[BeatGenerator] StartGameplay called before Initialize()!");
            return;
        }

        // FIXED: Pass DSP time directly to progression tracker
        progressionTracker.Initialize(
            totalBeatsInSong,
            startTimeDsp,  // ← DSP time, no conversion!
            beatInterval,
            barsBeforeEndForFinalBar
        );

        // Reset state
        ClearState();
        loopStartTime = startTimeDsp; // Store DSP time for loop tracking
        currentState = GameState.WaitingForFirstBar;

        Debug.Log($"[BeatGenerator] === GAMEPLAY STARTED ===");
        Debug.Log($"  Start time (DSP): {startTimeDsp:F4}");
        Debug.Log($"  Current DSP: {AudioSettings.dspTime:F4}");
        Debug.Log($"  Total beats: {totalBeatsInSong}");
        Debug.Log($"  Beat interval: {beatInterval:F4}s");
        Debug.Log($"  Song duration: {(totalBeatsInSong * beatInterval):F2}s");
        Debug.Log($"  BPM: {metronome.bpm}");
    }
    #endregion

    #region Metronome Event Handlers
    private void OnMetronomeTick()
    {
        if (currentState == GameState.GameComplete) return;

        // Beat 3: Play turn signal
        if (metronome.loopBeatCount == 3)
        {
            double signalTime = metronome.nextBeatTime + (metronome.timePerTick * 0.5);
            AudioManager.instance.PlayTurnSignal(signalTime);
        }

        // Beat 4: Allow player input
        if (metronome.loopBeatCount == 4)
        {
            InputStartTime = metronome.nextBeatTime;
            playerInputReader.allowInput = true;

            // Schedule listening animation slightly before input window
            float delay = (float)(metronome.timePerTick / 1.5f);
            Invoke(nameof(TriggerListeningAnimation), delay);
        }
    }

    private void OnMetronomeFreshBar()
    {
        if (currentState == GameState.GameComplete) return;

        loopStartTime = AudioSettings.dspTime;

        // CRITICAL: Handle pattern generation and scheduling based on state
        if (currentState == GameState.WaitingForFirstBar)
        {
            // First bar after grace period - generate and schedule first pattern
            currentState = GameState.Playing;
            GenerateNewPattern();
            ScheduleCurrentPattern();
            Debug.Log("[BeatGenerator] First fresh bar - pattern generation started");
        }
        else if (currentState == GameState.Playing || currentState == GameState.GeneratingFinalPattern)
        {
            // Subsequent bars - schedule the pattern that was generated during evaluation
            // (Pattern was already generated at beat 7.5 during EvaluateBar)
            ScheduleCurrentPattern();
            Debug.Log($"[BeatGenerator] Fresh bar - scheduling pre-generated pattern (state: {currentState})");
        }

        // Reset evaluation flag for next loop
        if (currentState != GameState.EvaluatingFinalBar)
        {
            evaluationPending = false;
        }
    }
    #endregion

    #region Evaluation & Pattern Generation
    private void CheckForEvaluation()
    {
        if (evaluationPending) return;

        double currentTime = AudioSettings.dspTime; // ← Use DSP time
        double elapsedInLoop = currentTime - loopStartTime;

        bool shouldEvaluate = elapsedInLoop >= (GameConstants.EVALUATION_TIMING_BEATS * beatInterval);

        bool correctPhase = metronome.loopBeatCount >= 7;
        if (metronome.loopBeatCount == 7)
        {
            double timeSinceLastBeat = AudioSettings.dspTime -
                (metronome.GetNextBeatTime() - metronome.timePerTick);
            correctPhase = timeSinceLastBeat > (metronome.timePerTick * GameConstants.BEAT_TIMING_THRESHOLD);
        }

        if (shouldEvaluate && correctPhase)
        {
            evaluationPending = true;
            EvaluateBar();
        }
    }

    private void CheckForFinalPattern()
    {
        if (currentState != GameState.Playing) return;

        double currentTime = AudioSettings.dspTime; // ← Use DSP time

        if (progressionTracker.ShouldGenerateFinalPattern(currentTime))
        {
            currentState = GameState.GeneratingFinalPattern;
            Debug.Log("[BeatGenerator] *** NEXT PATTERN WILL BE FINAL ***");
        }

        // Check if song is complete
        if (progressionTracker.IsSongComplete(currentTime) &&
            currentState != GameState.EvaluatingFinalBar &&
            currentState != GameState.GameComplete)
        {
            Debug.LogWarning("[BeatGenerator] Song time exceeded - forcing completion");
            currentState = GameState.EvaluatingFinalBar;
            Invoke(nameof(HandleGameComplete), 0.1f);
        }
    }

    private void EvaluateBar()
    {
        // Perform evaluation using new API
        var result = evaluator.EvaluateBar(
            playerInputReader.playerInputData,
            scheduledBeats
        );

        // Reset input
        playerInputReader.allowInput = false;
        playerInputReader.ResetInputs();

        // Handle state transition
        if (currentState == GameState.GeneratingFinalPattern)
        {
            // We just evaluated the final pattern
            currentState = GameState.EvaluatingFinalBar;
            Debug.Log("[BeatGenerator] *** FINAL PATTERN EVALUATED ***");

            OnFinalBarComplete?.Invoke();
            Invoke(nameof(HandleGameComplete), 0.5f);
        }
        else
        {
            // CRITICAL FIX: Generate the NEXT pattern but DON'T schedule it yet
            // Scheduling will happen on the next OnFreshBarEvent (beat 1)
            GenerateNewPattern();
            Debug.Log("[BeatGenerator] Pattern generated at beat 7.5, will schedule on next bar");
        }
    }
    /// <summary>
    /// Generate a new pattern (doesn't schedule it).
    /// </summary>
    private void GenerateNewPattern()
    {
        // Generate new pattern
        currentPattern = patternGenerator.GeneratePattern();

        // Clear old visuals (ready for new pattern when it's scheduled)
        beatVisualScheduler.ResetVisuals();
        playerInputVisual.ResetVisuals();

        Debug.Log($"[BeatGenerator] Generated pattern with {currentPattern.Count} beats");
    }

    #endregion

    #region Pattern Scheduling
    private void ScheduleCurrentPattern()
    {
        // DEFENSIVE: Only schedule at beat 1 (fresh bar)
        if (metronome.loopBeatCount != 1)
        {
            Debug.LogError($"[BeatGenerator] ScheduleCurrentPattern called at beat {metronome.loopBeatCount} instead of beat 1!");
            Debug.LogError($"  This will cause mis-timed patterns. Only call from OnFreshBarEvent.");
            return;
        }

        // Defensive check: Make sure we have a pattern to schedule
        if (currentPattern == null || currentPattern.Count == 0)
        {
            Debug.LogError("[BeatGenerator] No pattern to schedule!");
            return;
        }

        scheduledBeats.Clear();

        double currentBeatTimeDsp = metronome.nextBeatTime - metronome.timePerTick;
        PatternStartTime = currentBeatTimeDsp + playbackOffset;
        InputStartTime = PatternStartTime + (GameConstants.BEATS_PER_BAR * beatInterval);

        Debug.Log($"[BeatGenerator] Scheduling {currentPattern.Count} beats at fresh bar");
        Debug.Log($"  Loop beat: {metronome.loopBeatCount}");
        Debug.Log($"  Pattern start: {PatternStartTime:F4}");
        Debug.Log($"  DSP now: {AudioSettings.dspTime:F4}");

        foreach (Beat beat in currentPattern)
        {
            double scheduledTime = PatternStartTime + (beat.timeSlot * beatInterval);
            scheduledBeats.Add(new ScheduledBeat(scheduledTime, beat.isBongoSide));

            ScheduleAudio(scheduledTime, beat.isBongoSide);
            ScheduleVisuals(scheduledTime, beat.isBongoSide);
            ScheduleAnimations(scheduledTime, beat.isBongoSide);
        }
    }

    private void ScheduleAudio(double time, bool isRightSide)
    {
        if (isRightSide)
        {
            rightBongoSources[rightBongoIndex].PlayScheduled(time);
            rightBongoIndex = (rightBongoIndex + 1) % rightBongoSources.Count;
        }
        else
        {
            leftBongoSources[leftBongoIndex].PlayScheduled(time);
            leftBongoIndex = (leftBongoIndex + 1) % leftBongoSources.Count;
        }
    }

    private void ScheduleVisuals(double time, bool isRightSide)
    {
        beatVisualScheduler.ScheduleVisualBeat(time, isRightSide);
    }

    private void ScheduleAnimations(double time, bool isRightSide)
    {
        // Neutral animation slightly before beat
        double neutralTime = time - GameConstants.NEUTRAL_ANIMATION_LEAD_TIME;
        if (neutralTime > AudioSettings.dspTime)
        {
            StartCoroutine(WaitForDspTime(neutralTime, () => custardAnimator.HandleNeutral()));
        }

        // Bongo animation on beat
        StartCoroutine(ScheduleBongoAnimation(time, isRightSide));
    }

    private IEnumerator ScheduleBongoAnimation(double dspTime, bool isRightSide)
    {
        // Wait for scheduled time
        while (AudioSettings.dspTime < dspTime)
            yield return null;

        if (currentState == GameState.GameComplete) yield break;

        // Play animation
        if (isRightSide)
            custardAnimator.PlayRightBongo();
        else
            custardAnimator.PlayLeftBongo();

        // Hold animation
        float holdDuration = Mathf.Max(
            GameConstants.ANIMATION_HOLD_DURATION_MIN,
            (float)(beatInterval * GameConstants.ANIMATION_HOLD_MULTIPLIER)
        );
        yield return new WaitForSecondsRealtime(holdDuration);

        if (currentState == GameState.GameComplete) yield break;

        // Return to neutral (unless listening sprite is active)
        if (custardAnimator.spriteRenderer.sprite != custardAnimator.sprites[GameConstants.SPRITE_LISTENING])
        {
            custardAnimator.HandleNeutral();
        }
    }

    private IEnumerator WaitForDspTime(double targetTime, System.Action action)
    {
        while (AudioSettings.dspTime < targetTime)
            yield return null;

        if (currentState != GameState.GameComplete)
            action?.Invoke();
    }
    #endregion

    #region Game Completion
    private void HandleGameComplete()
    {
        currentState = GameState.GameComplete;

        StopAllCoroutines();
        CancelInvoke();

        playerInputReader.allowInput = false;

        Debug.Log($"[BeatGenerator] *** GAME COMPLETE - Final Score: {evaluator.Score} ***");

        Invoke(nameof(TriggerSongComplete), delayBeforeResults);
    }

    private void TriggerSongComplete()
    {
        Debug.Log("[BeatGenerator] Transitioning to results");

        scheduledBeats.Clear();
        OnSongComplete?.Invoke();
    }
    #endregion

    #region Pause Handling
    public void OnPause()
    {
        // Pause is now handled by GameClock - no local state needed
    }

    public void OnResume()
    {
        // Adjust timing for pause duration
        double pauseDuration = GameClock.Instance.GetLastPauseDuration();
        progressionTracker.AdjustForPause(pauseDuration);

        Debug.Log($"[BeatGenerator] Resumed - adjusted timing by {pauseDuration:F3}s");
    }
    #endregion

    #region Helpers
    private void TriggerListeningAnimation()
    {
        if (currentState != GameState.GameComplete)
            custardAnimator.HandleListening();
    }

    private void ClearState()
    {
        StopAllCoroutines();
        CancelInvoke();

        scheduledBeats.Clear();
        currentPattern.Clear();

        leftBongoIndex = 0;
        rightBongoIndex = 0;
        evaluationPending = false;
    }
    #endregion

    #region Public Cleanup
    public void ResetToInitialState()
    {
        Debug.Log("[BeatGenerator] Resetting to initial state");

        ClearState();
        progressionTracker.Reset();

        currentState = GameState.Uninitialized;
        loopStartTime = 0;
        totalBeatsInSong = 0;

        Debug.Log("[BeatGenerator] Reset complete");
    }
    #endregion
}