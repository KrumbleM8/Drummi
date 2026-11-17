using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class WireFlickTestPhase3
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FlickTest.unity", OpenSceneMode.Single);

        var metronome = Object.FindFirstObjectByType<Metronome>();
        var manager = Object.FindFirstObjectByType<FlickBeatManager>();
        var driver = Object.FindFirstObjectByType<FlickTestDriver>();
        var cam = Camera.main;
        if (metronome == null || manager == null || driver == null || cam == null)
        {
            Debug.LogError("[Coplay] Missing Phase 2 objects or camera.");
            return;
        }

        // 2D framing.
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (knob == null) Debug.LogWarning("[Coplay] Knob sprite not found — sprites will be empty.");

        // Sweep anchors.
        var leftAnchor = GetOrCreate("SweepLeftAnchor");
        leftAnchor.transform.position = new Vector3(-3.5f, 2.5f, 0f);
        var rightAnchor = GetOrCreate("SweepRightAnchor");
        rightAnchor.transform.position = new Vector3(3.5f, 2.5f, 0f);

        // Target: sweep on parent, visual on child.
        var target = GetOrCreate("FlickTarget");
        target.transform.position = new Vector3(-3.5f, 2.5f, 0f);
        var sweep = GetOrAdd<FlickTargetSweep>(target);

        GameObject visualGO;
        var visualTr = target.transform.Find("TargetVisual");
        if (visualTr == null)
        {
            visualGO = new GameObject("TargetVisual");
            visualGO.transform.SetParent(target.transform, false);
        }
        else visualGO = visualTr.gameObject;
        var targetSr = GetOrAdd<SpriteRenderer>(visualGO);
        targetSr.sprite = knob;
        targetSr.color = new Color(1f, 0.55f, 0.15f); // orange base
        targetSr.sortingOrder = 10;
        visualGO.transform.localScale = Vector3.one * 8f; // Knob is tiny
        var targetVisual = GetOrAdd<FlickTargetVisual>(visualGO);

        // Ball + home anchor.
        var home = GetOrCreate("BallHomeAnchor");
        home.transform.position = new Vector3(0f, -3.5f, 0f);

        var ball = GetOrCreate("FlickBall");
        ball.transform.position = home.transform.position;
        ball.transform.localScale = Vector3.one * 6f;
        var ballSr = GetOrAdd<SpriteRenderer>(ball);
        ballSr.sprite = knob;
        ballSr.color = new Color(0.4f, 0.8f, 1f); // light blue
        ballSr.sortingOrder = 11;
        var ballCtrl = GetOrAdd<FlickBallController>(ball);

        // Wire serialized refs.
        Set(sweep, "leftAnchor", leftAnchor.transform);
        Set(sweep, "rightAnchor", rightAnchor.transform);
        Set(sweep, "aimCamera", cam);

        Set(targetVisual, "beatManager", manager);
        Set(targetVisual, "spriteRenderer", targetSr);

        Set(ballCtrl, "beatManager", manager);
        Set(ballCtrl, "targetSweep", sweep);
        Set(ballCtrl, "targetVisual", targetVisual);
        Set(ballCtrl, "metronome", metronome);
        Set(ballCtrl, "homeAnchor", home.transform);

        Set(driver, "targetSweep", sweep);

        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Coplay] FlickTest Phase 3 wiring saved: {saved}");
    }

    private static GameObject GetOrCreate(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void Set(Component target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[Coplay] Property '{field}' not found on {target.GetType().Name}"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
