using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: moves the menu camera to a candidate pose, refreshes rim sorting, queues a screenshot.</summary>
public static class PreviewMenuCamera
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 15f, -8f), Quaternion.Euler(17f, 0f, 0f));
        var spinner = Object.FindFirstObjectByType<PlanetSpinner>();
        if (spinner != null) spinner.RefreshRimSorting();
        ScreenCapture.CaptureScreenshot("Temp/flashcard_capture.png", 1);
        return "Camera set to (0,15,-8) pitch 17; screenshot queued";
    }
}
