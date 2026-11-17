using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public static class FlashCardsColorDump
{
    static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    static string C(Color c) => $"({c.r:0.##},{c.g:0.##},{c.b:0.##},{c.a:0.##})";

    public static string Execute()
    {
        var sb = new StringBuilder();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                sb.AppendLine("IMG  " + Path(img.transform) + " " + C(img.color) + (img.sprite != null ? " sprite=" + img.sprite.name : ""));
            foreach (var txt in root.GetComponentsInChildren<TMP_Text>(true))
                sb.AppendLine("TXT  " + Path(txt.transform) + " " + C(txt.color) + " \"" + txt.text.Replace("\n", " ") + "\"");
        }
        return sb.ToString();
    }
}
