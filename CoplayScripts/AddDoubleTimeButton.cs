using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

public static class AddDoubleTimeButton
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        var mainPage   = Find("FlashCardsCanvas/MainPage");
        var playBtn    = Find("FlashCardsCanvas/MainPage/PlayButton");
        var homeBtn    = Find("FlashCardsCanvas/MainPage/HomeButton");
        var againBtn   = Find("FlashCardsCanvas/ScorePage/AgainButton");
        var controller = Object.FindObjectsByType<FlashCardsModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];

        // Guard against double-runs.
        if (mainPage.transform.Find("DoubleTimeButton") != null)
            return "DoubleTimeButton already exists — aborting.";

        // 1) Duplicate PlayButton -> DoubleTimeButton, between Play and Home.
        var dtGo = Object.Instantiate(playBtn, mainPage.transform);
        dtGo.name = "DoubleTimeButton";
        dtGo.transform.SetSiblingIndex(playBtn.transform.GetSiblingIndex() + 1);
        var dtRect = dtGo.GetComponent<RectTransform>();
        dtRect.anchoredPosition = new Vector2(0f, -170f); // 40px below Play (0,-20,h110)
        Undo.RegisterCreatedObjectUndo(dtGo, "Create DoubleTimeButton");

        // Label.
        var label = dtGo.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = "DOUBLE TIME"; sb.AppendLine($"Label set (fontSize={label.fontSize}, autoSize={label.enableAutoSizing})"); }
        else sb.AppendLine("WARNING: no TMP_Text child found on DoubleTimeButton");

        // onClick: StartFromMenu -> StartFromMenuDoubleTime.
        var dtClick = dtGo.GetComponent<Button>().onClick;
        while (dtClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(dtClick, 0);
        UnityEventTools.AddVoidPersistentListener(dtClick, controller.StartFromMenuDoubleTime);
        dtClick.SetPersistentListenerState(0, UnityEventCallState.RuntimeOnly);
        sb.AppendLine("DoubleTimeButton.onClick -> FlashCardsModeController.StartFromMenuDoubleTime");

        // 2) Push HomeButton down to keep 40px gaps.
        homeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -310f);
        sb.AppendLine("HomeButton moved to (0,-310)");

        // 3) AgainButton: [1] StartFromMenu -> RestartFromResults (keeps ResetGameValues at [0]).
        var againClick = againBtn.GetComponent<Button>().onClick;
        for (int i = againClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (againClick.GetPersistentMethodName(i) == "StartFromMenu")
                UnityEventTools.RemovePersistentListener(againClick, i);
        UnityEventTools.AddVoidPersistentListener(againClick, controller.RestartFromResults);
        againClick.SetPersistentListenerState(againClick.GetPersistentEventCount() - 1, UnityEventCallState.RuntimeOnly);
        sb.AppendLine($"AgainButton.onClick rewired ({againClick.GetPersistentEventCount()} listeners: " +
                      $"{againClick.GetPersistentMethodName(0)}, {againClick.GetPersistentMethodName(1)})");

        EditorUtility.SetDirty(mainPage);
        EditorUtility.SetDirty(homeBtn);
        EditorUtility.SetDirty(againBtn);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    private static GameObject Find(string path)
    {
        var parts = path.Split('/');
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        throw new System.Exception("Not found: " + path);
    }
}
