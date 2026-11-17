using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using KrumbleHut.Drummi.UI;

public static class AddFlickBarToHome
{
    // Adds Bar_Flick to the New_Home bars menu:
    //   1. duplicate Bar_Dungeon (same look/components),
    //   2. relabel "FLICK", rewire onClick to HomeFlowController.GoToFlickScene,
    //   3. append to BarsMenuController.bars,
    //   4. re-space all 5 bars evenly (h=0.12, gap=0.045, top edge 0.89).
    public static string Execute()
    {
        var menu = Object.FindFirstObjectByType<BarsMenuController>(FindObjectsInactive.Include);
        if (menu == null) return "ERROR: no BarsMenuController in open scene";

        // Idempotency: bail if Bar_Flick already exists.
        foreach (Transform child in menu.transform)
            if (child.name == "Bar_Flick") return "ERROR: Bar_Flick already exists";

        var menuSo = new SerializedObject(menu);
        var barsProp = menuSo.FindProperty("bars");
        if (barsProp.arraySize != 4) return $"ERROR: expected 4 bars, found {barsProp.arraySize}";

        // Source = Bar_Dungeon (index 2 top-to-bottom).
        Transform dungeonBar = null;
        foreach (Transform child in menu.transform)
            if (child.name == "Bar_Dungeon") { dungeonBar = child; break; }
        if (dungeonBar == null) return "ERROR: Bar_Dungeon not found under BarsMenu";

        var flickGo = Object.Instantiate(dungeonBar.gameObject, menu.transform);
        flickGo.name = "Bar_Flick";
        flickGo.transform.SetAsLastSibling();
        Undo.RegisterCreatedObjectUndo(flickGo, "Add Bar_Flick");

        // Label.
        var label = flickGo.GetComponentInChildren<TMP_Text>(true);
        if (label == null) { Object.DestroyImmediate(flickGo); return "ERROR: no TMP_Text under duplicated bar"; }
        label.text = "FLICK";

        // Rewire the single persistent onClick call to GoToFlickScene.
        var button = flickGo.GetComponent<Button>();
        var btnSo = new SerializedObject(button);
        var calls = btnSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls.arraySize != 1) { Object.DestroyImmediate(flickGo); return $"ERROR: expected 1 persistent call, found {calls.arraySize}"; }
        calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue = "GoToFlickScene";
        btnSo.ApplyModifiedProperties();

        // Append to bars list.
        barsProp.arraySize = 5;
        barsProp.GetArrayElementAtIndex(4).objectReferenceValue = flickGo.GetComponent<RectTransform>();
        menuSo.ApplyModifiedProperties();

        // Re-space all 5 bars: top edge 0.89, height 0.12, gap 0.045 (was h .15/g .06).
        const float top = 0.89f, h = 0.12f, gap = 0.045f;
        var log = "";
        for (int i = 0; i < 5; i++)
        {
            var rt = (RectTransform)barsProp.GetArrayElementAtIndex(i).objectReferenceValue;
            float yMax = top - i * (h + gap);
            rt.anchorMin = new Vector2(0f, yMax - h);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.anchoredPosition = Vector2.zero;
            log += $"{rt.gameObject.name}: y [{yMax - h:F3},{yMax:F3}]\n";
        }

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        return $"Bar_Flick added, onClick -> GoToFlickScene, bars list size 5.\n{log}Saved scene '{menu.gameObject.scene.name}'.";
    }
}
