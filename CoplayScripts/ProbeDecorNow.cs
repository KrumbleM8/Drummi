using System.Text;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: at the current planet angle, reports MountainMid's bottom edge vs the globe silhouette (centre and screen edges) and MountainOnGlobe's viewport bounds.</summary>
public static class ProbeDecorNow
{
    public static string Execute()
    {
        var cam = Camera.main;
        var spinner = Object.FindFirstObjectByType<PlanetSpinner>();
        var sb = new StringBuilder();
        sb.Append("angle=").Append(spinner.CurrentAngle.ToString("F1")).Append(" cam=").Append(cam.transform.position.ToString("F1")).Append('/').Append(cam.transform.eulerAngles.x.ToString("F1"));
        sb.Append(" | silhouette top x=0/0.5/1: ");
        foreach (float x in new[] { 0f, 0.5f, 1f })
        {
            float top = -1f;
            for (float y = 1f; y >= 0f; y -= 0.002f)
            {
                Ray r = cam.ViewportPointToRay(new Vector3(x, y, 0));
                Vector3 oc = r.origin - spinner.transform.position; float b = Vector3.Dot(oc, r.direction); float cc = Vector3.Dot(oc, oc) - 100f;
                if (b * b - cc >= 0f) { top = y; break; }
            }
            sb.Append(top.ToString("F3")).Append(' ');
        }
        var mid = GameObject.Find("ParallaxMid/MountainMid").GetComponent<SpriteRenderer>();
        Bounds mb = mid.bounds;
        sb.Append("| MountainMid vp y ").Append(cam.WorldToViewportPoint(new Vector3(0, mb.min.y, mb.center.z)).y.ToString("F3")).Append("..").Append(cam.WorldToViewportPoint(new Vector3(0, mb.max.y, mb.center.z)).y.ToString("F3"));
        var mog = GameObject.Find("PlanetPivot/MountainOnGlobe").GetComponent<SpriteRenderer>();
        Bounds ob = mog.bounds;
        Vector3 c0 = cam.WorldToViewportPoint(ob.min), c1 = cam.WorldToViewportPoint(ob.max);
        sb.Append(" | MountainOnGlobe vp min ").Append(c0.ToString("F2")).Append(" max ").Append(c1.ToString("F2")).Append(" order ").Append(mog.sortingOrder);
        var mountainSign = GameObject.Find("PlanetPivot/SignMount_4");
        var sr = mountainSign.GetComponentInChildren<SpriteRenderer>();
        sb.Append(" | sign4 order ").Append(sr.sortingOrder);
        return sb.ToString();
    }
}
