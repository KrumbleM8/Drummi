using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Play-mode helper: dumps FlashCardView / FlashCardController runtime state
/// relevant to the Classic variant — grid size + row count, on-deck root
/// visibility, reveal-rule fields — via reflection (private fields).
/// </summary>
public static class DumpClassicViewState
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[DumpClassic] Not in play mode."); return; }
        var sb = new StringBuilder();
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        var ctrl = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        if (view == null || ctrl == null) { Debug.Log("[DumpClassic] view=" + (view != null) + " ctrl=" + (ctrl != null)); return; }

        // Controller reveal rules
        sb.AppendLine("ctrl._pipelineReveal=" + Get(ctrl, "_pipelineReveal", F)
                      + " _compressRevealOverride=" + Get(ctrl, "_compressRevealOverride", F)
                      + " _revealBarsOverride=" + Get(ctrl, "_revealBarsOverride", F)
                      + " _nextPrepared=" + Get(ctrl, "_nextPrepared", F)
                      + " _cardSlotCount=" + Get(ctrl, "_cardSlotCount", F)
                      + " _cardRevealBeats=" + Get(ctrl, "_cardRevealBeats", F));

        // View state
        sb.AppendLine("view._layoutMode=" + Get(view, "_layoutMode", F)
                      + " alwaysOnDeckReveal(scene)=" + Get(view, "alwaysOnDeckReveal", F)
                      + " _onDeckRevealOverride=" + Get(view, "_onDeckRevealOverride", F)
                      + " _promoteOnPerform=" + Get(view, "_promoteOnPerform", F)
                      + " _state=" + Get(view, "_state", F));

        var onDeckRoot = Get(view, "_onDeckRoot", F) as RectTransform;
        sb.AppendLine("onDeckRoot=" + (onDeckRoot == null ? "null" : (onDeckRoot.gameObject.activeInHierarchy ? "ACTIVE" : "inactive")));
        var onDeckLabel = Get(view, "_onDeckLabel", F) as Component;
        sb.AppendLine("onDeckLabel=" + (onDeckLabel == null ? "null" : (onDeckLabel.gameObject.activeInHierarchy ? "ACTIVE" : "inactive")));

        // Grid: squares + distinct rows by anchored Y
        var squares = Get(view, "squares", F) as System.Array;
        int count = squares != null ? squares.Length : -1;
        var ys = new HashSet<int>();
        int visible = 0;
        if (squares != null)
        {
            foreach (var sq in squares)
            {
                if (sq == null) continue;
                var rootField = sq.GetType().GetField("root", F);
                var root = rootField != null ? rootField.GetValue(sq) as RectTransform : null;
                if (root == null) continue;
                if (root.gameObject.activeInHierarchy && root.localScale.x > 0.5f) visible++;
                ys.Add(Mathf.RoundToInt(root.anchoredPosition.y));
            }
        }
        sb.AppendLine("squares=" + count + " visible(scale>0.5)=" + visible + " distinctRows=" + ys.Count);
        Debug.Log("[DumpClassic]\n" + sb);
    }

    private static object Get(object target, string field, BindingFlags f)
    {
        var fi = target.GetType().GetField(field, f);
        if (fi == null) return "<no field " + field + ">";
        var v = fi.GetValue(target);
        return v == null ? "null" : v;
    }
}
