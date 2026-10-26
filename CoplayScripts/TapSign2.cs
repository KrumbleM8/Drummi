using UnityEngine;
using UnityEngine.UI;

/// <summary>PLAY MODE: press the SignButton of sign mount 2 (first press focuses, second launches).</summary>
public static class TapSign2
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var go = GameObject.Find("PlanetPivot/SignMount_2/MenuSign_Normal/SignCanvas/SignButton");
        if (go == null) return "ERROR: SignButton not found";
        var btn = go.GetComponent<Button>();
        btn.onClick.Invoke();
        return "Invoked SignButton of MenuSign_Normal";
    }
}
