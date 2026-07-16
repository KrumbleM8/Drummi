using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor script for Assets/Scenes/Dungeon.unity.
/// Adds a static transparent HitZone under OverlayCanvas/SingleDrumRig and wires it
/// to DungeonInputLayoutController.singleDrumPad.
///
/// The zone copies SingleDrumPad's rect but is a SIBLING of it — SingleDrumPad has a
/// BounceOnBeat scale animation, and parenting the zone under it would make the
/// touchable area pulse with the beat. Idempotent: re-running reuses an existing
/// HitZone and re-wires it.
/// </summary>
public class SetupSingleDrumTouch
{
    [MenuItem("Tools/Drummi/Setup Single Drum Touch")]
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ── Locate SingleDrumRig and SingleDrumPad ─────────────────────────
        GameObject overlayCanvas = null;
        GameObject roguelikeLayer = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "OverlayCanvas")  overlayCanvas  = go;
            if (go.name == "RoguelikeLayer") roguelikeLayer = go;
        }
        if (overlayCanvas == null || roguelikeLayer == null)
        {
            Debug.LogError("[SetupSingleDrumTouch] OverlayCanvas or RoguelikeLayer not found.");
            return;
        }

        var singleDrumRig = overlayCanvas.transform.Find("SingleDrumRig");
        var singleDrumPad = singleDrumRig != null ? singleDrumRig.Find("SingleDrumPad") : null;
        if (singleDrumRig == null || singleDrumPad == null)
        {
            Debug.LogError("[SetupSingleDrumTouch] SingleDrumRig/SingleDrumPad not found.");
            return;
        }
        var padRT = singleDrumPad.GetComponent<RectTransform>();

        // ── Create (or reuse) HitZone as a sibling of SingleDrumPad ───────
        var existing = singleDrumRig.Find("HitZone");
        RectTransform hitZoneRT;
        if (existing != null)
        {
            hitZoneRT = existing.GetComponent<RectTransform>();
        }
        else
        {
            var hitZoneGO = new GameObject("HitZone");
            hitZoneRT = hitZoneGO.AddComponent<RectTransform>();
            hitZoneGO.transform.SetParent(singleDrumRig, false);
        }

        // Copy SingleDrumPad's rect so the touch area matches the drum visual at rest.
        hitZoneRT.anchorMin        = padRT.anchorMin;
        hitZoneRT.anchorMax        = padRT.anchorMax;
        hitZoneRT.pivot            = padRT.pivot;
        hitZoneRT.anchoredPosition = padRT.anchoredPosition;
        hitZoneRT.sizeDelta        = padRT.sizeDelta;
        hitZoneRT.localScale       = Vector3.one;

        // ── Wire DungeonInputLayoutController.singleDrumPad ────────────────
        var dilc = roguelikeLayer.GetComponent<KrumbleHut.Drummi.Dungeons.DungeonInputLayoutController>();
        if (dilc == null)
        {
            Debug.LogError("[SetupSingleDrumTouch] DungeonInputLayoutController not found on RoguelikeLayer.");
            return;
        }
        var so = new SerializedObject(dilc);
        so.FindProperty("singleDrumPad").objectReferenceValue = hitZoneRT;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Save ────────────────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupSingleDrumTouch] HitZone created and singleDrumPad wired. Scene saved.");
    }
}
