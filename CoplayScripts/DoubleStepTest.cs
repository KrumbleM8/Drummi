using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: two Prev steps in the same frame — the second must DOKill and retarget the first's in-flight tween.</summary>
public static class DoubleStepTest
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var c = Object.FindAnyObjectByType<FlashCardsMenuController>();
        if (c == null) return "ERROR: controller not found";
        c.Prev();
        c.Prev();
        return "Invoked Prev twice in one frame";
    }
}
