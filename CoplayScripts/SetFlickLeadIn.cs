using UnityEditor;
using UnityEngine;

public static class SetFlickLeadIn
{
    public static void Execute()
    {
        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>("Assets/ScriptableObjects/Rooms/FlickRoom.asset");
        var so = new SerializedObject(room);
        so.FindProperty("flickLeadInBars").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetFlickLeadIn] FlickRoom.flickLeadInBars = 1");
    }
}
