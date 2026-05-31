using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor-only setup script: creates the Curtain panel inside DecorativeOverlayCanvas
/// and wires up the CurtainController component.
/// </summary>
public static class SetupCurtain
{
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ── Find DecorativeOverlayCanvas ──────────────────────────────────────
        GameObject decorativeCanvas = null;
        GameObject drumPadTouchGO   = null;
        GameObject introMenu        = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "DecorativeOverlayCanvas") decorativeCanvas = root;
            if (root.name == "DrumPadTouch")            drumPadTouchGO   = root;

            // IntroMenu is a child of MenuCanvas_Manager
            if (root.name == "MenuCanvas_Manager")
            {
                var t = root.transform.Find("IntroMenu");
                if (t != null) introMenu = t.gameObject;
            }
        }

        if (decorativeCanvas == null)
        {
            Debug.LogError("[SetupCurtain] DecorativeOverlayCanvas not found.");
            return;
        }

        // ── Remove any pre-existing curtain ───────────────────────────────────
        var existing = decorativeCanvas.transform.Find("Curtain");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            Debug.Log("[SetupCurtain] Removed existing Curtain.");
        }

        // ── Create Curtain panel ──────────────────────────────────────────────
        var curtainGO = new GameObject("Curtain", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(curtainGO, "Create Curtain");
        curtainGO.transform.SetParent(decorativeCanvas.transform, false);

        // Full-screen stretch
        var rt = curtainGO.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // Dark solid colour
        var img = curtainGO.GetComponent<Image>();
        img.color           = new Color(0.06f, 0.06f, 0.08f, 1f);
        img.raycastTarget   = false; // curtain is visual-only; don't block pad touches

        // ── Add CurtainController ─────────────────────────────────────────────
        var controller = curtainGO.AddComponent<KrumbleHut.DrummiDungeons.UI.CurtainController>();

        // Use SerializedObject to set private serialized fields
        var so = new SerializedObject(controller);

        so.FindProperty("curtainRect").objectReferenceValue  = rt;

        if (drumPadTouchGO != null)
            so.FindProperty("drumPadTouch").objectReferenceValue = drumPadTouchGO.GetComponent<DrumPadTouch>();
        else
            Debug.LogWarning("[SetupCurtain] DrumPadTouch not found — assign it manually.");

        if (introMenu != null)
            so.FindProperty("introMenu").objectReferenceValue = introMenu;
        else
            Debug.LogWarning("[SetupCurtain] IntroMenu not found — assign it manually.");

        so.ApplyModifiedProperties();

        // ── Mark scene dirty ──────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[SetupCurtain] Curtain created and wired successfully.");
    }
}
