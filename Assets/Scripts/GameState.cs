using UnityEngine;

public enum GameState
{
    Uninitialized,
    WaitingForFirstBar,
    Playing,
    GeneratingFinalPattern,
    EvaluatingFinalBar,
    GameComplete
}
