using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Play-mode test driver: swaps DungeonRunner's floors for the flick debug floor and
/// starts a run. Execute() keeps the player alive via a health top-up so a no-input
/// session can reach RoomLengthBars completion; ExecuteNoHeal() exercises death.
/// </summary>
public static class RunFlickFloorTest
{
    private const string FloorPath = "Assets/ScriptableObjects/Floors/FloorDebug_Flick.asset";
    private static EditorApplication.CallbackFunction _healer;

    public static void Execute()      { Run(true); }
    public static void ExecuteNoHeal(){ Run(false); }

    private static void Run(bool heal)
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[FlickFloorTest] Enter play mode first."); return; }

        var runner = Object.FindObjectOfType<DungeonRunner>(true);
        var floor  = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(FloorPath);
        if (runner == null || floor == null) { Debug.LogError($"[FlickFloorTest] runner={runner} floor={floor}"); return; }

        var floorsField = typeof(DungeonRunner).GetField("floors", BindingFlags.NonPublic | BindingFlags.Instance);
        floorsField.SetValue(runner, new List<DungeonFloorDefinition> { floor });
        Debug.Log("[FlickFloorTest] Floors replaced with FloorDebug_Flick");

        if (_healer != null) { EditorApplication.update -= _healer; _healer = null; }
        if (heal)
        {
            var health = Object.FindObjectOfType<DungeonHealth>(true);
            _healer = HealTick;
            _healTarget = health;
            EditorApplication.update += _healer;
        }

        // Start via the REAL start button so the full flow runs (SetMusic, SetMode,
        // MainMenu.SetActive(false), StartDungeonRun) — bypassing it leaves the main
        // menu active and raycast-blocking over the gameplay area.
        UnityEngine.UI.Button startBtn = null;
        foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>(true))
            if (b.name == "StartButton (1)") { startBtn = b; break; }
        if (startBtn == null) { Debug.LogError("[FlickFloorTest] StartButton (1) not found"); return; }
        startBtn.onClick.Invoke();
        Debug.Log($"[FlickFloorTest] Run started via StartButton (heal={heal})");
    }

    private static DungeonHealth _healTarget;

    private static void HealTick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= _healer;
            _healer = null;
            return;
        }
        if (_healTarget != null && _healTarget.CurrentHealth > 0 && _healTarget.CurrentHealth < 40)
        {
            _healTarget.ResetHealth();
            Debug.Log("[FlickFloorTest] Health topped up (test keep-alive)");
        }
    }
}
