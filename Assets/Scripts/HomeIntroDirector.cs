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
    /// seen from far away, down-right of centre: the camera sits at
    /// introCameraPosition, the planet is rolled to
    /// introRollAngle + 360·extraRotations (signs on the lower front face, the
    /// on-globe mountain on top) and floats/wobbles via PlanetIdleMotion, the
    /// parallax MountainMid is hidden and the menu is locked via
    /// FlashCardsMenuController.SetInteractive(false).
    ///
    /// Play → one linear progress tween. Phase 1 (introDuration): the camera
    /// flies to the menu pose it had in the scene (captured in Awake, no
    /// duplicated numbers) while the planet rolls BACKWARDS through its extra
    /// turn(s) and eases onto angle 0 (sign 0 on top); the parallax layers are
    /// frozen at identity so the spin doesn't swing the sky. When the camera
    /// arrives the menu unlocks (ArriveMenu). Phase 2 (mountainRiseDelay +
    /// mountainRiseDuration): MountainMid's renderer comes on below the horizon
    /// and it slides straight up into its rest position. The rise waits for
    /// the camera because from far away the globe is too small to occlude the
    /// 25 u wide sprite — its corners would poke out beside the planet.
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

        [Tooltip("ParallaxMid/MountainMid — hidden during the zoom, slides up over the horizon once the camera has arrived.")]
        [SerializeField] private Transform mountainMid;

        [Tooltip("Parallax layers held still (identity rotation, component disabled) while the planet spins its intro turn — a 360° roll would otherwise swing the sky. Re-enabled when the camera arrives; they re-sync to angle 0 with no jump.")]
        [SerializeField] private List<ParallaxRotationLayer> parallaxFrozenDuringIntro = new();

        [Tooltip("Optional idle float/wobble on PlanetPivot for the title state; faded out over the first idleBlendOutFraction of the zoom, then deactivated.")]
        [SerializeField] private PlanetIdleMotion idleMotion;

        [Header("Title Banner (OverlayCanvas)")]
        [SerializeField] private CanvasGroup titleBanner;
        [SerializeField] private RectTransform titleArt;
        [SerializeField] private Button playButton;

        [Tooltip("Menu HUD hidden while the title shows (e.g. InstrumentButton).")]
        [SerializeField] private List<GameObject> hudHiddenDuringTitle = new();

        [Header("Far View (title pose)")]
        [SerializeField] private Vector3 introCameraPosition = new(0f, 18f, -64f);

        [Tooltip("Pitch less than the line to the globe centre puts the planet low in frame; a negative yaw puts it to the right.")]
        [SerializeField] private Vector3 introCameraEuler = new(6f, -5f, 0f);

        [Tooltip("Planet angle in the far view (mod 360); the intro rolls it back to 0 (sign 0 on top). Positive = signs rolled toward the camera, so the roll back is 'backwards'.")]
        [SerializeField] private float introRollAngle = 115f;

        [Tooltip("Extra full backwards turns the planet makes before easing onto the signs (start angle = introRollAngle + 360·n).")]
        [SerializeField, Min(0)] private int extraRotations = 0;

        [Tooltip("Fraction of the zoom over which the idle float/wobble fades out.")]
        [SerializeField, Range(0.05f, 1f)] private float idleBlendOutFraction = 0.35f;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float introDuration = 3.5f;
        [SerializeField] private Ease zoomEase = Ease.InOutSine;

        [Tooltip("Ease of the whole roll (extra turns + settle) — an in/out ease spins fast through the turn and eases into the signs.")]
        [SerializeField] private Ease rollEase = Ease.InOutSine;

        [Header("Mountain Rise (after the camera arrives)")]
        [Tooltip("World units MountainMid starts below its rest position (top below the menu camera's horizon).")]
        [SerializeField, Min(0f)] private float mountainHideOffsetY = 14f;
        [SerializeField, Min(0f)] private float mountainRiseDelay = 0f;
        [SerializeField, Min(0.05f)] private float mountainRiseDuration = 0.9f;
        [SerializeField] private Ease mountainEase = Ease.OutSine;

        [Header("Banner Exit")]
        [SerializeField, Min(0.05f)] private float bannerFadeDuration = 0.4f;
        [SerializeField] private float bannerRisePixels = 300f;

        /// <summary>True while the title banner is up or the zoom is in flight.</summary>
        public bool IntroPending => _playIntro && !_arrived;

        private bool _playIntro;
        private bool _started;
        private bool _arrived;
        private float _progress;
        private float _startAngle;
        private float _totalDuration;

        private Vector3    _menuPos;
        private Quaternion _menuRot;
        private Quaternion _introRot;
        private Vector3    _mountainRest;
        private Renderer[] _mountainRenderers = System.Array.Empty<Renderer>();

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
            if (mountainMid != null)
            {
                _mountainRest = mountainMid.localPosition;
                _mountainRenderers = mountainMid.GetComponentsInChildren<Renderer>(true);
            }
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

            // Freeze the backdrop first so the start angle below doesn't roll it.
            foreach (ParallaxRotationLayer layer in parallaxFrozenDuringIntro)
            {
                if (layer == null) continue;
                layer.enabled = false;
                layer.transform.localRotation = Quaternion.identity;
            }
            if (mountainMid != null) mountainMid.localPosition = _mountainRest - Vector3.up * mountainHideOffsetY;
            SetMountainVisible(false);

            _startAngle = introRollAngle + 360f * extraRotations;
            if (spinner != null) spinner.SetAngle(_startAngle, force: true);

            if (idleMotion != null)
            {
                idleMotion.Weight = 1f;
                idleMotion.enabled = true;
            }

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
                // poked mid-zoom; ArriveMenu deactivates the banner outright.
                titleBanner.DOFade(0f, bannerFadeDuration).SetUpdate(true).SetLink(titleBanner.gameObject);
                if (titleArt != null)
                    titleArt.DOAnchorPosY(titleArt.anchoredPosition.y + bannerRisePixels, bannerFadeDuration)
                            .SetEase(Ease.InSine).SetUpdate(true).SetLink(titleArt.gameObject);
            }

            _totalDuration = introDuration + mountainRiseDelay + mountainRiseDuration;
            _progress = 0f;
            DOTween.To(() => _progress, Apply, 1f, _totalDuration)
                   .SetEase(Ease.Linear)
                   .SetUpdate(true)
                   .SetLink(gameObject)
                   .OnComplete(Finish);
        }

        private void Apply(float t)
        {
            _progress = t;
            float time = t * _totalDuration;

            // ── Phase 1: camera zoom + planet roll ───────────────────────────
            if (!_arrived)
            {
                float tz = Mathf.Clamp01(time / introDuration);
                float e = DOVirtual.EasedValue(0f, 1f, tz, zoomEase);

                if (menuCamera != null)
                    menuCamera.transform.SetPositionAndRotation(
                        Vector3.LerpUnclamped(introCameraPosition, _menuPos, e),
                        Quaternion.SlerpUnclamped(_introRot, _menuRot, e));

                // Last: the roll re-sorts rim children against the camera's new
                // pose; Refresh covers frames where the eased angle hasn't moved.
                if (spinner != null)
                {
                    float r = DOVirtual.EasedValue(0f, 1f, tz, rollEase);
                    spinner.SetAngle(Mathf.Lerp(_startAngle, 0f, r));
                    spinner.RefreshRimSorting();
                }

                // Idle float/wobble settles out as the roll takes over
                // (PlanetIdleMotion.LateUpdate recomposes rotation from the
                // spinner's angle, so the two never fight).
                if (idleMotion != null && idleMotion.enabled)
                {
                    float fade = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(tz / idleBlendOutFraction), Ease.InOutSine);
                    if (fade >= 1f) idleMotion.Deactivate();
                    else idleMotion.Weight = 1f - fade;
                }

                if (tz >= 1f) ArriveMenu();
            }

            // ── Phase 2: MountainMid slides up over the horizon ──────────────
            float riseStart = introDuration + mountainRiseDelay;
            if (time >= riseStart && mountainMid != null)
            {
                SetMountainVisible(true);
                float m = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01((time - riseStart) / mountainRiseDuration), mountainEase);
                mountainMid.localPosition = _mountainRest - Vector3.up * (mountainHideOffsetY * (1f - m));
            }
        }

        /// <summary>Camera has landed: snap the menu pose exactly and hand the menu back to the player.</summary>
        private void ArriveMenu()
        {
            if (_arrived) return;
            _arrived = true;

            if (idleMotion != null && idleMotion.enabled) idleMotion.Deactivate();
            if (menuCamera != null) menuCamera.transform.SetPositionAndRotation(_menuPos, _menuRot);
            if (spinner != null)
            {
                spinner.SetAngle(0f, force: true);
                spinner.RefreshRimSorting();
            }

            // Angle is 0 now, so OnEnable re-syncs each layer to identity — no jump.
            foreach (ParallaxRotationLayer layer in parallaxFrozenDuringIntro)
                if (layer != null) layer.enabled = true;

            if (titleBanner != null) titleBanner.gameObject.SetActive(false);
            foreach (GameObject go in hudHiddenDuringTitle)
                if (go != null) go.SetActive(true);

            if (menuController != null) menuController.SetInteractive(true);
        }

        private void Finish()
        {
            ArriveMenu();
            if (mountainMid != null)
            {
                mountainMid.localPosition = _mountainRest;
                SetMountainVisible(true);
            }
        }

        private void SetMountainVisible(bool visible)
        {
            for (int i = 0; i < _mountainRenderers.Length; i++)
                if (_mountainRenderers[i] != null && _mountainRenderers[i].enabled != visible)
                    _mountainRenderers[i].enabled = visible;
        }
    }
}
