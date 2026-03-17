using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    public static SceneLoadManager instance;
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // Exact behavior preserved: immediate load of scene 0.
    public void ResetDrummi()
    {
        // Reset AudioManager state before destroying the scene so the
        // persistent DontDestroyOnLoad instance is clean for the next session.
        if (AudioManager.instance != null)
        {
            AudioManager.instance.ResetState();
        }

        // Restore timeScale in case we're coming from a paused state.
        Time.timeScale = 1f;

        //Handle Saving/Loading of any other persistent state here if needed.
        SceneManager.LoadScene(0);
    }

    // Utility if you need it elsewhere, without changing UX.
    public void LoadSceneImmediate(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}

