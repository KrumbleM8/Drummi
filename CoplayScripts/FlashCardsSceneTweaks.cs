using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FlashCardsSceneTweaks
{
    public static string Execute()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/FlashCards.unity")
            return "ERROR: active scene is " + scene.path + ", expected FlashCards.unity";

        string log = "";

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Camera background
            Camera cam = root.GetComponentInChildren<Camera>(true);
            if (cam != null && cam.gameObject.name == "Main Camera")
            {
                Undo.RecordObject(cam, "Paper-white background");
                cam.backgroundColor = new Color(0.96f, 0.94f, 0.9f, 1f);
                EditorUtility.SetDirty(cam);
                log += "Main Camera background set to (0.96, 0.94, 0.9). ";
            }

            // ScorePage inactive
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name == "ScorePage" && t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "Hide ScorePage");
                    t.gameObject.SetActive(false);
                    EditorUtility.SetDirty(t.gameObject);
                    log += "ScorePage set inactive. ";
                }
                if (t.gameObject.name == "MainPage")
                {
                    log += "MainPage activeSelf=" + t.gameObject.activeSelf + ". ";
                }
            }
        }

        bool saved = EditorSceneManager.SaveOpenScenes();
        log += "Saved=" + saved;
        return log;
    }
}
