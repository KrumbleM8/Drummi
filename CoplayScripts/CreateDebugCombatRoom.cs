using UnityEditor;
using UnityEngine;

public static class CreateDebugCombatRoom
{
    private const string CombatPath = "Assets/ScriptableObjects/Rooms/Debug_FlickCombat.asset";
    private const string FloorPath  = "Assets/ScriptableObjects/Floors/FloorDebug_Flick.asset";

    public static void Execute()
    {
        var combat = AssetDatabase.LoadAssetAtPath<RoomDefinition>(CombatPath);
        if (combat == null)
        {
            combat = ScriptableObject.CreateInstance<RoomDefinition>();
            var so = new SerializedObject(combat);
            so.FindProperty("roomName").stringValue    = "Debug Flick Combat";
            so.FindProperty("roomType").enumValueIndex = (int)RoomType.CombatEncounter;
            so.FindProperty("roomLengthBars").intValue = 2; // short — debug floor iteration speed
            so.FindProperty("callResponsePhraseLengths").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(combat, CombatPath);
        }

        var floor = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(FloorPath);
        var fso = new SerializedObject(floor);
        var rooms = fso.FindProperty("rooms");
        rooms.GetArrayElementAtIndex(0).objectReferenceValue = combat;
        rooms.GetArrayElementAtIndex(2).objectReferenceValue = combat;
        fso.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[CreateDebugCombatRoom] Debug_FlickCombat created; FloorDebug_Flick = Combat(2 bars) → Flick(8) → Combat(2)");
    }
}
