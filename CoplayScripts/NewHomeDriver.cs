using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using KrumbleHut.Drummi.UI;

/// <summary>
/// PLAY-MODE ONLY driver for verifying the New_Home menu flow.
///   ExecuteRoll   — simulate 8 alternating drum hits (raises curtain / fills bar).
///   ExecuteHit    — simulate a single left-pad hit.
///   ExecuteTap    — invoke the tap-to-start path (HomeFlowController.StartGame).
///   ExecuteBongo  — click the Bar_Bongo button (real onClick path).
///   ExecuteStatus — report which flow stage is active.
/// </summary>
public static class NewHomeDriver
{
    public static string ExecuteRoll()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var pad = UnityEngine.Object.FindFirstObjectByType<DrumPadTouch>();
        if (pad == null) return "FAIL: no DrumPadTouch";
        for (int i = 0; i < 8; i++)
            Raise(pad, i % 2 == 0 ? "OnLeftHit" : "OnRightHit");
        return "OK: 8 alternating hits raised";
    }

    private static int _rollTicks;

    /// <summary>Sustained drum roll: one hit per editor update tick for ~200 ticks.</summary>
    public static string ExecuteLongRoll()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        _rollTicks = 200;
        UnityEditor.EditorApplication.update -= RollTick;
        UnityEditor.EditorApplication.update += RollTick;
        return "OK: long roll started (200 ticks)";
    }

    private static void RollTick()
    {
        var pad = Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<DrumPadTouch>() : null;
        if (pad == null || --_rollTicks <= 0)
            UnityEditor.EditorApplication.update -= RollTick;
        if (pad != null)
            Raise(pad, _rollTicks % 2 == 0 ? "OnLeftHit" : "OnRightHit");
    }

    /// <summary>Bypasses curtain physics: invokes CurtainController.ActivateReveal directly.</summary>
    public static string ExecuteReveal()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var curtain = UnityEngine.Object.FindFirstObjectByType<CurtainController>();
        if (curtain == null) return "FAIL: no CurtainController";
        var m = typeof(CurtainController).GetMethod("ActivateReveal", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(curtain, null);
        return "OK: ActivateReveal invoked";
    }

    /// <summary>Slow-motion reveal so mid-animation frames can be captured.</summary>
    public static string ExecuteSlowReveal()
    {
        Time.timeScale = 0.05f;
        return ExecuteReveal() + " (timeScale 0.05)";
    }

    public static string ExecuteRestoreTime()
    {
        Time.timeScale = 1f;
        return "OK: timeScale 1";
    }

    public static string ExecuteHit()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var pad = UnityEngine.Object.FindFirstObjectByType<DrumPadTouch>();
        if (pad == null) return "FAIL: no DrumPadTouch";
        Raise(pad, "OnLeftHit");
        return "OK: left hit raised";
    }

    public static string ExecuteTap()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var flow = UnityEngine.Object.FindFirstObjectByType<HomeFlowController>();
        if (flow == null) return "FAIL: no HomeFlowController";
        flow.StartGame();
        return "OK: StartGame invoked";
    }

    public static string ExecuteBongo()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var bar = GameObject.Find("Bar_Bongo");
        if (bar == null) return "FAIL: Bar_Bongo not found/active";
        bar.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        return "OK: Bar_Bongo clicked";
    }

    public static string ExecuteStatus()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        string scene   = SceneManager.GetActiveScene().name;
        var curtain    = GameObject.Find("Curtain");
        var titlePage  = GameObject.Find("TitlePage");
        var tapRoot    = GameObject.Find("TapToStartRoot");
        var barsMenu   = GameObject.Find("BarsMenu");
        var fill       = GameObject.Find("Fill");
        string fillAmt = fill != null ? fill.GetComponent<UnityEngine.UI.Image>().fillAmount.ToString("F2") : "n/a";
        return $"scene={scene} | curtain={(curtain != null ? "active" : "hidden")} | " +
               $"titlePage={(titlePage != null ? "active" : "hidden")} | " +
               $"tapRoot={(tapRoot != null ? "active" : "hidden")} | " +
               $"barsMenu={(barsMenu != null ? "active" : "hidden")} | fill={fillAmt}";
    }

    private static void Raise(object target, string eventField)
    {
        var field = target.GetType().GetField(eventField, BindingFlags.NonPublic | BindingFlags.Instance);
        (field?.GetValue(target) as Action)?.Invoke();
    }
}
