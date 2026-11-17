using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class FocusSign1
{
    public static string Execute()
    {
        var spinner = Object.FindObjectsByType<PlanetSpinner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spinner.Length == 0) return "No PlanetSpinner found.";
        spinner[0].FocusOn(1);
        return "FocusOn(1) called.";
    }
}
