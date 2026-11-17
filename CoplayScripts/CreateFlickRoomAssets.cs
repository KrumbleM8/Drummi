using UnityEditor;
using UnityEngine;

public static class CreateFlickRoomAssets
{
    private const string RoomPath      = "Assets/ScriptableObjects/Rooms/FlickRoom.asset";
    private const string FloorPath     = "Assets/ScriptableObjects/Floors/FloorDebug_Flick.asset";
    private const string StandardRoomPath = "Assets/ScriptableObjects/Rooms/StandardRoom.asset";

    public static void Execute()
    {
        // ── FlickRoom ────────────────────────────────────────────────────────
        var room = ScriptableObject.CreateInstance<RoomDefinition>();
        var so = new SerializedObject(room);
        so.FindProperty("roomName").stringValue                = "Flick Room";
        so.FindProperty("roomType").enumValueIndex             = (int)RoomType.Flick;
        so.FindProperty("inputLayout").enumValueIndex          = (int)InputLayoutType.Flick;
        so.FindProperty("roomLengthBars").intValue             = 8;
        so.FindProperty("flickSurvivorAttackUnits").intValue   = 1;
        // Remaining flick fields stay at sentinel defaults (core scene values apply).
        // C&R phrase lengths are inert for this room type; clear the field default for clarity.
        so.FindProperty("callResponsePhraseLengths").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(room, RoomPath);

        // ── FloorDebug_Flick: Combat → Flick → Combat ────────────────────────
        var standard = AssetDatabase.LoadAssetAtPath<RoomDefinition>(StandardRoomPath);
        if (standard == null) { Debug.LogError("[CreateFlickAssets] StandardRoom.asset not found"); return; }

        var floor = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        var fso = new SerializedObject(floor);
        fso.FindProperty("floorName").stringValue     = "Flick Debug Floor";
        fso.FindProperty("floorIndex").intValue       = 99;
        fso.FindProperty("bgmTrackIndex").intValue    = 1;
        fso.FindProperty("bpm").intValue              = 105;
        var rooms = fso.FindProperty("rooms");
        rooms.arraySize = 3;
        rooms.GetArrayElementAtIndex(0).objectReferenceValue = standard;
        rooms.GetArrayElementAtIndex(1).objectReferenceValue = room;
        rooms.GetArrayElementAtIndex(2).objectReferenceValue = standard;
        fso.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(floor, FloorPath);

        AssetDatabase.SaveAssets();
        Debug.Log($"[CreateFlickAssets] Created {RoomPath} and {FloorPath} (Combat → Flick → Combat)");
    }
}
