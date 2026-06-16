using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace KrumbleHut.Drummi.UI
{
    /// <summary>
    /// Sits on the persistent OverlayUtilCanvas. Manages the settings panel
    /// (slide-up animation, volume, vibration) and provides scene-load entry
    /// points that mirror UIMenuManager's GoTo* methods.
    /// </summary>
    public class OverlayUIController : MonoBehaviour
    {
        [Header("Settings Panel")]
        [Tooltip("Root RectTransform of the settings panel. Designer should position it on-screen; the controller hides it below on startup.")]
        [SerializeField] private RectTransform settingsPanel;
        [SerializeField] private float panelSlideDuration = 0.25f;
        [SerializeField] private AnimationCurve panelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Audio Mixer")]
        [Tooltip("Optional AudioMixer with exposed MusicVolume and SFXVolume parameters.")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string musicVolumeParam = "MusicVolume";
        [SerializeField] private string sfxVolumeParam   = "SFXVolume";

        [Header("Scene Transition")]
        [SerializeField] private ScreenTransition screenTransitioner;

        // ── PlayerPrefs keys ───────────────────────────────────────────────────
        private const string MusicVolKey  = "settings_music_vol";
        private const string SfxVolKey    = "settings_sfx_vol";
        private const string VibrationKey = "settings_vibration";

        // ── Panel state ────────────────────────────────────────────────────────
        private bool       _settingsOpen;
        private Coroutine  _panelCoroutine;
        private Vector2    _panelShownPos;
        private Vector2    _panelHiddenPos;

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

            LoadAndApplySettings();
        }

        // ── Settings Panel ─────────────────────────────────────────────────────

        /// <summary>Opens the settings panel with a slide-up animation.</summary>
        public void OpenSettings()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;
            AnimatePanel(show: true);
        }

        /// <summary>Closes the settings panel with a slide-down animation.</summary>
        public void CloseSettings()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            AnimatePanel(show: false);
        }

        /// <summary>Toggles the settings panel open or closed.</summary>
        public void ToggleSettings()
        {
            if (_settingsOpen) CloseSettings();
            else OpenSettings();
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
                elapsed += Time.deltaTime;
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

        /// <summary>Transitions to the Home scene.</summary>
        public void GoToHomeScene()    => StartCoroutine(SceneTransitionRoutine("Home"));

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
            if (screenTransitioner != null)
            {
                screenTransitioner.StartCover();
                yield return new WaitForSeconds(screenTransitioner.transitionDuration * 1.13f);
            }
            SceneManager.LoadScene(sceneName);
        }
    }
}
