using System.Text;
using KrumbleHut.Drummi.Dungeons;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PLAY-MODE ONLY: builds a runtime-only floor containing the CR_Rush room, wires a
/// CallResponseTestRunner (runtime AddComponent — no scene changes), and starts a run.
/// Everything reverts when play mode stops.
/// </summary>
public static class StartCRDebugRun
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        var sb = new StringBuilder();

        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "FAIL: DungeonRunner not found.";

        var crRoom = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            "Assets/ScriptableObjects/Rooms/CR_Rush.asset");
        if (crRoom == null) return "FAIL: CR_Rush.asset not found.";

        // Runtime-only floor: CR room only, same track/bpm as Floor1.
        var floor   = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        var floorSo = new SerializedObject(floor);
        floorSo.FindProperty("floorName").stringValue   = "CR Debug Floor (runtime)";
        floorSo.FindProperty("bgmTrackIndex").intValue  = 1;
        floorSo.FindProperty("bpm").intValue            = 105;
        var roomsProp = floorSo.FindProperty("rooms");
        roomsProp.ClearArray();
        roomsProp.InsertArrayElementAtIndex(0);
        roomsProp.GetArrayElementAtIndex(0).objectReferenceValue = crRoom;
        floorSo.ApplyModifiedPropertiesWithoutUndo();

        var runnerSo   = new SerializedObject(runner);
        var floorsProp = runnerSo.FindProperty("floors");
        floorsProp.ClearArray();
        floorsProp.InsertArrayElementAtIndex(0);
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = floor;
        runnerSo.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("Floors list replaced with runtime CR debug floor.");

        // Disable the FlashCard harness so it cannot interfere, then add the C&R harness.
        var fcHarness = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (fcHarness != null) { fcHarness.enabled = false; sb.AppendLine("FlashCardTestRunner disabled."); }

        var car    = Object.FindFirstObjectByType<CallAndResponseController>(FindObjectsInactive.Include);
        var reader = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
        var metro  = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
        if (car == null || reader == null || metro == null)
            return sb + "FAIL: C&R controller / input reader / metronome not found.";

        // Create inactive so OnEnable (event subscription) runs only AFTER refs are wired.
        var host = new GameObject("CRTestRunner (runtime)");
        host.SetActive(false);
        var harness = host.AddComponent<CallResponseTestRunner>();
        harness.controller  = car;
        harness.inputReader = reader;
        harness.metronome   = metro;
        harness.runScenarios = true;
        host.SetActive(true);
        sb.AppendLine("CallResponseTestRunner added, wired, then enabled.");

        if (GameManager.instance == null) return sb + "FAIL: GameManager.instance is null.";
        GameManager.instance.StartDungeonRun();
        sb.AppendLine("StartDungeonRun() called.");
        return sb.ToString();
    }
}
