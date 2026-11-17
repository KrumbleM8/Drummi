using System.Text;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.UI;

public static class InspectBars
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var menu = Resources.FindObjectsOfTypeAll<BarsMenuController>();
        foreach (var m in menu)
        {
            if (EditorUtility.IsPersistent(m)) continue;
            sb.AppendLine("BarsMenuController on: " + m.gameObject.name + " (active=" + m.gameObject.activeSelf + ")");
            var so = new SerializedObject(m);
            var barsProp = so.FindProperty("bars");
            for (int i = 0; i < barsProp.arraySize; i++)
            {
                var rt = barsProp.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
                if (rt == null) { sb.AppendLine($"  [{i}] null"); continue; }
                sb.AppendLine($"  [{i}] {rt.name} anchoredPos={rt.anchoredPosition} anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} siblingIndex={rt.GetSiblingIndex()}");
            }
            // Also list children of the menu root
            sb.AppendLine("  Children of root:");
            foreach (Transform child in m.transform)
                sb.AppendLine($"    {child.GetSiblingIndex()}: {child.name} y={((RectTransform)child).anchoredPosition.y}");
        }
        return sb.ToString();
    }
}
