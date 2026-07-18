using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// One-shot editor script that wires the drum-pad input-state visuals in
/// Assets/Scenes/Bongo.unity (opens it if not active).
///
/// Bongo specifics:
/// - Visual targets are BongoImage + BongoBackShadow (script-free children of
///   "Bongos"). The Bongos root itself must NOT be scaled: BongoAnimator hard-resets
///   its localScale to Vector3.one on every hit, and BounceOnBeat writes it per frame.
/// - The L/R hit-zone children are left untouched (transparent, input-only).
/// - DrumPadStateVisual goes on the GameManager object next to BongoModeInputReader.
///   Event-driven only — no dungeon anticipation (BeatGenerator already enables
///   allowInput slightly before the input window).
///
/// Idempotent: re-running reuses existing components and rewires.
/// </summary>
public class SetupBongoPadStateVisuals
{
    [MenuItem("Tools/Drummi/Setup Bongo Pad State Visuals")]
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Bongo.unity"))
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Bongo.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        var bongoAnimator = Object.FindFirstObjectByType<BongoAnimator>(FindObjectsInactive.Include);
        var inputReader   = Object.FindFirstObjectByType<BongoModeInputReader>(FindObjectsInactive.Include);
        if (bongoAnimator == null || inputReader == null)
        {
            Debug.LogError("[SetupBongoPadStateVisuals] BongoAnimator or BongoModeInputReader not found in Bongo.unity.");
            return;
        }

        var bongos     = bongoAnimator.transform;
        var bongoImage = bongos.Find("BongoImage");
        var bongoShadow = bongos.Find("BongoBackShadow");
        if (bongoImage == null || bongoShadow == null)
        {
            Debug.LogError("[SetupBongoPadStateVisuals] BongoImage or BongoBackShadow not found under Bongos.");
            return;
        }

        var groups = new CanvasGroup[]
        {
            EnsureCanvasGroup(bongoImage),
            EnsureCanvasGroup(bongoShadow),
        };

        var host   = inputReader.gameObject; // GameManager
        var visual = host.GetComponent<DrumPadStateVisual>();
        if (visual == null) visual = host.AddComponent<DrumPadStateVisual>();

        var so = new SerializedObject(visual);
        so.FindProperty("inputReader").objectReferenceValue      = null;
        so.FindProperty("bongoInputReader").objectReferenceValue = inputReader;
        SetArray(so, "padGroups",          groups);
        SetArray(so, "bounceOnBeats",      new Object[0]);
        SetArray(so, "padBounceFeedbacks", new Object[0]);
        SetArray(so, "directScaleTargets", new Object[] { bongoImage, bongoShadow });
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupBongoPadStateVisuals] Setup complete, scene saved.");
    }

    private static CanvasGroup EnsureCanvasGroup(Transform t)
    {
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.interactable   = true;
        cg.blocksRaycasts = true;
        return cg;
    }

    private static void SetArray(SerializedObject so, string propName, Object[] values)
    {
        var p = so.FindProperty(propName);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
