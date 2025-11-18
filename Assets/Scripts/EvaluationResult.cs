using UnityEngine;

public enum EvaluationGrade
{
    AllPerfect,
    AllGood,
    Passable,
    Failed
}

public class EvaluationResult
{
    public EvaluationGrade Grade { get; private set; }
    public int CorrectHits { get; private set; }
    public int PerfectHits { get; private set; }
    public int TotalBeats { get; private set; }
    public int PointsAwarded { get; private set; }

    public EvaluationResult(EvaluationGrade grade, int correct, int perfect, int total, int points)
    {
        Grade = grade;
        CorrectHits = correct;
        PerfectHits = perfect;
        TotalBeats = total;
        PointsAwarded = points;
    }

    public float Accuracy => TotalBeats > 0 ? (float)CorrectHits / TotalBeats : 0f;
}
