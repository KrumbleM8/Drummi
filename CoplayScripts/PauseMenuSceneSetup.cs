using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public static class PauseMenuSceneSetup
{
    private const string PrefabPath = "Assets/Prefabs/OverlayUtilCanvas.prefab";

    // ── Inspection ────────────────────────────────────────────────────────────

    public static void InspectNewHome()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/New_Home.unity");
        var sb = new StringBuilder();

        var curtain = Object.FindFirstObjectByType<CurtainController>(FindObjectsInactive.Include);
        if (curtain == null) sb.AppendLine("CurtainController: NOT FOUND");
        else
        {
            sb.AppendLine($"CurtainController on: {Path(curtain.transform)}");
            var so = new SerializedObject(curtain);
            var rectProp = so.FindProperty("curtainRect");
            var rect = rectProp.objectReferenceValue as RectTransform;
            sb.AppendLine($"  curtainRect: {(rect != null ? Path(rect) : "null")}");
        }

        var flow = Object.FindFirstObjectByType<HomeFlowController>(FindObjectsInactive.Include);
        sb.AppendLine(flow == null ? "HomeFlowController: NOT FOUND" : $"HomeFlowController on: {Path(flow.transform)}");

        Debug.Log("[PauseMenuSceneSetup] New_Home:\n" + sb);
    }

    public static void InspectArcade()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
        var sb = new StringBuilder();

        foreach (var name in new[] { "SettingsMenu", "SettingsIcon", "PausedText", "PauseHandler", "Metronome", "PlayerCanvas" })
        {
            var go = FindInScene(name);
            sb.AppendLine($"{name}: {(go != null ? Path(go.transform) : "NOT FOUND")}");
        }

        var ph = Object.FindFirstObjectByType<PauseHandler>(FindObjectsInactive.Include);
        if (ph != null)
        {
            var so = new SerializedObject(ph);
            sb.AppendLine($"PauseHandler.metronome: {so.FindProperty("metronome").objectReferenceValue}");
        }
        var sm = Object.FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        sb.AppendLine($"Old SettingsMenu component on: {(sm != null ? Path(sm.transform) : "NOT FOUND")}");

        Debug.Log("[PauseMenuSceneSetup] Arcade:\n" + sb);
    }

    public static void InspectFlashCards()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FlashCards.unity");
        var sb = new StringBuilder();

        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        sb.AppendLine($"GameManager on: {(gm != null ? Path(gm.transform) : "NOT FOUND")}, has PauseHandler: {(gm != null && gm.GetComponent<PauseHandler>() != null)}");

        foreach (var name in new[] { "PauseButton", "Metronome" })
        {
            var go = FindInScene(name);
            sb.AppendLine($"{name}: {(go != null ? Path(go.transform) : "NOT FOUND")}");
        }

        var pb = FindInScene("PauseButton");
        var btn = pb != null ? pb.GetComponent<Button>() : null;
        if (btn != null)
            for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
                sb.AppendLine($"  PauseButton listener {i}: {btn.onClick.GetPersistentTarget(i)} . {btn.onClick.GetPersistentMethodName(i)}");

        Debug.Log("[PauseMenuSceneSetup] FlashCards:\n" + sb);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public static void ApplyNewHome()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/New_Home.unity");

        var flow = Object.FindFirstObjectByType<HomeFlowController>(FindObjectsInactive.Include);
        var curtain = Object.FindFirstObjectByType<CurtainController>(FindObjectsInactive.Include);
        if (flow == null || curtain == null) { Debug.LogError("[PauseMenuSceneSetup] Missing HomeFlowController or CurtainController"); return; }

        var so = new SerializedObject(flow);
        so.FindProperty("curtainRoot").objectReferenceValue = curtain.gameObject;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(flow);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PauseMenuSceneSetup] New_Home: curtainRoot wired to " + Path(curtain.transform));
    }

    public static void ApplyArcade()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");

        // 1. Delete bespoke pause UI.
        var settingsMenuGO = FindInScene("SettingsMenu");
        var settingsIconGO = FindInScene("SettingsIcon");
        if (settingsMenuGO != null) Object.DestroyImmediate(settingsMenuGO);
        if (settingsIconGO != null) Object.DestroyImmediate(settingsIconGO);

        // 2. Remove old SettingsMenu component from PlayerCanvas.
        var oldSettings = Object.FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        if (oldSettings != null) Object.DestroyImmediate(oldSettings);

        // 3. Wire PauseHandler.metronome.
        var pauseHandler = Object.FindFirstObjectByType<PauseHandler>(FindObjectsInactive.Include);
        var metronome = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
        if (pauseHandler == null) { Debug.LogError("[PauseMenuSceneSetup] Arcade PauseHandler not found"); return; }
        if (metronome != null)
        {
            var phSo = new SerializedObject(pauseHandler);
            phSo.FindProperty("metronome").objectReferenceValue = metronome;
            phSo.ApplyModifiedProperties();
        }

        // 4. Add the shared overlay prefab and wire its pauseHandler.
        AddOverlayPrefab(pauseHandler);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PauseMenuSceneSetup] Arcade: old pause UI removed, OverlayUtilCanvas added and wired.");
    }

    public static void ApplyFlashCards()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FlashCards.unity");

        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null) { Debug.LogError("[PauseMenuSceneSetup] GameManager not found"); return; }

        // 1. Ensure PauseHandler on the GameManager GO with metronome wired.
        var pauseHandler = gm.GetComponent<PauseHandler>();
        if (pauseHandler == null) pauseHandler = gm.gameObject.AddComponent<PauseHandler>();
        var metronome = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
        if (metronome != null)
        {
            var phSo = new SerializedObject(pauseHandler);
            if (phSo.FindProperty("metronome").objectReferenceValue == null)
            {
                phSo.FindProperty("metronome").objectReferenceValue = metronome;
                phSo.ApplyModifiedProperties();
            }
        }

        // 2. Add the shared overlay prefab and wire its pauseHandler.
        var controller = AddOverlayPrefab(pauseHandler);

        // 3. Rewire PauseButton → ToggleSettings (was GameManager.TogglePause, which paused without UI).
        var pauseButtonGO = FindInScene("PauseButton");
        var btn = pauseButtonGO != null ? pauseButtonGO.GetComponent<Button>() : null;
        if (btn != null && controller != null)
        {
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEventTools.AddPersistentListener(btn.onClick, controller.ToggleSettings);
        }
        else Debug.LogWarning("[PauseMenuSceneSetup] FlashCards PauseButton not rewired (button or controller missing).");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PauseMenuSceneSetup] FlashCards: PauseHandler ensured, OverlayUtilCanvas added, PauseButton rewired.");
    }

    private static OverlayUIController AddOverlayPrefab(PauseHandler pauseHandler)
    {
        // Idempotent: reuse an existing instance if present.
        var controller = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[PauseMenuSceneSetup] Prefab not found: " + PrefabPath); return null; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            controller = instance.GetComponent<OverlayUIController>();
        }

        var so = new SerializedObject(controller);
        so.FindProperty("pauseHandler").objectReferenceValue = pauseHandler;
        so.ApplyModifiedProperties();
        return controller;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject FindInScene(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p + (t.gameObject.activeInHierarchy ? "" : " (inactive)");
    }
}
