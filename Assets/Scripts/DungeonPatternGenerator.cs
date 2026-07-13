using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates randomised Dungeon beat patterns.
/// Parallel to PatternGenerator.cs — same measure-length logic, but assigns
/// DungeonEnemyType (3 options by default) instead of a binary bongo side.
/// Pass <paramref name="allowedTypes"/> to restrict the type vocabulary —
/// e.g. <c>{ Left, Right }</c> for CombatTwoPad rooms.
/// </summary>
public class DungeonPatternGenerator
{
    private static readonly DungeonEnemyType[] AllTypes =
        { DungeonEnemyType.Left, DungeonEnemyType.Center, DungeonEnemyType.Right };

    private readonly float[]            allowedDurations;
    private readonly int                maxConsecutiveSameType;
    private readonly DungeonEnemyType[] _allowedTypes;

    public DungeonPatternGenerator(
        float[]            durations,
        int                maxConsecutiveSameType = 2,
        DungeonEnemyType[] allowedTypes           = null)
    {
        allowedDurations            = durations;
        this.maxConsecutiveSameType = maxConsecutiveSameType;
        _allowedTypes               = (allowedTypes != null && allowedTypes.Length > 0)
                                      ? allowedTypes
                                      : AllTypes;
    }

    public List<DungeonBeat> GeneratePattern()
    {
        var pattern  = new List<DungeonBeat>();
        float timeSlot = 0f;

        while (timeSlot < GameConstants.PATTERN_MEASURE_LENGTH)
        {
            float duration = SelectValidDuration(timeSlot);
            if (duration == 0f) break;

            DungeonEnemyType type = DetermineEnemyType(pattern);
            pattern.Add(new DungeonBeat(duration, timeSlot, type));
            timeSlot += duration;
        }

        return pattern;
    }

    /// <summary>
    /// Generates a phrase that fills exactly <paramref name="phraseLengthBeats"/> beats,
    /// using the same duration weights and consecutive-type rules as GeneratePattern().
    /// GeneratePattern() and the PATTERN_MEASURE_LENGTH constant are not touched.
    /// </summary>
    /// <param name="phraseLengthBeats">
    /// Target length in beats (e.g. 2, 4, 6, 8 for call-and-response rounds).
    /// Beats are added until no allowed duration fits within the remaining length.
    /// </param>
    public List<DungeonBeat> GeneratePhrase(float phraseLengthBeats)
    {
        var   pattern  = new List<DungeonBeat>();
        float timeSlot = 0f;

        while (timeSlot < phraseLengthBeats)
        {
            float duration = SelectValidDurationForPhrase(timeSlot, phraseLengthBeats);
            if (duration == 0f) break;

            DungeonEnemyType type = DetermineEnemyType(pattern);
            pattern.Add(new DungeonBeat(duration, timeSlot, type));
            timeSlot += duration;
        }

        return pattern;
    }

    /// <summary>
    /// Like <see cref="GeneratePhrase"/> but forces every beat's enemyType to
    /// <paramref name="constantType"/>. Rhythm generation (durations, timeslots) is
    /// identical; only the type field is overwritten. Used by SingleDrum C&amp;R mode so
    /// the performer bounces neutrally and EvaluateBar grades on timing alone.
    /// </summary>
    public List<DungeonBeat> GeneratePhraseWithConstantType(float phraseLengthBeats, DungeonEnemyType constantType)
    {
        var beats = GeneratePhrase(phraseLengthBeats);
        for (int i = 0; i < beats.Count; i++)
            beats[i].enemyType = constantType;
        return beats;
    }

    // ── Private ───────────────────────────────────────────────────────────

    // Used by GeneratePhrase only — mirrors SelectValidDuration but against
    // an arbitrary measure length instead of PATTERN_MEASURE_LENGTH.
    private float SelectValidDurationForPhrase(float currentTime, float phraseLengthBeats)
    {
        var valid = new List<float>();
        foreach (float d in allowedDurations)
        {
            if (currentTime + d <= phraseLengthBeats)
                valid.Add(d);
        }
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : 0f;
    }

    private float SelectValidDuration(float currentTime)
    {
        var valid = new List<float>();
        foreach (float d in allowedDurations)
        {
            if (currentTime + d <= GameConstants.PATTERN_MEASURE_LENGTH)
                valid.Add(d);
        }
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : 0f;
    }

    private DungeonEnemyType DetermineEnemyType(List<DungeonBeat> existing)
    {
        if (existing.Count < maxConsecutiveSameType)
            return _allowedTypes[Random.Range(0, _allowedTypes.Length)];

        // Prevent more than maxConsecutiveSameType in a row
        DungeonEnemyType lastType = existing[^1].enemyType;
        bool allSame = true;

        for (int i = 1; i <= maxConsecutiveSameType && i <= existing.Count; i++)
        {
            if (existing[^i].enemyType != lastType) { allSame = false; break; }
        }

        if (allSame)
        {
            DungeonEnemyType next;
            do { next = _allowedTypes[Random.Range(0, _allowedTypes.Length)]; }
            while (next == lastType);
            return next;
        }

        return _allowedTypes[Random.Range(0, _allowedTypes.Length)];
    }
}
