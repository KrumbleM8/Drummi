using UnityEngine;
using UnityEngine.UI;

public static class ClickFirstPick
{
    public static void Execute()
    {
        foreach (var b in Object.FindObjectsOfType<Button>())
        {
            if (b.isActiveAndEnabled && b.transform.root.name.Contains("Pick") ||
                (b.GetComponentInParent<Canvas>() != null && b.name.ToLower().Contains("pick")))
            {
                b.onClick.Invoke();
                Debug.Log($"[ClickPick] Invoked '{b.name}'");
                return;
            }
        }
        // Fallback: click the first active button whose hierarchy mentions the pick overlay.
        foreach (var b in Object.FindObjectsOfType<Button>())
        {
            Transform t = b.transform;
            while (t != null) { if (t.name.ToLower().Contains("pick")) { b.onClick.Invoke(); Debug.Log($"[ClickPick] Invoked '{b.name}' (fallback)"); return; } t = t.parent; }
        }
        Debug.LogError("[ClickPick] No pick button found");
    }
}
