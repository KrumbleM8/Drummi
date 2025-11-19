using UnityEngine;

/// <summary>
/// Single source of truth for all game timing.
/// Pre-calculates all timing values to eliminate drift and event latency.
/// All times are in DSP coordinates.
/// </summary>
public class TimingCoordinator : MonoBehaviour
{
    #region Singleton
    public static TimingCoordinator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Timing State
    private double gameStartTimeDsp;
    private double beatInterval;
    private int currentBarIndex = -1;
    private bool isInitialized = false;

    // Pre-calculated timing for current and next bar
    public BarTiming CurrentBar { get; private set; }
    public BarTiming NextBar { get; private set; }

    // Song progression
    private int totalBeatsInSong;
    private double songEndTimeDsp;
    private int barsBeforeEndForFinalBar;
    private bool finalBarGenerated = false;
    #endregion

    #region Timing Structure
    /// <summary>
    /// All timing information for a single 8-beat bar cycle.
    /// </summary>
    public struct BarTiming
    {
        public int BarIndex;                // Which bar this is (0, 1, 2, ...)
        public double BarStartTime;         // Beat 1 of the 8-beat cycle (DSP)
        public double PatternStartTime;     // When to play the pattern (Beat 1 + offset)
        public double InputWindowStart;     // When player can start inputting (Beat 5)
        public double EvaluationTime;       // When to evaluate inputs (Beat 7.5)
        public double NextBarStartTime;     // Beat 9 (= Beat 1 of next bar)
        public double TurnSignalTime;       // Beat 3.5 (halfway through beat 3)

        // Helper methods
        public double GetBeatTime(int beatNumber)
        {
            if (beatNumber < 1 || beatNumber > 8)
            {
                Debug.LogError($"Invalid beat number: {beatNumber}. Must be 1-8.");
                return BarStartTime;
            }
            double beatInterval = (NextBarStartTime - BarStartTime) / 8.0;
            return BarStartTime + ((beatNumber - 1) * beatInterval);
        }

        public bool IsInInputWindow(double currentTime)
        {
            return currentTime >= InputWindowStart && currentTime < EvaluationTime;
        }

        public override string ToString()
        {
            return $"Bar {BarIndex}: Start={BarStartTime:F4}, Pattern={PatternStartTime:F4}, Input={InputWindowStart:F4}, Eval={EvaluationTime:F4}";
        }
    }
    #endregion

    #region Public API - Initialization
    /// <summary>
    /// Initialize the timing coordinator for a game session.
    /// Call this once at game start with synchronized timing.
    /// </summary>
    public void Initialize(double startTimeDsp, double bpm, int totalBeats, int finalBarBuffer = 1)
    {
        gameStartTimeDsp = startTimeDsp;
        beatInterval = 60.0 / bpm;
        totalBeatsInSong = totalBeats;
        barsBeforeEndForFinalBar = finalBarBuffer;
        currentBarIndex = -1; // Will become 0 on first advance
        finalBarGenerated = false;

        // Calculate song end time
        double songDuration = totalBeats * beatInterval;
        songEndTimeDsp = gameStartTimeDsp + songDuration;

        // Pre-calculate first two bars
        CurrentBar = CalculateBarTiming(0);
        NextBar = CalculateBarTiming(1);
        currentBarIndex = 0;

        isInitialized = true;

        Debug.Log($"[TimingCoordinator] === INITIALIZED ===");
        Debug.Log($"  Start time (DSP): {startTimeDsp:F4}");
        Debug.Log($"  BPM: {bpm}");
        Debug.Log($"  Beat interval: {beatInterval:F4}s");
        Debug.Log($"  Total beats: {totalBeats}");
        Debug.Log($"  Song duration: {songDuration:F2}s");
        Debug.Log($"  Song end (DSP): {songEndTimeDsp:F4}");
        Debug.Log($"  Bar 0: {CurrentBar}");
        Debug.Log($"  Bar 1: {NextBar}");
    }
    #endregion

    #region Public API - Timing Queries
    /// <summary>
    /// Check if it's time to evaluate the current bar.
    /// </summary>
    public bool ShouldEvaluateNow()
    {
        if (!isInitialized) return false;
        return AudioSettings.dspTime >= CurrentBar.EvaluationTime;
    }

    /// <summary>
    /// Check if we should generate the final pattern.
    /// </summary>
    public bool ShouldGenerateFinalPattern()
    {
        if (!isInitialized || finalBarGenerated) return false;

        double currentTime = AudioSettings.dspTime;
        double elapsedTime = currentTime - gameStartTimeDsp;
        int currentBeat = Mathf.FloorToInt((float)(elapsedTime / beatInterval));
        int beatsUntilEnd = totalBeatsInSong - currentBeat;

        // Need enough time for one full cycle (8 beats) plus buffer
        int beatsNeededForCycle = GameConstants.BEATS_PER_LOOP + (barsBeforeEndForFinalBar * GameConstants.BEATS_PER_LOOP);

        bool shouldGenerate = beatsUntilEnd <= beatsNeededForCycle &&
                              beatsUntilEnd > (barsBeforeEndForFinalBar * GameConstants.BEATS_PER_LOOP);

        if (shouldGenerate)
        {
            finalBarGenerated = true;
            Debug.Log($"[TimingCoordinator] === FINAL PATTERN TRIGGER ===");
            Debug.Log($"  Current beat: {currentBeat}/{totalBeatsInSong}");
            Debug.Log($"  Beats until end: {beatsUntilEnd}");
        }

        return shouldGenerate;
    }

    /// <summary>
    /// Check if the song has completed.
    /// </summary>
    public bool IsSongComplete()
    {
        if (!isInitialized) return false;
        return AudioSettings.dspTime >= songEndTimeDsp;
    }

    /// <summary>
    /// Check if we're currently in the input window.
    /// </summary>
    public bool IsInInputWindow()
    {
        if (!isInitialized) return false;
        double currentTime = AudioSettings.dspTime;
        return CurrentBar.IsInInputWindow(currentTime);
    }

    /// <summary>
    /// Get the current bar index.
    /// </summary>
    public int GetCurrentBarIndex()
    {
        return currentBarIndex;
    }

    /// <summary>
    /// Get elapsed time since game start (in game time, not DSP time).
    /// </summary>
    public double GetElapsedGameTime()
    {
        if (!isInitialized) return 0;
        return GameClock.Instance.GameTime - gameStartTimeDsp;
    }
    #endregion

    #region Public API - Bar Progression
    /// <summary>
    /// Advance to the next bar. Call this after evaluation is complete.
    /// This is the ONLY method that advances timing state.
    /// </summary>
    public void AdvanceToNextBar()
    {
        if (!isInitialized)
        {
            Debug.LogError("[TimingCoordinator] Cannot advance - not initialized!");
            return;
        }

        currentBarIndex++;
        CurrentBar = NextBar;
        NextBar = CalculateBarTiming(currentBarIndex + 1);

        Debug.Log($"[TimingCoordinator] Advanced to bar {currentBarIndex}");
        Debug.Log($"  Current: {CurrentBar}");
        Debug.Log($"  Next: {NextBar}");
    }
    #endregion

    #region Timing Calculations
    /// <summary>
    /// Calculate all timing for a specific bar.
    /// </summary>
    private BarTiming CalculateBarTiming(int barIndex)
    {
        const double PATTERN_OFFSET = 0.003; // Small offset for audio scheduling

        // Each bar is 8 beats
        double barStart = gameStartTimeDsp + (barIndex * 8 * beatInterval);

        return new BarTiming
        {
            BarIndex = barIndex,
            BarStartTime = barStart,
            PatternStartTime = barStart + PATTERN_OFFSET,
            InputWindowStart = barStart + (4 * beatInterval), // Beat 5
            EvaluationTime = barStart + (7.5 * beatInterval), // Beat 7.5
            NextBarStartTime = barStart + (8 * beatInterval), // Beat 9
            TurnSignalTime = barStart + (3.5 * beatInterval)  // Beat 3.5
        };
    }
    #endregion

    #region Pause Handling
    /// <summary>
    /// Adjust all timing when resuming from pause.
    /// </summary>
    public void AdjustForPause(double pauseDuration)
    {
        if (!isInitialized) return;

        // Shift all times forward by pause duration
        gameStartTimeDsp += pauseDuration;
        songEndTimeDsp += pauseDuration;

        // Recalculate current and next bar with adjusted start time
        CurrentBar = CalculateBarTimingWithAdjustedStart(CurrentBar.BarIndex, pauseDuration);
        NextBar = CalculateBarTimingWithAdjustedStart(NextBar.BarIndex, pauseDuration);

        Debug.Log($"[TimingCoordinator] Adjusted for pause: +{pauseDuration:F3}s");
        Debug.Log($"  New current bar: {CurrentBar}");
    }

    private BarTiming CalculateBarTimingWithAdjustedStart(int barIndex, double adjustment)
    {
        const double PATTERN_OFFSET = 0.003;
        double barStart = gameStartTimeDsp + (barIndex * 8 * beatInterval);

        return new BarTiming
        {
            BarIndex = barIndex,
            BarStartTime = barStart,
            PatternStartTime = barStart + PATTERN_OFFSET,
            InputWindowStart = barStart + (4 * beatInterval),
            EvaluationTime = barStart + (7.5 * beatInterval),
            NextBarStartTime = barStart + (8 * beatInterval),
            TurnSignalTime = barStart + (3.75 * beatInterval)
        };
    }
    #endregion

    #region Reset
    /// <summary>
    /// Reset to uninitialized state.
    /// </summary>
    public void Reset()
    {
        isInitialized = false;
        currentBarIndex = -1;
        finalBarGenerated = false;
        gameStartTimeDsp = 0;
        songEndTimeDsp = 0;
        totalBeatsInSong = 0;

        Debug.Log("[TimingCoordinator] Reset");
    }
    #endregion

    #region Debug Helpers
    /// <summary>
    /// Get a debug string showing current timing state.
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return "[TimingCoordinator] Not initialized";

        double currentTime = AudioSettings.dspTime;
        double timeSinceStart = currentTime - gameStartTimeDsp;
        double timeUntilEval = CurrentBar.EvaluationTime - currentTime;
        double timeUntilSongEnd = songEndTimeDsp - currentTime;

        return $"[TimingCoordinator]\n" +
               $"  Bar: {currentBarIndex}\n" +
               $"  Time since start: {timeSinceStart:F2}s\n" +
               $"  Time until eval: {timeUntilEval:F2}s\n" +
               $"  Time until song end: {timeUntilSongEnd:F2}s\n" +
               $"  Current DSP: {currentTime:F4}\n" +
               $"  {CurrentBar}";
    }
    #endregion
}