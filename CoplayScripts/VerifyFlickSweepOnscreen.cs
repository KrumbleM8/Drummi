using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.Dungeons;

public static class VerifyFlickSweepOnscreen
{
    // Play-mode check: with the LIVE game camera (real game-view aspect), confirm
    // the enemy visual's full bounds stay inside the viewport at BOTH sweep
    // extremes (extremes = the anchor positions).
    public static string Execute()
    {
        var sweep = Object.FindFirstObjectByType<FlickTargetSweep>(FindObjectsInactive.Include);
        if (sweep == null) return "ERROR: no FlickTargetSweep";
        var so = new SerializedObject(sweep);
        var left  = so.FindProperty("leftAnchor").objectReferenceValue  as Transform;
        var right = so.FindProperty("rightAnchor").objectReferenceValue as Transform;

        var enemy = Object.FindFirstObjectByType<FlickEnemy>(FindObjectsInactive.Include);
        var enemySo = new SerializedObject(enemy);
        var visual = enemySo.FindProperty("visual").objectReferenceValue as FlickTargetVisual;
        var sr = visual.GetComponent<SpriteRenderer>();
        float halfW = sr.bounds.extents.x;
        float halfH = sr.bounds.extents.y;

        Camera cam = Camera.main;
        if (cam == null) return "ERROR: no main camera";

        string report = $"gameView aspect={cam.aspect:F4}, ortho={cam.orthographicSize}, visual half=({halfW:F2},{halfH:F2})\n";
        bool allOk = true;
        foreach (var (name, t) in new[] { ("LEFT", left), ("RIGHT", right) })
        {
            Vector3 p = t.position;
            Vector3 lo = cam.WorldToViewportPoint(new Vector3(p.x - halfW, p.y - halfH, p.z));
            Vector3 hi = cam.WorldToViewportPoint(new Vector3(p.x + halfW, p.y + halfH, p.z));
            bool ok = lo.x >= 0f && lo.y >= 0f && hi.x <= 1f && hi.y <= 1f;
            allOk &= ok;
            report += $"{name} anchor x={p.x:F2}: viewport x [{lo.x:F3},{hi.x:F3}] y [{lo.y:F3},{hi.y:F3}] -> {(ok ? "FULLY VISIBLE" : "CLIPPED")}\n";
        }
        report += allOk ? "PASS" : "FAIL";
        return report;
    }
}
