using System.IO;
using UnityEngine;

/// <summary>Synchronously renders the menu camera to a PNG (Temp/snap.png) — works even when the editor player loop is idle. Skips overlay canvases.</summary>
public static class SnapGameCamera
{
    public static string Execute()
    {
        GameObject camGo = GameObject.Find("Main Camera");
        if (camGo == null) return "FAIL: Main Camera not found/inactive.";
        Camera cam = camGo.GetComponent<Camera>();

        int w = 486, h = 968;
        RenderTexture rt = new RenderTexture(w, h, 24);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevTarget;

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        File.WriteAllBytes("Temp/snap.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return "Wrote Temp/snap.png";
    }
}
