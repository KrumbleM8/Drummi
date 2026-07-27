using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// One-shot editor script that wires the drum-pad input-state visuals in
/// Assets/Scenes/Dungeon.unity (must be the active scene).
///
/// - Adds a nested CanvasGroup to each pad visual (L_Input, M_Input, R_Input,
///   BongoImage, SingleDrumPad). Nested alphas multiply with the rig-root
///   CanvasGroups, so DungeonInputLayoutController's layout fades are unaffected.
/// - Adds DrumPadStateVisual to RoguelikeLayer and wires all references.
/// - Wires DungeonInputLayoutController.stateVisual.
///
/// Idempotent: re-running reuses existing components and rewires.
/// </summary>
public class SetupDrumPadStateVisuals
{
    [MenuItem("Tools/Drummi/Setup Drum Pad State Visuals")]
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var overlayCanvas  = FindRoot("OverlayCanvas");
        var roguelikeLayer = FindRoot("RoguelikeLayer");
        if (overlayCanvas == null || roguelikeLayer == null)
        {
            Debug.LogError("[SetupDrumPadStateVisuals] OverlayCanvas or RoguelikeLayer not found — is Dungeon.unity open?");
            return;
        }

        // ── Locate pad visuals ─────────────────────────────────────────────
        var lInput        = overlayCanvas.transform.Find("ThreePadRig/L_Input");
        var mInput        = overlayCanvas.transform.Find("ThreePadRig/M_Input");
        var rInput        = overlayCanvas.transform.Find("ThreePadRig/R_Input");
        var bongoImage    = overlayCanvas.transform.Find("BongoRig/BongoImage");
        var singleDrumPad = overlayCanvas.transform.Find("SingleDrumRig/SingleDrumPad");

        if (lInput == null || mInput == null || rInput == null || bongoImage == null || singleDrumPad == null)
        {
            Debug.LogError("[SetupDrumPadStateVisuals] One or more pad visuals not found under OverlayCanvas.");
            return;
        }

        // ── Nested CanvasGroups on each pad visual ─────────────────────────
        var groups = new CanvasGroup[]
        {
            EnsureCanvasGroup(lInput),
            EnsureCanvasGroup(mInput),
            EnsureCanvasGroup(rInput),
            EnsureCanvasGroup(bongoImage),
            EnsureCanvasGroup(singleDrumPad),
        };

        // ── Bounce scripts on pads that have them ──────────────────────────
        var bounces = new BounceOnBeat[]
        {
            lInput.GetComponent<BounceOnBeat>(),
            mInput.GetComponent<BounceOnBeat>(),
            rInput.GetComponent<BounceOnBeat>(),
            singleDrumPad.GetComponent<BounceOnBeat>(),
        };
        var feedbacks = new PadBounceFeedback[]
        {
            lInput.GetComponent<PadBounceFeedback>(),
            mInput.GetComponent<PadBounceFeedback>(),
            rInput.GetComponent<PadBounceFeedback>(),
            singleDrumPad.GetComponent<PadBounceFeedback>(),
        };
        foreach (var b in bounces)   if (b == null) Debug.LogWarning("[SetupDrumPadStateVisuals] Missing BounceOnBeat on a pad.");
        foreach (var f in feedbacks) if (f == null) Debug.LogWarning("[SetupDrumPadStateVisuals] Missing PadBounceFeedback on a pad.");

        var inputReader = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
        if (inputReader == null)
        {
            Debug.LogError("[SetupDrumPadStateVisuals] DungeonInputReader not found in scene.");
            return;
        }

        // ── Add + wire DrumPadStateVisual on RoguelikeLayer ────────────────
        var visual = roguelikeLayer.GetComponent<DrumPadStateVisual>();
        if (visual == null) visual = roguelikeLayer.AddComponent<DrumPadStateVisual>();

        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null)
            Debug.LogWarning("[SetupDrumPadStateVisuals] DungeonRunner not found — main-menu full-visual state will not apply.");

        var so = new SerializedObject(visual);
        so.FindProperty("inputReader").objectReferenceValue   = inputReader;
        so.FindProperty("dungeonRunner").objectReferenceValue = runner;
        SetArray(so, "padGroups",          groups);
        SetArray(so, "bounceOnBeats",      bounces);
        SetArray(so, "padBounceFeedbacks", feedbacks);
        SetArray(so, "directScaleTargets", new Object[] { bongoImage });
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire DungeonInputLayoutController.stateVisual ──────────────────
        var dilc = roguelikeLayer.GetComponent<DungeonInputLayoutController>();
        if (dilc != null)
        {
            var dilcSO = new SerializedObject(dilc);
            dilcSO.FindProperty("stateVisual").objectReferenceValue = visual;
            dilcSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[SetupDrumPadStateVisuals] DungeonInputLayoutController not found on RoguelikeLayer.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupDrumPadStateVisuals] Setup complete, scene saved.");
    }

    private static CanvasGroup EnsureCanvasGroup(Transform t)
    {
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        // Purely visual: DrumPadTouch bypasses raycasts, but keep defaults explicit.
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

    private static GameObject FindRoot(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager
                                       .GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
