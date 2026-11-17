using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: invokes FlashCardsMenu buttons. Entry points: ClickNext, ClickPrev, ClickBack, ClickFocusedSign.</summary>
public static class ClickFlashCardsMenu
{
    public static string ClickNext()  => Click("OverlayCanvas/NextButton");
    public static string ClickPrev()  => Click("OverlayCanvas/PrevButton");
    public static string ClickBack()  => Click("OverlayCanvas/BackButton");

    public static string ClickFocusedSign()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsSortMode.None))
        {
            var btn = node.GetComponentInChildren<Button>(true);
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                return $"Invoked focused sign '{node.name}'";
            }
        }
        return "ERROR: no interactable sign found";
    }

    private static string Click(string path)
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var go = GameObject.Find(path);
        if (go == null) return $"ERROR: '{path}' not found/active";
        var btn = go.GetComponent<Button>();
        if (btn == null) return $"ERROR: no Button on '{path}'";
        btn.onClick.Invoke();
        return $"Invoked onClick of '{path}'";
    }
}
