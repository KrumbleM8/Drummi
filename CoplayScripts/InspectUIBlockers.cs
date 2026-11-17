using UnityEngine;
using UnityEngine.UI;

public static class InspectUIBlockers
{
    public static void Execute()
    {
        Dump(GameObject.Find("DungeonCanvas/GameplayUI"));
        var menu = GameObject.Find("MenuCanvas_Manager");
        if (menu != null)
        {
            foreach (var g in menu.GetComponentsInChildren<Graphic>(true))
            {
                if (g.raycastTarget && g.gameObject.activeInHierarchy && g.name.Contains("StartButton"))
                    Dump(g.gameObject);
            }
        }
    }

    private static void Dump(GameObject go)
    {
        if (go == null) { Debug.Log("[UIBlock] target not found"); return; }
        var rt = go.transform as RectTransform;
        string rect = rt != null
            ? $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} anchors=({rt.anchorMin},{rt.anchorMax}) worldRect={GetWorldRect(rt)}"
            : "no RectTransform";
        Debug.Log($"[UIBlock] '{Path(go.transform)}' active={go.activeInHierarchy} {rect}");
        foreach (var g in go.GetComponents<Graphic>())
            Debug.Log($"[UIBlock]   graphic {g.GetType().Name} raycastTarget={g.raycastTarget} color={g.color}");
        foreach (var cg in go.GetComponentsInParent<CanvasGroup>(true))
            Debug.Log($"[UIBlock]   parent CanvasGroup '{cg.name}' alpha={cg.alpha} blocksRaycasts={cg.blocksRaycasts} interactable={cg.interactable}");
        var sel = go.GetComponent<Selectable>();
        if (sel != null) Debug.Log($"[UIBlock]   selectable {sel.GetType().Name} interactable={sel.IsInteractable()}");
    }

    private static Rect GetWorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return new Rect(c[0], c[2] - c[0]);
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
