using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class CheckSquareScales
{
    public static void Execute()
    {
        var ctrl = Object.FindObjectsByType<FlashCardController>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        double now = GameClock.Instance != null ? GameClock.Instance.GameTime : -1;
        var t = ctrl.CurrentTiming;
        string phase = now < t.ResponseStartTime ? "REVEAL" : (now < t.EvaluationTime ? "PERFORM" : "post-eval");
        double beatsIn = (now - t.CallStartTime) / ((t.ResponseStartTime - t.CallStartTime) / 8.0);
        var sb = new System.Text.StringBuilder();
        sb.Append($"[ScaleCheck] phase={phase} beatsIntoCall={beatsIn:F2} | ");
        var wobbles = Object.FindObjectsByType<UIRotateWobble>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(wobbles, (a, b) => string.Compare(a.gameObject.name, b.gameObject.name));
        foreach (var w in wobbles)
            sb.Append($"{w.gameObject.name}: s={w.transform.localScale.x:F2}; ");
        Debug.Log(sb.ToString());
    }
}
