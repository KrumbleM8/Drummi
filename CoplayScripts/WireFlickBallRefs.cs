using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class WireFlickBallRefs
{
    public static void Execute()
    {
        var ball = Object.FindObjectOfType<FlickBallController>(true);
        var reader = Object.FindObjectOfType<FlickInputReader>(true);
        var cam = Camera.main;

        if (ball == null || reader == null)
        {
            Debug.LogError($"[WireFlickBallRefs] Missing — ball={(ball != null)}, reader={(reader != null)}");
            return;
        }

        var so = new SerializedObject(ball);
        so.FindProperty("flickReader").objectReferenceValue = reader;
        so.FindProperty("aimCamera").objectReferenceValue = cam;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WireFlickBallRefs] Wired flickReader={reader.name}, aimCamera={(cam != null ? cam.name : "null")} and saved scene.");
    }
}
