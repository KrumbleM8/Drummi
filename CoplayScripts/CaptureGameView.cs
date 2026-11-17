using UnityEngine;

/// <summary>PLAY-MODE ONLY: queues a game-view screenshot to Temp/flashcard_capture.png.</summary>
public static class CaptureGameView
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        ScreenCapture.CaptureScreenshot("Temp/flashcard_capture.png", 1);
        return "Screenshot queued to Temp/flashcard_capture.png";
    }
}
