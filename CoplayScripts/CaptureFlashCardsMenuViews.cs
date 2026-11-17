using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// EDIT MODE: opens FlashCardsMenu.unity and renders the menu camera from each
/// sign's CameraAnchor at portrait resolution (412x815) to Temp/fcmenu_sign{i}.png.
/// </summary>
public static class CaptureFlashCardsMenuViews
{
    public static string Execute()
    {
        var log = new List<string>();
        EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);

        var cam = Camera.main;
        if (cam == null) return "FAIL: no MainCamera in scene";

        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsSortMode.None);
        System.Array.Sort(nodes, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        var rt = new RenderTexture(412, 815, 24);
        var tex = new Texture2D(412, 815, TextureFormat.RGB24, false);
        var prevTarget = cam.targetTexture;

        try
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform anchor = nodes[i].CameraAnchor;
                cam.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 412, 815), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string path = $"Temp/fcmenu_sign{i}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                log.Add($"Captured {path} from {nodes[i].name}");
            }
        }
        finally
        {
            cam.targetTexture = prevTarget;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        return string.Join("\n", log);
    }
}
