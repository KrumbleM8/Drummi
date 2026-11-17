using UnityEngine;

/// <summary>PLAY-MODE ONLY: numbered game-view screenshots (pick via methodName).</summary>
public static class CaptureGameViewSeries
{
    public static string Capture1() => Cap("Temp/flash_cap1.png");
    public static string Capture2() => Cap("Temp/flash_cap2.png");
    public static string Capture3() => Cap("Temp/flash_cap3.png");

    private static string Cap(string path)
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        ScreenCapture.CaptureScreenshot(path, 1);
        return $"Screenshot queued to {path}";
    }
}
