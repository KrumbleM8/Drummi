using System.Text;
using KrumbleHut.Drummi.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PHASE 4.1b — adds a "FlashCards" bubble to the Home scene by duplicating the
/// Dungeon bubble, relabeling it, and rewiring onPress to
/// UIMenuManager.GoToFlashCardsScene. Idempotent (removes an existing FlashCards
/// bubble first). Saves Home and reopens the Dungeon scene.
/// </summary>
public static class AddFlashCardsHomeBubble
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);
        sb.AppendLine("Opened Home scene.");

        var menuManager = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        if (menuManager == null) return "FAIL: no UIMenuManager in Home scene.";

        // Bubbles may be inactive or nested — search all BubbleButtons by GO name.
        GameObject source   = null;
        GameObject existing = null;
        foreach (var b in Object.FindObjectsByType<BubbleButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.gameObject.name == "Dungeon")    source   = b.gameObject;
            if (b.gameObject.name == "FlashCards") existing = b.gameObject;
        }
        if (source == null)
            return "FAIL: Dungeon bubble not found (BubbleButton scan).";

        // Idempotency: remove a previous copy.
        if (existing != null) { Object.DestroyImmediate(existing); sb.AppendLine("Old FlashCards bubble removed."); }

        var copy = Object.Instantiate(source, source.transform.parent);
        copy.name = "FlashCards";

        // Offset below/right of the source bubble so it doesn't overlap.
        var rt = copy.GetComponent<RectTransform>();
        var srcRt = source.GetComponent<RectTransform>();
        rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(0f, -190f);

        // Relabel any text children.
        foreach (var tmp in copy.GetComponentsInChildren<TMP_Text>(true))
            tmp.text = "Flash Cards";

        // Rewire onPress: wipe inherited persistent calls, add GoToFlashCardsScene.
        var bubble = copy.GetComponent<BubbleButton>();
        var so = new SerializedObject(bubble);
        var callsProp = so.FindProperty("onPress.m_PersistentCalls.m_Calls");
        callsProp.ClearArray();
        so.FindProperty("startUnlocked").boolValue = true; // free play — never locked
        so.FindProperty("unlockKey").stringValue   = string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();

        var onPressField = typeof(BubbleButton).GetField("onPress",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var onPress = (UnityEvent)onPressField.GetValue(bubble);
        UnityEventTools.AddPersistentListener(onPress, menuManager.GoToFlashCardsScene);
        EditorUtility.SetDirty(bubble);
        sb.AppendLine("Bubble duplicated, relabeled, rewired to GoToFlashCardsScene.");

        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine($"SaveOpenScenes: {saved}");
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        sb.AppendLine("Reopened Dungeon scene.");
        return sb.ToString();
    }
}
