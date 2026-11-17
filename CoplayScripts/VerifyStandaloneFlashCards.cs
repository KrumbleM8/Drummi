using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: standalone FlashCards regression. Execute = pre-check (no auto-start); PressPlay = legacy 1x entry.</summary>
public static class VerifyStandaloneFlashCards
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var log = new List<string>();
        log.Add($"Scene: {SceneManager.GetActiveScene().name}");
        log.Add($"PendingDefinition: {(FlashCardsModeController.PendingDefinition != null ? FlashCardsModeController.PendingDefinition.name : "null")}");

        var metronome = Object.FindAnyObjectByType<Metronome>();
        log.Add($"Metronome bpm: {(metronome != null ? metronome.bpm.ToString() : "MISSING")}");

        var transition = Object.FindAnyObjectByType<ScreenTransition>();
        log.Add($"Transition: covered={transition?.IsScreenCovered}, autoReveal={transition?.autoRevealOnStart}");
        return string.Join("\n", log);
    }

    public static string PressPlay()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var mode = Object.FindAnyObjectByType<FlashCardsModeController>();
        if (mode == null) return "ERROR: FlashCardsModeController not found";
        mode.StartFromMenu();
        return "Invoked StartFromMenu";
    }
}
