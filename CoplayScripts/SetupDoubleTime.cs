using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

public class SetupDoubleTime
{
    public static void Execute()
    {
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[Setup] FlashCardView not found"); return; }

        var so = new SerializedObject(view);
        var cardRootProp = so.FindProperty("cardRoot");
        var cardRoot = cardRootProp != null ? cardRootProp.objectReferenceValue as RectTransform : null;
        if (cardRoot == null) { Debug.Log("[Setup] cardRoot not wired"); return; }

        // Create (or re-style) the banner under cardRoot.
        Transform existing = cardRoot.Find("DoubleTimeBanner");
        GameObject bannerGo = existing != null ? existing.gameObject
                                               : new GameObject("DoubleTimeBanner", typeof(RectTransform));
        if (existing == null) bannerGo.transform.SetParent(cardRoot, false);

        var rt = (RectTransform)bannerGo.transform;
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(540f, 80f);
        rt.anchoredPosition = new Vector2(0f, 48f);
        rt.localRotation    = Quaternion.Euler(0f, 0f, 3f);

        var text = bannerGo.GetComponent<TextMeshProUGUI>();
        if (text == null) text = bannerGo.AddComponent<TextMeshProUGUI>();
        text.text          = "DOUBLE TIME!";
        text.fontSize      = 56f;
        text.fontStyle     = FontStyles.Bold | FontStyles.Italic;
        text.alignment     = TextAlignmentOptions.Center;
        text.color         = new Color(1f, 0.25f, 0.55f, 1f);
        text.raycastTarget = false;
        bannerGo.SetActive(false);

        var bannerProp = so.FindProperty("doubleTimeBanner");
        if (bannerProp == null) { Debug.Log("[Setup] doubleTimeBanner property not found on view"); return; }
        bannerProp.objectReferenceValue = rt;
        so.ApplyModifiedProperties();
        Debug.Log("[Setup] Banner created and wired to FlashCardView.doubleTimeBanner");

        // Ladder doubleTimeChance values (scene-serialized steps override code defaults).
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[Setup] FlashCardsModeController not found"); return; }
        var mso   = new SerializedObject(mode);
        var steps = mso.FindProperty("escalationSteps");
        float[] chances = { 0f, 0f, 0.15f, 0.25f, 0.35f };
        for (int i = 0; i < steps.arraySize; i++)
        {
            var p = steps.GetArrayElementAtIndex(i).FindPropertyRelative("doubleTimeChance");
            float v = i < chances.Length ? chances[i] : chances[chances.Length - 1];
            p.floatValue = v;
            Debug.Log($"[Setup] escalation step {i}: doubleTimeChance={v:F2}");
        }
        mso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log("[Setup] Done — scene marked dirty.");
    }
}
