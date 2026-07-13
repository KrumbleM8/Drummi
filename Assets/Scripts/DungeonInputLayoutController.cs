using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Manages switching between the three-pad (combat), single-drum (C&R), and bongo
    /// two-pad (CombatTwoPad) input rigs.
    ///
    /// Call SwitchLayout(InputLayoutType) from RoomController at the start of each room.
    /// The transition is purely cosmetic — input gating during the animation is already
    /// handled by DungeonInputReader.EncounterActive (false throughout lead-in bars).
    ///
    /// Touch detection: DrumPadTouch uses RectTransformUtility.RectangleContainsScreenPoint
    /// (bypasses the event system), so CanvasGroup.blocksRaycasts has no effect on touch
    /// detection. The ThreePad zones remain active even when alpha=0 (invisible).
    /// SingleDrumPad is purely visual (raycastTarget=false).
    /// TwoPad (BongoRig) swaps DrumPadTouch's active pad refs via OverridePads/RestorePads.
    ///
    /// INSPECTOR SETUP:
    ///   threePadRig       — CanvasGroup on OverlayCanvas/ThreePadRig
    ///   singleDrumRig     — CanvasGroup on OverlayCanvas/SingleDrumRig
    ///   bongoRig          — CanvasGroup on OverlayCanvas/BongoRig
    ///   bongoLeftPad      — RectTransform of BongoRig/L (transparent hit zone)
    ///   bongoRightPad     — RectTransform of BongoRig/R (transparent hit zone)
    ///   bongoAnimator     — BongoAnimator on BongoRig (enabled/disabled on layout switch)
    ///   drumPadTouch      — DrumPadTouch in the scene (for OverridePads/CenterPadEnabled)
    ///   transitionDuration — seconds for the scale-Y + fade animation (default 0.2)
    /// </summary>
    public class DungeonInputLayoutController : MonoBehaviour
    {
        [Header("Rigs")]
        [Tooltip("CanvasGroup on the parent of L_Input / M_Input / R_Input.")]
        [SerializeField] private CanvasGroup threePadRig;

        [Tooltip("CanvasGroup on the parent of SingleDrumPad.")]
        [SerializeField] private CanvasGroup singleDrumRig;

        [Tooltip("CanvasGroup on OverlayCanvas/BongoRig — the two-pad bongo rig.")]
        [SerializeField] private CanvasGroup bongoRig;

        [Header("Bongo Rig")]
        [Tooltip("Transparent left hit zone inside BongoRig (RectTransform).")]
        [SerializeField] private RectTransform bongoLeftPad;

        [Tooltip("Transparent right hit zone inside BongoRig (RectTransform).")]
        [SerializeField] private RectTransform bongoRightPad;

        [Tooltip("BongoAnimator on BongoRig — enabled when TwoPad is active so it subscribes to DrumPadTouch hits.")]
        [SerializeField] private BongoAnimator bongoAnimator;

        [Tooltip("DrumPadTouch in the scene — receives OverridePads/RestorePads and CenterPadEnabled changes.")]
        [SerializeField] private DrumPadTouch drumPadTouch;

        [Header("Transition")]
        [Tooltip("Duration of the scale-Y + fade animation in seconds.")]
        [SerializeField] private float transitionDuration = 0.2f;

        private InputLayoutType _current = InputLayoutType.ThreePad;
        private Tween _outTween;
        private Tween _inTween;

        private void Awake()
        {
            // Initialise to ThreePad visible; first LoadRoom corrects this if needed.
            ApplyInstant(threePadRig,   visible: true);
            ApplyInstant(singleDrumRig, visible: false);
            ApplyInstant(bongoRig,      visible: false);
            if (bongoAnimator != null) bongoAnimator.enabled = false;
        }

        /// <summary>
        /// Animate a layout switch. If the target matches the current layout the rig is
        /// snapped to fully visible (guard against drift) and the call returns immediately.
        /// Called from RoomController.SetState(Playing).
        /// </summary>
        public void SwitchLayout(InputLayoutType target)
        {
            if (target == _current)
            {
                ApplyInstant(GetRig(_current), visible: true);
                return;
            }

            // Teardown current layout's side-effects before changing _current.
            if (_current == InputLayoutType.TwoPad)
            {
                drumPadTouch?.RestorePads();
                if (drumPadTouch != null) drumPadTouch.CenterPadEnabled = true;
                if (bongoAnimator != null) bongoAnimator.enabled = false;
            }

            CanvasGroup outRig = GetRig(_current);
            CanvasGroup inRig  = GetRig(target);
            _current = target;

            // Setup new layout's side-effects.
            if (target == InputLayoutType.TwoPad)
            {
                drumPadTouch?.OverridePads(bongoLeftPad, bongoRightPad);
                if (drumPadTouch != null) drumPadTouch.CenterPadEnabled = false;
                if (bongoAnimator != null) bongoAnimator.enabled = true;
            }

            _outTween?.Kill();
            _inTween?.Kill();

            // Outgoing: squash Y to zero + fade out simultaneously.
            _outTween = DOTween.Sequence()
                .Append(outRig.DOFade(0f, transitionDuration).SetEase(Ease.InQuad))
                .Join(outRig.transform.DOScaleY(0f, transitionDuration).SetEase(Ease.InBack))
                .SetLink(gameObject);

            // Incoming: start squashed/invisible, expand + fade in simultaneously.
            Vector3 inScale = inRig.transform.localScale;
            inRig.transform.localScale = new Vector3(inScale.x, 0f, inScale.z);
            inRig.alpha = 0f;
            _inTween = DOTween.Sequence()
                .Append(inRig.DOFade(1f, transitionDuration).SetEase(Ease.OutQuad))
                .Join(inRig.transform.DOScaleY(1f, transitionDuration).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }

        // ── Private ──────────────────────────────────────────────────────────────

        private CanvasGroup GetRig(InputLayoutType t) => t switch
        {
            InputLayoutType.ThreePad   => threePadRig,
            InputLayoutType.SingleDrum => singleDrumRig,
            InputLayoutType.TwoPad     => bongoRig,
            _                          => threePadRig,
        };

        private static void ApplyInstant(CanvasGroup g, bool visible)
        {
            if (g == null) return;
            g.alpha = visible ? 1f : 0f;
            Vector3 s = g.transform.localScale;
            g.transform.localScale = new Vector3(s.x, visible ? 1f : 0f, s.z);
        }
    }
}
