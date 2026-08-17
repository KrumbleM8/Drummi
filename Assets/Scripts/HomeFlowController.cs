using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Drives the New_Home menu flow: Curtain → Title/TapToStart → BarsMenu.
    ///
    /// SCENE SETUP:
    ///   1. Wire CurtainController.onRevealed → OnCurtainRevealed (title entrance + theme music).
    ///   2. Wire TapStartFillBar.onFilled and the TapCatcher button → StartGame.
    ///   3. Wire each menu bar's Button → the matching GoTo*Scene method.
    /// </summary>
    public class HomeFlowController : MonoBehaviour
    {
        /// <summary>
        /// Set before loading New_Home (e.g. from a pause menu) to skip the
        /// curtain + tap-to-start intro and jump straight to the bars menu.
        /// Consumed and reset in Awake.
        /// </summary>
        public static bool SkipIntro;

        [Header("Curtain")]
        [Tooltip("Root GameObject holding the CurtainController. Disabled entirely when the intro is skipped.")]
        [SerializeField] private GameObject curtainRoot;

        [Header("Title")]
        [Tooltip("RectTransform of the title text that animates in and later docks at the top.")]
        [SerializeField] private RectTransform titleRect;

        [Tooltip("Empty RectTransform marking where the title docks after Start is pressed.")]
        [SerializeField] private RectTransform titleDockTarget;

        [Tooltip("Scale of the title once docked at the top.")]
        [SerializeField] private float titleDockScale = 0.5f;

        [Header("Tap To Start")]
        [Tooltip("Root holding the TapToStart label, fill bar and tap catcher. Hidden when the game starts.")]
        [SerializeField] private GameObject tapToStartRoot;

        [Header("Bars Menu")]
        [SerializeField] private BarsMenuController barsMenu;

        [Header("Frame Zoom")]
        [Tooltip("Decorative stage-frame overlay. After the curtain reveal it grows out of vision, as if entering the stage.")]
        [SerializeField] private RectTransform frameRect;

        [Tooltip("Delay after the curtain reveal before the frame starts growing.")]
        [SerializeField] private float frameZoomDelay = 0.5f;

        [Tooltip("Duration of the frame grow-out.")]
        [SerializeField] private float frameZoomDuration = 1.2f;

        [Tooltip("Scale at which the frame is fully out of vision.")]
        [SerializeField] private float frameZoomScale = 6f;

        [Header("Scene Transition")]
        [SerializeField] private ScreenTransition screenTransitioner;

        [Header("Music")]
        [Tooltip("Placeholder theme music slot. Plays (looping) on curtain reveal when assigned.")]
        [SerializeField] private AudioClip themeMusic;

        [Header("Animation")]
        [Tooltip("Duration of the title entrance pop.")]
        [SerializeField] private float titleEntranceDuration = 0.8f;

        [Tooltip("Duration of the title shrink/dock move.")]
        [SerializeField] private float titleDockDuration = 0.5f;

        // ── State ─────────────────────────────────────────────────────────────

        private bool _started;
        private bool _leaving;
        private bool _skipIntro;

        // ── Skip Intro ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (!SkipIntro) return;
            SkipIntro  = false;
            _skipIntro = true;

            // Disable the curtain before its Start() coroutine can run,
            // so it never hides the menu content or subscribes to input.
            if (curtainRoot != null)
                curtainRoot.SetActive(false);
        }

        private void Start()
        {
            if (!_skipIntro) return;

            Canvas.ForceUpdateCanvases();

            if (frameRect != null)
                frameRect.gameObject.SetActive(false);

            if (tapToStartRoot != null)
                tapToStartRoot.SetActive(false);

            // Title snaps straight to its docked position/scale.
            if (titleRect != null && titleDockTarget != null)
            {
                titleRect.position   = titleDockTarget.position;
                titleRect.localScale = Vector3.one * titleDockScale;
            }

            _started = true;

            if (barsMenu != null)
                barsMenu.PlayEntrance();

            PlayThemeMusic();
        }

        // ── Curtain → Title ───────────────────────────────────────────────────

        /// <summary>Wired to CurtainController.onRevealed.</summary>
        public void OnCurtainRevealed()
        {
            if (titleRect != null)
            {
                titleRect.localScale = Vector3.zero;
                titleRect.DOScale(1f, titleEntranceDuration).SetEase(Ease.OutBack);
            }

            // Enter the stage: after a brief delay the frame grows past the
            // screen edges, then is disabled.
            if (frameRect != null)
            {
                frameRect.DOScale(frameZoomScale, frameZoomDuration)
                         .SetDelay(frameZoomDelay)
                         .SetEase(Ease.InQuad)
                         .OnComplete(() => frameRect.gameObject.SetActive(false));
            }

            PlayThemeMusic();
        }

        private void PlayThemeMusic()
        {
            if (themeMusic == null)
            {
                Debug.Log("[HomeFlow] No theme music assigned — skipping (placeholder hook).");
                return;
            }

            if (AudioManager.instance == null)
            {
                Debug.LogWarning("[HomeFlow] AudioManager missing — cannot play theme music.");
                return;
            }

            // Direct speaker use: PlayMusic() indexes musicTracks[selectedSongIndex],
            // which is unrelated to the home theme.
            AudioSource music = AudioManager.instance.speakers[0];
            music.clip = themeMusic;
            music.loop = true;
            music.Play();
        }

        // ── Title → Bars ──────────────────────────────────────────────────────

        /// <summary>Wired to TapStartFillBar.onFilled and the TapCatcher button.</summary>
        public void StartGame()
        {
            if (_started) return;
            _started = true;

            if (tapToStartRoot != null)
                tapToStartRoot.SetActive(false);

            if (titleRect != null && titleDockTarget != null)
            {
                titleRect.DOMove(titleDockTarget.position, titleDockDuration).SetEase(Ease.InOutQuad);
                titleRect.DOScale(titleDockScale, titleDockDuration).SetEase(Ease.InOutQuad);
            }

            if (barsMenu != null)
                barsMenu.PlayEntrance();
        }

        // ── Bars → Scenes ─────────────────────────────────────────────────────

        public void GoToBongoScene()      => TransitionToScene("Bongo");
        public void GoToDungeonScene()    => TransitionToScene("Dungeon");
        public void GoToGardenScene()     => TransitionToScene("Garden");
        public void GoToFlashCardsScene() => TransitionToScene("FlashCards");
        public void GoToFlickScene()      => TransitionToScene("Flick");

        private void TransitionToScene(string sceneName)
        {
            if (_leaving) return;
            _leaving = true;
            StartCoroutine(TransitionToSceneCoroutine(sceneName));
        }

        private IEnumerator TransitionToSceneCoroutine(string sceneName)
        {
            screenTransitioner.StartCover();
            // Local multiplier — do NOT mutate screenTransitioner.transitionDuration
            // (UIMenuManager has a compounding *= bug we deliberately avoid here).
            yield return new WaitForSeconds(screenTransitioner.transitionDuration * 1.13f);
            SceneManager.LoadScene(sceneName);
        }
    }
}
