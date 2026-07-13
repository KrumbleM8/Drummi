using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot editor script that sets up the ThreePadRig, SingleDrumRig, and BongoRig
/// in Assets/Scenes/Dungeon.unity.
///
/// ThreePadRig  — groups the existing L_Input / M_Input / R_Input under a CanvasGroup.
/// SingleDrumRig — new group with a single wide drum pad (purely visual, C&R mode).
/// BongoRig     — new group with BongoImage + transparent L/R hit zones (TwoPad mode).
///
/// Existing pad positions/rotations are preserved (worldPositionStays = true).
/// DrumPadTouch pad refs are updated to the new paths.
/// DungeonInputLayoutController is added to RoguelikeLayer and fully wired.
/// RoomController.inputLayoutController is assigned.
/// </summary>
public class SetupDungeonRigs
{
    [UnityEditor.MenuItem("Tools/Drummi/Setup Dungeon Rigs")]
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ── Locate roots ───────────────────────────────────────────────────
        var overlayCanvas  = FindByPath("OverlayCanvas");
        var roguelikeLayer = FindByPath("RoguelikeLayer");
        var drumPadTouchGO = FindByPath("DrumPadTouch");
        var metronomeGO    = FindByPath("Metronome");

        if (overlayCanvas == null || roguelikeLayer == null || drumPadTouchGO == null)
        {
            Debug.LogError("[SetupDungeonRigs] Could not find required root objects.");
            return;
        }

        var overlayRT = overlayCanvas.GetComponent<RectTransform>();

        // ── Step 1: Create ThreePadRig ─────────────────────────────────────
        var threePadRigGO = new GameObject("ThreePadRig");
        var threePadRigRT = threePadRigGO.AddComponent<RectTransform>();
        threePadRigGO.transform.SetParent(overlayCanvas.transform, false);
        SetStretchFull(threePadRigRT);
        var threePadRigCG = threePadRigGO.AddComponent<CanvasGroup>();
        // Add transparent Image so CanvasGroup fade works properly
        var threePadImg = threePadRigGO.AddComponent<Image>();
        threePadImg.color = new Color(0, 0, 0, 0);
        threePadImg.raycastTarget = false;

        // ── Step 2: Parent existing pads under ThreePadRig ────────────────
        // worldPositionStays = true preserves their screen positions exactly.
        var lInput = overlayCanvas.transform.Find("L_Input");
        var mInput = overlayCanvas.transform.Find("M_Input");
        var rInput = overlayCanvas.transform.Find("R_Input");

        if (lInput != null) lInput.SetParent(threePadRigGO.transform, true);
        if (mInput != null) mInput.SetParent(threePadRigGO.transform, true);
        if (rInput != null) rInput.SetParent(threePadRigGO.transform, true);

        Debug.Log("[SetupDungeonRigs] ThreePadRig created and pads parented.");

        // ── Step 3: Create SingleDrumRig ───────────────────────────────────
        var singleDrumRigGO = new GameObject("SingleDrumRig");
        var singleDrumRigRT = singleDrumRigGO.AddComponent<RectTransform>();
        singleDrumRigGO.transform.SetParent(overlayCanvas.transform, false);
        SetStretchFull(singleDrumRigRT);
        var singleDrumRigCG = singleDrumRigGO.AddComponent<CanvasGroup>();
        var singleDrumRigImg = singleDrumRigGO.AddComponent<Image>();
        singleDrumRigImg.color = new Color(0, 0, 0, 0);
        singleDrumRigImg.raycastTarget = false;

        // SingleDrumPad — a wide single button, purely visual (raycastTarget=false)
        var singleDrumPadGO = new GameObject("SingleDrumPad");
        var singleDrumPadRT = singleDrumPadGO.AddComponent<RectTransform>();
        singleDrumPadGO.transform.SetParent(singleDrumRigGO.transform, false);
        // Centre-bottom anchor, matching the M_Input Y position — wide enough to span all three pads
        singleDrumPadRT.anchorMin        = new Vector2(0.5f, 0f);
        singleDrumPadRT.anchorMax        = new Vector2(0.5f, 0f);
        singleDrumPadRT.pivot            = new Vector2(0.5f, 0.5f);
        singleDrumPadRT.anchoredPosition = new Vector2(1f, 190.71f);
        singleDrumPadRT.sizeDelta        = new Vector2(750f, 325f);
        var singleDrumPadImg = singleDrumPadGO.AddComponent<Image>();
        singleDrumPadImg.raycastTarget = false;
        // Load the same button sprite as the three-pad buttons
        var padSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/LowPolyButton.png");
        if (padSprite != null) singleDrumPadImg.sprite = padSprite;
        singleDrumPadImg.color = Color.black;
        // Add PadBounceFeedback and BounceOnBeat to match the other pads visually
        var singleDrumBounce = singleDrumPadGO.AddComponent<BounceOnBeat>();
        var metronome = metronomeGO?.GetComponent<Metronome>();
        if (singleDrumBounce != null && metronome != null)
        {
            singleDrumBounce.metronome = metronome;
            singleDrumBounce.baseBounceScaleY = -0.51f;
            singleDrumBounce.bounceIntensity  = -2f;
            singleDrumBounce.smoothTime       = 0.15f;
        }

        // Start hidden
        singleDrumRigCG.alpha = 0f;
        singleDrumRigGO.transform.localScale = new Vector3(1f, 0f, 1f);

        Debug.Log("[SetupDungeonRigs] SingleDrumRig created.");

        // ── Step 4: Create BongoRig ────────────────────────────────────────
        var bongoRigGO = new GameObject("BongoRig");
        var bongoRigRT = bongoRigGO.AddComponent<RectTransform>();
        bongoRigGO.transform.SetParent(overlayCanvas.transform, false);
        bongoRigRT.anchorMin        = new Vector2(0.5f, 0f);
        bongoRigRT.anchorMax        = new Vector2(0.5f, 0f);
        bongoRigRT.pivot            = new Vector2(0.5f, 0.5f);
        bongoRigRT.anchoredPosition = new Vector2(6.15f, 88f);
        bongoRigRT.sizeDelta        = new Vector2(1200f, 586f);
        var bongoRigCG  = bongoRigGO.AddComponent<CanvasGroup>();
        var bongoRigImg = bongoRigGO.AddComponent<Image>();
        bongoRigImg.color         = new Color(1, 1, 1, 0);
        bongoRigImg.raycastTarget = false;

        // BongoAnimator
        var bongoAnimator = bongoRigGO.AddComponent<BongoAnimator>();
        bongoAnimator.scaleAmount    = 1.055f;
        bongoAnimator.scaleDuration  = 0.043f;
        bongoAnimator.rotationAmount = 10.5f;
        bongoAnimator.rangeMultiplier = 1.14f;
        // drumPadTouch set via reflection since it's [SerializeField] private
        var dptField = typeof(BongoAnimator).GetField("drumPadTouch",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var drumPadTouchComp = drumPadTouchGO.GetComponent<DrumPadTouch>();
        if (dptField != null) dptField.SetValue(bongoAnimator, drumPadTouchComp);

        // BounceOnBeat
        var bongoBounce = bongoRigGO.AddComponent<BounceOnBeat>();
        if (bongoBounce != null && metronome != null)
        {
            bongoBounce.metronome        = metronome;
            bongoBounce.baseBounceScaleY = 1.29f;
            bongoBounce.bounceIntensity  = 0.803f;
            bongoBounce.smoothTime       = 0.15f;
        }

        // BongoImage
        var bongoImageGO = new GameObject("BongoImage");
        var bongoImageRT = bongoImageGO.AddComponent<RectTransform>();
        bongoImageGO.transform.SetParent(bongoRigGO.transform, false);
        bongoImageRT.anchorMin        = new Vector2(0.5f, 0.5f);
        bongoImageRT.anchorMax        = new Vector2(0.5f, 0.5f);
        bongoImageRT.pivot            = new Vector2(0.5f, 0.5f);
        bongoImageRT.anchoredPosition = new Vector2(22.77f, 91.02f);
        bongoImageRT.sizeDelta        = new Vector2(719.128f, 404.008f);
        var bongoImageImg = bongoImageGO.AddComponent<Image>();
        var bongoSprite   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Bongo.png");
        if (bongoSprite != null) bongoImageImg.sprite = bongoSprite;
        bongoImageImg.raycastTarget = false;

        // L hit zone
        var bongoLGO = new GameObject("L");
        var bongoLRT = bongoLGO.AddComponent<RectTransform>();
        bongoLGO.transform.SetParent(bongoRigGO.transform, false);
        bongoLRT.anchorMin        = new Vector2(0f, 0f);
        bongoLRT.anchorMax        = new Vector2(0f, 0f);
        bongoLRT.pivot            = new Vector2(0f, 0f);
        bongoLRT.anchoredPosition = new Vector2(193.85f, 195.35f);
        bongoLRT.sizeDelta        = new Vector2(387.7f, 390.7f);
        var bongoLImg = bongoLGO.AddComponent<Image>();
        bongoLImg.color         = new Color(1, 1, 1, 0);
        bongoLImg.raycastTarget = true;

        // R hit zone
        var bongoRGO = new GameObject("R");
        var bongoRRT = bongoRGO.AddComponent<RectTransform>();
        bongoRGO.transform.SetParent(bongoRigGO.transform, false);
        bongoRRT.anchorMin        = new Vector2(1f, 0f);
        bongoRRT.anchorMax        = new Vector2(1f, 0f);
        bongoRRT.pivot            = new Vector2(1f, 0f);
        bongoRRT.anchoredPosition = new Vector2(-206.15f, 195.35f);
        bongoRRT.sizeDelta        = new Vector2(412.3f, 390.7f);
        var bongoRImg = bongoRGO.AddComponent<Image>();
        bongoRImg.color         = new Color(1, 1, 1, 0);
        bongoRImg.raycastTarget = true;

        // Start hidden and animator disabled
        bongoRigCG.alpha = 0f;
        bongoRigGO.transform.localScale = new Vector3(1f, 0f, 1f);
        bongoAnimator.enabled = false;

        Debug.Log("[SetupDungeonRigs] BongoRig created.");

        // ── Step 5: Add DungeonInputLayoutController to RoguelikeLayer ─────
        var dilc = roguelikeLayer.AddComponent<KrumbleHut.Drummi.Dungeons.DungeonInputLayoutController>();
        // Wire via SerializedObject so [SerializeField] private fields are set
        var so = new SerializedObject(dilc);
        so.FindProperty("threePadRig")   .objectReferenceValue = threePadRigCG;
        so.FindProperty("singleDrumRig") .objectReferenceValue = singleDrumRigCG;
        so.FindProperty("bongoRig")      .objectReferenceValue = bongoRigCG;
        so.FindProperty("bongoLeftPad")  .objectReferenceValue = bongoLRT;
        so.FindProperty("bongoRightPad") .objectReferenceValue = bongoRRT;
        so.FindProperty("bongoAnimator") .objectReferenceValue = bongoAnimator;
        so.FindProperty("drumPadTouch")  .objectReferenceValue = drumPadTouchComp;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SetupDungeonRigs] DungeonInputLayoutController added and wired.");

        // ── Step 6: Wire RoomController.inputLayoutController ──────────────
        var roomController = roguelikeLayer.GetComponent<RoomController>();
        if (roomController != null)
        {
            var rcSO = new SerializedObject(roomController);
            rcSO.FindProperty("inputLayoutController").objectReferenceValue = dilc;
            rcSO.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SetupDungeonRigs] RoomController.inputLayoutController wired.");
        }

        // ── Step 7: Update DrumPadTouch refs to new pad paths ──────────────
        // After parenting, paths are now ThreePadRig/L_Input etc.
        if (drumPadTouchComp != null && lInput != null && mInput != null && rInput != null)
        {
            var dptSO = new SerializedObject(drumPadTouchComp);
            dptSO.FindProperty("leftPad")  .objectReferenceValue = lInput.GetComponent<RectTransform>();
            dptSO.FindProperty("centerPad").objectReferenceValue = mInput.GetComponent<RectTransform>();
            dptSO.FindProperty("rightPad") .objectReferenceValue = rInput.GetComponent<RectTransform>();
            dptSO.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SetupDungeonRigs] DrumPadTouch pad refs updated.");
        }

        // ── Save ──────────────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupDungeonRigs] Scene saved. Setup complete.");
    }

    private static void SetStretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
        rt.pivot            = new Vector2(0.5f, 0.5f);
    }

    private static GameObject FindByPath(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager
                                       .GetActiveScene().GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }
}
