using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public static class SetFlickEnemyHP
{
    public static string Execute()
    {
        var enemy = Object.FindObjectOfType<FlickEnemy>(true);
        if (enemy == null) return "ERROR: no FlickEnemy in scene";

        var so = new SerializedObject(enemy);
        var prop = so.FindProperty("maxHP");
        int old = prop.intValue;
        prop.intValue = 60;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        return $"OK — maxHP {old} → 60, saved={saved}";
    }
}
