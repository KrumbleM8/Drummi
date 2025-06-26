using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    public GameObject menuObject;

    public void ToggleMenu()
    {
        if(menuObject.activeSelf)
        {
            menuObject.SetActive(false);
        }
        else
        {
            menuObject.SetActive(true);
        }
    }
}
