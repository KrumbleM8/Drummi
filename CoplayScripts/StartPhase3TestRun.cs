using System.Text;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PLAY-MODE ONLY: swaps the runtime DungeonRunner's floors list to the Phase 3
/// test floor, wires a DungeonFloorTestDriver (auto-tap orchestrator), and starts
/// a run via GameManager.StartDungeonRun().
/// Runtime-instance change only - reverts automatically when play mode stops.
/// Use ExecuteNoTap as the entry point to skip the tap driver (manual testing).
/// </summary>
public static class StartPhase3TestRun
{
    public static string Execute()
    {
        return Run(true);
    }

    public static string ExecuteNoTap()
    {
        return Run(false);
    }

    private static string Run(bool autoTap)
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";

        var sb = new StringBuilder();
        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "FAIL: DungeonRunner not found.";

        var floor = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(
            "Assets/ScriptableObjects/Phase3Test/Floor_Phase3Test.asset");
        if (floor == null) return "FAIL: Floor_Phase3Test asset not found - run CreatePhase3TestAssets first.";

        var so = new SerializedObject(runner);
        var floorsProp = so.FindProperty("floors");
        floorsProp.ClearArray();
        floorsProp.InsertArrayElementAtIndex(0);
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = floor;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("Floors list replaced with '" + floor.FloorName + "' (runtime only).");

        if (autoTap)
        {
            var fc    = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
            var cr    = Object.FindFirstObjectByType<CallAndResponseController>(FindObjectsInactive.Include);
            var input = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
            var beat  = Object.FindFirstObjectByType<DungeonBeatManager>(FindObjectsInactive.Include);
            var met   = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
            var fcRun = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
            if (fc == null || cr == null || input == null || beat == null || met == null)
                return sb.ToString() + "FAIL: missing scene systems for driver wiring.";

            var existing = Object.FindFirstObjectByType<DungeonFloorTestDriver>();
            if (existing != null) Object.Destroy(existing.gameObject);

            var host = new GameObject("Phase3FloorTestDriver");
            var drv  = host.AddComponent<DungeonFloorTestDriver>();
            drv.flashCardController     = fc;
            drv.callResponseController  = cr;
            drv.inputReader             = input;
            drv.beatManager             = beat;
            drv.metronome               = met;
            drv.flashCardRunner         = fcRun;
            sb.AppendLine("DungeonFloorTestDriver wired (flashCardRunner=" +
                          (fcRun != null ? "scene instance" : "MISSING") + ").");
        }
        else
        {
            sb.AppendLine("No tap driver created (manual mode).");
        }

        if (GameManager.instance == null) return sb.ToString() + "FAIL: GameManager.instance is null.";
        GameManager.instance.StartDungeonRun();
        sb.AppendLine("StartDungeonRun() called.");
        return sb.ToString();
    }
}
