using System.Text;
using KrumbleHut.Drummi.Dungeons;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PLAY-MODE ONLY: builds a runtime-only floor containing StandardRoom (normal combat)
/// and starts a run — regression smoke for Phase 1.1 (no tap injection; the changed
/// coyote branch requires CoyoteWindowStartOverride, which combat never sets).
/// </summary>
public static class StartCombatDebugRun
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        var sb = new StringBuilder();

        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "FAIL: DungeonRunner not found.";

        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            "Assets/ScriptableObjects/Rooms/StandardRoom.asset");
        if (room == null) return "FAIL: StandardRoom.asset not found.";

        var floor   = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        var floorSo = new SerializedObject(floor);
        floorSo.FindProperty("floorName").stringValue  = "Combat Debug Floor (runtime)";
        floorSo.FindProperty("bgmTrackIndex").intValue = 1;
        floorSo.FindProperty("bpm").intValue           = 105;
        var roomsProp = floorSo.FindProperty("rooms");
        roomsProp.ClearArray();
        roomsProp.InsertArrayElementAtIndex(0);
        roomsProp.GetArrayElementAtIndex(0).objectReferenceValue = room;
        floorSo.ApplyModifiedPropertiesWithoutUndo();

        var runnerSo   = new SerializedObject(runner);
        var floorsProp = runnerSo.FindProperty("floors");
        floorsProp.ClearArray();
        floorsProp.InsertArrayElementAtIndex(0);
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = floor;
        runnerSo.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("Floors list replaced with runtime combat debug floor.");

        var fcHarness = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (fcHarness != null) { fcHarness.enabled = false; sb.AppendLine("FlashCardTestRunner disabled."); }

        var reader = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
        var beatMgr = Object.FindFirstObjectByType<DungeonBeatManager>(FindObjectsInactive.Include);
        var metro = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
        if (reader == null || beatMgr == null || metro == null)
            return sb + "FAIL: input reader / beat manager / metronome not found.";

        // Create inactive so no Update runs before refs are wired.
        var host = new GameObject("CombatTestRunner (runtime)");
        host.SetActive(false);
        var harness = host.AddComponent<CombatTestRunner>();
        harness.inputReader = reader;
        harness.beatManager = beatMgr;
        harness.metronome   = metro;
        harness.runScenarios = true;
        host.SetActive(true);
        sb.AppendLine("CombatTestRunner added, wired, then enabled.");

        if (GameManager.instance == null) return sb + "FAIL: GameManager.instance is null.";
        GameManager.instance.StartDungeonRun();
        sb.AppendLine("StartDungeonRun() called.");
        return sb.ToString();
    }
}
