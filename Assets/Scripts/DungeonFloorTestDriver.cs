using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// PHASE 3 DEBUG HARNESS — orchestrates the per-encounter tap injectors across a
    /// mixed multi-room floor so a full run [Combat → FlashCard → C&amp;R → FlashCard]
    /// can be verified end-to-end without touch input.
    ///
    /// Per room type:
    ///   FlashCard — enables the scene's FlashCardTestRunner (event-armed; its pending
    ///               taps clear on encounter complete, so it is safe to leave enabled
    ///               across rooms).
    ///   C&amp;R   — AddComponent's a CallResponseTestRunner while the encounter runs,
    ///               destroys it when the encounter ends (event-armed, same safety).
    ///   Combat  — AddComponent's a CombatTestRunner only while standard combat is
    ///               active (no FlashCard/C&amp;R encounter running and the input
    ///               window armed); destroyed the moment either encounter type starts
    ///               so its poll-based arming can never fire stale taps into an
    ///               encounter room.
    ///
    /// Public fields so a play-mode editor script can AddComponent + wire at runtime.
    /// Editor-only tooling — never referenced by production code paths.
    /// </summary>
    public class DungeonFloorTestDriver : MonoBehaviour
    {
        public FlashCardController       flashCardController;
        public CallAndResponseController callResponseController;
        public DungeonInputReader        inputReader;
        public DungeonBeatManager        beatManager;
        public Metronome                 metronome;
        public FlashCardTestRunner       flashCardRunner;
        public bool                      runScenarios = true;

        private CallResponseTestRunner _crRunner;
        private GameObject             _crRunnerHost;
        private CombatTestRunner       _combatRunner;

        private void Update()
        {
            if (!runScenarios) return;

            bool fcActive = flashCardController    != null && flashCardController.IsRunning;
            bool crActive = callResponseController != null && callResponseController.IsRunning;

            // Flash Card runner: scene instance, kept enabled for the whole run.
            if (flashCardRunner != null && !flashCardRunner.enabled)
            {
                flashCardRunner.enabled = true;
                Debug.Log("[FloorTestDriver] FlashCardTestRunner enabled.");
            }

            // C&R runner: alive only while a C&R encounter runs.
            if (crActive && _crRunner == null)
            {
                // Create inactive → wire → activate: CallResponseTestRunner subscribes
                // to controller events in OnEnable, so its fields must be set BEFORE
                // the component is ever enabled (see harness doc comment).
                _crRunnerHost = new GameObject("CRTestRunnerHost");
                _crRunnerHost.transform.SetParent(transform);
                _crRunnerHost.SetActive(false);
                _crRunner = _crRunnerHost.AddComponent<CallResponseTestRunner>();
                _crRunner.controller  = callResponseController;
                _crRunner.inputReader = inputReader;
                _crRunner.metronome   = metronome;
                _crRunnerHost.SetActive(true);
                Debug.Log("[FloorTestDriver] CallResponseTestRunner created (inactive-wire-activate).");
            }
            else if (!crActive && _crRunner != null)
            {
                Destroy(_crRunnerHost);
                _crRunner     = null;
                _crRunnerHost = null;
                Debug.Log("[FloorTestDriver] CallResponseTestRunner destroyed.");
            }

            // Combat runner: only while standard combat is live. Destroyed the moment
            // any encounter type starts so stale poll-armed taps cannot leak.
            bool combatLive = !fcActive && !crActive
                              && inputReader != null && inputReader.EncounterActive;
            if (combatLive && _combatRunner == null)
            {
                _combatRunner = gameObject.AddComponent<CombatTestRunner>();
                _combatRunner.inputReader = inputReader;
                _combatRunner.beatManager = beatManager;
                _combatRunner.metronome   = metronome;
                Debug.Log("[FloorTestDriver] CombatTestRunner created.");
            }
            else if ((fcActive || crActive) && _combatRunner != null)
            {
                Destroy(_combatRunner);
                _combatRunner = null;
                Debug.Log("[FloorTestDriver] CombatTestRunner destroyed.");
            }
        }
    }
}
