public class ScheduledBeat
{
    public double scheduledTime; // This is now virtual time
    public bool isRightBongo;

    public ScheduledBeat(double time, bool isRight)
    {
        scheduledTime = time;
        isRightBongo = isRight;
    }

    // Helper method to get real DSP time when needed
    public double GetRealScheduledTime(double totalPausedTime)
    {
        return scheduledTime + totalPausedTime;
    }
}