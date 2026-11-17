using UnityEngine;
using UnityEngine.UI;

/// <summary>Starts a dungeon run via the real start button — no floor injection.</summary>
public static class StartRealRun
{
    public static void Execute()
    {
        foreach (var b in Object.FindObjectsOfType<Button>(true))
            if (b.name == "StartButton (1)") { b.onClick.Invoke(); Debug.Log("[StartRealRun] clicked"); return; }
        Debug.LogError("[StartRealRun] StartButton (1) not found");
    }
}
