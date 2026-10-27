using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whole-run tally for one Bongo mode performance, fed one bar at a time by
/// <see cref="BeatEvaluator.EvaluateBar"/> and read by the results screen's
/// paper report (<see cref="BongoResultsReport"/>).
///
/// Counts bars (played / passed / all-perfect "phrases"), per-beat hit quality,
/// and the signed timing lean of every timed hit so the report can tell the
/// player whether they tend to rush (early) or drag (late).
/// </summary>
public class BongoRunStats
{
    #region Bars
    /// <summary>Bars that reached evaluation (call + response completed).</summary>
    public int BarsPlayed { get; private set; }

    /// <summary>Bars graded anything other than <see cref="EvaluationGrade.Failed"/>.</summary>
    public int BarsPassed { get; private set; }

    /// <summary>Bars graded <see cref="EvaluationGrade.AllPerfect"/> — every beat hit perfectly, no extra taps.</summary>
    public int PerfectPhrases { get; private set; }

    public int BarsFailed => BarsPlayed - BarsPassed;
    #endregion

    #region Beats
    /// <summary>Beats the player was asked to echo across the whole run.</summary>
    public int TotalBeats { get; private set; }

    public int PerfectHits { get; private set; }
    public int GoodHits { get; private set; }
    public int WrongSideHits { get; private set; }

    /// <summary>Beats that got no usable tap (unmatched, or the tap arrived after the good window).</summary>
    public int Misses { get; private set; }

    /// <summary>Taps that arrived before any beat's good window — extra, rushed inputs.</summary>
    public int ExtraEarlyTaps { get; private set; }

    public int CorrectHits => PerfectHits + GoodHits;
    #endregion

    #region Timing lean
    /// <summary>Timed hits (perfect / good / wrong side) that landed before the beat, outside the perfect window.</summary>
    public int EarlyHits { get; private set; }

    /// <summary>Timed hits (perfect / good / wrong side) that landed after the beat, outside the perfect window.</summary>
    public int LateHits { get; private set; }

    /// <summary>Number of hits that contributed a timing sample.</summary>
    public int TimedHits { get; private set; }

    // Sum of signed timing error of every timed hit, as a fraction of one beat
    // (negative = early). Normalised so songs at different BPMs compare alike.
    private double _offsetBeatsSum;
    private double _offsetSecondsSum;

    /// <summary>Mean signed timing error in beats (negative = early). 0 when there are no samples.</summary>
    public float MeanOffsetBeats => TimedHits > 0 ? (float)(_offsetBeatsSum / TimedHits) : 0f;

    /// <summary>Mean signed timing error in milliseconds (negative = early). 0 when there are no samples.</summary>
    public float MeanOffsetMs => TimedHits > 0 ? (float)(_offsetSecondsSum / TimedHits * 1000.0) : 0f;

    public enum TimingLean { NotEnoughData, OnTime, SlightlyEarly, Early, SlightlyLate, Late }

    /// <summary>Fewer timed hits than this and the lean is reported as inconclusive.</summary>
    public const int MinHitsForLean = 4;

    /// <summary>|mean offset| (beats) below this reads as on time.</summary>
    public const float OnTimeBeats = 0.025f;

    /// <summary>|mean offset| (beats) below this reads as "a touch" early/late, above as clearly early/late.</summary>
    public const float SlightLeanBeats = 0.06f;

    public TimingLean Lean
    {
        get
        {
            if (TimedHits < MinHitsForLean) return TimingLean.NotEnoughData;
            float m = MeanOffsetBeats;
            if (Mathf.Abs(m) < OnTimeBeats) return TimingLean.OnTime;
            bool slight = Mathf.Abs(m) < SlightLeanBeats;
            if (m < 0f) return slight ? TimingLean.SlightlyEarly : TimingLean.Early;
            return slight ? TimingLean.SlightlyLate : TimingLean.Late;
        }
    }

    /// <summary>Player-facing one-liner for the timing lean.</summary>
    public string LeanText => Lean switch
    {
        TimingLean.OnTime        => "Right on time!",
        TimingLean.SlightlyEarly => "A touch early",
        TimingLean.Early         => "Rushing - you hit early",
        TimingLean.SlightlyLate  => "A touch late",
        TimingLean.Late          => "Dragging - you hit late",
        _                        => "Not enough hits to judge",
    };
    #endregion

    #region Recording
    /// <summary>
    /// Folds one evaluated bar into the run totals.
    /// </summary>
    /// <param name="result">The bar's grade and counts.</param>
    /// <param name="matches">Per-beat / per-input matches from the evaluator (signed timing error in seconds).</param>
    /// <param name="beatInterval">Seconds per beat at the bar's tempo, used to normalise timing error.</param>
    /// <param name="perfectThreshold">Seconds inside which a hit counts as perfect (hits inside it carry no lean).</param>
    public void RecordBar(EvaluationResult result, List<InputMatch> matches, double beatInterval, double perfectThreshold)
    {
        if (result == null) return;

        BarsPlayed++;
        if (result.Grade != EvaluationGrade.Failed) BarsPassed++;
        if (result.Grade == EvaluationGrade.AllPerfect) PerfectPhrases++;
        TotalBeats += result.TotalBeats;

        if (matches == null) return;

        foreach (var m in matches)
        {
            switch (m.Quality)
            {
                case InputMatch.MatchQuality.Perfect:   PerfectHits++;   RecordTiming(m.TimingError, beatInterval, perfectThreshold); break;
                case InputMatch.MatchQuality.Good:      GoodHits++;      RecordTiming(m.TimingError, beatInterval, perfectThreshold); break;
                case InputMatch.MatchQuality.WrongSide: WrongSideHits++; RecordTiming(m.TimingError, beatInterval, perfectThreshold); break;
                case InputMatch.MatchQuality.TooEarly:  ExtraEarlyTaps++; break;
                case InputMatch.MatchQuality.TooLate:   Misses++; break;
                case InputMatch.MatchQuality.Miss:      Misses++; break;
            }
        }
    }

    private void RecordTiming(double errorSeconds, double beatInterval, double perfectThreshold)
    {
        TimedHits++;
        _offsetSecondsSum += errorSeconds;
        if (beatInterval > 0) _offsetBeatsSum += errorSeconds / beatInterval;

        if (errorSeconds < -perfectThreshold) EarlyHits++;
        else if (errorSeconds > perfectThreshold) LateHits++;
    }

    public void Reset()
    {
        BarsPlayed = BarsPassed = PerfectPhrases = 0;
        TotalBeats = PerfectHits = GoodHits = WrongSideHits = Misses = ExtraEarlyTaps = 0;
        EarlyHits = LateHits = TimedHits = 0;
        _offsetBeatsSum = _offsetSecondsSum = 0;
    }

    /// <summary>
    /// Independent copy. GameManager snapshots the stats before the results
    /// transition so a late reset/cleanup can't blank the report.
    /// </summary>
    public BongoRunStats Clone()
    {
        return new BongoRunStats
        {
            BarsPlayed = BarsPlayed, BarsPassed = BarsPassed, PerfectPhrases = PerfectPhrases,
            TotalBeats = TotalBeats, PerfectHits = PerfectHits, GoodHits = GoodHits,
            WrongSideHits = WrongSideHits, Misses = Misses, ExtraEarlyTaps = ExtraEarlyTaps,
            EarlyHits = EarlyHits, LateHits = LateHits, TimedHits = TimedHits,
            _offsetBeatsSum = _offsetBeatsSum, _offsetSecondsSum = _offsetSecondsSum,
        };
    }
    #endregion

    public override string ToString()
        => $"bars {BarsPassed}/{BarsPlayed} passed, {PerfectPhrases} perfect phrases | " +
           $"hits P{PerfectHits} G{GoodHits} W{WrongSideHits} M{Misses} X{ExtraEarlyTaps} of {TotalBeats} | " +
           $"lean {Lean} ({MeanOffsetMs:+0;-0}ms, early {EarlyHits} / late {LateHits})";
}
