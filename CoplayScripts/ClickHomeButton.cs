using UnityEngine;
using UnityEngine.UI;

public static class ClickHomeButton
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var go = GameObject.Find("FlickCanvas/MainPage/HomeButton");
        if (go == null) go = GameObject.Find("FlashCardsCanvas/MainPage/HomeButton");
        if (go == null) return "ERROR: HomeButton not found/active";
        go.GetComponent<Button>().onClick.Invoke();
        return "Invoked HomeButton onClick";
    }
}
