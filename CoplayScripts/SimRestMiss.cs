using UnityEngine;
using System.Reflection;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PLAY-MODE TEST HARNESS: during a perform bar, finds the LAST rest slot and
/// simulates an out-of-window tap on it (HandlePlayerMissTap) so it should
/// persist while untouched rests auto-destroy at their beat ends.
/// </summary>
public static class SimRestMiss
{
    public static void Execute()
    {
        var view = Object.FindObjectsByType<FlashCardView>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        var t = typeof(FlashCardView);
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

        var state     = t.GetField("_state", F).GetValue(view).ToString();
        var templates = (SquareTemplate[])t.GetField("_templates", F).GetValue(view);
        int restSlot = -1;
        for (int i = templates.Length - 1; i >= 0; i--)
            if (templates[i] == SquareTemplate.Rest) { restSlot = i; break; }

        if (restSlot < 0) { Debug.Log($"[SimRestMiss] state={state} — no rest slot on this card"); return; }
        t.GetMethod("HandlePlayerMissTap", F).Invoke(view, new object[] { restSlot });
        Debug.Log($"[SimRestMiss] state={state} missTapped restSlot={restSlot} " +
                  $"templates=[{string.Join(",", System.Array.ConvertAll(templates, x => x.ToLogChar()))}]");
    }
}
