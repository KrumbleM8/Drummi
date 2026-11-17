using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public static class ClickFlickBar
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var go = GameObject.Find("Bar_Flick");
        if (go == null) return "ERROR: Bar_Flick not found/active";
        go.GetComponent<Button>().onClick.Invoke();
        return "Bar_Flick onClick invoked";
    }
}
