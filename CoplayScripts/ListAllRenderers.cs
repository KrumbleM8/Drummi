using UnityEngine;

/// <summary>Dumps every active renderer path + bounds center.</summary>
public static class ListAllRenderers
{
    public static string Execute()
    {
        string result = "";
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Vector3 c = r.bounds.center;
            string path = r.transform.name;
            Transform t = r.transform;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            result += path + " c=(" + c.x.ToString("F1") + "," + c.y.ToString("F1") + "," + c.z.ToString("F1") + ")\n";
        }
        return result;
    }
}
