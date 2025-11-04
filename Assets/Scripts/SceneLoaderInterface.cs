using UnityEngine;

public class SceneLoaderInterface : MonoBehaviour
{
    public void LoadSceneByName(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
