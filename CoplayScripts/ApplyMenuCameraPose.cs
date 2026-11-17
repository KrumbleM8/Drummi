using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>EDIT-MODE: sets the Home menu camera to (0,15,-8) pitch 19 and MountainMid rest y to -0.5, then saves the open scene.</summary>
public static class ApplyMenuCameraPose
{
    public static string Execute()
    {
        if (Application.isPlaying) return "FAIL: in play mode.";
        var cam = GameObject.Find("Main Camera");
        var mtn = GameObject.Find("ParallaxMid/MountainMid");
        if (cam == null || mtn == null) return "FAIL: objects not found.";
        Undo.RecordObjects(new Object[] { cam.transform, mtn.transform }, "Menu camera pose + mountain");
        cam.transform.SetPositionAndRotation(new Vector3(0f, 15f, -8f), Quaternion.Euler(19f, 0f, 0f));
        var lp = mtn.transform.localPosition; lp.y = -0.5f; mtn.transform.localPosition = lp;
        EditorUtility.SetDirty(cam.transform); EditorUtility.SetDirty(mtn.transform);
        EditorSceneManager.MarkSceneDirty(cam.scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        return "cam euler=" + cam.transform.eulerAngles + " mountain local=" + mtn.transform.localPosition + " saved=" + saved;
    }
}
