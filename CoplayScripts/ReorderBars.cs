using System.Text;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.UI;

public static class ReorderBars
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        BarsMenuController menu = null;
        foreach (var m in Resources.FindObjectsOfTypeAll<BarsMenuController>())
        {
            if (!EditorUtility.IsPersistent(m)) { menu = m; break; }
        }
        if (menu == null) return "BarsMenuController not found";

        RectTransform Find(string name)
        {
            var t = menu.transform.Find(name);
            return t as RectTransform;
        }

        var bongo = Find("Bar_Bongo");
        var flash = Find("Bar_FlashCards");
        var dungeon = Find("Bar_Dungeon");
        var garden = Find("Bar_Garden");
        if (bongo == null || flash == null || dungeon == null || garden == null)
            return "Missing bar(s)";

        // Anchor slots, top to bottom.
        var slots = new (Vector2 min, Vector2 max)[]
        {
            (new Vector2(0f, 0.74f), new Vector2(1f, 0.89f)),
            (new Vector2(0f, 0.53f), new Vector2(1f, 0.68f)),
            (new Vector2(0f, 0.32f), new Vector2(1f, 0.47f)),
            (new Vector2(0f, 0.11f), new Vector2(1f, 0.26f)),
        };

        var order = new[] { bongo, flash, dungeon, garden };
        for (int i = 0; i < order.Length; i++)
        {
            Undo.RecordObject(order[i], "Reorder bars");
            order[i].anchorMin = slots[i].min;
            order[i].anchorMax = slots[i].max;
            order[i].anchoredPosition = Vector2.zero;
            order[i].SetSiblingIndex(i);
            EditorUtility.SetDirty(order[i]);
        }

        // Reorder the serialized bars list to match visual top-to-bottom order.
        var so = new SerializedObject(menu);
        var barsProp = so.FindProperty("bars");
        barsProp.arraySize = 4;
        for (int i = 0; i < order.Length; i++)
            barsProp.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(menu);

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        sb.AppendLine("New order:");
        for (int i = 0; i < order.Length; i++)
            sb.AppendLine($"  [{i}] {order[i].name} anchorY={order[i].anchorMin.y}-{order[i].anchorMax.y} sibling={order[i].GetSiblingIndex()}");
        return sb.ToString();
    }
}
