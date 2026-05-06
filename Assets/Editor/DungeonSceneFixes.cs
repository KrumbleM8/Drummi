using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;

/// <summary>
/// Fixes the Dungeon scene so it runs standalone:
///   1. Rebuilds StartButton OnClick: SetMusic(0) → SetMode("Dungeon") → StartGame()
///   2. Sets Metronome.bpm default to 120 so timing is never 0/NaN at startup
/// </summary>
public class DungeonSceneFixes
{
    public static void Execute()
    {
        FixStartButton();
        FixMetronomeBpm();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[DungeonSceneFixes] Done.");
    }

    // ── StartButton ───────────────────────────────────────────────────────

    private static void FixStartButton()
    {
        var btnGO = GameObject.Find("Canvas/StartButton");
        if (btnGO == null) { Debug.LogError("[DungeonSceneFixes] StartButton not found."); return; }

        var btn = btnGO.GetComponent<Button>();
        if (btn == null) { Debug.LogError("[DungeonSceneFixes] Button component missing."); return; }

        var gmGO = GameObject.Find("GameManager");
        if (gmGO == null) { Debug.LogError("[DungeonSceneFixes] GameManager not found."); return; }

        var gm = gmGO.GetComponent<GameManager>();

        // Clear all existing listeners so we can set the right order
        var so = new SerializedObject(btn);
        var onClickProp = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        onClickProp.ClearArray();
        so.ApplyModifiedProperties();

        // 1. SetMusic(0)  — must run before StartGame so BPM is known
        UnityEventTools.AddIntPersistentListener(btn.onClick, gm.SetMusic, 0);

        // 2. SetMode("Dungeon")
        UnityEventTools.AddStringPersistentListener(btn.onClick, gm.SetMode, "Dungeon");

        // 3. StartGame()
        UnityEventTools.AddVoidPersistentListener(btn.onClick, gm.StartGame);

        EditorUtility.SetDirty(btn);
        Debug.Log("[DungeonSceneFixes] StartButton OnClick: SetMusic(0) → SetMode(\"Dungeon\") → StartGame()");
    }

    public static void SetMetronomeBpm111()
    {
        var met = GameObject.Find("Metronome")?.GetComponent<Metronome>();
        if (met == null) { Debug.LogError("[DungeonSceneFixes] Metronome not found."); return; }
        met.bpm = 111.0;
        EditorUtility.SetDirty(met);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[DungeonSceneFixes] Metronome.bpm set to 111.");
    }

    // ── Metronome default BPM ─────────────────────────────────────────────

    private static void FixMetronomeBpm()
    {
        var metGO = GameObject.Find("Metronome");
        if (metGO == null) { Debug.LogError("[DungeonSceneFixes] Metronome not found."); return; }

        var met = metGO.GetComponent<Metronome>();
        var so  = new SerializedObject(met);

        // Only set if it's currently 0 (uninitialized)
        var bpmProp = so.FindProperty("bpm");
        if (bpmProp != null && bpmProp.doubleValue == 0.0)
        {
            bpmProp.doubleValue = 120.0;
            so.ApplyModifiedProperties();
            Debug.Log("[DungeonSceneFixes] Metronome.bpm defaulted to 120.");
        }
        else
        {
            // bpm is a public field — try setting it directly
            if (met.bpm == 0) met.bpm = 120;
            EditorUtility.SetDirty(met);
            Debug.Log($"[DungeonSceneFixes] Metronome.bpm set to {met.bpm}.");
        }
    }
}
