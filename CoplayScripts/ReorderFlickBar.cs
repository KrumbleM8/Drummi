using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public static class ReorderFlickBar
{
    // Moves Bar_Flick between Bar_FlashCards and Bar_Dungeon (index 2 of 5,
    // top to bottom), re-applies the even anchor spacing, and gives Bar_Flick
    // a coral color to stand apart from Dungeon's purple.
    public static string Execute()
    {
        var menu = Object.FindFirstObjectByType<BarsMenuController>(FindObjectsInactive.Include);
        if (menu == null) return "ERROR: no BarsMenuController in open scene";

        var menuSo = new SerializedObject(menu);
        var barsProp = menuSo.FindProperty("bars");
        if (barsProp.arraySize != 5) return $"ERROR: expected 5 bars, found {barsProp.arraySize}";

        // Current order: Bongo, FlashCards, Dungeon, Garden, Flick → move idx 4 → 2.
        var flickRt = (RectTransform)barsProp.GetArrayElementAtIndex(4).objectReferenceValue;
        if (flickRt == null || flickRt.name != "Bar_Flick")
            return $"ERROR: expected Bar_Flick at index 4, found {(flickRt ? flickRt.name : "null")}";
        barsProp.MoveArrayElement(4, 2);
        menuSo.ApplyModifiedProperties();

        // Re-space top to bottom (same rhythm as before: top 0.89, h 0.12, gap 0.045)
        // and match sibling order to list order for a tidy hierarchy.
        const float top = 0.89f, h = 0.12f, gap = 0.045f;
        string log = "";
        for (int i = 0; i < 5; i++)
        {
            var rt = (RectTransform)barsProp.GetArrayElementAtIndex(i).objectReferenceValue;
            float yMax = top - i * (h + gap);
            rt.anchorMin = new Vector2(0f, yMax - h);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.anchoredPosition = Vector2.zero;
            rt.SetSiblingIndex(i);
            log += $"{rt.gameObject.name}: y [{yMax - h:F3},{yMax:F3}]\n";
        }

        // Coral — distinct from Bongo orange, FlashCards blue, Dungeon purple, Garden green.
        var flickImage = flickRt.GetComponent<Image>();
        flickImage.color = new Color(0.910f, 0.443f, 0.427f, 1f); // #E8716D

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        return $"Reordered + recolored.\n{log}Saved scene '{menu.gameObject.scene.name}'.";
    }
}
