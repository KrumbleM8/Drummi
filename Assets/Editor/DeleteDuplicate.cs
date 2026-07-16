using UnityEditor;

public class DeleteDuplicate
{
    public static void Execute()
    {
        bool deleted = AssetDatabase.DeleteAsset("Assets/Dungeon.unity");
        UnityEngine.Debug.Log($"[DeleteDuplicate] Assets/Dungeon.unity deleted: {deleted}");
    }
}
