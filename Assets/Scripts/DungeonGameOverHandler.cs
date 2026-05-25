using UnityEngine;

/// <summary>
/// Listens for DungeonHealth.OnHealthDepleted and drives the game-over flow:
///   1. Calls GameManager.TriggerGameOver() to immediately start the results sequence,
///      decoupled from beat timing.
///   2. Optionally plays a game-over audio sting via AudioManager.
/// When a DungeonRunner run is active, RoomController owns failure routing — this handler
/// defers to avoid double navigation.
/// </summary>
public class DungeonGameOverHandler : MonoBehaviour
{
    [SerializeField] private DungeonHealth health;

    [Tooltip("When a DungeonRunner run is active it owns the failure routing. " +
             "Assign the runner here so this handler defers to it and avoids double-routing.")]
    [SerializeField] private DungeonRunner runner;

    private void Awake()
    {
        if (health != null)
            health.OnHealthDepleted += HandleHealthDepleted;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnHealthDepleted -= HandleHealthDepleted;
    }

    private void HandleHealthDepleted()
    {
        // When a run is active DungeonRunner owns the failure routing via RoomResult.survived.
        // Defer to it to prevent double UI navigation.
        if (runner != null && runner.IsRunActive) return;

        // Trigger results immediately — bypasses waiting for the beat cycle to complete.
        // ShowResultsSequence handles cleanup (stopping beat manager, saving score, etc.)
        // and covers + reveals the screen before showing the Score page.
        GameManager.instance?.TriggerGameOver();

        // Play game-over sting (optional — only if AudioManager and clip are assigned)
        if (AudioManager.instance != null)
            AudioManager.instance.PlayGameOver();
    }
}
