using UnityEngine;

public static class SnapGameView
{
    public static string Execute()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "../Screenshots/gameview.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        return "Capture queued to " + path;
    }
}
