using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// One-off scene wiring for the per-block judging rework (FlashCardsMenu.unity):
///   1. Adds DungeonHealth (10 HP / 1 dmg / 1 stray / no regen) to FlashCardSystems.
///   2. Builds a non-interactable HealthBar slider where the strike pips were.
///   3. Wires DungeonHealth.healthSlider and FlashCardsModeController.health.
///   4. Deletes StrikePip0/1/2.
///   5. Scales the scene-serialized escalation thresholds x4 (cards -> blocks).
/// Does NOT save — run SaveOpenScenes separately and verify via git status.
/// Idempotent: safe to re-run.
/// </summary>
public static class WireFlashCardsHealth
{
    public static void Execute()
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[WireHealth] ABORT: FlashCardsModeController not found."); return; }

        // ── 1. DungeonHealth on FlashCardSystems ────────────────────────────
        var healthComp = mode.GetComponent<DungeonHealth>();
        if (healthComp == null) healthComp = mode.gameObject.AddComponent<DungeonHealth>();
        var healthSO = new SerializedObject(healthComp);
        healthSO.FindProperty("maxHealth").intValue           = 10;
        healthSO.FindProperty("damagePerMiss").intValue       = 1;
        healthSO.FindProperty("outOfWindowPenalty").intValue  = 1;
        healthSO.FindProperty("regenPerBar").floatValue       = 0f;
        healthSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 2. HealthBar slider at the strike-pip position ──────────────────
        var pip0 = FindInScene("StrikePip0");
        Transform hudParent = pip0 != null ? pip0.transform.parent : null;
        if (hudParent == null)
        {
            // Pips already deleted on a re-run — reuse the existing bar's parent.
            var existing = FindInScene("HealthBar");
            if (existing == null) { Debug.Log("[WireHealth] ABORT: neither StrikePip0 nor HealthBar found."); return; }
            hudParent = existing.transform.parent;
        }

        var barGO = FindInScene("HealthBar");
        Slider slider;
        if (barGO == null)
        {
            barGO = new GameObject("HealthBar", typeof(RectTransform), typeof(Slider));
            var barRT = (RectTransform)barGO.transform;
            barRT.SetParent(hudParent, false);
            barRT.anchorMin        = new Vector2(0f, 1f);
            barRT.anchorMax        = new Vector2(0f, 1f);
            barRT.pivot            = new Vector2(0f, 1f);
            barRT.anchoredPosition = new Vector2(60f, -60f);
            barRT.sizeDelta        = new Vector2(300f, 28f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRT = (RectTransform)bg.transform;
            bgRT.SetParent(barRT, false);
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.65f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var faRT = (RectTransform)fillArea.transform;
            faRT.SetParent(barRT, false);
            faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(3f, 3f); faRT.offsetMax = new Vector2(-3f, -3f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRT = (RectTransform)fill.transform;
            fillRT.SetParent(faRT, false);
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.25f, 1f);

            slider = barGO.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition   = Selectable.Transition.None;
            slider.navigation   = new Navigation { mode = Navigation.Mode.None };
            slider.targetGraphic = null;
            slider.fillRect     = fillRT;
            slider.direction    = Slider.Direction.LeftToRight;
            slider.minValue     = 0f;
            slider.maxValue     = 10f;
            slider.value        = 10f;
        }
        else
        {
            slider = barGO.GetComponent<Slider>();
        }

        // ── 3. Wire references ──────────────────────────────────────────────
        healthSO.Update();
        healthSO.FindProperty("healthSlider").objectReferenceValue = slider;
        healthSO.ApplyModifiedPropertiesWithoutUndo();

        var modeSO = new SerializedObject(mode);
        modeSO.FindProperty("health").objectReferenceValue = healthComp;

        // ── 5. Scale thresholds x4 (while we have the SerializedObject) ─────
        var steps = modeSO.FindProperty("escalationSteps");
        string scaled = "(none)";
        if (steps != null && steps.arraySize > 0)
        {
            // Only scale once: step 1 is 4 pre-scale, 16 post-scale.
            var probe = steps.arraySize > 1
                ? steps.GetArrayElementAtIndex(1).FindPropertyRelative("minBlocksCleared").intValue : 0;
            if (probe <= 10)
            {
                var vals = new System.Text.StringBuilder();
                for (int i = 0; i < steps.arraySize; i++)
                {
                    var p = steps.GetArrayElementAtIndex(i).FindPropertyRelative("minBlocksCleared");
                    p.intValue *= 4;
                    vals.Append(p.intValue).Append(i < steps.arraySize - 1 ? "/" : "");
                }
                scaled = vals.ToString();
            }
            else scaled = "already scaled, skipped";
        }
        modeSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 4. Delete strike pips ───────────────────────────────────────────
        int pipsDeleted = 0;
        foreach (var n in new[] { "StrikePip0", "StrikePip1", "StrikePip2" })
        {
            var go = FindInScene(n);
            if (go != null) { Object.DestroyImmediate(go); pipsDeleted++; }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[WireHealth] DONE — DungeonHealth wired (10/1/1/0), HealthBar slider ready, " +
                  $"pips deleted={pipsDeleted}, thresholds: {scaled}. Scene NOT saved yet.");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var t = FindRecursive(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindRecursive(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
