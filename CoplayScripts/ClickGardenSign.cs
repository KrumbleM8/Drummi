using UnityEngine;
using UnityEngine.UI;

/// <summary>PLAY-MODE: taps the Garden sign's button (sign must be focused to launch).</summary>
public static class ClickGardenSign
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "not in play mode";
        var go = GameObject.Find("PlanetPivot/SignMount_3/MenuSign_Garden/SignCanvas/SignButton");
        if (go == null) return "Garden SignButton not found";
        go.GetComponent<Button>().onClick.Invoke();
        return "invoked Garden sign at t=" + Time.unscaledTime.ToString("F2");
    }
}
