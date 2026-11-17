using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: snaps the planet to sign index 3 and queues a screenshot.</summary>
public static class SnapToSign3
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        Object.FindFirstObjectByType<PlanetSpinner>().SnapTo(3);
        ScreenCapture.CaptureScreenshot("Temp/flashcard_capture.png", 1);
        return "snapped to 3, screenshot queued";
    }
}
