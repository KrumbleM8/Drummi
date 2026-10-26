using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Tiny-planet menu for the Home scene — the game's main scene,
    /// which also contains the Flash Cards gameplay systems. Signs stand on a
    /// 3D globe; PlanetSpinner rolls it (vertical drag + inertia + snap) past a
    /// menu camera that is static once HomeIntroDirector's title intro has
    /// flown it in (SetInteractive gates the menu meanwhile). Tapping
    /// a distant sign spins it into focus; tapping the focused sign hides the
    /// menu objects, shows the gameplay objects, and starts the run in-scene via
    /// FlashCardsModeController.StartRun (no scene load).
    ///
    /// INSPECTOR SETUP (Home scene):
    ///   nodes — sign nodes in angular order (index 0 = start focus); must match
    ///   the spinner's signMounts order. spinner — the PlanetSpinner pivot.
    ///   backButton — overlay back-to-home (wired to BackToHome()).
    ///   screenTransitioner — the shared OverlayUtilCanvas TransitionScreen (the
    ///   SAME ScreenTransition GameManager reveals after StartGame — a separate
    ///   menu panel would stay covered forever).
    ///   modeController — the scene's FlashCardsModeController.
    ///   menuObjects / gameplayObjects — roots toggled by the menu↔gameplay swap.
    ///
    /// SIGN PREVIEWS: each sign may carry a MenuSignPreview (how-to-play
    /// panel). This controller alone decides visibility — RefreshPreviews
    /// shows exactly the focused sign's panel when the globe IsSettled and
    /// the menu is interactive (not leaving, not in the intro) and hides
    /// every panel otherwise; MenuSignNode adds the locked gate.
    /// </summary>
    public class FlashCardsMenuController : MonoBehaviour
    {
        [Header("Signs")]
        [Tooltip("Sign nodes in angular order — index 0 is focused on Start. Must match the spinner's signMounts order.")]
        [SerializeField] private List<MenuSignNode> nodes = new();

        [Header("Planet")]
        [Tooltip("The planet pivot that spins signs into focus.")]
        [SerializeField] private PlanetSpinner spinner;

        [Header("Overlay Buttons")]
        [SerializeField] private Button backButton;

        [Header("Transitions (optional)")]
        [SerializeField] private ScreenTransition screenTransitioner;

        [Header("Gameplay (same scene)")]
        [SerializeField] private FlashCardsModeController modeController;

        [Tooltip("Active only while the planet menu shows: menu camera, planet, parallax layers, signs.")]
        [SerializeField] private List<GameObject> menuObjects = new();

        [Tooltip("Active only during a run: FlashCardsCanvas, FlashCardSystems, GameplayCamera.")]
        [SerializeField] private List<GameObject> gameplayObjects = new();

        /// <summary>
        /// Sign index to focus when this scene next loads (-1 = default 0). Set
        /// before a SceneLaunchDefinition leaves the scene so returning from the
        /// launched scene (e.g. Bongo) lands back on that sign. Consumed and reset
        /// in Start — statics survive domain-reload-off play sessions.
        /// </summary>
        public static int PendingFocusIndex = -1;

        private int  _index;

        // Latched while leaving the menu (gameplay running or scene transition
        // out) — blocks select; cleared by ReturnToMenuRoutine.
        private bool _leaving;

        private void Start()
        {
            foreach (MenuSignNode node in nodes)
                if (node != null) node.Selected += HandleNodeSelected;

            if (backButton != null) backButton.onClick.AddListener(BackToHome);
            if (spinner != null)
            {
                spinner.FocusedIndexChanged += HandleFocusedIndexChanged;
                spinner.SettledChanged      += HandleSettledChanged; // before SnapToCurrent — catches the first settle
            }

            _index = PendingFocusIndex >= 0 && nodes.Count > 0
                ? Mathf.Clamp(PendingFocusIndex, 0, nodes.Count - 1)
                : 0;
            PendingFocusIndex = -1;
            SnapToCurrent();
            UpdateFocusVisuals();
        }

        private void OnDestroy()
        {
            foreach (MenuSignNode node in nodes)
                if (node != null) node.Selected -= HandleNodeSelected;

            if (backButton != null) backButton.onClick.RemoveListener(BackToHome);
            if (spinner != null)
            {
                spinner.FocusedIndexChanged -= HandleFocusedIndexChanged;
                spinner.SettledChanged      -= HandleSettledChanged;
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────

        /// <summary>Spinner settled nearest a new sign (drag, inertia, or FocusOn tween).</summary>
        private void HandleFocusedIndexChanged(int index)
        {
            _index = Mathf.Clamp(index, 0, nodes.Count - 1);
            UpdateFocusVisuals();
        }

        private void SnapToCurrent()
        {
            if (spinner != null) spinner.SnapTo(_index);
        }

        private void UpdateFocusVisuals()
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null) nodes[i].SetFocused(i == _index);
            RefreshPreviews();
        }

        /// <summary>Globe came to rest on a sign, or started moving again.</summary>
        private void HandleSettledChanged(bool settled) => RefreshPreviews();

        /// <summary>
        /// Single decision point for the how-to-play panels: only the focused
        /// sign's, only while the globe rests on it and the menu is live.
        /// Safe to call at any time (nodes forward to an optional preview).
        /// </summary>
        private void RefreshPreviews()
        {
            bool show = !_leaving && spinner != null && spinner.IsSettled;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null) nodes[i].SetPreviewShown(show && i == _index);
        }

        // ── Launch / Exit ─────────────────────────────────────────────────────

        private void HandleNodeSelected(MenuSignNode node)
        {
            if (_leaving || node.Definition == null) return;

            // Drag release over a sign is not a tap.
            if (spinner != null && spinner.SuppressChildTap) return;

            // Tapping a distant sign spins it into focus; only the focused sign launches.
            int tappedIndex = nodes.IndexOf(node);
            if (tappedIndex != _index)
            {
                if (spinner != null && tappedIndex >= 0) spinner.FocusOn(tappedIndex);
                return;
            }

            // Locked signs can be focused and inspected but never launch.
            if (node.IsLocked)
            {
                node.PlayLockedShake();
                return;
            }

            _leaving = true;
            if (spinner != null) spinner.SetInputEnabled(false);
            RefreshPreviews(); // hide before the cover starts

            switch (node.Definition)
            {
                case FlashCardsModeDefinition flashCards:
                    StartCoroutine(LaunchRun(flashCards));
                    break;

                case SceneLaunchDefinition sceneLaunch:
                    // Remember this sign so the launched scene's exit lands back on it.
                    PendingFocusIndex = _index;
                    StartCoroutine(TransitionToScene(sceneLaunch.sceneName));
                    break;

                default:
                    Debug.LogError($"[FlashCardsMenu] Sign '{node.name}' has an unsupported definition type " +
                                   $"({node.Definition.GetType().Name}) — nothing launched.");
                    _leaving = false;
                    if (spinner != null) spinner.SetInputEnabled(true);
                    RefreshPreviews();
                    break;
            }
        }

        /// <summary>
        /// In-scene launch: cover the screen, swap menu→gameplay objects, start
        /// the run. GameManager's StartGameSequence sees the screen already
        /// covered (StartCover is a no-op then) and reveals into gameplay.
        /// </summary>
        private IEnumerator LaunchRun(FlashCardsModeDefinition definition)
        {
            FadeOutMenuMusic();
            if (screenTransitioner != null)
            {
                screenTransitioner.StartCover();
                while (!screenTransitioner.IsScreenCovered) yield return null;
            }

            SetMenuShown(false);
            modeController.StartRun(definition);
        }

        /// <summary>
        /// Results-page "Menu" button (after GameManager.ResetGameValues): cover,
        /// swap gameplay→menu objects, restore the focused sign, reveal.
        /// </summary>
        public void ReturnToMenuFromResults() => StartCoroutine(ReturnToMenuRoutine());

        private IEnumerator ReturnToMenuRoutine()
        {
            if (screenTransitioner != null)
            {
                screenTransitioner.StartCover();
                while (!screenTransitioner.IsScreenCovered) yield return null;
            }

            SetMenuShown(true);
            SnapToCurrent();
            UpdateFocusVisuals();
            if (spinner != null) spinner.SetInputEnabled(true);
            _leaving = false;
            RefreshPreviews(); // preview fades in under the reveal

            if (screenTransitioner != null) screenTransitioner.StartReveal();
        }

        /// <summary>
        /// Lock/unlock the whole menu (sign taps + planet drag). HomeIntroDirector
        /// locks it while the title banner shows and the camera flies in, then
        /// unlocks it once the planet has settled on sign 0.
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            _leaving = !interactive;
            if (spinner != null) spinner.SetInputEnabled(interactive);
            RefreshPreviews();
        }

        private void SetMenuShown(bool menuShown)
        {
            foreach (GameObject go in menuObjects)
                if (go != null) go.SetActive(menuShown);
            foreach (GameObject go in gameplayObjects)
                if (go != null) go.SetActive(!menuShown);
        }

        /// <summary>Back button: return to the home menu, skipping its intro.</summary>
        public void BackToHome()
        {
            if (_leaving) return;
            _leaving = true;

            if (spinner != null) spinner.SetInputEnabled(false);
            RefreshPreviews();
            HomeFlowController.SkipIntro = true;
            StartCoroutine(TransitionToScene("New_Home"));
        }

        private IEnumerator TransitionToScene(string sceneName)
        {
            // AudioManager persists across scenes (DontDestroyOnLoad) — stop any
            // menu music here so it never bleeds into the loaded scene. Explicit
            // != null: Unity's overloaded null check must run on the static.
            if (AudioManager.instance != null) AudioManager.instance.StopMusic();
            FadeOutMenuMusic();

            if (screenTransitioner != null)
            {
                // Same local-multiplier pattern as HomeFlowController — do NOT
                // mutate screenTransitioner.transitionDuration.
                screenTransitioner.StartCover();
                yield return new WaitForSecondsRealtime(screenTransitioner.transitionDuration * 1.13f);
            }
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Fade the menu music (MenuBeatClock on the MenuMusic menu object)
        /// under the cover so it never cuts hard when the menu objects
        /// deactivate or the scene unloads. Explicit != null: static access.
        /// </summary>
        private void FadeOutMenuMusic()
        {
            if (MenuBeatClock.Instance == null) return;
            float seconds = screenTransitioner != null ? screenTransitioner.transitionDuration : 0.3f;
            MenuBeatClock.Instance.FadeOut(seconds);
        }
    }
}
