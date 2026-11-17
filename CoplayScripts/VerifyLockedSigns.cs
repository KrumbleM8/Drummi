using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

public static class VerifyLockedSigns
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var node in nodes)
        {
            var label = node.GetComponentInChildren<TMP_Text>(true);
            var board = node.transform.Find("Board");
            var boardColor = board != null ? board.GetComponent<SpriteRenderer>().color : Color.clear;
            sb.AppendLine(node.name + ": IsLocked=" + node.IsLocked
                + " label='" + (label != null ? label.text : "<none>") + "'"
                + " boardColor=" + boardColor);
        }

        // MenuSign_DoubleTime (index 1) was spun into focus earlier — tap it.
        var focusedLocked = GameObject.Find("PlanetPivot/SignMount_1/MenuSign_DoubleTime");
        if (focusedLocked != null)
        {
            var btn = focusedLocked.GetComponentInChildren<Button>(true);
            btn.onClick.Invoke();
            var systems = GameObject.Find("FlashCardSystems"); // null while inactive
            sb.AppendLine("Clicked focused locked sign -> shakeTweening="
                + DOTween.IsTweening(focusedLocked.transform)
                + " gameplayStarted=" + (systems != null && systems.activeInHierarchy));
        }
        else sb.AppendLine("MenuSign_DoubleTime not found!");

        return sb.ToString();
    }
}
