using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public static class RepositionFlickSweepAnchors
{
    // Pulls the flick sweep anchors inward so the enemy visual stays fully
    // onscreen: |x| = cameraHalfWidth - visualHalfWidth - margin.
    public static string Execute()
    {
        var sweep = Object.FindFirstObjectByType<FlickTargetSweep>(FindObjectsInactive.Include);
        if (sweep == null) return "ERROR: no FlickTargetSweep in open scene";

        var so = new SerializedObject(sweep);
        var left  = so.FindProperty("leftAnchor").objectReferenceValue  as Transform;
        var right = so.FindProperty("rightAnchor").objectReferenceValue as Transform;
        if (left == null || right == null) return "ERROR: sweep anchors not assigned";

        var enemy = Object.FindFirstObjectByType<FlickEnemy>(FindObjectsInactive.Include);
        if (enemy == null) return "ERROR: no FlickEnemy in open scene";
        var enemySo = new SerializedObject(enemy);
        var visual = enemySo.FindProperty("visual").objectReferenceValue as FlickTargetVisual;
        if (visual == null) return "ERROR: FlickEnemy.visual not assigned";
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null) return "ERROR: no SpriteRenderer on TargetVisual";

        float visualHalfWidth = sr.bounds.extents.x;
        float camHalfWidth = 5f * (412f / 815f); // ortho size 5, portrait 412x815
        float margin = 0.1f;
        float newX = camHalfWidth - visualHalfWidth - margin;
        if (newX <= 0f) return $"ERROR: computed newX {newX} <= 0 (visual half-width {visualHalfWidth})";

        Vector3 lp = left.position;
        Vector3 rp = right.position;
        left.position  = new Vector3(-newX, lp.y, lp.z);
        right.position = new Vector3( newX, rp.y, rp.z);

        EditorSceneManager.MarkSceneDirty(sweep.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        return $"Scene '{sweep.gameObject.scene.name}': visualHalfWidth={visualHalfWidth:F3}, " +
               $"camHalfWidth={camHalfWidth:F3}, anchors {lp.x:F2}/{rp.x:F2} -> ±{newX:F3}. Saved.";
    }
}
