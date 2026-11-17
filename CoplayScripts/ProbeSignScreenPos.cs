using System.Text;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: logs camera pose and the viewport-space bounds of each sign mount's renderers.</summary>
public static class ProbeSignScreenPos
{
    public static void Execute()
    {
        var cam = Camera.main;
        var sb = new StringBuilder();
        sb.AppendLine("[Probe] screen " + Screen.width + "x" + Screen.height + " fov=" + cam.fieldOfView + " cam pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F1"));
        var dir = Object.FindFirstObjectByType<HomeIntroDirector>();
        if (dir != null) sb.AppendLine("[Probe] introPending=" + dir.IntroPending);
        var spinner = Object.FindFirstObjectByType<PlanetSpinner>();
        if (spinner != null) sb.AppendLine("[Probe] planet angle=" + spinner.transform.eulerAngles.ToString("F1") + " pos=" + spinner.transform.position.ToString("F2"));
        for (int i = 0; i < 3; i++)
        {
            var m = GameObject.Find("PlanetPivot/SignMount_" + i);
            if (m == null) { sb.AppendLine("[Probe] no mount " + i); continue; }
            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { sb.AppendLine("[Probe] mount " + i + " no renderers"); continue; }
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Vector3 vmin = cam.WorldToViewportPoint(b.min), vmax = cam.WorldToViewportPoint(b.max), vc = cam.WorldToViewportPoint(b.center);
            sb.AppendLine("[Probe] mount " + i + " world " + b.min.ToString("F2") + " .. " + b.max.ToString("F2") + " | viewport centre " + vc.ToString("F3") + " min " + vmin.ToString("F3") + " max " + vmax.ToString("F3"));
            var rts = m.GetComponentsInChildren<RectTransform>();
            foreach (var rt in rts)
            {
                var btn = rt.GetComponent<UnityEngine.UI.Button>();
                if (btn == null) continue;
                var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                sb.AppendLine("[Probe]   button " + rt.name + " corners vp: " + cam.WorldToViewportPoint(corners[0]).ToString("F3") + " " + cam.WorldToViewportPoint(corners[2]).ToString("F3"));
            }
        }
        Debug.Log(sb.ToString());
    }
}
