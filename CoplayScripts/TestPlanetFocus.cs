using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE ONLY: spins the planet menu to sign index 1 (Double Time).</summary>
public static class TestPlanetFocus
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        GameObject pivot = GameObject.Find("PlanetPivot");
        if (pivot == null) return "FAIL: PlanetPivot not found.";
        PlanetSpinner spinner = pivot.GetComponent<PlanetSpinner>();
        spinner.FocusOn(1);
        return "FocusOn(1) called. CurrentAngle=" + spinner.CurrentAngle;
    }
}
