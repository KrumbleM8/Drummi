using DG.Tweening;
using UnityEngine;

// PLAY-MODE (Bongo.unity, during a run): reference captures of real gameplay.
//   Execute()         — 14 screenshots at 0.35 s intervals to Temp/bongo_cap_NN.png
//   ExecuteWithHits() — same burst, plus simulated player hits (L/R alternating every
//                       0.3 s) via BongoModeInputReader.TriggerInput so response-window
//                       visuals (pad squash, hit flash) appear in some frames.
public static class CaptureBongoBurst
{
    private const int   Count    = 14;
    private const float Interval = 0.35f;

    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[BongoBurst] not in play mode"); return; }
        for (int i = 0; i < Count; i++)
        {
            string path = "Temp/bongo_cap_" + i.ToString("00") + ".png";
            DOVirtual.DelayedCall(i * Interval, () => ScreenCapture.CaptureScreenshot(path, 1), true);
        }
        Debug.Log("[BongoBurst] scheduled " + Count + " captures");
    }

    public static void ExecuteWithHits()
    {
        if (!Application.isPlaying) { Debug.Log("[BongoBurst] not in play mode"); return; }
        var reader = Object.FindFirstObjectByType<BongoModeInputReader>();
        if (reader == null) Debug.Log("[BongoBurst] no BongoModeInputReader (hits skipped)");
        for (int i = 0; i < Count * 2; i++)
        {
            bool right = (i % 2) == 1;
            if (reader != null)
                DOVirtual.DelayedCall(i * 0.3f, () => reader.TriggerInput(right), true);
        }
        Execute();
    }
}
