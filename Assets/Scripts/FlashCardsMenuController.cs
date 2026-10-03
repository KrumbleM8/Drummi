using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Signpost menu for the FlashCardsMenu scene — the game's main scene, which
    /// also contains the Flash Cards gameplay systems. The camera snaps between
    /// the authored anchors of an ordered list of MenuSignNodes via chevron
    /// buttons; tapping the focused sign hides the menu objects, shows the
    /// gameplay objects, and starts the run in-scene via
    /// FlashCardsModeController.StartRun (no scene load).
    ///
    /// All camera motion is DOTween (no Time.time animation). Tweens are
    /// interruptible: a chevron tap mid-tween DOKills the camera and retargets.
    ///
    /// INSPECTOR SETUP (FlashCardsMenu scene):
    ///   nodes — sign nodes in path order. menuCamera — the menu camera.
    ///   nextButton / prevButton — overlay chevrons (wired to Next()/Prev()).
    ///   backButton — overlay back-to-home (wired to BackToHome()).
    ///   screenTransitioner — the shared OverlayUtilCanvas TransitionScreen (the
    ///   SAME ScreenTransition GameManager reveals after StartGame — a separate
    ///   menu panel would stay covered forever).
    ///   modeController — the scene's FlashCardsModeController.
    ///   menuObjects / gameplayObjects — roots toggled by the menu↔gameplay swap.
    /// </summary>
    public class FlashCardsMenuController : MonoBehaviour
    {
        [Header("Signs")]
        [Tooltip("Sign nodes in path order — index 0 is focused on Start.")]
        [SerializeField] private List<MenuSignNode> nodes = new();

        [Header("Camera")]
        [SerializeField] private Camera menuCamera;
        [SerializeField, Min(0.01f)] private float tweenDuration = 0.35f;

        [Header("Overlay Buttons")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button backButton;

        [Header("Transitions (optional)")]
        [SerializeField] private ScreenTransition screenTransitioner;

        [Header("Gameplay (same scene)")]
        [SerializeField] private FlashCardsModeController modeController;

        [Tooltip("Active only while the signpost menu shows: menu camera, landscape, signs, chevron canvas.")]
        [SerializeField] private List<GameObject> menuObjects = new();

        [Tooltip("Active only during a run: FlashCardsCanvas, FlashCardSystems, GameplayCamera.")]
        [SerializeField] private List<GameObject> gameplayObjects = new();

        private int  _index;

        // Latched while leaving the menu (gameplay running or scene transition
        // out) — blocks Step/select; cleared by ReturnToMenuRoutine.
        private bool _leaving;

        private void Start()
        {
            foreach (MenuSignNode node in nodes)
                if (node != null) node.Selected += HandleNodeSelected;

            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (prevButton != null) prevButton.onClick.AddListener(Prev);
            if (backButton != null) backButton.onClick.AddListener(BackToHome);

            _index = 0;
            SnapToCurrent();
            UpdateChrome();
        }

        private void OnDestroy()
        {
            foreach (MenuSignNode node in nodes)
                if (node != null) node.Selected -= HandleNodeSelected;

            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (prevButton != null) prevButton.onClick.RemoveListener(Prev);
            if (backButton != null) backButton.onClick.RemoveListener(BackToHome);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        public void Next() => Step(1);
        public void Prev() => Step(-1);

        /// <summary>Move focus by delta signs (clamped) and tween the camera there.</summary>
        public void Step(int delta)
        {
            if (_leaving || nodes.Count == 0 || menuCamera == null) return;

            int target = Mathf.Clamp(_index + delta, 0, nodes.Count - 1);
            if (target == _index) return;

            _index = target;
            Transform anchor = nodes[_index].CameraAnchor;

            // Interruptible: kill any in-flight move and retarget from wherever
            // the camera currently is.
            Transform cam = menuCamera.transform;
            cam.DOKill();
            cam.DOMove(anchor.position, tweenDuration).SetEase(Ease.OutExpo).SetLink(gameObject);
            cam.DORotateQuaternion(anchor.rotation, tweenDuration).SetEase(Ease.OutExpo).SetLink(gameObject);

            UpdateChrome();
        }

        private void SnapToCurrent()
        {
            if (nodes.Count == 0 || menuCamera == null) return;
            Transform anchor = nodes[_index].CameraAnchor;
            menuCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private void UpdateChrome()
        {
            if (prevButton != null) prevButton.gameObject.SetActive(_index > 0);
            if (nextButton != null) nextButton.gameObject.SetActive(_index < nodes.Count - 1);

            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null) nodes[i].SetFocused(i == _index);
        }

        // ── Launch / Exit ─────────────────────────────────────────────────────

        private void HandleNodeSelected(MenuSignNode node)
        {
            if (_leaving || node.Definition == null) return;
            _leaving = true;

            StartCoroutine(LaunchRun(node.Definition));
        }

        /// <summary>
        /// In-scene launch: cover the screen, swap menu→gameplay objects, start
        /// the run. GameManager's StartGameSequence sees the screen already
        /// covered (StartCover is a no-op then) and reveals into gameplay.
        /// </summary>
        private IEnumerator LaunchRun(FlashCardsModeDefinition definition)
        {
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
            UpdateChrome();
            _leaving = false;

            if (screenTransitioner != null) screenTransitioner.StartReveal();
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

            HomeFlowController.SkipIntro = true;
            StartCoroutine(TransitionToScene("New_Home"));
        }

        private IEnumerator TransitionToScene(string sceneName)
        {
            if (screenTransitioner != null)
            {
                // Same local-multiplier pattern as HomeFlowController — do NOT
                // mutate screenTransitioner.transitionDuration.
                screenTransitioner.StartCover();
                yield return new WaitForSecondsRealtime(screenTransitioner.transitionDuration * 1.13f);
            }
            SceneManager.LoadScene(sceneName);
        }
    }
}
