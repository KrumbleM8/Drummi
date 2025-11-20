using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates rhythm gameplay: generates beat patterns, schedules audio/visuals,
/// and manages game progression through the song.
/// REFACTORED: Uses TimingCoordinator for all timing (no more event handlers)
/// </summary>
public class BeatGenerator : MonoBehaviour
{
    #region Inspector References
    [Header("Core Systems")]
    [SerializeField] public Metronome metronome; // Still used for visual feedback
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
    private PatternGenerator patternGenerator;

    private List<Beat> currentPattern = new List<Beat>();
    private List<ScheduledBeat> scheduledBeats = new List<ScheduledBeat>();

    private double beatInterval;

    private int leftBongoIndex = 0;
    private int rightBongoIndex = 0;

    private bool hasScheduledFirstPattern = false;
    private bool isFinalBar = false;

    // Track scheduled audio with VIRTUAL times for pause/resume
    private class ScheduledAudio
    {
        public AudioSource source;
        public double virtualTime;  // Virtual time when this should play
        public bool isRightBongo;
    }
    private List<ScheduledAudio> scheduledAudio = new List<ScheduledAudio>();
    #endregion

    #region Difficulty Presets
    private readonly float[] starterDurations = { 1f };
    private readonly float[] standardDurations = { 1f, 0.5f };
    private readonly float[] spicyDurations = { 0.75f, 0.5f, 0.25f };
    #endregion

    #region Public Properties
    public double PatternStartTime { get; private set; }
    public double InputStartTime { get; private set; }
    public List<ScheduledBeat> ScheduledBeats => scheduledBeats;
    #endregion

    #region Lifecycle
    private void Update()
    {
        if (GameClock.Instance.IsPaused || currentState == GameState.Uninitialized)
            return;

        var coordinator = TimingCoordinator.Instance;

        // Check for initial pattern scheduling (happens once)
        if (!hasScheduledFirstPattern && currentState == GameState.WaitingForFirstBar)
        {
            // GRACE PERIOD: Bar 0 is the grace period (8 beats with no pattern)
            // Only schedule first pattern when we're approaching bar 1
            if (coordinator.GetCurrentBarIndex() == 0)
            {
                // Still in grace period - check if we should transition to bar 1
                double timeUntilNextBar = coordinator.NextBar.BarStartTime - GameClock.Instance.GameTime;
                if (timeUntilNextBar <= 0.1) // 100ms before bar 1 starts
                {
                    // Advance to bar 1 (end of grace period)
                    coordinator.AdvanceToNextBar();

                    // Now schedule the first pattern for bar 1
                    GenerateAndScheduleInitialPattern();
                    hasScheduledFirstPattern = true;
                    currentState = GameState.Playing;

                    Debug.Log("[BeatGenerator] Grace period complete - starting gameplay at bar 1");
                }
            }
            else
            {
                // Fallback: if somehow we're past bar 0, schedule immediately
                GenerateAndScheduleInitialPattern();
                hasScheduledFirstPattern = true;
                currentState = GameState.Playing;
            }
        }

        // Check for evaluation timing
        if (currentState == GameState.Playing || currentState == GameState.GeneratingFinalPattern)
        {
            if (coordinator.ShouldEvaluateNow())
            {
                EvaluateCurrentBar();
            }
        }

        // Check for final pattern trigger
        if (currentState == GameState.Playing)
        {
            if (coordinator.ShouldGenerateFinalPattern())
            {
                currentState = GameState.GeneratingFinalPattern;
                isFinalBar = true;
                Debug.Log("[BeatGenerator] *** NEXT EVALUATION WILL BE FINAL ***");
            }
        }

        // Check for song completion
        if (coordinator.IsSongComplete() &&
            currentState != GameState.EvaluatingFinalBar &&
            currentState != GameState.GameComplete)
        {
            Debug.LogWarning("[BeatGenerator] Song time exceeded - forcing completion");
            HandleGameComplete();
        }

        // Trigger listening animation at appropriate time (only after grace period)
        if (currentState == GameState.Playing ||
            currentState == GameState.GeneratingFinalPattern)
        {
            CheckAndTriggerListeningAnimation();
        }
    }
    #endregion

    #region Public API - Initialization
    /// <summary>
    /// Initialize beat generator with song parameters.
    /// </summary>
    public void Initialize(int bpm, int songIndex)
    {
        // Set core parameters
        metronome.bpm = bpm;
        beatInterval = 60.0 / bpm;

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
        hasScheduledFirstPattern = false;
        isFinalBar = false;

        AudioClip clip = AudioManager.instance.musicTracks[songIndex];
        int totalBeats = clip != null ? Mathf.FloorToInt((float)(clip.length / beatInterval)) : 0;

        Debug.Log($"[BeatGenerator] Initialized - BPM: {bpm}, Song: {clip?.name}, Beats: {totalBeats}");
    }

    /// <summary>
    /// Start gameplay. Called when game begins.
    /// </summary>
    public void StartGameplay(double startTimeVirtual)
    {
        ClearState();
        currentState = GameState.WaitingForFirstBar;

        // Schedule turn signal for grace period (bar 0) using virtual time
        double gracePeriodTurnSignalVirtual = TimingCoordinator.Instance.CurrentBar.TurnSignalTime;
        if (gracePeriodTurnSignalVirtual > GameClock.Instance.GameTime)
        {
            double realDspTime = GameClock.Instance.VirtualToRealDsp(gracePeriodTurnSignalVirtual);
            AudioManager.instance.PlayTurnSignal(realDspTime);
            Debug.Log($"[BeatGenerator] Grace period turn signal scheduled (Virtual: {gracePeriodTurnSignalVirtual:F4}, Real: {realDspTime:F4})");
        }

        Debug.Log($"[BeatGenerator] === GAMEPLAY STARTED ===");
        Debug.Log($"  Start time (Virtual): {startTimeVirtual:F4}");
        Debug.Log($"  Current (Virtual): {GameClock.Instance.GameTime:F4}");
        Debug.Log($"  Beat interval: {beatInterval:F4}s");
    }
    #endregion

    #region Pattern Generation & Scheduling
    /// <summary>
    /// Generate and schedule the very first pattern.
    /// </summary>
    private void GenerateAndScheduleInitialPattern()
    {
        Debug.Log("[BeatGenerator] Generating and scheduling initial pattern");

        GenerateNewPattern();
        SchedulePattern(TimingCoordinator.Instance.CurrentBar);
    }

    /// <summary>
    /// Generate a new pattern (doesn't schedule it).
    /// </summary>
    private void GenerateNewPattern()
    {
        currentPattern = patternGenerator.GeneratePattern();

        // Clear old visuals
        beatVisualScheduler.ResetVisuals();
        playerInputVisual.ResetVisuals();

        Debug.Log($"[BeatGenerator] Generated pattern with {currentPattern.Count} beats");
    }

    /// <summary>
    /// Schedule the current pattern using timing from coordinator (virtual time).
    /// Converts to real DSP time when actually scheduling audio.
    /// </summary>
    private void SchedulePattern(TimingCoordinator.BarTiming timing)
    {
        scheduledBeats.Clear();
        scheduledAudio.Clear(); // Clear old scheduled audio tracking

        PatternStartTime = timing.PatternStartTime;  // Virtual time
        InputStartTime = timing.InputWindowStart;    // Virtual time

        double currentVirtual = GameClock.Instance.GameTime;
        double scheduleAhead = PatternStartTime - currentVirtual;

        Debug.Log($"[BeatGenerator] Scheduling pattern for bar {timing.BarIndex}");
        Debug.Log($"  Pattern start (Virtual): {PatternStartTime:F4}");
        Debug.Log($"  Current (Virtual): {currentVirtual:F4}");
        Debug.Log($"  Scheduling ahead by: {scheduleAhead * 1000:F1}ms");

        // Schedule turn signal for this bar (in virtual time)
        if (timing.TurnSignalTime > currentVirtual)
        {
            // Convert virtual→real for audio scheduling
            double realDspTime = GameClock.Instance.VirtualToRealDsp(timing.TurnSignalTime);
            AudioManager.instance.PlayTurnSignal(realDspTime);
            Debug.Log($"  Turn signal scheduled (Virtual: {timing.TurnSignalTime:F4}, Real: {realDspTime:F4})");
        }

        foreach (Beat beat in currentPattern)
        {
            double virtualScheduledTime = PatternStartTime + (beat.timeSlot * beatInterval);
            scheduledBeats.Add(new ScheduledBeat(virtualScheduledTime, beat.isBongoSide));

            ScheduleAudio(virtualScheduledTime, beat.isBongoSide);  // Pass virtual time
            ScheduleVisuals(virtualScheduledTime, beat.isBongoSide);
            ScheduleAnimations(virtualScheduledTime, beat.isBongoSide);
        }
    }

    private void ScheduleAudio(double virtualTime, bool isRightSide)
    {
        AudioSource source;

        if (isRightSide)
        {
            source = rightBongoSources[rightBongoIndex];
            rightBongoIndex = (rightBongoIndex + 1) % rightBongoSources.Count;
        }
        else
        {
            source = leftBongoSources[leftBongoIndex];
            leftBongoIndex = (leftBongoIndex + 1) % leftBongoSources.Count;
        }

        // Convert virtual time → real DSP time for Unity's audio system
        double realDspTime = GameClock.Instance.VirtualToRealDsp(virtualTime);
        source.PlayScheduled(realDspTime);

        // Track this scheduled audio with VIRTUAL time (for pause/resume)
        scheduledAudio.Add(new ScheduledAudio
        {
            source = source,
            virtualTime = virtualTime,  // Store virtual time!
            isRightBongo = isRightSide
        });
    }

    private void ScheduleVisuals(double time, bool isRightSide)
    {
        beatVisualScheduler.ScheduleVisualBeat(time, isRightSide);
    }

    private void ScheduleAnimations(double virtualTime, bool isRightSide)
    {
        // Neutral animation slightly before beat
        double neutralTimeVirtual = virtualTime - GameConstants.NEUTRAL_ANIMATION_LEAD_TIME;
        if (neutralTimeVirtual > GameClock.Instance.GameTime)
        {
            StartCoroutine(WaitForVirtualTime(neutralTimeVirtual, () => custardAnimator.HandleNeutral()));
        }

        // Bongo animation on beat
        StartCoroutine(ScheduleBongoAnimation(virtualTime, isRightSide));
    }

    private IEnumerator ScheduleBongoAnimation(double virtualTime, bool isRightSide)
    {
        // Wait for scheduled time (using virtual time)
        while (GameClock.Instance.GameTime < virtualTime)
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

    private IEnumerator WaitForVirtualTime(double targetVirtualTime, System.Action action)
    {
        while (GameClock.Instance.GameTime < targetVirtualTime)
            yield return null;

        if (currentState != GameState.GameComplete)
            action?.Invoke();
    }
    #endregion

    #region Evaluation
    /// <summary>
    /// Evaluate the current bar and prepare the next one.
    /// </summary>
    private void EvaluateCurrentBar()
    {
        Debug.Log($"[BeatGenerator] Evaluating bar {TimingCoordinator.Instance.GetCurrentBarIndex()}");

        // Perform evaluation
        var result = evaluator.EvaluateBar(
            playerInputReader.playerInputData,
            scheduledBeats
        );

        // Reset input
        playerInputReader.allowInput = false;
        playerInputReader.ResetInputs();

        // Handle state transitions
        if (isFinalBar)
        {
            // Just evaluated the final bar
            currentState = GameState.EvaluatingFinalBar;
            Debug.Log("[BeatGenerator] *** FINAL BAR EVALUATED ***");

            OnFinalBarComplete?.Invoke();
            Invoke(nameof(HandleGameComplete), 0.5f);
        }
        else
        {
            // Normal bar - generate next pattern and schedule it
            GenerateNewPattern();

            // Advance coordinator to next bar
            TimingCoordinator.Instance.AdvanceToNextBar();

            // Schedule the newly generated pattern for the next bar
            SchedulePattern(TimingCoordinator.Instance.CurrentBar);
        }
    }
    #endregion

    #region Timed Triggers
    private double lastListeningCheck = -1;

    private void CheckAndTriggerListeningAnimation()
    {
        var currentBar = TimingCoordinator.Instance.CurrentBar;
        double currentTime = GameClock.Instance.GameTime;  // Virtual time

        // Trigger slightly before input window
        double listeningTriggerTime = currentBar.InputWindowStart - (beatInterval / 1.5);

        // Only trigger once per bar
        if (listeningTriggerTime != lastListeningCheck &&
            currentTime >= listeningTriggerTime &&
            currentTime < listeningTriggerTime + 0.1) // Small window
        {
            if (currentState != GameState.GameComplete)
            {
                custardAnimator.HandleListening();
                playerInputReader.allowInput = true;
            }
            lastListeningCheck = listeningTriggerTime;
        }
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
        Debug.Log("[BeatGenerator] === PAUSING ===");
        Debug.Log($"  Cancelling {scheduledAudio.Count} scheduled audio events");

        // Stop all audio sources to cancel their scheduled audio
        foreach (var audio in scheduledAudio)
        {
            if (audio.source != null)
            {
                audio.source.Stop();
            }
        }

        // Don't clear the list! We need it to reschedule on resume

        // Stop all animation coroutines
        StopAllCoroutines();

        Debug.Log("[BeatGenerator] Pause complete - audio cancelled, coroutines stopped");
    }

    public void OnResume()
    {
        Debug.Log("[BeatGenerator] === RESUMING ===");

        double currentVirtual = GameClock.Instance.GameTime;
        int rescheduledCount = 0;

        // Reschedule audio that hasn't played yet
        foreach (var audio in scheduledAudio)
        {
            if (audio.virtualTime > currentVirtual)  // Only reschedule future events
            {
                // Reconvert virtual→real with new totalPausedTime
                double newRealDspTime = GameClock.Instance.VirtualToRealDsp(audio.virtualTime);

                if (audio.source != null)
                {
                    audio.source.PlayScheduled(newRealDspTime);
                    rescheduledCount++;

                    Debug.Log($"  Rescheduled beat: Virtual={audio.virtualTime:F4}, Real={newRealDspTime:F4}, In {(newRealDspTime - AudioSettings.dspTime) * 1000:F0}ms");
                }
            }
        }

        // Reschedule turn signal if it hasn't played yet
        var currentBar = TimingCoordinator.Instance.CurrentBar;
        if (currentBar.TurnSignalTime > currentVirtual)
        {
            double realDspTime = GameClock.Instance.VirtualToRealDsp(currentBar.TurnSignalTime);
            AudioManager.instance.PlayTurnSignal(realDspTime);
            Debug.Log($"  Rescheduled turn signal: Virtual={currentBar.TurnSignalTime:F4}, Real={realDspTime:F4}");
        }

        // Restart animation coroutines for beats that haven't played yet
        RestartAnimationCoroutines();

        Debug.Log($"[BeatGenerator] Resume complete - {rescheduledCount} audio events rescheduled");
    }

    /// <summary>
    /// Restart animation coroutines for beats that haven't played yet.
    /// </summary>
    private void RestartAnimationCoroutines()
    {
        double currentVirtual = GameClock.Instance.GameTime;

        foreach (var scheduledBeat in scheduledBeats)
        {
            if (scheduledBeat.scheduledTime > currentVirtual)  // Virtual time
            {
                // Reschedule animations for this beat
                double neutralTimeVirtual = scheduledBeat.scheduledTime - GameConstants.NEUTRAL_ANIMATION_LEAD_TIME;
                if (neutralTimeVirtual > currentVirtual)
                {
                    StartCoroutine(WaitForVirtualTime(neutralTimeVirtual, () => custardAnimator.HandleNeutral()));
                }

                StartCoroutine(ScheduleBongoAnimation(scheduledBeat.scheduledTime, scheduledBeat.isRightBongo));
            }
        }
    }
    #endregion

    #region Helpers
    private void ClearState()
    {
        StopAllCoroutines();
        CancelInvoke();

        scheduledBeats.Clear();
        currentPattern.Clear();
        scheduledAudio.Clear();  // Clear scheduled audio tracking

        leftBongoIndex = 0;
        rightBongoIndex = 0;
        hasScheduledFirstPattern = false;
        isFinalBar = false;
        lastListeningCheck = -1;
    }
    #endregion

    #region Public Cleanup
    public void ResetToInitialState()
    {
        Debug.Log("[BeatGenerator] Resetting to initial state");

        ClearState();
        currentState = GameState.Uninitialized;

        Debug.Log("[BeatGenerator] Reset complete");
    }
    #endregion
}