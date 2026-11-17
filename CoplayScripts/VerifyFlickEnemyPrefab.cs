using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.Dungeons;

public static class VerifyFlickEnemyPrefab
{
    public static string Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FlickEnemy.prefab");
        if (prefab == null) return "ERROR: prefab not found";

        var sweep = prefab.GetComponent<FlickTargetSweep>();
        var enemy = prefab.GetComponent<FlickEnemy>();
        var visual = prefab.GetComponentInChildren<FlickTargetVisual>(true);

        string Field(Object c, string prop)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p == null) return prop + "=?";
            return prop + "=" + (p.objectReferenceValue != null ? p.objectReferenceValue.name : "NULL");
        }

        return "Sweep: " + (sweep != null ? Field(sweep, "leftAnchor") + ", " + Field(sweep, "rightAnchor") + ", " + Field(sweep, "aimCamera") : "MISSING") +
               " | Enemy: " + (enemy != null ? Field(enemy, "visual") + ", " + Field(enemy, "barRoot") + ", " + Field(enemy, "fillPivot") : "MISSING") +
               " | Visual: " + (visual != null ? Field(visual, "beatManager") + ", " + Field(visual, "spriteRenderer") : "MISSING");
    }
}
