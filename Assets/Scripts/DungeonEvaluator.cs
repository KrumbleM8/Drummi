using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Evaluates Dungeon mode player inputs against scheduled beats and manages scoring.
/// Parallel to BeatEvaluator.cs — same timing-window and grading logic, but operates
/// on DungeonInput / DungeonScheduledBeat and checks DungeonEnemyType instead of bongo side.
///
/// Timing thresholds are BPM-scaled fractions of one beat, identical to Bongo mode.
/// </summary>
public class DungeonEvaluator : MonoBehaviour
{
    #region Inspector
    [Header("References")]
    [SerializeField] private Metronome    metronome;
    [SerializeField] private TMP_Text     scoreText;
    [SerializeField] private DungeonHealth health;

    [Header("Timing Windows (fraction of one beat)")]
    [SerializeField][Range(0f, 0.5f)] private float perfectFraction = 0.08f;
    [SerializeField][Range(0f, 0.5f)] private float goodFraction    = 0.25f;

    [Header("Scoring")]
    [SerializeField] private int perfectReward   = 200;
    [SerializeField] private int goodReward      = 100;
    [SerializeField] private int passableReward  = 50;
    [SerializeField] private int allowedMistakes = 1;
    #endregion

    #region Public State
    public int  Score          { get; private set; }
    public int  PerfectHits    { get; private set; }
    public int  TotalPerfectHits { get; private set; }
    public bool HasFailedOnce  { get; private set; }
    public bool IsNewHighScore => Score > _previousHighScore;
    #endregion

    #region Private
    private const string HIGH_SCORE_KEY = "DungeonHS";
    private int _previousHighScore;

    private float PerfectThreshold => (float)(60.0 / metronome.bpm) * perfectFraction;
    private float GoodThreshold    => (float)(60.0 / metronome.bpm) * goodFraction;
    #endregion

    #region Lifecycle
    private void Start()
    {
        Score = 0;
        HasFailedOnce = false;
        UpdateScoreDisplay();
    }
    #endregion

    #region Public API — Evaluation
    /// <summary>
    /// Evaluate all player inputs against scheduled beats for one bar.
    /// timeOffset = InputWindowStart - PatternStartTime (maps pattern-phase beats
    /// forward into response-phase time, identical to BeatEvaluator's approach).
    /// </summary>
    public EvaluationResult EvaluateBar(
        List<DungeonInput>         inputs,
        List<DungeonScheduledBeat> beats,
        double                     timeOffset)
    {
        if (GameClock.Instance.IsPaused)
        {
            Debug.LogWarning("[DungeonEvaluator] Attempted evaluation while paused");
            return null;
        }

        if (beats.Count == 0)
        {
            Debug.LogWarning("[DungeonEvaluator] No beats to evaluate");
            return null;
        }

        var matches  = MatchInputsToBeats(inputs, beats, timeOffset);
        int correct  = CountCorrect(matches);
        int perfect  = CountPerfect(matches);
        var grade    = DetermineGrade(correct, perfect, inputs.Count, beats.Count);
        int points   = CalculatePoints(grade);
        var result   = new EvaluationResult(grade, correct, perfect, beats.Count, points);

        ApplyResult(result);
        LogResult(result, inputs.Count);
        return result;
    }

    /// <summary>
    /// Immediate single-input evaluation for visual feedback.
    /// Returns the MatchQuality and the specific beat that was matched so the caller
    /// can resolve the exact paired enemy (Bug 2 fix).
    /// </summary>
    public (InputMatch.MatchQuality quality, DungeonScheduledBeat matchedBeat) EvaluateSingleInput(
        DungeonInput               input,
        List<DungeonScheduledBeat> beats,
        double                     timeOffset)
    {
        if (beats == null || beats.Count == 0)
            return (InputMatch.MatchQuality.Miss, null);

        // Find the closest adjusted beat to this input
        DungeonScheduledBeat closest  = null;
        double               smallest = double.MaxValue;

        foreach (var b in beats)
        {
            double adj   = b.scheduledTime + timeOffset;
            double delta = Math.Abs(input.inputTime - adj);
            if (delta < smallest) { smallest = delta; closest = b; }
        }

        double adjusted = closest.scheduledTime + timeOffset;
        double error    = input.inputTime - adjusted;

        if (error < -GoodThreshold) return (InputMatch.MatchQuality.TooEarly,  closest);
        if (error >  GoodThreshold) return (InputMatch.MatchQuality.TooLate,   closest);
        if (closest.enemyType != input.enemyType) return (InputMatch.MatchQuality.WrongSide, closest);

        var q = Mathf.Abs((float)error) <= PerfectThreshold
            ? InputMatch.MatchQuality.Perfect
            : InputMatch.MatchQuality.Good;
        return (q, closest);
    }
    #endregion

    #region Matching
    private List<InputMatch> MatchInputsToBeats(
        List<DungeonInput>         inputs,
        List<DungeonScheduledBeat> beats,
        double                     timeOffset)
    {
        var sortedInputs = new List<DungeonInput>(inputs);
        var sortedBeats  = new List<DungeonScheduledBeat>(beats);
        sortedInputs.Sort((a, b) => a.inputTime.CompareTo(b.inputTime));
        sortedBeats.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));

        var matches = new List<InputMatch>();
        int ii = 0, bi = 0;

        while (bi < sortedBeats.Count && ii < sortedInputs.Count)
        {
            double adjustedBeat = sortedBeats[bi].scheduledTime + timeOffset;
            double delta        = sortedInputs[ii].inputTime - adjustedBeat;
            var    match        = new InputMatch { BeatIndex = bi, InputIndex = ii, TimingError = delta };

            if (delta < -GoodThreshold)
            {
                match.Quality = InputMatch.MatchQuality.TooEarly;
                matches.Add(match); ii++;
            }
            else if (delta > GoodThreshold)
            {
                match.Quality = InputMatch.MatchQuality.TooLate;
                matches.Add(match); bi++;
            }
            else
            {
                bool correctType = sortedBeats[bi].enemyType == sortedInputs[ii].enemyType;
                match.Quality = correctType
                    ? (Mathf.Abs((float)delta) <= PerfectThreshold
                        ? InputMatch.MatchQuality.Perfect
                        : InputMatch.MatchQuality.Good)
                    : InputMatch.MatchQuality.WrongSide;

                matches.Add(match); bi++; ii++;
            }
        }

        // Remaining beats are misses
        while (bi < sortedBeats.Count)
        {
            matches.Add(new InputMatch
            {
                BeatIndex  = bi,
                InputIndex = -1,
                Quality    = InputMatch.MatchQuality.Miss
            });
            bi++;
        }

        return matches;
    }

    private int CountCorrect(List<InputMatch> matches)
    {
        int n = 0;
        foreach (var m in matches)
            if (m.Quality == InputMatch.MatchQuality.Perfect || m.Quality == InputMatch.MatchQuality.Good) n++;
        return n;
    }

    private int CountPerfect(List<InputMatch> matches)
    {
        int n = 0;
        foreach (var m in matches)
            if (m.Quality == InputMatch.MatchQuality.Perfect) n++;
        return n;
    }
    #endregion

    #region Grading
    private EvaluationGrade DetermineGrade(int correct, int perfect, int inputs, int total)
    {
        if (correct == total && perfect == total && inputs == total) return EvaluationGrade.AllPerfect;
        if (correct == total && inputs == total)                     return EvaluationGrade.AllGood;

        bool mostHit    = correct >= total - allowedMistakes;
        bool minorError = Mathf.Abs(inputs - total) <= allowedMistakes;
        if (mostHit && (inputs == total || minorError))             return EvaluationGrade.Passable;

        return EvaluationGrade.Failed;
    }

    private int CalculatePoints(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.AllPerfect => perfectReward,
        EvaluationGrade.AllGood    => goodReward,
        EvaluationGrade.Passable   => passableReward,
        _                          => 0
    };
    #endregion

    #region Result Application
    private void ApplyResult(EvaluationResult result)
    {
        PerfectHits       = result.PerfectHits;
        TotalPerfectHits += result.PerfectHits;

        if (result.Grade == EvaluationGrade.Failed)
            HandleFailure(result.TotalBeats - result.CorrectHits);
        else
            HandleSuccess(result.PointsAwarded);
    }

    private void HandleSuccess(int points)
    {
        if      (points == perfectReward) AudioManager.instance.PlayAllPerfect();
        else if (points == goodReward)    AudioManager.instance.PlayCorrect();
        else                              AudioManager.instance.PlayPassable();

        Score += points;
        UpdateScoreDisplay();
        HasFailedOnce = false;
        Debug.Log($"[DungeonEvaluator] Success! +{points}  Total: {Score}");
    }

    private void HandleFailure(int missedBeats)
    {
        AudioManager.instance.PlayIncorrect();
        health?.TakeMissDamage(missedBeats);
        Debug.Log("[DungeonEvaluator] Failed bar");

        if (!HasFailedOnce)
        {
            HasFailedOnce = true;
        }
        else if (Score > 0)
        {
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
        int saved = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        if (Score > saved) { PlayerPrefs.SetInt(HIGH_SCORE_KEY, Score); PlayerPrefs.Save(); }
    }

    public void ResetScore()
    {
        _previousHighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        Score            = 0;
        PerfectHits      = 0;
        TotalPerfectHits = 0;
        HasFailedOnce    = false;
        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null) scoreText.text = Score.ToString();
    }
    #endregion

    #region Debug
    private void LogResult(EvaluationResult result, int inputCount)
    {
        Debug.Log($"[DungeonEvaluator] Grade: {result.Grade}  " +
                  $"Correct: {result.CorrectHits}/{result.TotalBeats}  " +
                  $"Perfect: {result.PerfectHits}/{result.TotalBeats}  " +
                  $"Inputs: {inputCount}  +{result.PointsAwarded}  Total: {Score}");
    }
    #endregion
}
