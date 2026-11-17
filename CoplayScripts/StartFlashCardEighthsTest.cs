using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Phase 5d play-mode driver: enables the FlashCardTestRunner (play-mode-only,
/// reverts on stop) and starts a FlashCard encounter.
/// Entry points: Execute (full vocab, 5 cards) / ExecuteLegacy (Quarter|Rest, 4 cards).
/// </summary>
public static class StartFlashCardEighthsTest
{
    public static string Execute() => Start(
        FlashCardVocab.Quarter | FlashCardVocab.EighthPair |
        FlashCardVocab.DownbeatEighth | FlashCardVocab.OffbeatEighth |
        FlashCardVocab.Rest, 5);

    public static string ExecuteLegacy() => Start(
        FlashCardVocab.Quarter | FlashCardVocab.Rest, 4);

    private static string Start(FlashCardVocab vocab, int cards)
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";

        var ctrl   = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        var runner = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (ctrl == null || runner == null) return "FAIL: controller or runner missing";
        if (GameClock.Instance == null)     return "FAIL: GameClock missing";

        runner.enabled = true; // play-mode only; not saved

        // The injected harness bypasses RoomController, which normally initializes
        // health at room entry. HP=0 makes the first penalty trigger the game-over
        // flow mid-encounter, so reset health like a real room start would.
        var health = Object.FindFirstObjectByType<DungeonHealth>(FindObjectsInactive.Include);
        if (health != null) health.ResetHealth();

        double start = GameClock.Instance.GameTime + 0.5;
        ctrl.StartEncounter(start, cards, new Vector2Int(3, 6), vocab);
        return $"OK: encounter started vocab={vocab} cards={cards} start={start:F3} time={GameClock.Instance.GameTime:F3}";
    }
}
