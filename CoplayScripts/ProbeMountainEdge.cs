using System.Text;
using UnityEngine;

/// <summary>EDIT-MODE: viewport bounds of MountainMid vs the globe horizon at planet angles 0/35/70, plus the front sign button.</summary>
public static class ProbeMountainEdge
{
    public static string Execute()
    {
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        var mid = GameObject.Find("ParallaxMid").transform;
        var mtn = GameObject.Find("ParallaxMid/MountainMid");
        var sr = mtn.GetComponent<SpriteRenderer>();
        var sb = new StringBuilder();
        sb.Append("cam ").Append(cam.transform.position.ToString("F2")).Append(" euler ").Append(cam.transform.eulerAngles.ToString("F1"));
        sb.Append(" | MountainMid local ").Append(mtn.transform.localPosition.ToString("F2")).Append(" scale ").Append(mtn.transform.localScale.ToString("F2"));
        sb.Append(" sprite ").Append(sr.sprite != null ? sr.sprite.name : "null").Append(" bounds size ").Append(sr.bounds.size.ToString("F2")).AppendLine();
        // Horizon: upper tangent point of the R=10 sphere at origin, in the YZ plane.
        Vector3 c = cam.transform.position; float R = 10f;
        float rho = Mathf.Sqrt(c.y * c.y + c.z * c.z);
        float a0 = Mathf.Atan2(c.z, c.y), da = Mathf.Acos(R / rho);
        Vector3 t1 = new Vector3(0, Mathf.Cos(a0 + da), Mathf.Sin(a0 + da)) * R;
        Vector3 t2 = new Vector3(0, Mathf.Cos(a0 - da), Mathf.Sin(a0 - da)) * R;
        float hz = Mathf.Max(cam.WorldToViewportPoint(t1).y, cam.WorldToViewportPoint(t2).y);
        sb.Append("horizon top vp y=").Append(hz.ToString("F3")).AppendLine();
        Quaternion saved = mid.localRotation;
        foreach (float ang in new[] { 0f, 35f, 70f })
        {
            mid.localEulerAngles = new Vector3(-ang * 0.12f, 0, 0);
            Bounds b = sr.bounds;
            Vector3 lo = cam.WorldToViewportPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            Vector3 hi = cam.WorldToViewportPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            sb.Append("angle ").Append(ang).Append(": mountain vp y ").Append(lo.y.ToString("F3")).Append(" .. ").Append(hi.y.ToString("F3"))
              .Append(" (bottom - horizon = ").Append((lo.y - hz).ToString("F3")).Append(")").AppendLine();
        }
        mid.localRotation = saved;
        var btn = GameObject.Find("PlanetPivot/SignMount_0/MenuSign_Bongo/SignCanvas/SignButton").GetComponent<RectTransform>();
        var corners = new Vector3[4]; btn.GetWorldCorners(corners);
        sb.Append("front button vp y ").Append(cam.WorldToViewportPoint(corners[0]).y.ToString("F3")).Append(" .. ").Append(cam.WorldToViewportPoint(corners[2]).y.ToString("F3"));
        return sb.ToString();
    }
}
