using UnityEngine;
using UnityEngine.UI;

/// <summary>PLAY-MODE ONLY: taps the currently focused sign's button (launch test).</summary>
public static class TestTapFocusedSign
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        GameObject sign = GameObject.Find("MenuSign_DoubleTime");
        if (sign == null) return "FAIL: MenuSign_DoubleTime not found (inactive?).";
        Button button = sign.transform.Find("SignCanvas/SignButton").GetComponent<Button>();
        button.onClick.Invoke();
        return "Tapped focused sign button.";
    }
}
