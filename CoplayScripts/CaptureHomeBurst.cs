using DG.Tweening;
using UnityEngine;

// PLAY-MODE: 10 game-view screenshots at 0.45 s intervals to Temp/home_cap_NN.png
// (covers one 4 s Bongo preview loop with a little overlap).
public static class CaptureHomeBurst
{
    private const int   Count    = 10;
    private const float Interval = 0.45f;

    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[HomeBurst] not in play mode"); return; }
        for (int i = 0; i < Count; i++)
        {
            string path = "Temp/home_cap_" + i.ToString("00") + ".png";
            DOVirtual.DelayedCall(i * Interval, () => ScreenCapture.CaptureScreenshot(path, 1), true);
        }
        Debug.Log("[HomeBurst] scheduled " + Count + " captures");
    }
}
