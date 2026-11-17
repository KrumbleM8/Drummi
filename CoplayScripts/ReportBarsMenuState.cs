using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public static class ReportBarsMenuState
{
    public static string Execute()
    {
        var menu = Object.FindFirstObjectByType<BarsMenuController>(FindObjectsInactive.Include);
        if (menu == null) return "ERROR: no BarsMenuController";
        var canvas = menu.GetComponentInParent<Canvas>(true);
        string path = menu.transform.name;
        for (var t = menu.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
        string bars = "";
        foreach (Transform child in menu.transform)
            bars += $"{child.name} active={child.gameObject.activeSelf} anchoredPos={((RectTransform)child).anchoredPosition}\n";
        return $"path={path}\ncanvas={(canvas != null ? canvas.name : "none")}\nmenuActive={menu.gameObject.activeInHierarchy}\n{bars}";
    }
}
