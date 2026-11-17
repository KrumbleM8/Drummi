using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public static class ClickButtonByPath
{
    public static string Execute(JObject args)
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        string path = (string)args["path"];
        var go = GameObject.Find(path);
        if (go == null) return $"ERROR: '{path}' not found/active";
        var btn = go.GetComponent<Button>();
        if (btn == null) return $"ERROR: no Button on '{path}'";
        btn.onClick.Invoke();
        return $"Invoked onClick of '{path}'";
    }
}
