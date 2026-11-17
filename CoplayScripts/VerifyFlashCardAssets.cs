using UnityEditor;
using UnityEngine;

public static class VerifyFlashCardAssets
{
    public static string Execute()
    {
        AssetDatabase.Refresh();

        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            "Assets/ScriptableObjects/Rooms/Debug_FlashCard.asset");
        var floor = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(
            "Assets/ScriptableObjects/Floors/FloorDebug_FlashCard.asset");

        if (room == null)  return "FAIL: Debug_FlashCard room asset did not load";
        if (floor == null) return "FAIL: FloorDebug_FlashCard floor asset did not load";

        string roomInfo = $"Room OK: name='{room.RoomName}' type={room.RoomType} layout={room.InputLayout} " +
                          $"bgmOverride={room.BgmTrackOverride} bpmOverride={room.BpmOverride}";
        string floorInfo = $"Floor OK: name='{floor.FloorName}' track={floor.BgmTrackIndex} bpm={floor.Bpm} " +
                           $"tutorial={floor.IsTutorialFloor} rooms={floor.Rooms.Count} " +
                           $"room0={(floor.Rooms.Count > 0 && floor.Rooms[0] != null ? floor.Rooms[0].RoomName : "NULL")}";
        return roomInfo + "\n" + floorInfo;
    }
}
