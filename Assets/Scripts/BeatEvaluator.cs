using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

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

    [Header("Timing Windows (fraction of one beat)")]
    [SerializeField][Range(0f, 0.5f)] private float perfectFraction = 0.08f;
    [SerializeField][Range(0f, 0.5f)] private float goodFraction = 0.25f;

    private float PerfectThreshold => (float)(60.0 / beatGenerator.metronome.bpm) * perfectFraction;
    private float GoodThreshold => (float)(60.0 / beatGenerator.metronome.bpm) * goodFraction;

    [Header("Scoring")]
    [SerializeField] private int perfectReward = 200;
    [SerializeField] private int goodReward = 100;
    [SerializeField] private int passableReward = 50;
    [SerializeField] private int allowedMistakes = 1;
    #endregion

    #region Public State
    public int Score { get; private set; }
    public int PerfectHits { get; private set; }
    public int TotalPerfectHits { get; private set; }
    public bool HasFailedOnce { get; private set; }

    /// <summary>True if the current Score exceeds the high score recorded at the start of this session.</summary>
    public bool IsNewHighScore => Score > _previousHighScore;

    /// <summary>
    /// Whole-run tally (bars passed, perfect phrases, hit breakdown, early/late lean)
    /// for the results-screen report. Reset with the score; never cleared by a
    /// double-fail, so the report always covers the full run.
    /// </summary>
    public BongoRunStats RunStats { get; } = new BongoRunStats();
    #endregion

    #region High Score Key
    // Legacy single board. Per-song boards: SongSelectController.FinalizeSong
    // sets the song's key via BongoModeController.SetHighScoreKey before StartGame.
    private const string DefaultHighScoreKey = "GlitchyHS";
    private string _highScoreKey = DefaultHighScoreKey;

    /// <summary>PlayerPrefs key of the board the current/last run is scored against.</summary>
    public string HighScoreKey => _highScoreKey;

    /// <summary>
    /// Overrides the PlayerPrefs high-score key (one board per song). Call BEFORE
    /// ResetScore so IsNewHighScore compares against the right board.
    /// </summary>
    public void SetHighScoreKey(string key)
        => _highScoreKey = string.IsNullOrEmpty(key) ? DefaultHighScoreKey : key;
    #endregion

    private int _previousHighScore;

    // Beats already matched by a successful (Perfect/Good) input this bar.
    // EvaluateSingleInput skips these so spamming can't earn multiple success
    // visuals off one beat indicator — mirroring EvaluateBar's two-pointer
    // matching, where each beat is consumed by at most one input.
    // ScheduledBeat instances are recreated every bar (SchedulePattern), so
    // stale references can never collide; cleared each EvaluateBar anyway.
    private readonly HashSet<ScheduledBeat> _claimedBeats = new HashSet<ScheduledBeat>();

    // Live "clean phrase" tracking for the phrase-clear burst. Counts every
    // input this bar that EvaluateSingleInput did NOT grade Perfect/Good
    // (early, late, wrong side, spare tap). Reset with the claimed beats.
    private int _barBadInputs;

    /// <summary>
    /// True right after an EvaluateSingleInput call that claimed the LAST
    /// unclaimed beat of the bar with no bad input so far — i.e. the player
    /// has just played the whole phrase clean (at least AllGood). This is the
    /// "last note of a full pass" moment the phrase-clear burst fires on; a
    /// spare tap afterwards can still downgrade the final grade to Passable.
    /// </summary>
    public bool LastInputClearedBar { get; private set; }

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

        // Bar is over — release claimed beats for the next pattern.
        _claimedBeats.Clear();
        _barBadInputs = 0;
        LastInputClearedBar = false;

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

        // Fold into the whole-run report before the score/fail bookkeeping,
        // so a double-fail score reset can never touch the tally.
        RunStats.RecordBar(result, matches, 60.0 / beatGenerator.metronome.bpm, PerfectThreshold);

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
            if (delta < -GoodThreshold)
            {
                match.Quality = InputMatch.MatchQuality.TooEarly;
                matches.Add(match);
                inputIndex++;
            }
            // Too late - beat was missed, try next beat
            else if (delta > GoodThreshold)
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
                    if (Mathf.Abs((float)delta) <= PerfectThreshold)
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
        TotalPerfectHits += result.PerfectHits;

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

    /// <summary>
    /// Evaluates a single input immediately against the closest scheduled beat.
    /// Returns the MatchQuality of the input.
    /// </summary>
    public InputMatch.MatchQuality EvaluateSingleInput(BongoInput input)
    {
        LastInputClearedBar = false;

        var scheduledBeats = beatGenerator.ScheduledBeats;
        if (scheduledBeats == null || scheduledBeats.Count == 0)
        {
            Debug.LogWarning("[BeatEvaluator] No scheduled beats to evaluate input against.");
            return InputMatch.MatchQuality.Miss;
        }

        double timeOffset = beatGenerator.InputStartTime - beatGenerator.PatternStartTime;

        // Find the closest UNCLAIMED scheduled beat to the input time.
        // Claimed beats are skipped so a spammed input can't re-match a beat
        // that already produced a success visual this bar.
        ScheduledBeat closestBeat = null;
        double smallestDelta = double.MaxValue;

        foreach (var beat in scheduledBeats)
        {
            if (_claimedBeats.Contains(beat))
                continue;

            double adjustedBeatTime = beat.scheduledTime + timeOffset;
            double delta = Math.Abs(input.inputTime - adjustedBeatTime);

            if (delta < smallestDelta)
            {
                smallestDelta = delta;
                closestBeat = beat;
            }
        }

        // Every beat this bar has already been claimed — the extra input can't
        // be a success.
        if (closestBeat == null)
        {
            _barBadInputs++;
            return InputMatch.MatchQuality.Miss;
        }

        // Evaluate against the closest beat
        double adjustedClosestBeatTime = closestBeat.scheduledTime + timeOffset;
        double timingError = input.inputTime - adjustedClosestBeatTime;

        if (timingError < -GoodThreshold)
        {
            _barBadInputs++;
            return InputMatch.MatchQuality.TooEarly;
        }

        if (timingError > GoodThreshold)
        {
            _barBadInputs++;
            return InputMatch.MatchQuality.TooLate;
        }

        // Within timing window — check side
        if (closestBeat.isRightBongo != input.isRightBongo)
        {
            _barBadInputs++;
            return InputMatch.MatchQuality.WrongSide;
        }

        // Success — claim the beat so no further input can match it this bar.
        _claimedBeats.Add(closestBeat);

        // Last beat claimed with a clean sheet: the phrase is a full pass so far.
        LastInputClearedBar = _barBadInputs == 0 && _claimedBeats.Count == scheduledBeats.Count;

        if (Mathf.Abs((float)timingError) <= PerfectThreshold)
            return InputMatch.MatchQuality.Perfect;

        return InputMatch.MatchQuality.Good;
    }
    #endregion

    #region Score Management
    public void SaveHighScore()
    {
        int currentHighScore = PlayerPrefs.GetInt(_highScoreKey, 0);
        if (Score > currentHighScore)
        {
            PlayerPrefs.SetInt(_highScoreKey, Score);
            PlayerPrefs.Save();
            Debug.Log($"[BeatEvaluator] New high score: {Score} ({_highScoreKey})");
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
        _previousHighScore = PlayerPrefs.GetInt(_highScoreKey, 0);
        _claimedBeats.Clear();
        _barBadInputs = 0;
        LastInputClearedBar = false;
        Score = 0;
        PerfectHits = 0;
        TotalPerfectHits = 0;
        HasFailedOnce = false;
        RunStats.Reset();
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