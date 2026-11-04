using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Sits on each scene's OverlayUtilCanvas. Manages the pause/settings panel
    /// (slide-up animation, resume count-in, volume, vibration), the back key and
    /// app-backgrounding triggers, and provides scene-load entry points that
    /// mirror UIMenuManager's GoTo* methods.
    /// </summary>
    public class OverlayUIController : MonoBehaviour
    {
        [Header("Settings Panel")]
        [Tooltip("Root RectTransform of the settings panel. Designer should position it on-screen; the controller hides it below on startup.")]
        [SerializeField] private RectTransform settingsPanel;
        [SerializeField] private float panelSlideDuration = 0.25f;
        [SerializeField] private AnimationCurve panelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Restart button root. Hidden while a mode is selected but idle at its own menu (Home globe, song select).")]
        [SerializeField] private GameObject restartButton;
        [Tooltip("Big 3-2-1 number shown before a paused run resumes. Optional; without it Resume is instant.")]
        [SerializeField] private TMP_Text countInText;
        [Tooltip("Root toggled with the count-in (disc + number). Defaults to the text's own object.")]
        [SerializeField] private GameObject countInRoot;

        [Header("Settings Widgets")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle vibrationToggle;

        [Header("Audio Mixer")]
        [Tooltip("Optional AudioMixer with exposed MusicVolume and SFXVolume parameters.")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string musicVolumeParam = "MusicVolume";
        [SerializeField] private string sfxVolumeParam   = "SFXVolume";

        [Header("Pause")]
        [Tooltip("Optional. When assigned, opening/closing settings automatically pauses and resumes the game. Leave null on scenes with no gameplay.")]
        [SerializeField] private PauseHandler pauseHandler;

        [Header("Navigation")]
        [Tooltip("Scene the settings panel's Main Menu button returns to. Default Home (the FlashCards " +
                 "globe). Override only for a scene that should return somewhere else.")]
        [SerializeField] private string homeSceneName = "Home";

        [Header("Scene Transition")]
        [SerializeField] private ScreenTransition screenTransitioner;

        // ── PlayerPrefs keys ───────────────────────────────────────────────────
        private const string MusicVolKey  = "settings_music_vol";
        private const string SfxVolKey    = "settings_sfx_vol";
        private const string VibrationKey = "settings_vibration";

        // ── Panel state ────────────────────────────────────────────────────────
        private bool       _settingsOpen;
        private bool       _countingIn;
        private Coroutine  _panelCoroutine;
        private Vector2    _panelShownPos;
        private Vector2    _panelHiddenPos;

        private static bool GameplayRunning => GameManager.instance != null && GameManager.instance.IsPlaying;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (settingsPanel != null)
            {
                Canvas.ForceUpdateCanvases();
                _panelShownPos  = settingsPanel.anchoredPosition;
                _panelHiddenPos = new Vector2(_panelShownPos.x, _panelShownPos.y - settingsPanel.rect.height - 100f);
                settingsPanel.anchoredPosition = _panelHiddenPos;
                settingsPanel.gameObject.SetActive(false);
            }
            if (countInText != null && countInRoot == null) countInRoot = countInText.gameObject;
            if (countInRoot != null) countInRoot.SetActive(false);

            if (musicSlider     != null) musicSlider.SetValueWithoutNotify(GetMusicVolume());
            if (sfxSlider       != null) sfxSlider.SetValueWithoutNotify(GetSfxVolume());
            if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(GetVibration());

            LoadAndApplySettings();
        }

        private void Update()
        {
            // Android's back button arrives as the Escape key.
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || _countingIn) return;
            if (_settingsOpen) Resume();
            else OpenSettings();
        }

        private void OnApplicationPause(bool paused)
        {
            // Switching apps or taking a call mid-run: freeze the run and show the menu.
            if (paused && pauseHandler != null && GameplayRunning && !_settingsOpen && !_countingIn)
                OpenSettings();
        }

        // ── Settings Panel ─────────────────────────────────────────────────────

        /// <summary>Opens the settings panel with a slide-up animation and pauses the game if a PauseHandler is assigned.</summary>
        public void OpenSettings()
        {
            if (_settingsOpen || _countingIn) return;
            _settingsOpen = true;
            pauseHandler?.TogglePause();
            // Restart is pointless while a mode is selected but idle at its own menu.
            if (restartButton != null)
                restartButton.SetActive(GameManager.instance == null || GameManager.instance.ActiveMode == null || GameplayRunning);
            AnimatePanel(show: true);
        }

        /// <summary>Closes the settings panel with a slide-down animation and resumes the game immediately if a PauseHandler is assigned.</summary>
        public void CloseSettings()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            pauseHandler?.TogglePause();
            AnimatePanel(show: false);
        }

        /// <summary>Resume button: counts 3-2-1 on the beat when a run is in progress, otherwise closes immediately.</summary>
        public void Resume()
        {
            if (!_settingsOpen || _countingIn) return;
            if (pauseHandler == null || countInText == null || !GameplayRunning)
            {
                CloseSettings();
                return;
            }
            _settingsOpen = false;
            AnimatePanel(show: false);
            StartCoroutine(CountInRoutine());
        }

        /// <summary>Toggles the settings panel open or closed.</summary>
        public void ToggleSettings()
        {
            if (_settingsOpen) Resume();
            else OpenSettings();
        }

        private IEnumerator CountInRoutine()
        {
            _countingIn = true;
            float step = Mathf.Clamp((float)pauseHandler.BeatInterval, 0.4f, 1f);
            countInRoot.SetActive(true);
            for (int n = 3; n >= 1; n--)
            {
                countInText.text = n.ToString();
                yield return new WaitForSecondsRealtime(step);
            }
            countInRoot.SetActive(false);
            _countingIn = false;
            pauseHandler.TogglePause();
        }

        private void AnimatePanel(bool show)
        {
            if (settingsPanel == null) return;
            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);
            _panelCoroutine = StartCoroutine(PanelRoutine(show));
        }

        private IEnumerator PanelRoutine(bool show)
        {
            settingsPanel.gameObject.SetActive(true);

            Vector2 from = show ? _panelHiddenPos : _panelShownPos;
            Vector2 to   = show ? _panelShownPos  : _panelHiddenPos;

            float elapsed = 0f;
            while (elapsed < panelSlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = panelCurve.Evaluate(Mathf.Clamp01(elapsed / panelSlideDuration));
                settingsPanel.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }

            settingsPanel.anchoredPosition = to;
            if (!show) settingsPanel.gameObject.SetActive(false);
            _panelCoroutine = null;
        }

        // ── Volume ─────────────────────────────────────────────────────────────

        /// <summary>Sets music volume. Pass a normalised value 0–1 (e.g. from a UI Slider).</summary>
        public void SetMusicVolume(float normalised)
        {
            normalised = Mathf.Clamp01(normalised);
            PlayerPrefs.SetFloat(MusicVolKey, normalised);
            ApplyMixerVolume(musicVolumeParam, normalised);
        }

        /// <summary>Sets SFX volume. Pass a normalised value 0–1.</summary>
        public void SetSfxVolume(float normalised)
        {
            normalised = Mathf.Clamp01(normalised);
            PlayerPrefs.SetFloat(SfxVolKey, normalised);
            ApplyMixerVolume(sfxVolumeParam, normalised);
        }

        /// <summary>Returns the saved music volume (0–1), defaulting to 1.</summary>
        public float GetMusicVolume() => PlayerPrefs.GetFloat(MusicVolKey, 1f);

        /// <summary>Returns the saved SFX volume (0–1), defaulting to 1.</summary>
        public float GetSfxVolume() => PlayerPrefs.GetFloat(SfxVolKey, 1f);

        private void ApplyMixerVolume(string param, float normalised)
        {
            if (audioMixer == null) return;
            float dB = normalised > 0.0001f ? Mathf.Log10(normalised) * 20f : -80f;
            audioMixer.SetFloat(param, dB);
        }

        // ── Vibration ──────────────────────────────────────────────────────────

        /// <summary>Enables or disables haptic feedback. Persisted via PlayerPrefs.</summary>
        public void SetVibration(bool enabled)
        {
            PlayerPrefs.SetInt(VibrationKey, enabled ? 1 : 0);
        }

        /// <summary>Returns the saved vibration preference, defaulting to on.</summary>
        public bool GetVibration() => PlayerPrefs.GetInt(VibrationKey, 1) == 1;

        /// <summary>Toggle handler wired to a UI Toggle's OnValueChanged event.</summary>
        public void OnVibrationToggleChanged(bool value) => SetVibration(value);

        // ── Persistence ────────────────────────────────────────────────────────

        private void LoadAndApplySettings()
        {
            ApplyMixerVolume(musicVolumeParam, PlayerPrefs.GetFloat(MusicVolKey, 1f));
            ApplyMixerVolume(sfxVolumeParam,   PlayerPrefs.GetFloat(SfxVolKey,   1f));
        }

        // ── Scene Loading ──────────────────────────────────────────────────────

        /// <summary>
        /// Transitions to this scene's home menu (homeSceneName — Home, the FlashCards globe, by default).
        /// If the target is New_Home its curtain/tap-to-start
        /// intro is skipped.
        /// </summary>
        public void GoToHomeScene()
        {
            HomeFlowController.SkipIntro = true;
            StartCoroutine(SceneTransitionRoutine(string.IsNullOrEmpty(homeSceneName) ? "Home" : homeSceneName));
        }

        /// <summary>Transitions to the FlashCards globe menu (the game's main scene).</summary>
        public void GoToFlashCardsScene() => StartCoroutine(SceneTransitionRoutine("Home"));

        /// <summary>Reloads the current scene, restarting the active game mode from its own menu.</summary>
        public void RestartScene()
        {
            // Flash Cards runs live inside Home: a restart there must land on
            // the sign menu, not replay the title intro (HomeIntroDirector
            // consumes this flag, like the Home buttons above).
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "Home") HomeFlowController.SkipIntro = true;
            StartCoroutine(SceneTransitionRoutine(sceneName));
        }

        /// <summary>Transitions to the Arcade scene.</summary>
        public void GoToArcadeScene()  => StartCoroutine(SceneTransitionRoutine("Arcade"));

        /// <summary>Transitions to the Garden scene.</summary>
        public void GoToGardenScene()  => StartCoroutine(SceneTransitionRoutine("Garden"));

        /// <summary>Transitions to the Dungeon scene.</summary>
        public void GoToDungeonScene() => StartCoroutine(SceneTransitionRoutine("Dungeon"));

        /// <summary>Transitions to the Bongo scene.</summary>
        public void GoToBongoScene()   => StartCoroutine(SceneTransitionRoutine("Bongo"));

        private IEnumerator SceneTransitionRoutine(string sceneName)
        {
            // If the game is paused, resume it before transitioning so that
            // AudioManager, GameClock, and Time.timeScale are all clean for the new scene.
            if (pauseHandler != null && GameClock.Instance != null && GameClock.Instance.IsPaused)
                pauseHandler.TogglePause();

            // Stop persistent music now, before the new scene loads.
            // PlayOnAwake audio in the incoming scene runs during its own Awake — after this point —
            // so it is never interrupted.
            // NOTE: explicit != null (not ?.) — Unity's overloaded null check must run, otherwise a
            // destroyed instance (stale static with domain reload off) throws and kills this coroutine.
            if (AudioManager.instance != null)
                AudioManager.instance.StopMusic();

            if (screenTransitioner != null)
            {
                screenTransitioner.StartCover();
                yield return new WaitForSecondsRealtime(screenTransitioner.transitionDuration * 1.13f);
            }
            SceneManager.LoadScene(sceneName);
        }
    }
}
