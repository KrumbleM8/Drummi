using System.Text;
using TMPro;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class SnapAndReport
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // Which sign is front (closest to camera)?
        var cam = Camera.main;
        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var n in nodes)
        {
            var label = n.GetComponentInChildren<TMP_Text>(true);
            float d = cam != null ? Vector3.Distance(cam.transform.position, n.transform.position) : -1f;
            sb.AppendLine(n.name + " label='" + (label != null ? label.text : "?") + "' camDist=" + d.ToString("F1")
                + " worldPos=" + n.transform.position);
        }

        string path = System.IO.Path.Combine(Application.dataPath, "../Screenshots/gameview.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        sb.AppendLine("Capture queued.");
        return sb.ToString();
    }
}
