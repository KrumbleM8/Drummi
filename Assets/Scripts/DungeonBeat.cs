/// <summary>
/// A single beat in a Dungeon mode pattern.
/// Parallel to Beat.cs but carries a DungeonEnemyType instead of a bool side.
/// </summary>
public class DungeonBeat
{
    public float duration;
    public float timeSlot;
    public DungeonEnemyType enemyType;

    public DungeonBeat(float duration, float timeSlot, DungeonEnemyType enemyType)
    {
        this.duration  = duration;
        this.timeSlot  = timeSlot;
        this.enemyType = enemyType;
    }
}
