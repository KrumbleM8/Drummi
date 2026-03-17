using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates rhythm gameplay: generates beat patterns, schedules audio/visuals,
/// and manages game progression through the song.
/// REFACTORED: Uses TimingCoordinator for all timing (no more event handlers)
/// UPDATED: Now handles TurnSignal pause/resume correctly
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

    [Header("Adaptive Difficulty")]
    [SerializeField] private AdaptiveDifficultyManager adaptiveDifficulty;
    [SerializeField] private bool enableAdaptiveDifficulty = true;
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

    // Track scheduled turn signals with VIRTUAL times for pause/resume
    private class ScheduledTurnSignal
    {
        public double virtualTime;  // Virtual time when this should play
        public int barIndex;        // Which bar this turn signal is for
    }
    private List<ScheduledTurnSignal> scheduledTurnSignals = new List<ScheduledTurnSignal>();
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
        float[] durations;

        if (difficultyIndex == 0 && enableAdaptiveDifficulty == true)
        {
            // ADAPTIVE DIFFICULTY for starter mode
            adaptiveDifficulty.Reset();
            durations = adaptiveDifficulty.GetCurrentDurations();
            Debug.Log("[BeatGenerator] Using ADAPTIVE difficulty for starter mode");
        }
        else
        {
            // Fixed difficulties for standard/spicy
            durations = difficultyIndex switch
            {
                1 => standardDurations,
                2 => spicyDurations,
                _ => spicyDurations
            };
        }

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
            ScheduleTurnSignal(gracePeriodTurnSignalVirtual, 0);
            Debug.Log($"[BeatGenerator] Grace period turn signal scheduled (Virtual: {gracePeriodTurnSignalVirtual:F4})");
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
    /// Uses quaver-limited generation ONLY for starter difficulty in hard mode.
    /// </summary>
    private void GenerateNewPattern()
    {
        // Only use quaver-limited generation for starter difficulty (difficultyIndex == 0)
        if (difficultyIndex == 0 && adaptiveDifficulty.IsInHardMode() && enableAdaptiveDifficulty == true)
        {
            // Starter difficulty in hard mode: respect quaver limit
            int limit = adaptiveDifficulty.GetCurrentQuaverLimit();
            Debug.Log($"[BeatGenerator] ★ ADAPTIVE MODE: Generating with MAX {limit} quaver(s)");
            GeneratePatternWithQuaverLimit();
        }
        else
        {
            // All other cases: normal generation (standard, spicy, or starter in easy mode)
            currentPattern = patternGenerator.GeneratePattern();
        }

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
            ScheduleTurnSignal(timing.TurnSignalTime, timing.BarIndex);
            Debug.Log($"  Turn signal scheduled (Virtual: {timing.TurnSignalTime:F4})");
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

    /// <summary>
    /// Schedule a turn signal at the specified virtual time.
    /// Tracks it for pause/resume handling.
    /// </summary>
    private void ScheduleTurnSignal(double virtualTime, int barIndex)
    {
        // Convert virtual→real for audio scheduling
        double realDspTime = GameClock.Instance.VirtualToRealDsp(virtualTime);
        AudioManager.instance.PlayTurnSignal(realDspTime);

        // Track this turn signal for pause/resume
        scheduledTurnSignals.Add(new ScheduledTurnSignal
        {
            virtualTime = virtualTime,
            barIndex = barIndex
        });
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

        // Guard against the source being destroyed (e.g. if it was a child of a
        // singleton that got Destroy(gameObject)'d on duplicate detection).
        if (source == null)
        {
            Debug.LogWarning("[BeatGenerator] Bongo AudioSource is null or destroyed — skipping schedule.");
            return;
        }

        double realDspTime = GameClock.Instance.VirtualToRealDsp(virtualTime);
        source.PlayScheduled(realDspTime);

        scheduledAudio.Add(new ScheduledAudio
        {
            source = source,
            virtualTime = virtualTime,
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

        // === NEW: Process adaptive difficulty ===
        if (difficultyIndex == 0 && !isFinalBar && enableAdaptiveDifficulty == true)
        {
            // Determine if round was successful
            bool wasSuccessful = DetermineRoundSuccess();

            bool stateChanged = adaptiveDifficulty.ProcessRoundResult(wasSuccessful);

            // If difficulty changed, update the pattern generator
            if (stateChanged)
            {
                float[] newDurations = adaptiveDifficulty.GetCurrentDurations();
                patternGenerator = new PatternGenerator(newDurations, maxSameSideHits);
                Debug.Log($"[BeatGenerator] Pattern generator updated with new durations: [{string.Join(", ", newDurations)}]");
            }
        }
        // === END NEW CODE ===

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

    /// <summary>
    /// Determine if a round was successful based on player performance.
    /// Adjust this logic based on your success criteria.
    /// </summary>
    private bool DetermineRoundSuccess()
    {
        // Count valid inputs (hits that were registered)
        int validInputs = 0;

        foreach (var input in playerInputReader.playerInputData)
        {
            // Assuming input.inputTime > 0 means the player hit something
            // Adjust this condition based on your PlayerInputData structure
            if (input.inputTime > 0)
            {
                validInputs++;
            }
        }

        // Success = player hit at least 2 out of 3 beats (66% success rate)
        // You can adjust this threshold as needed
        int requiredHits = Mathf.CeilToInt(scheduledBeats.Count * 0.66f);
        bool success = validInputs >= requiredHits;

        Debug.Log($"[BeatGenerator] Round result: {validInputs}/{scheduledBeats.Count} hits = {(success ? "SUCCESS" : "FAIL")}");

        return success;
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
        Debug.Log($"  Cancelling {scheduledAudio.Count} beat audio events");
        Debug.Log($"  Cancelling {scheduledTurnSignals.Count} turn signal events");

        // Stop all beat audio sources to cancel their scheduled audio
        foreach (var audio in scheduledAudio)
        {
            if (audio.source != null)
            {
                audio.source.Stop();
            }
        }

        // Stop and destroy all turn signal GameObjects
        CancelAllTurnSignals();

        // Don't clear the lists! We need them to reschedule on resume

        // Stop all animation coroutines
        StopAllCoroutines();

        Debug.Log("[BeatGenerator] Pause complete - all audio cancelled, coroutines stopped");
    }

    public void OnResume()
    {
        Debug.Log("[BeatGenerator] === RESUMING ===");

        double currentVirtual = GameClock.Instance.GameTime;
        int rescheduledBeatsCount = 0;
        int rescheduledSignalsCount = 0;

        // Reschedule beat audio that hasn't played yet
        foreach (var audio in scheduledAudio)
        {
            if (audio.virtualTime > currentVirtual)  // Only reschedule future events
            {
                // Reconvert virtual→real with new totalPausedTime
                double newRealDspTime = GameClock.Instance.VirtualToRealDsp(audio.virtualTime);

                if (audio.source != null)
                {
                    audio.source.PlayScheduled(newRealDspTime);
                    rescheduledBeatsCount++;

                    Debug.Log($"  Rescheduled beat: Virtual={audio.virtualTime:F4}, Real={newRealDspTime:F4}, In {(newRealDspTime - AudioSettings.dspTime) * 1000:F0}ms");
                }
            }
        }

        // Reschedule turn signals that haven't played yet
        List<ScheduledTurnSignal> signalsToReschedule = new List<ScheduledTurnSignal>();
        foreach (var signal in scheduledTurnSignals)
        {
            if (signal.virtualTime > currentVirtual)
            {
                signalsToReschedule.Add(signal);
            }
        }

        // Clear old turn signal tracking and reschedule
        scheduledTurnSignals.Clear();
        foreach (var signal in signalsToReschedule)
        {
            double realDspTime = GameClock.Instance.VirtualToRealDsp(signal.virtualTime);
            AudioManager.instance.PlayTurnSignal(realDspTime);

            // Re-add to tracking list
            scheduledTurnSignals.Add(signal);
            rescheduledSignalsCount++;

            Debug.Log($"  Rescheduled turn signal: Virtual={signal.virtualTime:F4}, Real={realDspTime:F4}, Bar={signal.barIndex}");
        }

        // Restart animation coroutines for beats that haven't played yet
        RestartAnimationCoroutines();

        Debug.Log($"[BeatGenerator] Resume complete - {rescheduledBeatsCount} beats, {rescheduledSignalsCount} turn signals rescheduled");
    }

    /// <summary>
    /// Cancel all scheduled turn signals by destroying their GameObjects.
    /// Turn signals are created as dynamic GameObjects named "TurnSignalVoice".
    /// </summary>
    private void CancelAllTurnSignals()
    {
        if (AudioManager.instance == null) return;

        // Find all TurnSignalVoice GameObjects and destroy them
        Transform audioManagerTransform = AudioManager.instance.transform;
        List<GameObject> turnSignalObjects = new List<GameObject>();

        // Collect all turn signal GameObjects
        foreach (Transform child in audioManagerTransform)
        {
            if (child.gameObject.name == "TurnSignalVoice")
            {
                turnSignalObjects.Add(child.gameObject);
            }
        }

        // Destroy them
        foreach (var obj in turnSignalObjects)
        {
            Destroy(obj);
        }

        Debug.Log($"  Cancelled {turnSignalObjects.Count} turn signal GameObject(s)");
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
        scheduledAudio.Clear();
        scheduledTurnSignals.Clear();

        leftBongoIndex = 0;
        rightBongoIndex = 0;
        hasScheduledFirstPattern = false;
        isFinalBar = false;
        lastListeningCheck = -1;

        // Reset adaptive difficulty when clearing state (for starter mode)
        if (difficultyIndex == 0)
        {
            adaptiveDifficulty.Reset();
        }
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

    #region Adaptive Pattern Generation
    /// <summary>
    /// Count the number of quaver beats (0.5f duration) in a pattern.
    /// </summary>
    private int CountQuaversInPattern(List<Beat> pattern)
    {
        int count = 0;
        foreach (var beat in pattern)
        {
            // Quavers appear at 0.5 intervals: 0.5, 1.5, 2.5, etc.
            float fractionalPart = beat.timeSlot % 1f;
            if (Mathf.Approximately(fractionalPart, 0.5f))
            {
                count++;
            }
        }
        return count;
    }

    /// Enforce quaver limit by replacing excess quavers with crotchets.
    /// This is a fallback if pattern generation keeps producing too many quavers.
    /// </summary>
    /// <summary>
    /// Enforce quaver limit by replacing excess quavers with crotchets.
    /// Converts excess quavers back to crotchets to respect the limit.
    /// </summary>
    /// <summary>
    /// Enforce quaver limit by replacing excess quavers with crotchets.
    /// Converts excess quavers back to crotchets to respect the limit.
    /// </summary>
    private List<Beat> EnforceQuaverLimit(List<Beat> pattern, int maxQuavers)
    {
        // First, separate quavers from crotchets
        List<Beat> quavers = new List<Beat>();
        List<Beat> crotchets = new List<Beat>();

        foreach (var beat in pattern)
        {
            float fractionalPart = beat.timeSlot % 1f;
            if (Mathf.Approximately(fractionalPart, 0.5f))
            {
                quavers.Add(beat);
            }
            else
            {
                crotchets.Add(beat);
            }
        }

        // If we have too many quavers, convert the excess to crotchets
        if (quavers.Count > maxQuavers)
        {
            int quaversToConvert = quavers.Count - maxQuavers;
            Debug.Log($"[BeatGenerator] Converting {quaversToConvert} quaver(s) to crotchet(s)");

            // Keep only the first maxQuavers quavers, convert the rest
            List<Beat> quaversToKeep = new List<Beat>();
            List<Beat> quaversToReplace = new List<Beat>();

            for (int i = 0; i < quavers.Count; i++)
            {
                if (i < maxQuavers)
                {
                    quaversToKeep.Add(quavers[i]);
                }
                else
                {
                    quaversToReplace.Add(quavers[i]);
                }
            }

            // Convert excess quavers to crotchets
            foreach (var quaver in quaversToReplace)
            {
                // Find nearest whole beat position
                float nearestWholeBeat = Mathf.Round(quaver.timeSlot);

                // Check if this position would collide with an existing crotchet
                bool collision = crotchets.Exists(c => Mathf.Approximately(c.timeSlot, nearestWholeBeat));

                if (!collision)
                {
                    // Safe to add crotchet at this position
                    crotchets.Add(new Beat(1f, nearestWholeBeat, quaver.isBongoSide));
                    Debug.Log($"[BeatGenerator] Converted quaver at {quaver.timeSlot:F1} to crotchet at {nearestWholeBeat:F1}");
                }
                else
                {
                    // Collision - just remove this quaver (don't replace it)
                    Debug.Log($"[BeatGenerator] Removed quaver at {quaver.timeSlot:F1} (would collide with existing crotchet)");
                }
            }

            // Use the kept quavers instead of all quavers
            quavers = quaversToKeep;
        }

        // Combine remaining quavers with all crotchets
        List<Beat> result = new List<Beat>();
        result.AddRange(crotchets);
        result.AddRange(quavers);

        // Sort by timeSlot to maintain correct order
        result.Sort((a, b) => a.timeSlot.CompareTo(b.timeSlot));

        return result;
    }

    /// <summary>
    /// Generate a pattern respecting the quaver limit for adaptive difficulty.
    /// Generates crotchets-only pattern, then manually inserts limited quavers.
    /// </summary>
    private void GeneratePatternWithQuaverLimit()
    {
        int quaverLimit = adaptiveDifficulty.GetCurrentQuaverLimit();

        // IMPORTANT: Generate pattern using ONLY crotchets (1f duration)
        // We'll manually add quavers after
        PatternGenerator crotchetOnlyGenerator = new PatternGenerator(
            new float[] { 1f },  // Only crotchets
            maxSameSideHits
        );

        // Generate a crotchet-only pattern
        List<Beat> crotchetPattern = crotchetOnlyGenerator.GeneratePattern();

        // Now manually replace some crotchets with quavers (up to the limit)
        currentPattern = InsertQuaversIntoPattern(crotchetPattern, quaverLimit);

        int actualQuaverCount = CountQuaversInPattern(currentPattern);
        Debug.Log($"[BeatGenerator] ★ Generated pattern with EXACTLY {actualQuaverCount} quaver(s) (limit: {quaverLimit})");
    }

    /// <summary>
    /// Insert a specific number of quavers into a crotchet-only pattern.
    /// Converts random crotchets to quavers without exceeding the limit.
    /// </summary>
    private List<Beat> InsertQuaversIntoPattern(List<Beat> crotchetPattern, int quaverLimit)
    {
        if (quaverLimit <= 0 || crotchetPattern.Count == 0)
        {
            return crotchetPattern; // No quavers allowed or no beats to convert
        }

        // We'll convert up to 'quaverLimit' crotchets into quavers
        // A quaver split: one crotchet at position X becomes two quavers at X and X+0.5

        List<Beat> result = new List<Beat>(crotchetPattern);
        int quaversInserted = 0;

        // Try to insert quavers by splitting crotchets
        // We'll randomly select which crotchets to split
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < result.Count; i++)
        {
            // Only consider beats that are currently crotchets
            float fractionalPart = result[i].timeSlot % 1f;
            if (Mathf.Approximately(fractionalPart, 0f))
            {
                availableIndices.Add(i);
            }
        }

        // Shuffle for randomness
        for (int i = availableIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = availableIndices[i];
            availableIndices[i] = availableIndices[j];
            availableIndices[j] = temp;
        }

        // Insert quavers by splitting crotchets
        int processedIndices = 0;
        while (quaversInserted < quaverLimit && processedIndices < availableIndices.Count)
        {
            int index = availableIndices[processedIndices];
            processedIndices++;

            if (index >= result.Count) continue; // Safety check

            Beat crotchet = result[index];

            // Split this crotchet into two quavers
            // Original position stays as first quaver
            Beat firstQuaver = new Beat(0.5f, crotchet.timeSlot, crotchet.isBongoSide);

            // Second quaver goes 0.5 beats later
            // Alternate the side for variety
            Beat secondQuaver = new Beat(0.5f, crotchet.timeSlot + 0.5f, !crotchet.isBongoSide);

            // Replace the crotchet with the first quaver
            result[index] = firstQuaver;

            // Insert the second quaver after it
            result.Insert(index + 1, secondQuaver);

            quaversInserted += 2; // We added 2 quavers

            Debug.Log($"[BeatGenerator] Split crotchet at {crotchet.timeSlot:F1} into quavers at {firstQuaver.timeSlot:F1} and {secondQuaver.timeSlot:F1}");

            // Update available indices to account for the insertion
            for (int i = processedIndices; i < availableIndices.Count; i++)
            {
                if (availableIndices[i] > index)
                {
                    availableIndices[i]++;
                }
            }
        }

        // Sort by timeSlot to maintain correct order
        result.Sort((a, b) => a.timeSlot.CompareTo(b.timeSlot));

        return result;
    }
    #endregion
}