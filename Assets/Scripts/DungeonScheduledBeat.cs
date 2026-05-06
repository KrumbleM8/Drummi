/// <summary>
/// A beat that has been scheduled for a Dungeon mode bar.
/// Parallel to ScheduledBeat.cs but carries a DungeonEnemyType instead of isRightBongo.
/// scheduledTime is virtual (GameClock-based).
/// </summary>
public class DungeonScheduledBeat
{
    public double scheduledTime; // Virtual time
    public DungeonEnemyType enemyType;

    public DungeonScheduledBeat(double scheduledTime, DungeonEnemyType enemyType)
    {
        this.scheduledTime = scheduledTime;
        this.enemyType     = enemyType;
    }
}
