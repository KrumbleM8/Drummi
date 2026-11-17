using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

public static class TapSnakeSign
{
    // Play-mode helper: taps the Snake sign's button. First call spins it into
    // focus; a second call (after the spin settles) launches the run.
    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[TapSnakeSign] Not in play mode.");
            return;
        }

        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node.Definition == null || node.Definition.displayName != "Snake") continue;
            var button = node.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.Log("[TapSnakeSign] Snake sign found but no Button child.");
                return;
            }
            button.onClick.Invoke();
            Debug.Log("[TapSnakeSign] Tapped Snake sign: " + node.name);
            return;
        }
        Debug.Log("[TapSnakeSign] No Snake sign found (menu hidden or definition mismatch).");
    }
}
