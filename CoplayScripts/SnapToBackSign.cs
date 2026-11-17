using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: snaps the planet to sign index 2 (back sign) and queues a screenshot.</summary>
public static class SnapToBackSign
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        Object.FindFirstObjectByType<PlanetSpinner>().SnapTo(2);
        ScreenCapture.CaptureScreenshot("Temp/flashcard_capture.png", 1);
        return "snapped to 2, screenshot queued";
    }
}
