using UnityEngine;
using UnityEngine.UI;

/// <summary>PLAY-MODE: taps the title banner's Play button (OverlayCanvas/TitleBanner/PlayButton).</summary>
public static class ClickHomePlay
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[ClickHomePlay] not in play mode"); return; }
        var go = GameObject.Find("OverlayCanvas/TitleBanner/PlayButton");
        if (go == null) { Debug.Log("[ClickHomePlay] PlayButton not found/active"); return; }
        var btn = go.GetComponent<Button>();
        btn.onClick.Invoke();
        Debug.Log("[ClickHomePlay] invoked Play at t=" + Time.unscaledTime.ToString("F2"));
    }
}
