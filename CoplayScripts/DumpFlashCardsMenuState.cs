using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: dumps FlashCardsMenu runtime state — camera pose, chevron visibility, sign interactability/labels.</summary>
public static class DumpFlashCardsMenuState
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var log = new List<string>();

        var cam = Camera.main;
        log.Add($"Camera: pos {cam.transform.position}, rot {cam.transform.rotation.eulerAngles}");

        foreach (var name in new[] { "PrevButton", "NextButton", "BackButton" })
        {
            var go = GameObject.Find("OverlayCanvas/" + name); // null if inactive
            log.Add($"{name}: {(go != null ? "ACTIVE" : "inactive/missing")}");
        }

        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(nodes, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (var n in nodes)
        {
            var btn = n.GetComponentInChildren<Button>(true);
            var lbl = n.GetComponentInChildren<TMPro.TMP_Text>(true);
            log.Add($"{n.name}: interactable={btn.interactable}, label='{lbl.text}', canvasCam={(n.GetComponentInChildren<Canvas>(true).worldCamera != null ? "set" : "NULL")}");
        }

        var transition = Object.FindAnyObjectByType<ScreenTransition>();
        log.Add($"Transition: covered={transition?.IsScreenCovered}, transitioning={transition?.IsTransitioning}");

        return string.Join("\n", log);
    }
}
