/// <summary>
/// Records a single player input in Dungeon mode.
/// Parallel to BongoInput.cs but carries a DungeonEnemyType instead of isRightBongo.
/// inputTime is virtual (GameClock.GameTime).
/// </summary>
public class DungeonInput
{
    public double inputTime;
    public DungeonEnemyType enemyType;

    public DungeonInput(double inputTime, DungeonEnemyType enemyType)
    {
        this.inputTime  = inputTime;
        this.enemyType  = enemyType;
    }
}
