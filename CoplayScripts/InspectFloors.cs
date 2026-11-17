using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectFloors
{
    public static string Execute()
    {
        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "ERROR: DungeonRunner not found.";

        var so = new SerializedObject(runner);
        var floorsProp = so.FindProperty("floors");
        var sb = new StringBuilder();
        sb.AppendLine($"floors ({floorsProp.arraySize}):");
        for (int i = 0; i < floorsProp.arraySize; i++)
        {
            var f = floorsProp.GetArrayElementAtIndex(i).objectReferenceValue as DungeonFloorDefinition;
            sb.AppendLine(f == null
                ? $"  [{i}] <null>"
                : $"  [{i}] {f.name} — '{f.FloorName}', tutorial={f.IsTutorialFloor}, bgm={f.BgmTrackIndex}, bpm={f.Bpm}, rooms={f.Rooms.Count}");
        }

        sb.AppendLine("All DungeonFloorDefinition assets:");
        foreach (var guid in AssetDatabase.FindAssets("t:DungeonFloorDefinition"))
            sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(guid));
        return sb.ToString();
    }
}
