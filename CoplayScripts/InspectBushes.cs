using UnityEngine;
using UnityEditor;
using System.Text;

public static class InspectBushes
{
    private static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.parent == null ? t.name + "/" + path : t.name + "/" + path; }
        return path;
    }

    public static string Execute()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "HillPropA", "HillPropB", "HillPropC", "TreeA", "TreeB", "Clouds" })
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.AppendLine(name + ": NOT FOUND"); continue; }

            var sr = go.GetComponent<SpriteRenderer>();
            sb.AppendLine($"{name}: path={PathOf(go.transform)} children={go.transform.childCount} " +
                          $"localScale={go.transform.localScale} worldScale={go.transform.lossyScale} " +
                          $"sprite={(sr != null && sr.sprite != null ? sr.sprite.name : "none")} " +
                          $"spriteBounds={(sr != null && sr.sprite != null ? sr.sprite.bounds.size.ToString() : "n/a")} " +
                          $"components={string.Join(",", System.Array.ConvertAll(go.GetComponents<Component>(), c => c.GetType().Name))}");

            foreach (Transform child in go.transform)
            {
                var csr = child.GetComponent<SpriteRenderer>();
                sb.AppendLine($"  child {child.name}: sprite={(csr != null && csr.sprite != null ? csr.sprite.name : "none")} " +
                              $"components={string.Join(",", System.Array.ConvertAll(child.GetComponents<Component>(), c => c.GetType().Name))}");
            }
        }

        // Full list of PlanetPivot children for context
        var pivot = GameObject.Find("PlanetPivot");
        if (pivot != null)
        {
            sb.AppendLine("PlanetPivot children:");
            foreach (Transform child in pivot.transform)
            {
                var csr = child.GetComponent<SpriteRenderer>();
                sb.AppendLine($"  {child.name} (children={child.childCount}, sprite={(csr != null && csr.sprite != null ? csr.sprite.name : "none")})");
            }
        }
        return sb.ToString();
    }
}
