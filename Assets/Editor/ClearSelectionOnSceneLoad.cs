using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Clears the editor selection after a scene opens or scripts reload.
/// Prevents SerializedObjectNotCreatableException caused by Unity trying to
/// restore a stale inspector selection (an InstanceID from a previous scene/
/// domain state that now resolves to a null Component).
/// </summary>
[InitializeOnLoad]
public static class ClearSelectionOnSceneLoad
{
    static ClearSelectionOnSceneLoad()
    {
        EditorSceneManager.sceneOpening += OnSceneOpening;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    // Clear before the new scene populates — stops Unity attempting to restore
    // an InstanceID that belonged to a scene object in the *previous* session.
    private static void OnSceneOpening(string path, OpenSceneMode mode)
    {
        Selection.activeObject = null;
    }

    // After a domain reload Unity re-applies the stored selection.  Schedule a
    // clear one editor tick later so it runs after that restoration completes.
    private static void OnAfterAssemblyReload()
    {
        EditorApplication.delayCall += () => Selection.activeObject = null;
    }
}
