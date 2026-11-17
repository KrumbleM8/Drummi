using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EDIT-MODE ONLY: opens Home.unity, dumps the Bongos / Curtain / Frame / OverlayCanvas
/// subtrees (rects, sprites with asset paths, components), then reopens New_Home.
/// </summary>
public static class InspectHomeScene
{
    public static string Execute()
    {
        if (Application.isPlaying) return "FAIL: exit play mode first.";
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);

        foreach (var rootName in new[] { "OverlayCanvas", "MenuCanvas_Manager", "HomeController", "MainMenu" })
        {
            var go = GameObject.Find(rootName);
            if (go == null) { sb.AppendLine($"--- {rootName}: NOT FOUND (may be inactive)"); continue; }
            Dump(go.transform, 0, sb, maxDepth: 4);
            sb.AppendLine();
        }

        // Also find inactive roots.
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            sb.AppendLine($"ROOT: {root.name} (active={root.activeSelf})");

        EditorSceneManager.OpenScene("Assets/Scenes/New_Home.unity", OpenSceneMode.Single);
        return sb.ToString();
    }

    private static void Dump(Transform t, int depth, StringBuilder sb, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        var rt = t as RectTransform;
        string rect = rt != null
            ? $" aMin={rt.anchorMin} aMax={rt.anchorMax} piv={rt.pivot} pos={rt.anchoredPosition} size={rt.sizeDelta} scale={rt.localScale}"
            : "";
        sb.Append($"{indent}{t.name} (active={t.gameObject.activeSelf}){rect}");

        var img = t.GetComponent<Image>();
        if (img != null)
        {
            string spritePath = img.sprite != null ? AssetDatabase.GetAssetPath(img.sprite) + ":" + img.sprite.name : "none";
            sb.Append($" | Image sprite={spritePath} color={img.color} type={img.type} raycast={img.raycastTarget}");
        }
        foreach (var comp in t.GetComponents<MonoBehaviour>())
            if (comp != null) sb.Append($" | {comp.GetType().Name}");
        sb.AppendLine();

        for (int i = 0; i < t.childCount; i++)
            Dump(t.GetChild(i), depth + 1, sb, maxDepth);
    }
}
