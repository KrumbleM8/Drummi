using UnityEngine;

/// <summary>
/// Single source of truth for all game timing.
/// IMPORTANT: All times are stored in VIRTUAL coordinates (GameClock.GameTime).
/// Virtual time = DSP time with pauses removed.
/// When converting to schedule audio, use GameClock.VirtualToRealDsp().
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
    private double gameStartTimeVirtual;  // Virtual time when game started
    private double beatInterval;
    private int currentBarIndex = -1;
    private bool isInitialized = false;

    // Pre-calculated timing for current and next bar (in virtual coordinates)
    public BarTiming CurrentBar { get; private set; }
    public BarTiming NextBar { get; private set; }

    // Song progression (in virtual coordinates)
    private int totalBeatsInSong;
    private double songEndTimeVirtual;
    private int barsBeforeEndForFinalBar;
    private bool finalBarGenerated = false;
    #endregion

    #region Timing Structure
    /// <summary>
    /// All timing information for a single 8-beat bar cycle.
    /// IMPORTANT: All times are in VIRTUAL coordinates.
    /// </summary>
    public struct BarTiming
    {
        public int BarIndex;
        public double BarStartTime;      // Virtual time
        public double PatternStartTime;  // Virtual time
        public double InputWindowStart;  // Virtual time
        public double EvaluationTime;    // Virtual time
        public double NextBarStartTime;  // Virtual time
        public double TurnSignalTime;    // Virtual time

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

        public bool IsInInputWindow(double currentVirtualTime)
        {
            return currentVirtualTime >= InputWindowStart && currentVirtualTime < EvaluationTime;
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
    /// startTimeVirtual should be GameClock.GameTime at game start.
    /// All timing is calculated in virtual coordinates.
    /// </summary>
    public void Initialize(double startTimeVirtual, double bpm, int totalBeats, int finalBarBuffer = 1)
    {
        gameStartTimeVirtual = startTimeVirtual;
        beatInterval = 60.0 / bpm;
        totalBeatsInSong = totalBeats;
        barsBeforeEndForFinalBar = finalBarBuffer;
        currentBarIndex = -1;
        finalBarGenerated = false;

        // Calculate song end time (virtual)
        double songDuration = totalBeats * beatInterval;
        songEndTimeVirtual = gameStartTimeVirtual + songDuration;

        // Pre-calculate first two bars
        CurrentBar = CalculateBarTiming(0);
        NextBar = CalculateBarTiming(1);
        currentBarIndex = 0;

        isInitialized = true;

        Debug.Log($"[TimingCoordinator] === INITIALIZED (Virtual Time) ===");
        Debug.Log($"  Start time (Virtual): {startTimeVirtual:F4}");
        Debug.Log($"  BPM: {bpm}");
        Debug.Log($"  Beat interval: {beatInterval:F4}s");
        Debug.Log($"  Total beats: {totalBeats}");
        Debug.Log($"  Song duration: {songDuration:F2}s");
        Debug.Log($"  Song end (Virtual): {songEndTimeVirtual:F4}");
        Debug.Log($"  Bar 0: {CurrentBar}");
        Debug.Log($"  Bar 1: {NextBar}");
    }
    #endregion

    #region Public API - Timing Queries
    /// <summary>
    /// Check if it's time to evaluate the current bar.
    /// Uses virtual time for comparison.
    /// </summary>
    public bool ShouldEvaluateNow()
    {
        if (!isInitialized) return false;
        return GameClock.Instance.GameTime >= CurrentBar.EvaluationTime;
    }

    /// <summary>
    /// Check if we should generate the final pattern.
    /// Uses virtual time for calculations.
    /// </summary>
    public bool ShouldGenerateFinalPattern()
    {
        if (!isInitialized || finalBarGenerated) return false;

        double currentTime = GameClock.Instance.GameTime;
        double elapsedTime = currentTime - gameStartTimeVirtual;
        int currentBeat = Mathf.FloorToInt((float)(elapsedTime / beatInterval));
        int beatsUntilEnd = totalBeatsInSong - currentBeat;

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
    /// Uses virtual time for comparison.
    /// </summary>
    public bool IsSongComplete()
    {
        if (!isInitialized) return false;
        return GameClock.Instance.GameTime >= songEndTimeVirtual;
    }

    /// <summary>
    /// Check if we're currently in the input window.
    /// Uses virtual time for comparison.
    /// </summary>
    public bool IsInInputWindow()
    {
        if (!isInitialized) return false;
        double currentTime = GameClock.Instance.GameTime;
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
    /// Get elapsed time since game start (in virtual time).
    /// </summary>
    public double GetElapsedGameTime()
    {
        if (!isInitialized) return 0;
        return GameClock.Instance.GameTime - gameStartTimeVirtual;
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
    /// Calculate all timing for a specific bar in virtual coordinates.
    /// </summary>
    private BarTiming CalculateBarTiming(int barIndex)
    {
        const double PATTERN_OFFSET = 0.003; // Small offset for audio scheduling

        // Each bar is 8 beats (virtual time)
        double barStart = gameStartTimeVirtual + (barIndex * 8 * beatInterval);

        return new BarTiming
        {
            BarIndex = barIndex,
            BarStartTime = barStart,
            PatternStartTime = barStart + PATTERN_OFFSET,
            InputWindowStart = barStart + (4 * beatInterval), // Beat 5
            EvaluationTime = barStart + (7.5 * beatInterval), // Beat 7.5
            NextBarStartTime = barStart + (8 * beatInterval), // Beat 9
            TurnSignalTime = barStart + (3.5 * beatInterval)  // Beat 4.5
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
        gameStartTimeVirtual = 0;
        songEndTimeVirtual = 0;
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

        double currentTime = GameClock.Instance.GameTime;
        double timeSinceStart = currentTime - gameStartTimeVirtual;
        double timeUntilEval = CurrentBar.EvaluationTime - currentTime;
        double timeUntilSongEnd = songEndTimeVirtual - currentTime;

        return $"[TimingCoordinator] (Virtual Time)\n" +
               $"  Bar: {currentBarIndex}\n" +
               $"  Time since start: {timeSinceStart:F2}s\n" +
               $"  Time until eval: {timeUntilEval:F2}s\n" +
               $"  Time until song end: {timeUntilSongEnd:F2}s\n" +
               $"  Current Virtual: {currentTime:F4}\n" +
               $"  {CurrentBar}";
    }
    #endregion
}