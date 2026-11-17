using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

public static class AddBallUIProxy
{
    public static void Execute()
    {
        // OverlayCanvas hosts the pad rigs + decorative Frame.
        GameObject overlayCanvas = null;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "OverlayCanvas") { overlayCanvas = root; break; }
            var t = root.transform.Find("OverlayCanvas");
            if (t != null) { overlayCanvas = t.gameObject; break; }
        }
        if (overlayCanvas == null) { Debug.LogError("[BallProxy] OverlayCanvas not found"); return; }

        var existing = overlayCanvas.transform.Find("FlickBallUIProxy");
        if (existing != null) { Debug.LogError("[BallProxy] Proxy already exists"); return; }

        var ballGO = GameObject.Find("FlickRig/FlickBall");
        var ballSr = ballGO != null ? ballGO.GetComponent<SpriteRenderer>() : null;
        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>(true);
        if (ballSr == null || cam == null) { Debug.LogError($"[BallProxy] ballSr={ballSr} cam={cam}"); return; }

        var go = new GameObject("FlickBallUIProxy", typeof(RectTransform));
        go.transform.SetParent(overlayCanvas.transform, false);
        go.transform.SetAsLastSibling(); // above Frame + pad rigs within this canvas

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100, 100); // overwritten every frame by the proxy

        var img = go.AddComponent<Image>();
        img.sprite = ballSr.sprite;
        img.color = ballSr.color;
        img.raycastTarget = false;
        img.enabled = false;

        var proxy = go.AddComponent<FlickBallUIProxy>();
        var so = new SerializedObject(proxy);
        so.FindProperty("ballRenderer").objectReferenceValue = ballSr;
        so.FindProperty("worldCamera").objectReferenceValue  = cam;
        so.FindProperty("image").objectReferenceValue        = img;
        so.ApplyModifiedPropertiesWithoutUndo();

        var frame = overlayCanvas.transform.Find("Frame");
        Debug.Log($"[BallProxy] Created under '{overlayCanvas.name}' at sibling {go.transform.GetSiblingIndex()} " +
                  $"(Frame sibling: {(frame != null ? frame.GetSiblingIndex().ToString() : "n/a")})");

        EditorSceneManager.MarkSceneDirty(go.scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BallProxy] saved={saved}");
    }
}
