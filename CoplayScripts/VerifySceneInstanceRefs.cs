using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.Dungeons;

public static class VerifySceneInstanceRefs
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no scene instance";
        var visual = sweep.GetComponentInChildren<FlickTargetVisual>(true);

        string Field(Object c, string prop)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            return prop + "=" + (p != null && p.objectReferenceValue != null ? p.objectReferenceValue.name : "NULL");
        }

        return "SceneInstance Sweep: " + Field(sweep, "leftAnchor") + ", " + Field(sweep, "rightAnchor") + ", " +
               Field(sweep, "aimCamera") + " | Visual: " + Field(visual, "beatManager");
    }
}
