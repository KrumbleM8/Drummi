using UnityEngine;

public class InputMatch
{
    public enum MatchQuality
    {
        Perfect,
        Good,
        WrongSide,
        TooEarly,
        TooLate,
        Miss
    }

    public MatchQuality Quality { get; set; }
    public double TimingError { get; set; }
    public int BeatIndex { get; set; }
    public int InputIndex { get; set; }
}
