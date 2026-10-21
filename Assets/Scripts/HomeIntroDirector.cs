using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Title state + intro for the Home globe menu. On a fresh load the player
    /// sees the title banner (OverlayCanvas/TitleBanner) over the whole globe
    /// seen from far away: the camera sits at introCameraPosition, the planet
    /// is rolled to introRollAngle (signs still on top, leaning), the parallax
    /// MountainMid is sunk below the back horizon and the menu is locked via
    /// FlashCardsMenuController.SetInteractive(false).
    ///
    /// Play → one linear progress tween drives three channels in lock-step:
    /// camera flies to the menu pose it had in the scene (captured in Awake, no
    /// duplicated numbers), the planet rolls BACKWARDS to angle 0 (sign 0 on
    /// top) and MountainMid slides straight up into its rest position over the
    /// far horizon. The banner fades/rises meanwhile but keeps blocking taps
    /// until Finish unlocks the menu.
    ///
    /// Skips (lands directly on the signs) when HomeFlowController.SkipIntro
    /// (pause-menu Home / Restart) or FlashCardsMenuController.PendingFocusIndex
    /// (return from Bongo) is set. Execution order 100: Start runs after the
    /// menu controller's Start, so its SnapTo(_index) is overridden in the
    /// same frame — before the first render — and the controller is already
    /// listening to FocusedIndexChanged.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HomeIntroDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera menuCamera;
        [SerializeField] private PlanetSpinner spinner;
        [SerializeField] private FlashCardsMenuController menuController;

        [Tooltip("ParallaxMid/MountainMid — hidden below the horizon in the far view, slides up during the zoom.")]
        [SerializeField] private Transform mountainMid;

        [Header("Title Banner (OverlayCanvas)")]
        [SerializeField] private CanvasGroup titleBanner;
        [SerializeField] private RectTransform titleArt;
        [SerializeField] private Button playButton;

        [Tooltip("Menu HUD hidden while the title shows (e.g. InstrumentButton).")]
        [SerializeField] private List<GameObject> hudHiddenDuringTitle = new();

        [Header("Far View (title pose)")]
        [SerializeField] private Vector3 introCameraPosition = new(0f, 14f, -48f);
        [SerializeField] private Vector3 introCameraEuler = new(10f, 0f, 0f);

        [Tooltip("Planet angle in the far view; the intro rolls it back to 0 (sign 0 on top). Positive = signs rolled toward the camera.")]
        [SerializeField] private float introRollAngle = 35f;

        [Tooltip("World units MountainMid starts below its rest position (fully behind the globe from the far camera).")]
        [SerializeField, Min(0f)] private float mountainHideOffsetY = 14f;

        [Tooltip("Normalised intro time at which MountainMid starts rising.")]
        [SerializeField, Range(0f, 0.95f)] private float mountainRiseStart = 0.4f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float introDuration = 2.5f;
        [SerializeField, Min(0.05f)] private float bannerFadeDuration = 0.4f;
        [SerializeField] private float bannerRisePixels = 300f;
        [SerializeField] private Ease zoomEase = Ease.InOutSine;
        [SerializeField] private Ease mountainEase = Ease.OutSine;

        /// <summary>True while the title banner is up or the zoom is in flight.</summary>
        public bool IntroPending => _playIntro && !_finished;

        private bool _playIntro;
        private bool _started;
        private bool _finished;
        private float _progress;

        private Vector3    _menuPos;
        private Quaternion _menuRot;
        private Quaternion _introRot;
        private Vector3    _mountainRest;

        private void Awake()
        {
            // Decide here: FlashCardsMenuController.Start consumes PendingFocusIndex.
            _playIntro = !HomeFlowController.SkipIntro && FlashCardsMenuController.PendingFocusIndex < 0;
            HomeFlowController.SkipIntro = false;

            if (menuCamera != null)
            {
                _menuPos = menuCamera.transform.position;
                _menuRot = menuCamera.transform.rotation;
            }
            _introRot = Quaternion.Euler(introCameraEuler);
            if (mountainMid != null) _mountainRest = mountainMid.localPosition;
        }

        private void Start()
        {
            if (!_playIntro)
            {
                if (titleBanner != null) titleBanner.gameObject.SetActive(false);
                return;
            }

            if (menuController != null) menuController.SetInteractive(false);
            if (menuCamera != null) menuCamera.transform.SetPositionAndRotation(introCameraPosition, _introRot);
            if (mountainMid != null) mountainMid.localPosition = _mountainRest - Vector3.up * mountainHideOffsetY;
            if (spinner != null) spinner.SetAngle(introRollAngle, force: true);

            foreach (GameObject go in hudHiddenDuringTitle)
                if (go != null) go.SetActive(false);

            if (titleBanner != null)
            {
                titleBanner.gameObject.SetActive(true);
                titleBanner.alpha = 1f;
                titleBanner.interactable = true;
                titleBanner.blocksRaycasts = true;
            }
            if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayPressed);
        }

        /// <summary>Play button: dismiss the banner and fly the camera into the menu.</summary>
        public void OnPlayPressed()
        {
            if (_started || !_playIntro) return;
            _started = true;

            if (playButton != null) playButton.interactable = false;

            if (titleBanner != null)
            {
                // Alpha only — blocksRaycasts stays on so signs/bushes can't be
                // poked mid-zoom; Finish deactivates the banner outright.
                titleBanner.DOFade(0f, bannerFadeDuration).SetUpdate(true).SetLink(titleBanner.gameObject);
                if (titleArt != null)
                    titleArt.DOAnchorPosY(titleArt.anchoredPosition.y + bannerRisePixels, bannerFadeDuration)
                            .SetEase(Ease.InSine).SetUpdate(true).SetLink(titleArt.gameObject);
            }

            _progress = 0f;
            DOTween.To(() => _progress, Apply, 1f, introDuration)
                   .SetEase(Ease.Linear)
                   .SetUpdate(true)
                   .SetLink(gameObject)
                   .OnComplete(Finish);
        }

        private void Apply(float t)
        {
            _progress = t;
            float e = DOVirtual.EasedValue(0f, 1f, t, zoomEase);

            if (menuCamera != null)
                menuCamera.transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(introCameraPosition, _menuPos, e),
                    Quaternion.SlerpUnclamped(_introRot, _menuRot, e));

            if (mountainMid != null)
            {
                float rise = DOVirtual.EasedValue(0f, 1f, Mathf.InverseLerp(mountainRiseStart, 1f, t), mountainEase);
                mountainMid.localPosition = _mountainRest - Vector3.up * (mountainHideOffsetY * (1f - rise));
            }

            // Last: the roll re-sorts rim children against the camera's new pose;
            // Refresh covers frames where the eased angle hasn't moved.
            if (spinner != null)
            {
                spinner.SetAngle(Mathf.Lerp(introRollAngle, 0f, e));
                spinner.RefreshRimSorting();
            }
        }

        private void Finish()
        {
            _finished = true;

            if (menuCamera != null) menuCamera.transform.SetPositionAndRotation(_menuPos, _menuRot);
            if (mountainMid != null) mountainMid.localPosition = _mountainRest;
            if (spinner != null)
            {
                spinner.SetAngle(0f, force: true);
                spinner.RefreshRimSorting();
            }

            if (titleBanner != null) titleBanner.gameObject.SetActive(false);
            foreach (GameObject go in hudHiddenDuringTitle)
                if (go != null) go.SetActive(true);

            if (menuController != null) menuController.SetInteractive(true);
        }
    }
}
