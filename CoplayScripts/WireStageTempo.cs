using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Stages wiring for FlashCardsMenu:
///  - escalationSteps: tempoMultiplier 1/1/1.1/1.2/1.3 on the existing 5 steps,
///    plus two new steps (28 cleared x1.4, 35 cleared x1.5) cloned from step 4.
///  - stageBreakBeats = 8, sting = Pickup11.wav, count-in = click.wav.
///  - StageUpBanner TMP (inactive) next to streakText, matching its font/color.
/// Saves the scene.
/// </summary>
public static class WireStageTempo
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu")
        {
            Debug.Log($"[Stages] ABORT: active scene is '{scene.name}', expected FlashCardsMenu.");
            return;
        }

        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[Stages] ABORT: FlashCardsModeController not found."); return; }

        var so = new SerializedObject(mode);

        // ── Ladder ──
        var steps = so.FindProperty("escalationSteps");
        if (steps == null || !steps.isArray) { Debug.Log("[Stages] ABORT: escalationSteps not found."); return; }
        if (steps.arraySize < 5) { Debug.Log($"[Stages] ABORT: expected >=5 steps, found {steps.arraySize}."); return; }

        float[] tempos = { 1f, 1f, 1.1f, 1.2f, 1.3f };
        for (int i = 0; i < 5; i++)
            steps.GetArrayElementAtIndex(i).FindPropertyRelative("tempoMultiplier").floatValue = tempos[i];

        // Grow to 7 — InsertArrayElementAtIndex clones the source element (step 4 config).
        while (steps.arraySize < 7) steps.InsertArrayElementAtIndex(steps.arraySize - 1);
        SetStep(steps.GetArrayElementAtIndex(5), 28, 1.4f);
        SetStep(steps.GetArrayElementAtIndex(6), 35, 1.5f);

        // ── Break config + clips ──
        so.FindProperty("stageBreakBeats").intValue = 8;
        var sting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Pickup11.wav");
        var click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click.wav");
        so.FindProperty("stageUpStingClip").objectReferenceValue  = sting;
        so.FindProperty("stageCountInClip").objectReferenceValue = click;
        if (sting == null || click == null) Debug.Log("[Stages] WARNING: sting or click clip failed to load.");

        // ── Banner ──
        var streakProp = so.FindProperty("streakText");
        var streak = streakProp != null ? streakProp.objectReferenceValue as TMP_Text : null;
        if (streak == null) { Debug.Log("[Stages] ABORT: streakText not assigned — cannot place banner."); return; }

        var bannerProp = so.FindProperty("stageBannerText");
        TMP_Text banner = bannerProp.objectReferenceValue as TMP_Text;
        if (banner == null)
        {
            var go = new GameObject("StageUpBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(streak.transform.parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 250f);
            rt.sizeDelta = new Vector2(900f, 360f);

            banner = go.GetComponent<TextMeshProUGUI>();
            banner.font               = streak.font;
            banner.fontSharedMaterial = streak.fontSharedMaterial;
            banner.color              = streak.color;
            banner.fontStyle          = FontStyles.Bold;
            banner.fontSize           = 110f;
            banner.alignment          = TextAlignmentOptions.Center;
            banner.text               = "SPEED UP!\n<size=60%>x1.1</size>";
            go.SetActive(false);
        }
        bannerProp.objectReferenceValue = banner;

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Stages] Wired: 7 ladder steps (tempos 1/1/1.1/1.2/1.3/1.4/1.5), breakBeats=8, " +
                  $"sting={(sting ? sting.name : "NULL")}, click={(click ? click.name : "NULL")}, " +
                  $"banner='{GetPath(banner.transform)}'. SaveOpenScenes={saved}.");
    }

    private static void SetStep(SerializedProperty step, int minCleared, float tempo)
    {
        step.FindPropertyRelative("minCardsCleared").intValue      = minCleared;
        step.FindPropertyRelative("tempoMultiplier").floatValue    = tempo;
        step.FindPropertyRelative("doubleTimeChance").floatValue   = 0f;
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
