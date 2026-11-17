using UnityEditor;
using UnityEngine;

public static class ResaveFlickRoom
{
    public static void Execute()
    {
        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>("Assets/ScriptableObjects/Rooms/FlickRoom.asset");
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        Debug.Log("[ResaveFlickRoom] Resaved (drops orphaned flickLeadInBars field)");
    }
}
