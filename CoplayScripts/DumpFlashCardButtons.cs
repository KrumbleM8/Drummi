using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public static class DumpFlashCardButtons
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        foreach (var path in new[]
        {
            "FlashCardsCanvas/MainPage/PlayButton",
            "FlashCardsCanvas/ScorePage/AgainButton",
        })
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                // GameObject.Find misses inactive objects — fall back to transform walk.
                go = FindInactive(path);
            }
            if (go == null) { sb.AppendLine($"{path}: NOT FOUND"); continue; }
            var btn = go.GetComponent<Button>();
            var ev = btn.onClick;
            sb.AppendLine($"{path}: {ev.GetPersistentEventCount()} listeners");
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                var target = ev.GetPersistentTarget(i);
                sb.AppendLine($"  [{i}] target={(target ? target.name + " (" + target.GetType().Name + ")" : "null")} method={ev.GetPersistentMethodName(i)}");
            }
        }

        // Also locate the FlashCardsModeController instance path.
        foreach (var mc in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mc.GetType().Name == "FlashCardsModeController")
                sb.AppendLine($"FlashCardsModeController on: {GetPath(mc.transform)}");
        }
        return sb.ToString();
    }

    private static GameObject FindInactive(string path)
    {
        var parts = path.Split('/');
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.parent == null ? t.name + "/" + p : t.name + "/" + p; }
        return p;
    }
}
