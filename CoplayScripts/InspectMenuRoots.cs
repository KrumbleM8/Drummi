using System.Text;
using UnityEngine;

/// <summary>Reports active state of the menu/gameplay root objects.</summary>
public static class InspectMenuRoots
{
    public static string Execute()
    {
        string[] names = { "Main Camera", "ParallaxFar", "ParallaxMid", "PlanetPivot",
                           "OverlayCanvas", "GameplayCamera", "FlashCardsCanvas", "FlashCardSystems" };
        var sb = new StringBuilder();
        foreach (string n in names)
        {
            // GameObject.Find misses inactive objects — fall back to a full sweep.
            GameObject go = GameObject.Find(n);
            if (go == null)
            {
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == n) { go = root; break; }
            }
            sb.Append(n).Append("=").Append(go == null ? "MISSING" : go.activeSelf.ToString()).Append("; ");
        }
        return sb.ToString();
    }
}
