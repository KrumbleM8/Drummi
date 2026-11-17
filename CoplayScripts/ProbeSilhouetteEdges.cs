using System.Text;
using UnityEngine;

/// <summary>EDIT-MODE: with the camera temporarily at pitch 19, finds the globe silhouette's top at viewport x = 0 / 0.5 / 1 and the MountainMid bottom edge at angles 0 and 70.</summary>
public static class ProbeSilhouetteEdges
{
    public static string Execute()
    {
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        var mid = GameObject.Find("ParallaxMid").transform;
        var sr = GameObject.Find("ParallaxMid/MountainMid").GetComponent<SpriteRenderer>();
        Quaternion savedRot = cam.transform.rotation; Quaternion savedMid = mid.localRotation;
        var sb = new StringBuilder();
        foreach (float pitch in new[] { 17f, 19f })
        {
            cam.transform.rotation = Quaternion.Euler(pitch, 0, 0);
            sb.Append("pitch ").Append(pitch).Append(": silhouette top vp y at x=0/0.5/1 = ");
            foreach (float x in new[] { 0f, 0.5f, 1f })
            {
                float top = -1f;
                for (float y = 1f; y >= 0f; y -= 0.002f)
                {
                    Ray r = cam.ViewportPointToRay(new Vector3(x, y, 0));
                    // ray-sphere (origin, R=10)
                    Vector3 oc = r.origin; float b = Vector3.Dot(oc, r.direction); float cc = Vector3.Dot(oc, oc) - 100f;
                    if (b * b - cc >= 0f) { top = y; break; }
                }
                sb.Append(top.ToString("F3")).Append(' ');
            }
            foreach (float ang in new[] { 0f, 70f })
            {
                mid.localEulerAngles = new Vector3(-ang * 0.12f, 0, 0);
                Bounds bd = sr.bounds;
                sb.Append("| mtn@").Append(ang).Append(" vp y ").Append(cam.WorldToViewportPoint(new Vector3(0, bd.min.y, bd.center.z)).y.ToString("F3"))
                  .Append("..").Append(cam.WorldToViewportPoint(new Vector3(0, bd.max.y, bd.center.z)).y.ToString("F3")).Append(' ');
            }
            // vp-y per world unit of mountain height at z=28
            Vector3 p0 = cam.WorldToViewportPoint(new Vector3(0, 0, 28)), p1 = cam.WorldToViewportPoint(new Vector3(0, 1, 28));
            sb.Append("| vp per unit y @z28 = ").Append((p1.y - p0.y).ToString("F4")).AppendLine();
        }
        cam.transform.rotation = savedRot; mid.localRotation = savedMid;
        return sb.ToString();
    }
}
