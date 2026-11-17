using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

public static class CheckGhosts
{
    public static void Execute()
    {
        var ctrl = Object.FindObjectsByType<FlashCardController>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        double now = GameClock.Instance != null ? GameClock.Instance.GameTime : -1;
        var t = ctrl.CurrentTiming;
        string phase = now < t.ResponseStartTime ? "REVEAL" : (now < t.EvaluationTime ? "PERFORM" : "post-eval");

        int ghostCount = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (img.gameObject.name == "SlotGhost") ghostCount++;
        sb.Append($"[GhostCheck] phase={phase} ghosts={ghostCount} | ");

        var wobbles = Object.FindObjectsByType<UIRotateWobble>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(wobbles, (a, b) => string.Compare(a.gameObject.name, b.gameObject.name));
        foreach (var w in wobbles)
        {
            var img = w.GetComponent<Image>();
            sb.Append($"{w.gameObject.name}: s={w.transform.localScale.x:F2} backOn={(img != null && img.enabled)}; ");
        }
        Debug.Log(sb.ToString());
    }
}
