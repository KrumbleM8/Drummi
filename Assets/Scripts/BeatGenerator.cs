using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatGenerator : MonoBehaviour
{
    public Metronome metronome;
    public BeatVisualScheduler beatVisualScheduler;
    public PlayerInputVisualHandler playerInputVisual;
    public BeatEvaluator evaluator;
    public PlayerInputReader playerInputReader;
    public CustardAnimationHandler custardAnimator;

    public List<AudioSource> leftBongoSources;
    public List<AudioSource> rightBongoSources;

    public int maxBeats = 8;
    public int difficultyIndex = 0;
    public int maxSameSideHits = 2;

    private readonly float[] starterBeatDurations = { 1f };
    private readonly float[] standardBeatDurations = { 1f, 0.5f };
    private readonly float[] spicyBeatDurations = { 0.75f, 0.5f, 0.25f };
    private float[] chosenBeatDurations;

    public float evaluationBeatThreshold = 7.5f;
    private bool evaluationTriggered = false;

    private int leftBongoIndex = 0;
    private int rightBongoIndex = 0;

    public List<ScheduledBeat> scheduledBeats = new List<ScheduledBeat>();

    private double pauseStartTime = 0.0;
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    public double VirtualDspTime()
    {
        return metronome != null ? metronome.VirtualDspTime : AudioSettings.dspTime;
    }

    private double loopStartTime = 0.0;
    private double gameStartTime = 0.0;
    public double patternStartTime = 0.0;
    public double inputStartTime { get; private set; }

    private double beatInterval;
    public float playbackOffset;

    private double gracePeriodEndTime = 0.0;
    private bool gracePeriodActive = true;

    public bool normalGeneration = true;

    private bool previousSide;

    private bool hasReceivedFirstFreshBar = false;

    [Header("Song End Detection")]
    public int barsBeforeEndToStopGeneration = 2;
    public int barsBeforeEndForFinalBar = 1;
    public float delayBeforeTransition = 2f;
    public int totalBeatsInSong = 0;
    private bool songEndDetected = false;
    public double songEndTime = 0.0;
    private double finalBarEndTime = 0.0;
    private bool isFinalPattern = false;
    private bool finalEvaluationTriggered = false;
    private bool gameTimingInitialized = false;
    public System.Action OnSongComplete;
    public System.Action OnFinalBarComplete;

    private readonly List<Beat> beatPattern = new();

    private AudioClip selectedClip;

    [Header("Screen Transition")]
    public ScreenTransition screenTransition;
    public GameObject scoreScreenMenu;
    public GameObject gameplayElements;

    private void OnEnable()
    {
        metronome.OnTickEvent += HandleOnTick;
        metronome.OnFreshBarEvent += HandleOnFreshBar;
    }

    private void OnDisable()
    {
        metronome.OnTickEvent -= HandleOnTick;
        metronome.OnFreshBarEvent -= HandleOnFreshBar;
    }

    public void SetBPM()
    {
        beatInterval = 60.0 / metronome.bpm;
        gracePeriodEndTime = VirtualDspTime() + (8 * beatInterval);
        loopStartTime = VirtualDspTime();

        switch (metronome.bpm)
        {
            default:
                playbackOffset = 0.001f;
                break;
        }

        metronome.RefreshValues();

        if (AudioManager.instance != null &&
            AudioManager.instance.musicTracks != null &&
            AudioManager.instance.selectedSongIndex < AudioManager.instance.musicTracks.Length)
        {
            selectedClip = AudioManager.instance.musicTracks[AudioManager.instance.selectedSongIndex];

            if (selectedClip != null)
            {
                double clipDuration = selectedClip.length;
                totalBeatsInSong = Mathf.FloorToInt((float)(clipDuration / beatInterval));
                Debug.Log($"[SetBPM] Song '{selectedClip.name}' has {totalBeatsInSong} beats. Timing will be initialized when game starts.");
            }
            else
            {
                Debug.LogError("[SetBPM] Selected music track is null!");
            }
        }
        else
        {
            Debug.LogError("[SetBPM] AudioManager or music tracks not available!");
        }
    }

    private void Start()
    {
        HandleOnFreshBar();

        chosenBeatDurations = difficultyIndex switch
        {
            0 => starterBeatDurations,
            1 => standardBeatDurations,
            2 => spicyBeatDurations,
            _ => spicyBeatDurations,
        };
    }

    private void Update()
    {
        if (isPaused || songEndDetected || finalEvaluationTriggered)
        {
            return;
        }

        double currentTime = VirtualDspTime();
        double currentLoopTime = currentTime - loopStartTime;

        if (!evaluationTriggered && !gracePeriodActive &&
            currentLoopTime >= evaluationBeatThreshold * beatInterval &&
            IsCorrectTimingForGeneration())
        {
            Invoke("EvaluateOneQuaverBeforeBar", 0);
        }
    }

    private bool IsCorrectTimingForGeneration()
    {
        if (metronome.loopBeatCount >= 8)
        {
            return true;
        }

        if (metronome.loopBeatCount == 7)
        {
            double timeSinceLastBeat = VirtualDspTime() - (metronome.GetNextBeatTime() - metronome.timePerTick);
            return timeSinceLastBeat > (metronome.timePerTick * 0.6);
        }

        return false;
    }

    private void EvaluateOneQuaverBeforeBar()
    {
        evaluator.EvaluatePlayerInput(playerInputReader.playerInputData);
        playerInputReader.allowInput = false;
        playerInputReader.ResetInputs();

        if (isFinalPattern)
        {
            finalEvaluationTriggered = true;
            Debug.Log("*** FINAL PATTERN EVALUATED - ENDING GAME ***");

            OnFinalBarComplete?.Invoke();

            Invoke(nameof(HandleFinalEvaluationComplete), 0.5f);
        }
        else
        {
            GenerateNewPattern();
            PlayPattern();
        }

        evaluationTriggered = true;
    }

    private void HandleFinalEvaluationComplete()
    {
        songEndDetected = true;

        Debug.Log("*** FREEZING GAME - SONG COMPLETE ***");

        StopAllCoroutines();
        CancelInvoke();

        if (evaluator != null)
        {
            Debug.Log($"Final Score: {evaluator.score}");
        }

        if (playerInputReader != null)
        {
            playerInputReader.allowInput = false;
        }

        Invoke(nameof(TriggerSongComplete), delayBeforeTransition);
    }

    private void HandleOnTick()
    {
        if (songEndDetected || finalEvaluationTriggered) return;

        double quaver = metronome.timePerTick * 0.5;
        double beatTime = metronome.nextBeatTime;

        if (metronome.loopBeatCount == 3)
        {
            AudioManager.instance.PlayTurnSignal(beatTime + quaver);
        }

        if (metronome.loopBeatCount == 4)
        {
            inputStartTime = metronome.nextBeatTime;
            playerInputReader.allowInput = true;
            Invoke(nameof(SetListenAnimation), (float)(metronome.timePerTick / 1.5f));
        }
    }

    private void SetListenAnimation()
    {
        if (songEndDetected || finalEvaluationTriggered) return;
        custardAnimator.HandleListening();
    }

    private void HandleOnFreshBar()
    {
        if (songEndDetected || finalEvaluationTriggered) return;

        loopStartTime = VirtualDspTime();

        if (!hasReceivedFirstFreshBar)
        {
            hasReceivedFirstFreshBar = true;
            gracePeriodActive = false;
            Debug.Log("[HandleOnFreshBar] Grace period ended - ready for pattern generation");

            InitializeGameTiming();
        }

        if (!finalEvaluationTriggered)
        {
            Invoke(nameof(DelayReset), 1);
        }
    }

    private void InitializeGameTiming()
    {
        if (gameTimingInitialized)
        {
            Debug.LogWarning("[InitializeGameTiming] Already initialized - ignoring duplicate call");
            return;
        }

        gameTimingInitialized = true;

        gameStartTime = VirtualDspTime();
        loopStartTime = gameStartTime;
        songEndTime = gameStartTime + (totalBeatsInSong * beatInterval);

        int beatsForFinalBar = totalBeatsInSong - (barsBeforeEndForFinalBar * 8);
        finalBarEndTime = gameStartTime + (beatsForFinalBar * beatInterval);

        Debug.Log("=== GAME TIMING INITIALIZED ===");
        Debug.Log($"Song: {selectedClip?.name ?? "Unknown"}");
        Debug.Log($"Total beats: {totalBeatsInSong} at {metronome.bpm} BPM");
        Debug.Log($"Game starts at: {gameStartTime:F2}");
        Debug.Log($"Song will end at: {songEndTime:F2}");
        Debug.Log($"Final bar ends at beat {beatsForFinalBar} (time: {finalBarEndTime:F2})");
        Debug.Log($"Current DSP time: {AudioSettings.dspTime:F2}");
        Debug.Log($"Bars before end to stop gen: {barsBeforeEndToStopGeneration}");
        Debug.Log($"Bars before end for final bar: {barsBeforeEndForFinalBar}");
        Debug.Log("================================");
    }

    private void DelayReset()
    {
        if (finalEvaluationTriggered || songEndDetected) return;
        evaluationTriggered = false;
    }

    public void GenerateNewPattern()
    {
        if (isFinalPattern)
        {
            Debug.LogWarning("[GenerateNewPattern] Called after final pattern - ignoring");
            return;
        }

        beatVisualScheduler.ResetVisuals();
        playerInputVisual.ResetVisuals();
        scheduledBeats.Clear();

        if (gameTimingInitialized && totalBeatsInSong > 0 && !isFinalPattern)
        {
            double currentTime = VirtualDspTime();
            double elapsedTimeSinceGameStart = currentTime - gameStartTime;
            int absoluteBeatCount = Mathf.FloorToInt((float)(elapsedTimeSinceGameStart / beatInterval));
            int beatsUntilEnd = totalBeatsInSong - absoluteBeatCount;

            int bufferBeats = barsBeforeEndForFinalBar * 8;
            int beatsNeededForCompleteCycle = 8 + bufferBeats;

            Debug.Log($"[GenerateNewPattern] absoluteBeat={absoluteBeatCount}/{totalBeatsInSong}, beatsUntilEnd={beatsUntilEnd}, threshold={beatsNeededForCompleteCycle}, buffer={bufferBeats}");

            if (beatsUntilEnd <= beatsNeededForCompleteCycle && beatsUntilEnd > bufferBeats)
            {
                isFinalPattern = true;
                Debug.Log($"*** GENERATING FINAL PATTERN *** (absoluteBeat: {absoluteBeatCount}, beatsUntilEnd: {beatsUntilEnd})");
            }
        }
        else if (!gameTimingInitialized)
        {
            Debug.Log("[GenerateNewPattern] Game timing not yet initialized - skipping final pattern check");
        }

        float timeSlot = 0f;
        float measureLength = 3.5f;
        beatPattern.Clear();

        while (timeSlot < measureLength)
        {
            List<float> validDurations = new();
            foreach (float d in chosenBeatDurations)
                if (timeSlot + d <= measureLength)
                    validDurations.Add(d);

            if (validDurations.Count == 0) break;

            float chosenDuration = validDurations[Random.Range(0, validDurations.Count)];

            bool isBongoSide;
            if (beatPattern.Count >= maxSameSideHits)
            {
                bool lastSide = beatPattern[^1].isBongoSide;
                bool allSame = true;
                for (int i = 1; i <= maxSameSideHits; i++)
                {
                    if (beatPattern[^i].isBongoSide != lastSide)
                    {
                        allSame = false;
                        break;
                    }
                }
                isBongoSide = allSame ? !lastSide : (Random.value > 0.5f);
            }
            else
            {
                isBongoSide = Random.value > 0.5f;
            }

            beatPattern.Add(new Beat(chosenDuration, timeSlot, isBongoSide));
            timeSlot += chosenDuration;
        }
    }

    public void PlayPattern()
    {
        scheduledBeats.Clear();
        double beatIntervalLocal = 60.0 / metronome.bpm;
        patternStartTime = metronome.GetNextBeatTime() + playbackOffset;

        foreach (Beat beat in beatPattern)
        {
            double scheduledTime = patternStartTime + (beat.timeSlot * beatIntervalLocal);
            scheduledBeats.Add(new ScheduledBeat(scheduledTime, beat.isBongoSide));

            if (beat.isBongoSide)
            {
                rightBongoSources[rightBongoIndex].PlayScheduled(scheduledTime);
                beatVisualScheduler.ScheduleVisualBeat(scheduledTime, true);
                rightBongoIndex = (rightBongoIndex + 1) % rightBongoSources.Count;
            }
            else
            {
                leftBongoSources[leftBongoIndex].PlayScheduled(scheduledTime);
                beatVisualScheduler.ScheduleVisualBeat(scheduledTime, false);
                leftBongoIndex = (leftBongoIndex + 1) % leftBongoSources.Count;
            }

            double neutralTime = scheduledTime - 0.1;
            if (neutralTime > AudioSettings.dspTime)
            {
                StartCoroutine(ScheduleAnimation(neutralTime, () => custardAnimator.HandleNeutral()));
            }

            if (beat.isBongoSide)
            {
                StartCoroutine(ScheduleBongoWithReturn(scheduledTime, true));
            }
            else
            {
                StartCoroutine(ScheduleBongoWithReturn(scheduledTime, false));
            }
        }

        inputStartTime = patternStartTime + (4 * beatIntervalLocal);
    }

    private System.Collections.IEnumerator ScheduleAnimation(double dspTime, System.Action action)
    {
        double delay = dspTime - AudioSettings.dspTime;
        if (delay > 0)
            yield return new WaitForSecondsRealtime((float)delay);

        if (songEndDetected || finalEvaluationTriggered) yield break;

        action?.Invoke();
    }

    private System.Collections.IEnumerator ScheduleBongoWithReturn(double dspTime, bool isRight)
    {
        double delay = dspTime - AudioSettings.dspTime;
        if (delay > 0)
            yield return new WaitForSecondsRealtime((float)delay);

        if (songEndDetected || finalEvaluationTriggered) yield break;

        if (isRight)
            custardAnimator.PlayRightBongo();
        else
            custardAnimator.PlayLeftBongo();

        float holdDuration = Mathf.Max(0.3f, (float)(beatInterval * 0.9f));
        yield return new WaitForSecondsRealtime(holdDuration);

        if (songEndDetected || finalEvaluationTriggered) yield break;

        if (custardAnimator.spriteRenderer.sprite == custardAnimator.sprites[4])
        {

        }
        else
        {
            custardAnimator.HandleNeutral();
        }
    }

    public void StartGame()
    {
        GenerateNewPattern();
    }

    public void OnPause()
    {
        if (!isPaused)
        {
            isPaused = true;
            pauseStartTime = AudioSettings.dspTime;
            Debug.Log($"[OnPause] Game paused at DSP time: {AudioSettings.dspTime:F2}");
        }
    }

    public void OnResume()
    {
        if (isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            isPaused = false;

            if (gameTimingInitialized)
            {
                songEndTime += pauseDuration;
                finalBarEndTime += pauseDuration;
                gameStartTime += pauseDuration;

                Debug.Log($"[OnResume] Game resumed after {pauseDuration:F2}s pause");
                Debug.Log($"[OnResume] Updated songEndTime: {songEndTime:F2}");
                Debug.Log($"[OnResume] Updated finalBarEndTime: {finalBarEndTime:F2}");
                Debug.Log($"[OnResume] Updated gameStartTime: {gameStartTime:F2}");
            }
            else
            {
                Debug.Log($"[OnResume] Game resumed but timing not yet initialized");
            }
        }
    }

    private void TriggerSongComplete()
    {
        Debug.Log("=== TRANSITIONING TO RESULTS ===");

        StopAllCoroutines();
        CancelInvoke();

        if (playerInputReader != null)
        {
            playerInputReader.allowInput = false;
        }

        scheduledBeats.Clear();

        OnSongComplete?.Invoke();

        Debug.Log($"FINAL SCORE: {evaluator?.score ?? 0}");

        if (screenTransition != null)
        {
            screenTransition.StartCover();
            StartCoroutine(WaitForTransitionThenShowResults());
        }
        else
        {
            Debug.LogError("ScreenTransition reference is missing! Assign it in Inspector.");
            ShowResultsScreen();
        }
    }

    private IEnumerator WaitForTransitionThenShowResults()
    {
        Debug.Log("[WaitForTransitionThenShowResults] Waiting for screen transition to cover...");

        while (!screenTransition.IsScreenCovered)
        {
            yield return null;
        }

        Debug.Log("[WaitForTransitionThenShowResults] Screen fully covered - showing results");
        screenTransition.StartReveal();
        ShowResultsScreen();
    }

    private void ShowResultsScreen()
    {
        Debug.Log("=== SHOWING RESULTS SCREEN ===");
        GameManager.instance.ResetGameValues();
        // Disable GameplayElements (contains sliders, indicators, UI)
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(false);
            Debug.Log("GameplayElements disabled");
        }
        else
        {
            Debug.LogWarning("GameplayElements reference missing! Assign in Inspector.");
        }


        if (beatVisualScheduler != null)
        {
            beatVisualScheduler.CleanupAndDisable();
            Debug.Log("BeatVisualScheduler cleaned up and disabled");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.CleanupAndDisable();
            Debug.Log("PlayerInputVisualHandler cleaned up and disabled");
        }

        if (metronome != null && metronome.enabled)
        {
            metronome.ResetToInitialState();
            metronome.enabled = false;
            Debug.Log("Metronome disabled");
        }

        // Stop all audio
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PauseMusic();
            Debug.Log("Music stopped");
        }

        // Show score screen
        if (scoreScreenMenu != null)
        {
            UIMenuManager menuManager = FindFirstObjectByType<UIMenuManager>();

            scoreScreenMenu.SetActive(true);
            menuManager.SetScoreToCurrentPage();
            var scoreScreen = scoreScreenMenu.GetComponent<ScoreScreen>();
            if (scoreScreen != null && evaluator != null)
            {
                scoreScreen.DisplayScore(evaluator.score, evaluator.perfectHits);
            }

            Debug.Log("ScoreScreenMenu enabled");
        }
        else
        {
            Debug.LogError("ScoreScreenMenu reference is missing! Assign it in Inspector.");
        }

        CleanupAndDisable();
        Debug.Log("BeatGenerator cleaned up and disabled");
        Debug.Log("=== RESULTS SCREEN READY ===");
    }

    private void CleanupAndDisable()
    {
        Debug.Log("[BeatGenerator] Cleaning up before disable");

        // Stop everything
        StopAllCoroutines();
        CancelInvoke();

        // Clear beats
        scheduledBeats.Clear();
        beatPattern.Clear();

        // Disable the component
        enabled = false;

        Debug.Log("[BeatGenerator] Cleanup complete");
    }

    public void ResetToInitialState()
    {
        Debug.Log("[BeatGenerator] Resetting to initial state");

        // Stop everything
        StopAllCoroutines();
        CancelInvoke();

        // Reset flags - INCLUDING gameTimingInitialized!
        evaluationTriggered = false;
        songEndDetected = false;
        finalEvaluationTriggered = false;
        isFinalPattern = false;
        gameTimingInitialized = false;  // CRITICAL: Allow timing to be re-initialized
        hasReceivedFirstFreshBar = false;  // CRITICAL: Allow fresh bar to trigger again
        gracePeriodActive = true;
        isPaused = false;

        // Reset timing
        pauseStartTime = 0.0;
        loopStartTime = 0.0;
        gameStartTime = 0.0;
        songEndTime = 0.0;
        finalBarEndTime = 0.0;
        totalBeatsInSong = 0;  // ADDED: Reset beat count

        // Clear beats
        scheduledBeats.Clear();
        beatPattern.Clear();

        // Reset indices
        leftBongoIndex = 0;
        rightBongoIndex = 0;

        Debug.Log("[BeatGenerator] Reset complete - ready for fresh start");
    }

    public void InitializeForNewGame()
    {
        Debug.Log("[BeatGenerator] Initializing for new game - triggering grace period");
        HandleOnFreshBar(); // This sets up loopStartTime and grace period, just like Start() did
    }
}