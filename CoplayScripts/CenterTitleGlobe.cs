using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// EDIT-MODE, Home.unity open. Aims HomeIntroDirector's far title camera straight at
/// the globe centre: yaw 0 and pitch = the line from introCameraPosition to the
/// PlanetPivot, so the planet sits in the middle of the frame. Saves the scene.
/// </summary>
public static class CenterTitleGlobe
{
    /// <summary>Degrees the camera looks ABOVE the globe centre, so the planet sits that much lower in frame (0 = dead centre).</summary>
    private const float PitchUpOffset = 2.2f;

    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name;

        var director = Object.FindFirstObjectByType<HomeIntroDirector>(FindObjectsInactive.Include);
        var pivot    = GameObject.Find("PlanetPivot");
        if (director == null || pivot == null) return "director/pivot missing";

        var so  = new SerializedObject(director);
        Vector3 camPos = so.FindProperty("introCameraPosition").vector3Value;
        Vector3 toGlobe = pivot.transform.position - camPos;
        Quaternion look = Quaternion.LookRotation(toGlobe, Vector3.up);
        Vector3 euler = look.eulerAngles;
        if (euler.x > 180f) euler.x -= 360f;
        if (euler.y > 180f) euler.y -= 360f;
        euler.x -= PitchUpOffset; // smaller pitch = camera looks higher = globe lower in frame
        euler.z = 0f;

        Vector3 old = so.FindProperty("introCameraEuler").vector3Value;
        so.FindProperty("introCameraEuler").vector3Value = euler;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        string result = $"introCameraEuler {old} -> {euler} (camPos {camPos}, globe {pivot.transform.position}), saved={saved}";
        Debug.Log("[CenterTitleGlobe] " + result);
        return result;
    }
}
