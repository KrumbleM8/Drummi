using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.Dungeons;

public static class InspectFlickBar
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no FlickTargetSweep";
        var visual = sweep.GetComponentInChildren<FlickTargetVisual>(true);
        var vsr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        var bar = sweep.transform.Find("EnemyHealthBar");

        var cam = Camera.main;
        string camInfo = cam != null ? $"ortho={cam.orthographic} size={cam.orthographicSize}" : "no main cam";

        return $"FlickTarget lossyScale={sweep.transform.lossyScale}, " +
               $"visual localScale={(visual != null ? visual.transform.localScale.ToString() : "?")}, " +
               $"visual world bounds={(vsr != null ? vsr.bounds.size.ToString() : "?")}, " +
               $"bar localPos={(bar != null ? bar.localPosition.ToString() : "?")}, " +
               $"bar localScale={(bar != null ? bar.localScale.ToString() : "?")}, " +
               $"cam: {camInfo}";
    }
}
