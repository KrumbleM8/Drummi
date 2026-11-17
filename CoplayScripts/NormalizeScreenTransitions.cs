using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NormalizeScreenTransitions
{
    private const float Duration = 0.3f;

    public static void Execute()
    {
        foreach (var scenePath in new[] { "Assets/Scenes/New_Home.unity" })
        {
            EditorSceneManager.OpenScene(scenePath);
            bool changed = false;

            foreach (var st in Object.FindObjectsByType<ScreenTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Skip instances of the OverlayUtilCanvas prefab — they already inherit 0.3 from the prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(st)) continue;

                if (!Mathf.Approximately(st.transitionDuration, Duration))
                {
                    Debug.Log($"[NormalizeScreenTransitions] {scenePath}: {st.gameObject.name} {st.transitionDuration} -> {Duration}");
                    st.transitionDuration = Duration;
                    EditorUtility.SetDirty(st);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
            else Debug.Log($"[NormalizeScreenTransitions] {scenePath}: no changes needed");
        }
        Debug.Log("[NormalizeScreenTransitions] Done.");
    }
}
