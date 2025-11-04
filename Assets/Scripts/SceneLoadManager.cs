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
        // TODO: handle saves, transitions if you add them later.
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

