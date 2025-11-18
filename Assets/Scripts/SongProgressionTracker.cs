using UnityEngine;

/// <summary>
/// Tracks song progression in DSP time coordinates.
/// Uses GameClock only for pause detection, not time conversion.
/// </summary>
public class SongProgressionTracker
{
    private int totalBeatsInSong;
    private double gameStartTimeDsp;      // When the game started (DSP time)
    private double songEndTimeDsp;        // When the song will end (DSP time)
    private double finalBarStartTimeDsp;  // When the final bar should begin (DSP time)
    private int barsBeforeEndForFinalBar;
    private double beatInterval;

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Initialize the progression tracker.
    /// ALL TIMES IN DSP COORDINATES.
    /// </summary>
    public void Initialize(int totalBeats, double startTimeDsp, double beatIntervalSeconds, int finalBarBuffer)
    {
        totalBeatsInSong = totalBeats;
        gameStartTimeDsp = startTimeDsp;
        barsBeforeEndForFinalBar = finalBarBuffer;
        beatInterval = beatIntervalSeconds;

        // Calculate end times in DSP coordinates
        double songDuration = totalBeats * beatIntervalSeconds;
        songEndTimeDsp = gameStartTimeDsp + songDuration;

        // Calculate when final pattern should start
        int beatsForFinalBar = totalBeats - (finalBarBuffer * GameConstants.BEATS_PER_LOOP);
        finalBarStartTimeDsp = gameStartTimeDsp + (beatsForFinalBar * beatIntervalSeconds);

        IsInitialized = true;

        Debug.Log($"[Progression] === INITIALIZED (DSP Time) ===");
        Debug.Log($"  Total beats: {totalBeats}");
        Debug.Log($"  Beat interval: {beatIntervalSeconds:F4}s");
        Debug.Log($"  Song duration: {songDuration:F2}s");
        Debug.Log($"  Game start (DSP): {startTimeDsp:F4}");
        Debug.Log($"  Song end (DSP): {songEndTimeDsp:F4}");
        Debug.Log($"  Final bar (DSP): {finalBarStartTimeDsp:F4}");
        Debug.Log($"  Final bar at beat: {beatsForFinalBar}/{totalBeats}");
    }

    /// <summary>
    /// Check if we should generate the final pattern now.
    /// Uses CURRENT DSP time.
    /// </summary>
    public bool ShouldGenerateFinalPattern(double currentDspTime)
    {
        if (!IsInitialized) return false;

        double elapsedTime = currentDspTime - gameStartTimeDsp;
        int currentBeat = Mathf.FloorToInt((float)(elapsedTime / beatInterval));
        int beatsUntilEnd = totalBeatsInSong - currentBeat;

        // Need enough time for one full cycle (8 beats) plus buffer
        int beatsNeededForCycle = GameConstants.BEATS_PER_LOOP + (barsBeforeEndForFinalBar * GameConstants.BEATS_PER_LOOP);

        bool shouldGenerate = beatsUntilEnd <= beatsNeededForCycle &&
                              beatsUntilEnd > (barsBeforeEndForFinalBar * GameConstants.BEATS_PER_LOOP);

        if (shouldGenerate)
        {
            Debug.Log($"[Progression] === TRIGGERING FINAL PATTERN ===");
            Debug.Log($"  Current DSP: {currentDspTime:F4}");
            Debug.Log($"  Start DSP: {gameStartTimeDsp:F4}");
            Debug.Log($"  Elapsed: {elapsedTime:F2}s");
            Debug.Log($"  Current beat: {currentBeat}/{totalBeatsInSong}");
            Debug.Log($"  Beats until end: {beatsUntilEnd}");
            Debug.Log($"  Cycle needs: {beatsNeededForCycle} beats");
        }

        return shouldGenerate;
    }

    /// <summary>
    /// Check if the song has completed.
    /// Uses CURRENT DSP time.
    /// </summary>
    public bool IsSongComplete(double currentDspTime)
    {
        if (!IsInitialized) return false;

        bool isComplete = currentDspTime >= songEndTimeDsp;

        if (isComplete)
        {
            double elapsedTime = currentDspTime - gameStartTimeDsp;
            double expectedDuration = totalBeatsInSong * beatInterval;

            Debug.Log($"[Progression] === SONG COMPLETE ===");
            Debug.Log($"  Current DSP: {currentDspTime:F4}");
            Debug.Log($"  Game start DSP: {gameStartTimeDsp:F4}");
            Debug.Log($"  Song end DSP: {songEndTimeDsp:F4}");
            Debug.Log($"  Elapsed: {elapsedTime:F2}s");
            Debug.Log($"  Expected: {expectedDuration:F2}s");
            Debug.Log($"  Total beats: {totalBeatsInSong}");

            // Sanity check
            if (elapsedTime < expectedDuration * 0.9)
            {
                Debug.LogError($"[Progression] WARNING: Song completed early! Only {elapsedTime:F1}s / {expectedDuration:F1}s");
            }
        }

        return isComplete;
    }

    /// <summary>
    /// Adjust all times when resuming from pause.
    /// </summary>
    public void AdjustForPause(double pauseDuration)
    {
        if (!IsInitialized) return;

        // Shift all DSP times forward by pause duration
        gameStartTimeDsp += pauseDuration;
        songEndTimeDsp += pauseDuration;
        finalBarStartTimeDsp += pauseDuration;

        Debug.Log($"[Progression] Adjusted for pause: +{pauseDuration:F3}s");
        Debug.Log($"  New song end DSP: {songEndTimeDsp:F4}");
    }

    /// <summary>
    /// Reset to uninitialized state.
    /// </summary>
    public void Reset()
    {
        IsInitialized = false;
        totalBeatsInSong = 0;
        gameStartTimeDsp = 0;
        songEndTimeDsp = 0;
        finalBarStartTimeDsp = 0;

        Debug.Log("[Progression] Reset");
    }
}