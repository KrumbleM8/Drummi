using UnityEngine;
using System.Reflection;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PLAY-MODE TEST HARNESS: during a perform bar, simulates confirmed hits on the
/// FlashCardView — slot 0 (Perfect) and slot 1 (Good, forced to EighthPair so the
/// first call chips and a second call destroys). Logs hit/destroy state.
/// </summary>
public static class SimulateFlashCardHits
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.LogError("[SimHits] Not in play mode"); return; }
        var views = Object.FindObjectsByType<FlashCardView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (views.Length == 0) { Debug.LogError("[SimHits] No FlashCardView"); return; }
        var view = views[0];
        var t = typeof(FlashCardView);
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

        Debug.Log($"[SimHits] state={t.GetField("_state", F).GetValue(view)}");

        // Force slot 1 to a 2-note template so we can exercise the chip path.
        var templates = (SquareTemplate[])t.GetField("_templates", F).GetValue(view);
        if (!((bool[])t.GetField("_destroyed", F).GetValue(view))[1])
            templates[1] = SquareTemplate.EighthPair;

        var hit = t.GetMethod("HandlePlayerHit", F);
        hit.Invoke(view, new object[] { 0, InputMatch.MatchQuality.Perfect });
        hit.Invoke(view, new object[] { 1, InputMatch.MatchQuality.Good });

        var hits = (int[])t.GetField("_hitCounts", F).GetValue(view);
        var dest = (bool[])t.GetField("_destroyed", F).GetValue(view);
        Debug.Log($"[SimHits] slot0 hits={hits[0]} destroyed={dest[0]} | slot1 hits={hits[1]} destroyed={dest[1]}");
    }
}
