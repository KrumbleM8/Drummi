using UnityEngine;

public static class GameConstants
{
    // Timing
    public const int BEATS_PER_BAR = 4;
    public const int BEATS_PER_LOOP = 8;
    public const float EVALUATION_TIMING_BEATS = 7.5f;
    public const double NEUTRAL_ANIMATION_LEAD_TIME = 0.1;
    public const float ANIMATION_HOLD_DURATION_MIN = 0.3f;
    public const float ANIMATION_HOLD_MULTIPLIER = 0.9f;

    // Pattern Generation
    public const float PATTERN_MEASURE_LENGTH = 3.5f;

    // Custard Sprites
    public const int SPRITE_LISTENING = 4;

    // Timing Precision
    public const double BEAT_TIMING_THRESHOLD = 0.6;
}
