using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class HighscoreSetter : MonoBehaviour
{
    void Start()
    {
        switch (transform.name)
        {
            case "ContentGlitchy":
                transform.Find("Highscore").GetChild(1).GetComponent<TMP_Text>().text = PlayerPrefs.GetInt("GlitchyHS", 0).ToString();
                break;
            case "ContentLatin":
                transform.Find("Highscore").GetChild(1).GetComponent<TMP_Text>().text = PlayerPrefs.GetInt("LatinHS", 0).ToString();
                break;
            case "ContentJazz":
                transform.Find("Highscore").GetChild(1).GetComponent<TMP_Text>().text = PlayerPrefs.GetInt("JazzHS").ToString();
                break;
            default:
                break;
        }
    }
}
