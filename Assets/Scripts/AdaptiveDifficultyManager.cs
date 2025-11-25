#region Adaptive Difficulty (Starter Only)
using UnityEngine;

/// <summary>
/// Manages adaptive difficulty for starter mode (difficultyIndex = 0).
/// Tracks performance and adjusts pattern complexity dynamically.
/// Progressively increases quaver count as player improves.
/// </summary>
[System.Serializable]
public class AdaptiveDifficultyManager : MonoBehaviour
{
    [Header("State Transition Settings")]
    [Tooltip("Number of consecutive correct rounds needed to increase from Easy to Hard")]
    public int roundsToEnterHard = 4;

    [Tooltip("Number of consecutive failed rounds needed to decrease from Hard to Easy")]
    public int roundsToExitHard = 2;

    [Header("Quaver Progression Settings")]
    [Tooltip("Number of correct rounds in hard mode needed to increase quaver limit")]
    public int correctRoundsPerQuaverIncrease = 2;

    [Tooltip("Maximum number of quavers allowed in a pattern")]
    public int maxQuaverLimit = 6;

    [Tooltip("Starting quaver limit when entering hard mode")]
    public int initialQuaverLimit = 1;

    [Header("Difficulty Durations")]
    [Tooltip("Beat durations for easy mode (crotchets only)")]
    public float[] easyDurations = { 1f };

    [Tooltip("Beat durations for hard mode (crotchets + quavers)")]
    public float[] hardDurations = { 1f, 0.5f };

    // State
    private enum AdaptiveState { Easy, Hard }
    private AdaptiveState currentState = AdaptiveState.Easy;

    // Counters for state transitions
    private int consecutiveCorrect = 0;      // Counts successes in Easy mode
    private int consecutiveFailed = 0;       // Counts failures in Hard mode

    // Counters for quaver progression
    private int correctInHardMode = 0;       // Counts successes in Hard mode
    private int currentQuaverLimit = 1;      // Current max quavers allowed

    /// <summary>
    /// Get the current durations array based on adaptive state.
    /// </summary>
    public float[] GetCurrentDurations()
    {
        return currentState == AdaptiveState.Easy ? easyDurations : hardDurations;
    }

    /// <summary>
    /// Get the current quaver limit (only relevant in Hard mode).
    /// </summary>
    public int GetCurrentQuaverLimit()
    {
        return currentState == AdaptiveState.Hard ? currentQuaverLimit : 0;
    }

    /// <summary>
    /// Check if we're in hard mode (quavers enabled).
    /// </summary>
    public bool IsInHardMode()
    {
        return currentState == AdaptiveState.Hard;
    }

    /// <summary>
    /// Process the result of a completed round and update difficulty state.
    /// </summary>
    /// <param name="wasSuccessful">Whether the player successfully completed the round</param>
    /// <returns>True if difficulty state changed OR quaver limit changed</returns>
    public bool ProcessRoundResult(bool wasSuccessful)
    {
        bool somethingChanged = false;

        if (wasSuccessful)
        {
            if (currentState == AdaptiveState.Easy)
            {
                // In Easy mode: count towards entering Hard mode
                consecutiveCorrect++;
                consecutiveFailed = 0;

                // Check if we should enter Hard mode
                if (consecutiveCorrect >= roundsToEnterHard)
                {
                    currentState = AdaptiveState.Hard;
                    consecutiveCorrect = 0;
                    correctInHardMode = 0;
                    currentQuaverLimit = initialQuaverLimit;
                    somethingChanged = true;

                    Debug.Log($"[AdaptiveDifficulty] ★ Entered HARD mode (quavers enabled) - Starting limit: {currentQuaverLimit} quaver(s)");
                }
                else
                {
                    Debug.Log($"[AdaptiveDifficulty] Easy mode: {consecutiveCorrect}/{roundsToEnterHard} correct rounds");
                }
            }
            else // Hard mode
            {
                // In Hard mode: count towards quaver limit increase
                correctInHardMode++;
                consecutiveFailed = 0;

                // Check if we should increase quaver limit
                int requiredForNextIncrease = correctRoundsPerQuaverIncrease;
                if (correctInHardMode >= requiredForNextIncrease && currentQuaverLimit < maxQuaverLimit)
                {
                    currentQuaverLimit++;
                    correctInHardMode = 0; // Reset counter after increase
                    somethingChanged = true;

                    Debug.Log($"[AdaptiveDifficulty] ★ Quaver limit increased to {currentQuaverLimit} (max: {maxQuaverLimit})");
                }
                else
                {
                    int remaining = requiredForNextIncrease - correctInHardMode;
                    string limitStatus = currentQuaverLimit >= maxQuaverLimit ? " [MAX]" : $" ({remaining} more to next)";
                    Debug.Log($"[AdaptiveDifficulty] Hard mode: Quaver limit = {currentQuaverLimit}{limitStatus}");
                }
            }
        }
        else // Failed round
        {
            if (currentState == AdaptiveState.Easy)
            {
                // In Easy mode: just reset counter
                consecutiveCorrect = 0;
                Debug.Log($"[AdaptiveDifficulty] Easy mode: Failed (reset progress)");
            }
            else // Hard mode
            {
                // In Hard mode: count towards exiting to Easy
                consecutiveFailed++;
                correctInHardMode = 0; // Reset quaver progression on failure

                // Check if we should exit Hard mode
                if (consecutiveFailed >= roundsToExitHard)
                {
                    currentState = AdaptiveState.Easy;
                    consecutiveFailed = 0;
                    consecutiveCorrect = 0;
                    currentQuaverLimit = initialQuaverLimit; // Reset for next time
                    somethingChanged = true;

                    Debug.Log($"[AdaptiveDifficulty] ★ Exited to EASY mode (crotchets only) after {roundsToExitHard} failures");
                }
                else
                {
                    Debug.Log($"[AdaptiveDifficulty] Hard mode: Failed ({consecutiveFailed}/{roundsToExitHard} to exit)");
                }
            }
        }

        return somethingChanged;
    }

    /// <summary>
    /// Reset all tracking (call when starting a new game).
    /// </summary>
    public void Reset()
    {
        currentState = AdaptiveState.Easy;
        consecutiveCorrect = 0;
        consecutiveFailed = 0;
        correctInHardMode = 0;
        currentQuaverLimit = initialQuaverLimit;

        Debug.Log("[AdaptiveDifficulty] Reset to Easy mode");
    }

    /// <summary>
    /// Get current state for debugging/UI.
    /// </summary>
    public string GetCurrentStateDescription()
    {
        if (currentState == AdaptiveState.Easy)
        {
            return $"Easy ({consecutiveCorrect}/{roundsToEnterHard} to Hard)";
        }
        else
        {
            string quaverInfo = currentQuaverLimit >= maxQuaverLimit
                ? $"{currentQuaverLimit} quavers [MAX]"
                : $"{currentQuaverLimit} quavers ({correctInHardMode}/{correctRoundsPerQuaverIncrease} to next)";
            return $"Hard - {quaverInfo}";
        }
    }

    /// <summary>
    /// Get detailed stats for UI display.
    /// </summary>
    public (string mode, int quaverLimit, int progress, int threshold) GetDetailedStats()
    {
        if (currentState == AdaptiveState.Easy)
        {
            return ("Easy", 0, consecutiveCorrect, roundsToEnterHard);
        }
        else
        {
            return ("Hard", currentQuaverLimit, correctInHardMode, correctRoundsPerQuaverIncrease);
        }
    }
}
#endregion