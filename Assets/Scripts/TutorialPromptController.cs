using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Controls the "Play tutorial" toggle on the dungeon start page.
    ///
    /// First-ever run: the toggle is hidden — DungeonRunner.StartRun() forces the
    /// tutorial floor anyway (the completion flag is unset).
    /// After the tutorial has been completed once: the toggle is shown (off by
    /// default) and lets the player opt in to replaying the tutorial floor.
    /// </summary>
    public class TutorialPromptController : MonoBehaviour
    {
        [SerializeField] private DungeonRunner dungeonRunner;

        [Tooltip("Root of the toggle UI — hidden until the tutorial has been completed once.")]
        [SerializeField] private GameObject toggleRoot;

        [SerializeField] private Toggle replayTutorialToggle;

        private void OnEnable()
        {
            bool completed = DungeonRunner.TutorialCompleted;

            if (toggleRoot != null) toggleRoot.SetActive(completed);

            if (!completed)
            {
                // First run: tutorial is forced by StartRun(); no replay request needed.
                if (dungeonRunner != null) dungeonRunner.ReplayTutorialRequested = false;
                return;
            }

            if (replayTutorialToggle != null)
            {
                replayTutorialToggle.SetIsOnWithoutNotify(false);
                if (dungeonRunner != null) dungeonRunner.ReplayTutorialRequested = false;
            }
        }

        /// <summary>Wired to the Toggle's OnValueChanged event.</summary>
        public void OnReplayToggleChanged(bool isOn)
        {
            if (dungeonRunner != null) dungeonRunner.ReplayTutorialRequested = isOn;
        }
    }
}
