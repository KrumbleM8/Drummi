using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Evaluates player input against scheduled beats and manages scoring.
/// Simplified with clear separation of concerns and no useless methods.
/// </summary>
public class BeatEvaluator : MonoBehaviour
{
    #region Inspector Configuration
    [Header("References")]
    [SerializeField] private BeatGenerator beatGenerator;
    [SerializeField] private CustardAnimationHandler custardAnimator;
    [SerializeField] private TMP_Text scoreText;

    [Header("Timing Windows (seconds)")]
    [SerializeField] private float perfectThreshold = 0.05f;
    [SerializeField] private float goodThreshold = 0.2f;

    [Header("Scoring")]
    [SerializeField] private int perfectReward = 200;
    [SerializeField] private int goodReward = 100;
    [SerializeField] private int passableReward = 50;
    [SerializeField] private int allowedMistakes = 1;
    #endregion

    #region Public State
    public int Score { get; private set; }
    public int PerfectHits { get; private set; }
    public bool HasFailedOnce { get; private set; }
    #endregion

    #region Constants
    private const string HIGH_SCORE_KEY = "GlitchyHS";
    #endregion

    #region Lifecycle
    private void Start()
    {
        Score = 0;
        HasFailedOnce = false;
        UpdateScoreDisplay();
    }
    #endregion

    #region Public API - Evaluation
    /// <summary>
    /// Evaluate all player inputs against scheduled beats.
    /// This is the ONLY evaluation method - no more confusion.
    /// </summary>
    public EvaluationResult EvaluateBar(List<BongoInput> playerInputs, List<ScheduledBeat> scheduledBeats)
    {
        if (GameClock.Instance.IsPaused)
        {
            Debug.LogWarning("[BeatEvaluator] Attempted evaluation while paused");
            return null;
        }

        if (scheduledBeats.Count == 0)
        {
            Debug.LogWarning("[BeatEvaluator] No beats to evaluate");
            return null;
        }

        // Perform matching
        var matches = MatchInputsToBeats(playerInputs, scheduledBeats);

        // Calculate results
        int correctHits = CountCorrectHits(matches);
        int perfectHits = CountPerfectHits(matches);

        // Determine grade
        EvaluationGrade grade = DetermineGrade(correctHits, perfectHits,
                                                playerInputs.Count, scheduledBeats.Count);

        // Calculate points
        int points = CalculatePoints(grade);

        // Create result
        var result = new EvaluationResult(grade, correctHits, perfectHits, scheduledBeats.Count, points);

        // Apply result to game state
        ApplyEvaluationResult(result);

        // Log for debugging
        LogEvaluationResult(result, playerInputs.Count);

        return result;
    }
    #endregion

    #region Matching Logic
    /// <summary>
    /// Match player inputs to scheduled beats using a two-pointer algorithm.
    /// More efficient and clearer than the original nested loops.
    /// </summary>
    private List<InputMatch> MatchInputsToBeats(List<BongoInput> inputs, List<ScheduledBeat> beats)
    {
        // Sort both lists by time (defensive - should already be sorted)
        var sortedInputs = new List<BongoInput>(inputs);
        var sortedBeats = new List<ScheduledBeat>(beats);

        sortedInputs.Sort((a, b) => a.inputTime.CompareTo(b.inputTime));
        sortedBeats.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));

        List<InputMatch> matches = new List<InputMatch>();
        int inputIndex = 0;
        int beatIndex = 0;

        // Calculate the input window start time (4 beats after pattern start)
        double inputWindowStart = beatGenerator.InputStartTime;
        double beatInterval = 60.0 / beatGenerator.metronome.bpm;

        while (beatIndex < sortedBeats.Count && inputIndex < sortedInputs.Count)
        {
            // Adjust scheduled time to input window
            double adjustedBeatTime = sortedBeats[beatIndex].scheduledTime +
                                     (beatGenerator.InputStartTime - beatGenerator.PatternStartTime);
            double inputTime = sortedInputs[inputIndex].inputTime;
            double delta = inputTime - adjustedBeatTime;

            var match = new InputMatch
            {
                BeatIndex = beatIndex,
                InputIndex = inputIndex,
                TimingError = delta
            };

            // Too early - input doesn't match this beat, try next input
            if (delta < -goodThreshold)
            {
                match.Quality = InputMatch.MatchQuality.TooEarly;
                matches.Add(match);
                inputIndex++;
            }
            // Too late - beat was missed, try next beat
            else if (delta > goodThreshold)
            {
                match.Quality = InputMatch.MatchQuality.TooLate;
                matches.Add(match);
                beatIndex++;
            }
            // Within timing window - check side correctness
            else
            {
                bool correctSide = sortedBeats[beatIndex].isRightBongo == sortedInputs[inputIndex].isRightBongo;

                if (correctSide)
                {
                    // Perfect timing
                    if (Mathf.Abs((float)delta) <= perfectThreshold)
                    {
                        match.Quality = InputMatch.MatchQuality.Perfect;
                    }
                    // Good timing
                    else
                    {
                        match.Quality = InputMatch.MatchQuality.Good;
                    }
                }
                else
                {
                    match.Quality = InputMatch.MatchQuality.WrongSide;
                }

                matches.Add(match);
                beatIndex++;
                inputIndex++;
            }
        }

        // Mark remaining beats as missed
        while (beatIndex < sortedBeats.Count)
        {
            matches.Add(new InputMatch
            {
                BeatIndex = beatIndex,
                InputIndex = -1,
                Quality = InputMatch.MatchQuality.Miss,
                TimingError = 0
            });
            beatIndex++;
        }

        return matches;
    }

    private int CountCorrectHits(List<InputMatch> matches)
    {
        int count = 0;
        foreach (var match in matches)
        {
            if (match.Quality == InputMatch.MatchQuality.Perfect ||
                match.Quality == InputMatch.MatchQuality.Good)
            {
                count++;
            }
        }
        return count;
    }

    private int CountPerfectHits(List<InputMatch> matches)
    {
        int count = 0;
        foreach (var match in matches)
        {
            if (match.Quality == InputMatch.MatchQuality.Perfect)
            {
                count++;
            }
        }
        return count;
    }
    #endregion

    #region Grading Logic
    /// <summary>
    /// Determine grade based on performance. Simplified from original nested if hell.
    /// </summary>
    private EvaluationGrade DetermineGrade(int correctHits, int perfectHits,
                                           int totalInputs, int totalBeats)
    {
        // Perfect: All beats hit with perfect timing and correct input count
        if (correctHits == totalBeats &&
            perfectHits == totalBeats &&
            totalInputs == totalBeats)
        {
            return EvaluationGrade.AllPerfect;
        }

        // Good: All beats hit (not all perfect) with correct input count
        if (correctHits == totalBeats && totalInputs == totalBeats)
        {
            return EvaluationGrade.AllGood;
        }

        // Passable: Most beats hit OR all beats with minor input count error
        bool mostBeatsHit = correctHits >= totalBeats - allowedMistakes;
        bool minorInputError = Mathf.Abs(totalInputs - totalBeats) <= allowedMistakes;

        if (mostBeatsHit && (totalInputs == totalBeats || minorInputError))
        {
            return EvaluationGrade.Passable;
        }

        // Failed: Everything else
        return EvaluationGrade.Failed;
    }

    private int CalculatePoints(EvaluationGrade grade)
    {
        return grade switch
        {
            EvaluationGrade.AllPerfect => perfectReward,
            EvaluationGrade.AllGood => goodReward,
            EvaluationGrade.Passable => passableReward,
            EvaluationGrade.Failed => 0,
            _ => 0
        };
    }
    #endregion

    #region Result Application
    private void ApplyEvaluationResult(EvaluationResult result)
    {
        PerfectHits = result.PerfectHits;

        if (result.Grade == EvaluationGrade.Failed)
        {
            HandleFailure();
        }
        else
        {
            HandleSuccess(result.PointsAwarded);
        }
    }

    private void HandleSuccess(int points)
    {
        Debug.Log($"[BeatEvaluator] Success! +{points} points");

        // Play appropriate feedback sound
        if (points == perfectReward)
        {
            AudioManager.instance.PlayAllPerfect();
        }
        else if (points == goodReward)
        {
            AudioManager.instance.PlayCorrect();
        }
        else
        {
            AudioManager.instance.PlayPassable();
        }

        // Animate character
        custardAnimator.HandleSuccess();

        // Update score
        Score += points;
        SaveHighScore();
        UpdateScoreDisplay();

        // Reset failure state
        HasFailedOnce = false;
    }

    private void HandleFailure()
    {
        Debug.Log("[BeatEvaluator] Failed bar");

        // Animate character
        custardAnimator.HandleFailure();
        AudioManager.instance.PlayIncorrect();

        // Two-strike system
        if (!HasFailedOnce)
        {
            HasFailedOnce = true;
        }
        else if (Score > 0)
        {
            // Second failure - reset score
            SaveHighScore();
            Score = 0;
            UpdateScoreDisplay();
            AudioManager.instance.PlayTotalFail();
        }
    }
    #endregion

    #region Score Management
    public void SaveHighScore()
    {
        int currentHighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        if (Score > currentHighScore)
        {
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, Score);
            PlayerPrefs.Save();
            Debug.Log($"[BeatEvaluator] New high score: {Score}");
        }
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = Score.ToString();
        }
    }

    public void ResetScore()
    {
        Score = 0;
        PerfectHits = 0;
        HasFailedOnce = false;
        UpdateScoreDisplay();
    }
    #endregion

    #region Debugging
    private void LogEvaluationResult(EvaluationResult result, int inputCount)
    {
        Debug.Log($"[BeatEvaluator] Evaluation Complete:");
        Debug.Log($"  Grade: {result.Grade}");
        Debug.Log($"  Correct: {result.CorrectHits}/{result.TotalBeats}");
        Debug.Log($"  Perfect: {result.PerfectHits}/{result.TotalBeats}");
        Debug.Log($"  Inputs: {inputCount}");
        Debug.Log($"  Points: +{result.PointsAwarded}");
        Debug.Log($"  Total Score: {Score}");
    }
    #endregion
}