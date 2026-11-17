using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PLAY-MODE: taps the title Play button, then screenshots the zoom at
/// 0.7 / 1.3 / 1.9 s (Temp/intro_mid1..3.png) and runs ProbeHomeIntro at 3.2 s.
/// Uses DOTween's DelayedCall so no MonoBehaviour is needed.
/// </summary>
public static class RunIntroCaptureSeries
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[IntroSeries] not in play mode"); return; }
        var go = GameObject.Find("OverlayCanvas/TitleBanner/PlayButton");
        if (go == null) { Debug.Log("[IntroSeries] PlayButton not found/active"); return; }
        go.GetComponent<Button>().onClick.Invoke();
        float t0 = Time.unscaledTime;
        Debug.Log("[IntroSeries] Play tapped at " + t0.ToString("F2"));

        DOVirtual.DelayedCall(0.8f, () => ScreenCapture.CaptureScreenshot("Temp/intro_mid1.png", 1)).SetUpdate(true);
        DOVirtual.DelayedCall(1.6f, () => ScreenCapture.CaptureScreenshot("Temp/intro_mid2.png", 1)).SetUpdate(true);
        DOVirtual.DelayedCall(2.4f, () => ScreenCapture.CaptureScreenshot("Temp/intro_mid3.png", 1)).SetUpdate(true);
        DOVirtual.DelayedCall(3.2f, () => ScreenCapture.CaptureScreenshot("Temp/intro_mid4.png", 1)).SetUpdate(true);
        DOVirtual.DelayedCall(3.9f, () => ScreenCapture.CaptureScreenshot("Temp/intro_mid5.png", 1)).SetUpdate(true);
        DOVirtual.DelayedCall(5.0f, () =>
        {
            var cam = GameObject.Find("Main Camera");
            Debug.Log("[IntroSeries] zoom should be finished, elapsed=" + (Time.unscaledTime - t0).ToString("F2") +
                      " camera=" + (cam != null ? cam.transform.position.ToString("F2") : "?"));
        }).SetUpdate(true);
    }
}
