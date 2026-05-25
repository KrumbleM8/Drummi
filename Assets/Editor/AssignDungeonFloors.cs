using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AssignDungeonFloors
{
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        DungeonRunner runner = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            runner = root.GetComponentInChildren<DungeonRunner>(true);
            if (runner != null) break;
        }

        if (runner == null) { Debug.LogError("[AssignDungeonFloors] DungeonRunner not found"); return; }

        var f1 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor1.asset");
        var f2 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor2.asset");
        var f3 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor3.asset");

        if (f1 == null || f2 == null || f3 == null)
        {
            Debug.LogError("[AssignDungeonFloors] One or more floor assets not found");
            return;
        }

        var so = new SerializedObject(runner);
        var floorsProp = so.FindProperty("floors");
        floorsProp.ClearArray();
        floorsProp.arraySize = 3;
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = f1;
        floorsProp.GetArrayElementAtIndex(1).objectReferenceValue = f2;
        floorsProp.GetArrayElementAtIndex(2).objectReferenceValue = f3;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(runner);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[AssignDungeonFloors] Floors assigned: Floor1, Floor2, Floor3");
    }
}
