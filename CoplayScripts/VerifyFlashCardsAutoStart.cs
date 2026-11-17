using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: after launching from the signpost menu, verifies the FlashCards scene auto-started with the right variant.</summary>
public static class VerifyFlashCardsAutoStart
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var log = new List<string>();

        log.Add($"Scene: {SceneManager.GetActiveScene().name}");
        log.Add($"PendingDefinition: {(FlashCardsModeController.PendingDefinition != null ? FlashCardsModeController.PendingDefinition.name : "null (consumed)")}");

        var metronome = Object.FindAnyObjectByType<Metronome>();
        log.Add($"Metronome: bpm={(metronome != null ? metronome.bpm.ToString() : "MISSING")}, enabled={(metronome != null && metronome.enabled)}");

        var evaluator = Object.FindAnyObjectByType<DungeonEvaluator>();
        if (evaluator != null)
        {
            var so = new SerializedObject(evaluator);
            var p = so.FindProperty("highScoreKey");
            log.Add($"Evaluator highScoreKey: {(p != null ? p.stringValue : "PROP NOT FOUND")}");
        }
        else log.Add("Evaluator: MISSING");

        var gm = GameManager.instance;
        log.Add($"GameManager.ActiveMode: {(gm != null && gm.ActiveMode != null ? gm.ActiveMode.ModeId : "null")}");

        var audio = AudioManager.instance;
        log.Add($"AudioManager: selectedSongIndex={(audio != null ? audio.selectedSongIndex.ToString() : "MISSING")}");

        return string.Join("\n", log);
    }
}
