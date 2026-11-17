using UnityEngine;

/// <summary>Lists active renderers whose bounds sit near the globe focus region.</summary>
public static class ListRenderersNearFocus
{
    public static string Execute()
    {
        string result = "";
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Vector3 c = r.bounds.center;
            if (Mathf.Abs(c.x) < 2f && c.y > 8f && c.y < 14f && c.z > -7f && c.z < 3f)
            {
                string path = r.transform.name;
                Transform t = r.transform;
                while (t.parent != null)
                {
                    t = t.parent;
                    path = t.name + "/" + path;
                }
                result += path + " c=(" + c.x.ToString("F1") + "," + c.y.ToString("F1") + "," + c.z.ToString("F1") + ") | ";
            }
        }
        if (result == "") result = "none";
        return result;
    }
}
