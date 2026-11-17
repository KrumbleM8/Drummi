using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class CheckWobbleState
{
    public static void Execute()
    {
        var ctrl = Object.FindObjectsByType<FlashCardController>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        double now = GameClock.Instance != null ? GameClock.Instance.GameTime : -1;
        var t = ctrl.CurrentTiming;
        string phase = now < t.ResponseStartTime ? "REVEAL" : (now < t.EvaluationTime ? "PERFORM" : "post-eval");
        var sb = new System.Text.StringBuilder();
        sb.Append($"[WobbleCheck] phase={phase} now={now:F3} resp={t.ResponseStartTime:F3} eval={t.EvaluationTime:F3} | ");
        var wobbles = Object.FindObjectsByType<UIRotateWobble>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.Append($"wobblesFound={wobbles.Length} | ");
        foreach (var w in wobbles)
        {
            var rt = (RectTransform)w.transform;
            sb.Append($"{w.gameObject.name}: en={w.enabled} z={rt.localEulerAngles.z:F2}; ");
        }
        Debug.Log(sb.ToString());
    }
}
