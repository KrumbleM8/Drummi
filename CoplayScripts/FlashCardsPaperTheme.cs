using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using KrumbleHut.Drummi.FlashCards;

public static class FlashCardsPaperTheme
{
    static readonly Color Paper = new Color(0.96f, 0.94f, 0.90f, 1f);
    static readonly Color Ink   = new Color(0.15f, 0.13f, 0.11f, 1f);
    static readonly Color Gold  = new Color(0.93f, 0.79f, 0.44f, 1f);
    static readonly Color Card  = new Color(1f, 0.99f, 0.96f, 0.97f);
    static readonly Color Slot  = new Color(0.90f, 0.87f, 0.82f, 1f);
    static readonly Color PipEmpty = new Color(0.15f, 0.13f, 0.11f, 0.25f);

    static StringBuilder _log = new();

    static void SetImg(Transform root, string path, Color c)
    {
        var t = root.Find(path);
        if (t == null) { _log.AppendLine("MISSING " + path); return; }
        var img = t.GetComponent<Image>();
        Undo.RecordObject(img, "Paper theme");
        img.color = c;
        EditorUtility.SetDirty(img);
        _log.AppendLine("img " + path);
    }

    static void SetTxt(Transform root, string path, Color c)
    {
        var t = root.Find(path);
        if (t == null) { _log.AppendLine("MISSING " + path); return; }
        var txt = t.GetComponent<TMP_Text>();
        Undo.RecordObject(txt, "Paper theme");
        txt.color = c;
        EditorUtility.SetDirty(txt);
        _log.AppendLine("txt " + path);
    }

    public static string Execute()
    {
        _log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/FlashCards.unity")
            return "ERROR: active scene is " + scene.path;

        GameObject canvasGo = GameObject.Find("FlashCardsCanvas");
        if (canvasGo == null) return "ERROR: FlashCardsCanvas not found";
        Transform c = canvasGo.transform;

        SetImg(c, "MainPage", Paper);
        SetImg(c, "ScorePage", Paper);
        SetImg(c, "GameplayUI/FlashCardPanel/CardBackground", Card);
        for (int i = 0; i < 8; i++)
        {
            SetImg(c, $"GameplayUI/FlashCardPanel/Sq{i}", Slot);
            SetImg(c, $"GameplayUI/FlashCardPanel/Sq{i}/Glyph", Ink);
            SetImg(c, $"GameplayUI/FlashCardPanel/Sq{i}/Cursor", Gold);
        }
        for (int i = 0; i < 3; i++)
            SetImg(c, $"GameplayUI/StrikePip{i}", PipEmpty);

        SetTxt(c, "MainPage/Title", Ink);
        SetTxt(c, "MainPage/Subtitle", Ink);
        SetTxt(c, "ScorePage/ResultsTitle", Ink);
        SetTxt(c, "ScorePage/FinalScoreText", Ink);
        SetTxt(c, "ScorePage/PerfectHitsText", Ink);
        SetTxt(c, "ScorePage/BestStreakText", Ink);
        SetTxt(c, "ScorePage/CardsClearedText", Ink);
        SetTxt(c, "GameplayUI/ScoreText", Ink);
        SetTxt(c, "GameplayUI/StreakText", Ink);

        // Serialized pipEmptyColor on the mode controller so runtime resets match
        var controller = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("pipEmptyColor");
            if (prop != null)
            {
                prop.colorValue = PipEmpty;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                _log.AppendLine("pipEmptyColor updated");
            }
            else _log.AppendLine("MISSING pipEmptyColor property");
        }
        else _log.AppendLine("MISSING FlashCardsModeController");

        bool saved = EditorSceneManager.SaveOpenScenes();
        _log.AppendLine("Saved=" + saved);
        return _log.ToString();
    }
}
