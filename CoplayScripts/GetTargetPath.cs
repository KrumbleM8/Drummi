using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class GetTargetPath
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR";
        string path = sweep.transform.name;
        for (var t = sweep.transform.parent; t != null; t = t.parent)
            path = t.name + "/" + path;
        return "/" + path;
    }
}
