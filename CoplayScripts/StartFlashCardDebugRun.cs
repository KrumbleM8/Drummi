using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PLAY-MODE ONLY: swaps the runtime DungeonRunner's floors list to the Flash Card
/// debug floor and starts a run via GameManager.StartDungeonRun().
/// Runtime-instance change only — reverts automatically when play mode stops.
/// </summary>
public static class StartFlashCardDebugRun
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";

        var sb = new StringBuilder();
        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "FAIL: DungeonRunner not found.";

        var debugFloor = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(
            "Assets/ScriptableObjects/Floors/FloorDebug_FlashCard.asset");
        if (debugFloor == null) return "FAIL: debug floor asset not found.";

        var so = new SerializedObject(runner);
        var floorsProp = so.FindProperty("floors");
        floorsProp.ClearArray();
        floorsProp.InsertArrayElementAtIndex(0);
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = debugFloor;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine($"Floors list replaced with '{debugFloor.FloorName}' (runtime only).");

        if (GameManager.instance == null) return sb + "FAIL: GameManager.instance is null.";
        GameManager.instance.StartDungeonRun();
        sb.AppendLine("StartDungeonRun() called.");
        return sb.ToString();
    }
}
