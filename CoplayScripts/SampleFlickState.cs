using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>Logs enemy visibility/position + sweep state every ~0.5s for 40s.</summary>
public static class SampleFlickState
{
    private static EditorApplication.CallbackFunction _tick;
    private static double _next;
    private static double _end;

    public static void Execute()
    {
        var enemy  = Object.FindObjectOfType<FlickEnemy>(true);
        var visual = Object.FindObjectOfType<FlickTargetVisual>(true);
        if (enemy == null) { Debug.LogError("[Sample] no enemy"); return; }
        var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;

        if (_tick != null) EditorApplication.update -= _tick;
        _next = 0; _end = EditorApplication.timeSinceStartup + 40;
        _tick = () =>
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _end)
            { EditorApplication.update -= _tick; _tick = null; return; }
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;

            double vt = GameClock.Instance != null ? GameClock.Instance.GameTime : -1;
            Debug.Log($"[Sample] vt={vt:F2} enemyAlive={enemy.IsAlive} " +
                      $"visualRendererOn={(sr != null && sr.enabled)} pos={enemy.transform.position}");
        };
        EditorApplication.update += _tick;
        Debug.Log("[Sample] sampling started");
    }
}
