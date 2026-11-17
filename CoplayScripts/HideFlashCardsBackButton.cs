using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Standalone FlashCards: deactivate the back-to-New_Home button GameObject in
/// FlashCardsMenu so the menu has no route into Drummi. Saves the scene.
/// </summary>
public static class HideFlashCardsBackButton
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu")
        {
            Debug.Log($"[HideBackBtn] ABORT: active scene is '{scene.name}', expected FlashCardsMenu.");
            return;
        }

        var controller = Object.FindFirstObjectByType<FlashCardsMenuController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.Log("[HideBackBtn] ABORT: no FlashCardsMenuController found in scene.");
            return;
        }

        var so = new SerializedObject(controller);
        var prop = so.FindProperty("backButton");
        if (prop == null || prop.objectReferenceValue == null)
        {
            Debug.Log("[HideBackBtn] ABORT: backButton field is null/unassigned.");
            return;
        }

        var btn = (UnityEngine.UI.Button)prop.objectReferenceValue;
        var go = btn.gameObject;
        string path = GetPath(go.transform);
        if (!go.activeSelf)
        {
            Debug.Log($"[HideBackBtn] Already inactive: {path}. Nothing to do.");
            return;
        }

        Undo.RecordObject(go, "Hide FlashCards back button");
        go.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[HideBackBtn] Deactivated '{path}'. SaveOpenScenes={saved}.");
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.parent == null ? t.name + "/" + p : t.name + "/" + p; }
        return p;
    }
}
