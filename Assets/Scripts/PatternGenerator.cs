using System.Collections.Generic;
using UnityEngine;

public class PatternGenerator
{
    private readonly float[] allowedDurations;
    private readonly int maxSameSideHits;

    public PatternGenerator(float[] durations, int maxConsecutiveSameSide)
    {
        allowedDurations = durations;
        maxSameSideHits = maxConsecutiveSameSide;
    }

    public List<Beat> GeneratePattern()
    {
        List<Beat> pattern = new List<Beat>();
        float timeSlot = 0f;

        while (timeSlot < GameConstants.PATTERN_MEASURE_LENGTH)
        {
            float chosenDuration = SelectValidDuration(timeSlot);
            if (chosenDuration == 0f) break;

            bool side = DetermineSide(pattern);
            pattern.Add(new Beat(chosenDuration, timeSlot, side));
            timeSlot += chosenDuration;
        }

        return pattern;
    }

    private float SelectValidDuration(float currentTime)
    {
        List<float> valid = new List<float>();
        foreach (float duration in allowedDurations)
        {
            if (currentTime + duration <= GameConstants.PATTERN_MEASURE_LENGTH)
                valid.Add(duration);
        }

        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : 0f;
    }

    private bool DetermineSide(List<Beat> existingPattern)
    {
        if (existingPattern.Count < maxSameSideHits)
            return Random.value > 0.5f;

        // Check if last N hits were all the same side
        bool lastSide = existingPattern[^1].isBongoSide;
        bool allSameSide = true;

        for (int i = 1; i <= maxSameSideHits && i <= existingPattern.Count; i++)
        {
            if (existingPattern[^i].isBongoSide != lastSide)
            {
                allSameSide = false;
                break;
            }
        }

        return allSameSide ? !lastSide : Random.value > 0.5f;
    }
}
