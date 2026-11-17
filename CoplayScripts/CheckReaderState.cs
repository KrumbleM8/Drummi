using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class CheckReaderState
{
    public static void Execute()
    {
        var reader = Object.FindFirstObjectByType<FlickInputReader>(FindObjectsInactive.Include);
        if (reader == null) { Debug.LogError("[DiagReader] No FlickInputReader in scene."); return; }
        Debug.Log($"[DiagReader] EncounterActive={reader.EncounterActive} " +
                  $"isActiveAndEnabled={reader.isActiveAndEnabled} " +
                  $"goActive={reader.gameObject.activeInHierarchy} " +
                  $"clock={(GameClock.Instance != null ? GameClock.Instance.IsPaused.ToString() : "NULL")}");
    }
}
