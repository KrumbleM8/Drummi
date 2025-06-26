using UnityEngine;
using UnityEngine.SceneManagement;
public class Bootstrap : MonoBehaviour
{
    public int targetFramerate = 60;

    private void Start()
    {
        //Check device resolution
        //Adjust for any shit devices or something

        Application.targetFrameRate = targetFramerate;

        //Check for DLC
        //Check for errors or conflicts in player prefs or some shit i dunno

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
