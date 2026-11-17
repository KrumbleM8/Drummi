using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>Reports the FlashCard rig state in the open scene (works in edit or play mode).</summary>
public static class InspectFlashCardRig
{
    public static string Execute()
    {
        var ctrl   = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        var runner = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        var view   = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);

        string c = ctrl   == null ? "controller: MISSING" : $"controller: '{ctrl.gameObject.name}' activeInHierarchy={ctrl.gameObject.activeInHierarchy} enabled={ctrl.enabled}";
        string r = runner == null ? "runner: MISSING"     : $"runner: '{runner.gameObject.name}' activeInHierarchy={runner.gameObject.activeInHierarchy} enabled={runner.enabled}";
        string v = view   == null ? "view: MISSING"       : $"view: '{view.gameObject.name}' activeInHierarchy={view.gameObject.activeInHierarchy} enabled={view.enabled}";
        return $"{c} | {r} | {v}";
    }
}
