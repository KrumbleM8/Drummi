/// <summary>
/// Captures the outcome of a single dungeon room encounter.
/// Produced by RoomRunner at the end of a room and consumed by
/// the floor/progression layer.
/// </summary>
public class RoomResult
{
    /// <summary>True if the player survived the room (health > 0 at end).</summary>
    public bool Survived;

    /// <summary>Score earned during the room.</summary>
    public int Score;

    /// <summary>Health remaining when the room ended.</summary>
    public int HealthRemaining;

    /// <summary>Tier of the room that was played.</summary>
    public EnemyTier Tier;

    /// <summary>Wall-clock seconds elapsed from room start to room end.</summary>
    public float TimeElapsed;
}
