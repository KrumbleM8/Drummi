using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class UnifyScreenTransition
{
    // Rewires GameManager.screenTransition + UIMenuManager.screenTransitioner to the
    // OverlayUtilCanvas prefab's TransitionScreen (the Bongo/Dungeon pattern), then
    // deletes the redundant scene-local TransitionPanel. Run per open scene.
    public static string Execute()
    {
        // Canonical transition: the one under the OverlayUtilCanvas prefab instance.
        ScreenTransition canonical = null;
        foreach (var st in Object.FindObjectsByType<ScreenTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (PrefabUtility.IsPartOfPrefabInstance(st) && st.gameObject.name == "TransitionScreen")
                canonical = st;
        if (canonical == null) return "ERROR: no prefab-instance TransitionScreen in open scene";

        var localPanel = GameObject.Find("TransitionPanel");
        if (localPanel == null)
        {
            // Inactive lookup fallback (panel may start disabled).
            foreach (var st in Object.FindObjectsByType<ScreenTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!PrefabUtility.IsPartOfPrefabInstance(st) && st.gameObject.name == "TransitionPanel")
                    localPanel = st.gameObject;
        }
        if (localPanel == null) return "ERROR: no scene-local TransitionPanel found";

        string log = "";
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        var menu = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        foreach (var (comp, field) in new (Object, string)[] { (gm, "screenTransition"), (menu, "screenTransitioner") })
        {
            if (comp == null) { log += $"WARN: no component for {field}\n"; continue; }
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            string was = prop.objectReferenceValue ? prop.objectReferenceValue.name : "null";
            prop.objectReferenceValue = canonical;
            so.ApplyModifiedProperties();
            log += $"{comp.GetType().Name}.{field}: {was} -> TransitionScreen (prefab)\n";
        }

        Object.DestroyImmediate(localPanel);
        log += "Deleted local TransitionPanel\n";

        var scene = canonical.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        return $"{log}Saved scene '{scene.name}'.";
    }
}
