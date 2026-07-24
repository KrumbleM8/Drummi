using System.Collections;
using TMPro;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Presents the floor-transition moment between dungeon floors: covers the screen
    /// with the standard black wipe, shows a floor title card ("Floor 2", "Tutorial", …)
    /// for a real-time hold, then hides the card again once the next room's reveal
    /// completes (the card is a child of the wipe panel, so it slides off with it).
    ///
    /// Driven by DungeonRunner.FloorTransitionRoutine(). The reveal itself is NOT
    /// triggered here — RoomController's Entering state calls ScreenTransition.StartReveal()
    /// when the next room loads, exactly as it does for every room.
    /// </summary>
    public class FloorTransitionController : MonoBehaviour
    {
        [Tooltip("The SAME ScreenTransition panel instance RoomController/GameManager use. " +
                 "RoomController's Entering gate watches this object — wiring a different " +
                 "panel would leave the room waiting forever.")]
        [SerializeField] private ScreenTransition screenTransition;

        [Tooltip("Title card root — must be a CHILD of the ScreenTransition wipe panel so it " +
                 "rides the wipe on cover/reveal. Inactive by default.")]
        [SerializeField] private GameObject titleCardRoot;

        [SerializeField] private TMP_Text titleText;

        [Tooltip("Real-time seconds the title card holds on screen once the cover completes. " +
                 "Non-musical UI timing — same license as RoomController.introDuration.")]
        [SerializeField] private float titleHoldSeconds = 2f;

        /// <summary>
        /// Covers the screen with the wipe, shows the floor title card, and holds for
        /// <see cref="titleHoldSeconds"/> (real time). On completion the screen is still
        /// covered — the caller loads the next room, whose Entering state reveals the wipe.
        /// A watcher then deactivates the card after the reveal, so unrelated covers
        /// (game over, results) never show a stale title.
        /// </summary>
        /// <param name="floorTitle">Display name of the floor being entered.</param>
        public IEnumerator PlayCoverAndTitle(string floorTitle)
        {
            if (screenTransition == null)
            {
                Debug.LogError("[FloorTransitionController] screenTransition not assigned — skipping transition card.");
                yield break;
            }

            if (titleText != null) titleText.text = floorTitle;
            if (titleCardRoot != null) titleCardRoot.SetActive(true);

            screenTransition.StartCover();
            while (!screenTransition.IsScreenCovered)
                yield return null;

            yield return new WaitForSecondsRealtime(titleHoldSeconds);

            // Hide the card once the upcoming reveal finishes (started by the next
            // room's Entering state, after this coroutine returns).
            StartCoroutine(HideCardAfterReveal());
        }

        private IEnumerator HideCardAfterReveal()
        {
            // Wait for the reveal to begin, then to fully complete.
            while (screenTransition.IsScreenCovered)
                yield return null;
            while (screenTransition.IsTransitioning)
                yield return null;

            if (titleCardRoot != null) titleCardRoot.SetActive(false);
        }
    }
}
