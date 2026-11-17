using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using KrumbleHut.Drummi.FlashCards;

/// <summary>READ-ONLY recon of FlashCardsMenu: landscape layers (+viewport coverage from anchor 0), signs/anchors, sort mode, lighting.</summary>
public static class ReconFlashCardsMenuLayout
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);

        var cam = Camera.main;
        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(nodes, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        // Pose camera at anchor 0 for coverage estimates (scene not saved).
        var anchor0 = nodes[0].CameraAnchor;
        cam.transform.SetPositionAndRotation(anchor0.position, anchor0.rotation);

        float Coverage(Renderer r)
        {
            var b = r.bounds;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            bool anyInFront = false;
            foreach (var corner in new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z),
            })
            {
                var v = cam.WorldToViewportPoint(corner);
                if (v.z <= 0f) continue;
                anyInFront = true;
                min = Vector2.Min(min, new Vector2(v.x, v.y));
                max = Vector2.Max(max, new Vector2(v.x, v.y));
            }
            if (!anyInFront) return 0f;
            float w = Mathf.Clamp01(max.x) - Mathf.Clamp01(min.x);
            float h = Mathf.Clamp01(max.y) - Mathf.Clamp01(min.y);
            return Mathf.Clamp01(w) * Mathf.Clamp01(h);
        }

        sb.AppendLine("== LANDSCAPE ==");
        var landscape = GameObject.Find("Landscape");
        foreach (Transform t in landscape.transform)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            var size = sr.bounds.size;
            sb.AppendLine($"{t.name}: pos={t.position}, worldSize=({size.x:F1}x{size.y:F1}), sprite={sr.sprite.name}, " +
                          $"layer={sr.sortingLayerName}/{sr.sortingOrder}, mat={sr.sharedMaterial.name}, coverage={Coverage(sr):P0}");
        }

        sb.AppendLine("\n== SIGNS ==");
        foreach (var n in nodes)
        {
            var a = n.CameraAnchor;
            sb.AppendLine($"{n.name}: pos={n.transform.position}, rot={n.transform.rotation.eulerAngles}");
            sb.AppendLine($"   anchor local: pos={a.localPosition}, rot={a.localRotation.eulerAngles} | world: pos={a.position}, rot={a.rotation.eulerAngles}");
        }

        sb.AppendLine("\n== SORTING ==");
        sb.AppendLine($"GraphicsSettings.transparencySortMode: {GraphicsSettings.transparencySortMode}, axis: {GraphicsSettings.transparencySortAxis}");
        sb.AppendLine($"Camera: ortho={cam.orthographic}, fov={cam.fieldOfView}, near={cam.nearClipPlane}, far={cam.farClipPlane}, " +
                      $"transparencySortMode={cam.transparencySortMode}, sortAxis={cam.transparencySortAxis}, clear={cam.clearFlags}/{cam.backgroundColor}");

        sb.AppendLine("\n== LIGHTING ==");
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"Light2D '{l.name}': type={l.lightType}, intensity={l.intensity}, color={l.color}");
        var mats = new HashSet<string>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            mats.Add(sr.sharedMaterial.name + " (shader: " + sr.sharedMaterial.shader.name + ")");
        sb.AppendLine("SpriteRenderer materials in scene: " + string.Join("; ", mats));

        return sb.ToString();
    }
}
