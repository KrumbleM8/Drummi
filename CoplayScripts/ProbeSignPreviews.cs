using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

// PLAY-MODE probes for the sign how-to-play previews. Output tag [PreviewProbe].
//   Execute()        — dump settle/focus state + every preview's alpha; PASS/FAIL
//                      "exactly the focused sign's preview is up" (run at least 0.6 s after a settle).
//   ExecuteFocus1()  — FocusOn(1): +0.1 s expect unsettled, all alphas below 1; +0.9 s expect node 1 alpha 1, node 0 alpha 0.
//   ExecuteFocus4()  — FocusOn(4): +1.0 s expect settled, node 4 locked, alpha 0.
//   ExecuteFocus0()  — FocusOn(0) then the steady assert.
//   ExecuteIntro()   — right after a fresh load with the intro: expect IntroPending and all alphas 0.
// GOTCHA: Coplay's script compiler fails (masked Roslyn resource error) on a bare '<' inside an
// XML doc comment — keep these notes as plain // comments.
public static class ProbeSignPreviews
{
    private const string Tag = "[PreviewProbe] ";

    private static PlanetSpinner _spinner;
    private static List<MenuSignNode> _nodes = new List<MenuSignNode>();
    private static List<MenuSignPreview> _previews = new List<MenuSignPreview>();

    public static void Execute()
    {
        if (!Ready()) return;
        Debug.Log(Tag + Dump());
        AssertExactlyFocused("steady");
    }

    // Steady assert 6 s from now (press Play first; the intro fly-in takes ~4 s).
    public static void ExecuteSteadyIn6()
    {
        if (!Ready()) return;
        DOVirtual.DelayedCall(6f, Execute, true);
        Debug.Log(Tag + "steady assert scheduled in 6 s");
    }

    public static void ExecuteFocus0()
    {
        if (!Ready()) return;
        _spinner.FocusOn(0);
        DOVirtual.DelayedCall(0.9f, AfterFocus0, true);
    }

    public static void ExecuteFocus1()
    {
        if (!Ready()) return;
        _spinner.FocusOn(1);
        DOVirtual.DelayedCall(0.1f, MidTween, true);
        DOVirtual.DelayedCall(0.9f, AfterFocus1, true);
    }

    public static void ExecuteFocus2()
    {
        if (!Ready()) return;
        _spinner.FocusOn(2);
        DOVirtual.DelayedCall(0.9f, AfterFocus0, true); // steady assert on whichever sign is focused
    }

    public static void ExecuteFocus3()
    {
        if (!Ready()) return;
        _spinner.FocusOn(3);
        DOVirtual.DelayedCall(0.9f, AfterFocus0, true);
    }

    public static void ExecuteFocus4()
    {
        if (!Ready()) return;
        _spinner.FocusOn(4);
        DOVirtual.DelayedCall(1.0f, AfterFocus4, true);
    }

    public static void ExecuteIntro()
    {
        if (!Ready()) return;
        HomeIntroDirector[] intros = Object.FindObjectsByType<HomeIntroDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool pending = intros.Length > 0 && intros[0].IntroPending;
        bool allZero = true;
        for (int i = 0; i < _previews.Count; i++)
            if (_previews[i] != null && _previews[i].Alpha > 0.001f) allZero = false;
        Debug.Log(Tag + Dump());
        Report(pending && allZero, "intro: pending=" + pending + " allAlpha0=" + allZero);
    }

    // Frame-rate independent ordering check: subscribe to SettledChanged, hop to
    // sign 0, and record the requested preview state inside each callback.
    // Expect: settled=False with every preview unrequested, then settled=True
    // with only preview 0 requested.
    private static StringBuilder _trace;
    private static int _traceEvents;

    public static void ExecuteHopTrace()
    {
        if (!Ready()) return;
        _trace = new StringBuilder();
        _traceEvents = 0;
        _trace.Append("start: settled=").Append(_spinner.IsSettled).Append(" focused=").Append(_spinner.FocusedIndex)
              .Append(" dt=").Append(Time.unscaledDeltaTime.ToString("F3"));
        _spinner.SettledChanged += OnTraceSettled;
        _spinner.FocusOn(0);
        _trace.Append("\n  after FocusOn(0) call: settled=").Append(_spinner.IsSettled).Append(" shown=").Append(ShownMask());
        DOVirtual.DelayedCall(1.5f, EndTrace, true);
    }

    private static void OnTraceSettled(bool settled)
    {
        _traceEvents++;
        _trace.Append("\n  event ").Append(_traceEvents).Append(": settled=").Append(settled)
              .Append(" focused=").Append(_spinner.FocusedIndex)
              .Append(" angle=").Append(_spinner.CurrentAngle.ToString("F1"))
              .Append(" shown=").Append(ShownMask())
              .Append(" dt=").Append(Time.unscaledDeltaTime.ToString("F3"));
    }

    private static void EndTrace()
    {
        _spinner.SettledChanged -= OnTraceSettled;
        _trace.Append("\n  end: settled=").Append(_spinner.IsSettled).Append(" shown=").Append(ShownMask())
              .Append(" alpha0=").Append(Alpha(0).ToString("F2"));
        bool ok = _traceEvents == 2 && _spinner.IsSettled && _spinner.FocusedIndex == 0
                  && Alpha(0) > 0.99f && ShownMask() == "10000";
        Debug.Log(Tag + _trace);
        Report(ok, "hop trace: events=" + _traceEvents + " (expect 2: false then true)");
    }

    private static string ShownMask()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _previews.Count; i++)
            sb.Append(_previews[i] != null && _previews[i].IsShown ? '1' : '0');
        return sb.ToString();
    }

    // ── Delayed checks ────────────────────────────────────────────────────────

    private static void MidTween()
    {
        bool allBelow = true;
        for (int i = 0; i < _previews.Count; i++)
            if (_previews[i] != null && _previews[i].Alpha >= 0.999f) allBelow = false;
        Report(!_spinner.IsSettled && allBelow, "mid-tween: settled=" + _spinner.IsSettled + " allAlpha<1=" + allBelow);
    }

    private static void AfterFocus0()
    {
        Debug.Log(Tag + Dump());
        AssertExactlyFocused("after FocusOn(0)");
    }

    private static void AfterFocus1()
    {
        Debug.Log(Tag + Dump());
        Report(_spinner.IsSettled && _spinner.FocusedIndex == 1 && Alpha(1) > 0.99f && Alpha(0) < 0.01f,
               "after FocusOn(1): settled=" + _spinner.IsSettled + " idx=" + _spinner.FocusedIndex
               + " a1=" + Alpha(1).ToString("F2") + " a0=" + Alpha(0).ToString("F2"));
    }

    private static void AfterFocus4()
    {
        Debug.Log(Tag + Dump());
        bool locked = _nodes.Count > 4 && _nodes[4] != null && _nodes[4].IsLocked;
        Report(_spinner.IsSettled && locked && Alpha(4) < 0.01f,
               "locked sign: settled=" + _spinner.IsSettled + " locked=" + locked + " a4=" + Alpha(4).ToString("F2"));
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static bool Ready()
    {
        if (!Application.isPlaying) { Debug.Log(Tag + "not in play mode"); return false; }
        PlanetSpinner[] spinners = Object.FindObjectsByType<PlanetSpinner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spinners.Length == 0) { Debug.Log(Tag + "no PlanetSpinner"); return false; }
        _spinner = spinners[0];

        _nodes.Clear();
        _previews.Clear();
        FlashCardsMenuController[] flows = Object.FindObjectsByType<FlashCardsMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (flows.Length > 0)
        {
            var field = typeof(FlashCardsMenuController).GetField("nodes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = field != null ? field.GetValue(flows[0]) as List<MenuSignNode> : null;
            if (list != null) _nodes.AddRange(list);
        }
        for (int i = 0; i < _nodes.Count; i++)
            _previews.Add(_nodes[i] != null ? _nodes[i].GetComponentInChildren<MenuSignPreview>(true) : null);
        return true;
    }

    private static float Alpha(int i)
    {
        if (i >= _previews.Count || _previews[i] == null) return -1f;
        return _previews[i].Alpha;
    }

    private static string Dump()
    {
        var sb = new StringBuilder();
        sb.Append("settled=").Append(_spinner.IsSettled)
          .Append(" focused=").Append(_spinner.FocusedIndex)
          .Append(" angle=").Append(_spinner.CurrentAngle.ToString("F1"))
          .Append(" pivotActive=").Append(_spinner.gameObject.activeInHierarchy);
        for (int i = 0; i < _nodes.Count; i++)
        {
            MenuSignNode n = _nodes[i];
            MenuSignPreview p = _previews[i];
            sb.Append("\n  [").Append(i).Append("] ").Append(n != null ? n.name : "null")
              .Append(" locked=").Append(n != null && n.IsLocked)
              .Append(" preview=").Append(p != null
                  ? ("alpha=" + p.Alpha.ToString("F2") + " shown=" + p.IsShown + " anim=" + p.IsAnimating)
                  : "NONE");
        }
        return sb.ToString();
    }

    private static void AssertExactlyFocused(string label)
    {
        int idx = _spinner.FocusedIndex;
        bool ok = _spinner.IsSettled;
        for (int i = 0; i < _previews.Count; i++)
        {
            MenuSignPreview p = _previews[i];
            if (p == null) continue;
            bool locked = _nodes[i] != null && _nodes[i].IsLocked;
            bool expectUp = i == idx && !locked;
            if (expectUp && (p.Alpha < 0.99f || !p.IsAnimating)) ok = false;
            if (!expectUp && (p.Alpha > 0.01f || p.IsAnimating)) ok = false;
        }
        Report(ok, label + ": only focused (" + idx + ") preview up, settled=" + _spinner.IsSettled);
    }

    private static void Report(bool pass, string msg)
    {
        Debug.Log(Tag + (pass ? "PASS " : "FAIL ") + msg);
    }
}
