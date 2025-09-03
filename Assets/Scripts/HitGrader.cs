using UnityEngine;

public enum HitGrade
{
    Miss,
    Early,
    Correct,
    Late
}

public class HitGrader : MonoBehaviour
{
    [Tooltip("How close (in seconds) to the beat the player must press to be considered 'Correct'")]
    public float perfectWindow = 0.1f;

    [Tooltip("Outside this window is considered a Miss")]
    public float acceptableWindow = 0.25f;

    public int missCount = 0;
    public int earlyLateCount = 0;

    public bool IsPunishable => missCount > 1 || earlyLateCount > 2;

    public void ResetGrading()
    {
        missCount = 0;
        earlyLateCount = 0;
    }

    public HitGrade GradeHit(float beatTime, float inputTime)
    {
        float offset = inputTime - beatTime;
        float absOffset = Mathf.Abs(offset);

        if (absOffset <= perfectWindow)
        {
            return HitGrade.Correct;
        }
        else if (absOffset <= acceptableWindow)
        {
            return offset < 0 ? HitGrade.Early : HitGrade.Late;
        }
        else
        {
            return HitGrade.Miss;
        }
    }

    public void RegisterGrade(HitGrade grade)
    {
        switch (grade)
        {
            case HitGrade.Miss:
                missCount++;
                break;
            case HitGrade.Early:
            case HitGrade.Late:
                earlyLateCount++;
                break;
            case HitGrade.Correct:
                break;
        }
    }
}
