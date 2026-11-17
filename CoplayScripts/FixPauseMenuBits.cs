using KrumbleHut.Drummi.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-off follow-up to BuildPauseMenu: wire countInRoot on the overlay prefab,
// remove Pulse's old (inactive at edit time) PauseButton, and drop stale prefab
// overrides left on each scene's overlay instance by the prefab rebuilds.
public static class FixPauseMenuBits
{
    const string OverlayPath = "Assets/Prefabs/OverlayUtilCanvas.prefab";

    public static void Execute()
    {
        var root = PrefabUtility.LoadPrefabContents(OverlayPath);
        var ctrl = root.GetComponent<OverlayUIController>();
        var so = new SerializedObject(ctrl);
        var countIn = root.transform.Find("CountIn");
        so.FindProperty("countInRoot").objectReferenceValue = countIn != null ? countIn.gameObject : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[FixPauseMenu] countInRoot wired=" + (countIn != null));

        string active = EditorSceneManager.GetActiveScene().path;
        if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();

        foreach (var name in new[] { "Home", "Bongo", "Garden", "Pulse" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
            int removed = 0;
            if (name == "Pulse")
            {
                foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (b.name == "PauseButton") { Object.DestroyImmediate(b.gameObject); removed++; }
            }
            var overlay = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
            int before = PrefabUtility.GetPropertyModifications(overlay.gameObject).Length;
            PrefabUtility.RemoveUnusedOverrides(new[] { overlay.gameObject }, InteractionMode.AutomatedAction);
            int after = PrefabUtility.GetPropertyModifications(overlay.gameObject).Length;
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FixPauseMenu] {name}: pauseButtonsRemoved={removed} overrides {before}->{after} saved={saved}");
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        Debug.Log("[FixPauseMenu] DONE");
    }
}
