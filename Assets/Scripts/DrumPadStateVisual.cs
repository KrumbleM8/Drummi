using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Purely cosmetic input-state indicator for the Dungeon drum pads.
    ///
    /// When input is ENABLED (DungeonInputReader.allowInput = true) the pads sit at
    /// full size / full opacity; when DISABLED they shrink and fade to a still-visible
    /// state. This component only OBSERVES the input state via AllowInputChanged —
    /// it never gates or drives input logic, and no input is enabled from tween
    /// callbacks.
    ///
    /// CONFLICT AVOIDANCE:
    ///   Scale — BounceOnBeat writes localScale every frame and PadBounceFeedback
    ///   writes it during its bounce coroutine, so this component never tweens those
    ///   transforms directly. Instead it tweens a single 0..1 state value and pushes
    ///   the resulting multiplier into ScaleMultiplier on both scripts (bounce
    ///   amplitude shrinks proportionally). Visuals with no scale-writing scripts
    ///   (BongoImage) are scaled directly against a cached base scale.
    ///
    ///   Alpha — the rig-root CanvasGroups are owned by DungeonInputLayoutController
    ///   (layout switch fades), so the state alpha lives on NESTED per-pad
    ///   CanvasGroups; nested alphas multiply, so the two never fight.
    ///
    /// INSPECTOR SETUP (wired by Tools/Drummi/Setup Drum Pad State Visuals):
    ///   inputReader        — DungeonInputReader in the scene
    ///   padGroups          — CanvasGroups on L_Input, M_Input, R_Input, BongoImage, SingleDrumPad
    ///   bounceOnBeats      — BounceOnBeat on L_Input, M_Input, R_Input, SingleDrumPad
    ///   padBounceFeedbacks — PadBounceFeedback on L_Input, M_Input, R_Input, SingleDrumPad
    ///   directScaleTargets — BongoImage (no scale-writing scripts)
    /// </summary>
    public class DrumPadStateVisual : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("DungeonInputReader whose allowInput state these visuals mirror (Dungeon scene). Assign exactly one of inputReader / bongoInputReader.")]
        [SerializeField] private DungeonInputReader inputReader;

        [Tooltip("BongoModeInputReader whose allowInput state these visuals mirror (Bongo scene). Event-driven only — no window anticipation, because BeatGenerator already enables allowInput slightly before the input window.")]
        [SerializeField] private BongoModeInputReader bongoInputReader;

        [Tooltip("Nested CanvasGroups on each pad visual. Alphas multiply with the rig-root CanvasGroup, so layout-switch fades are unaffected.")]
        [SerializeField] private CanvasGroup[] padGroups;

        [Tooltip("BounceOnBeat components on pad visuals — driven via ScaleMultiplier so both never write localScale simultaneously.")]
        [SerializeField] private BounceOnBeat[] bounceOnBeats;

        [Tooltip("PadBounceFeedback components on pad visuals — driven via ScaleMultiplier.")]
        [SerializeField] private PadBounceFeedback[] padBounceFeedbacks;

        [Tooltip("Pad visuals with no scale-writing scripts (BongoImage) — scaled directly against their cached base scale.")]
        [SerializeField] private Transform[] directScaleTargets;

        [Header("Disabled State")]
        [Tooltip("Uniform scale multiplier applied to pad visuals while input is disabled.")]
        [SerializeField] private float disabledScale = 0.85f;

        [Tooltip("Alpha of the nested pad CanvasGroups while input is disabled.")]
        [SerializeField] private float disabledAlpha = 0.5f;

        [Header("Tween")]
        [Tooltip("Seconds to animate to the enabled (full) state.")]
        [SerializeField] private float enableDuration = 0.3f;

        [Tooltip("Seconds BEFORE the input window opens that the enable fade starts. Purely cosmetic anticipation — reads the upcoming window time (virtual clock) but never touches gating. 0 = start exactly when input opens. Matching enableDuration makes the fade finish right as the window opens.")]
        [SerializeField] private float enableLeadTime = 0.3f;

        [Tooltip("Seconds to animate to the disabled (shrunken/faded) state.")]
        [SerializeField] private float disableDuration = 0.2f;

        [Tooltip("Ease for both directions.")]
        [SerializeField] private Ease ease = Ease.OutQuad;

        // 0 = fully disabled visuals, 1 = fully enabled visuals.
        private float     _state = 1f;
        private bool      _targetEnabled = true; // state the visuals are at/heading to
        private Tween     _stateTween;
        private Vector3[] _directBaseScales;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _directBaseScales = new Vector3[directScaleTargets.Length];
            for (int i = 0; i < directScaleTargets.Length; i++)
                if (directScaleTargets[i] != null)
                    _directBaseScales[i] = directScaleTargets[i].localScale;
        }

        private void OnEnable()
        {
            if (inputReader != null)      inputReader.AllowInputChanged      += OnAllowInputChanged;
            if (bongoInputReader != null) bongoInputReader.AllowInputChanged += OnAllowInputChanged;
        }

        private void OnDisable()
        {
            if (inputReader != null)      inputReader.AllowInputChanged      -= OnAllowInputChanged;
            if (bongoInputReader != null) bongoInputReader.AllowInputChanged -= OnAllowInputChanged;
        }

        private void Start()
        {
            // Snap to the true current state at boot — no tween from stale values.
            SnapToCurrent();
        }

        private void Update()
        {
            // Anticipation and self-correction are Dungeon-only. The Bongo reader is
            // purely event-driven: BeatGenerator already flips allowInput slightly
            // before the input window (CheckAndTriggerListeningAnimation).
            if (inputReader == null) return;

            if (_targetEnabled || inputReader.allowInput)
            {
                // Self-correct: if we anticipated a window that never opened (the
                // encounter ended during the spawn/call phase), fall back to the
                // disabled visuals. Normal disables arrive via AllowInputChanged.
                if (_targetEnabled && !inputReader.allowInput && !inputReader.EncounterActive)
                    SetInputEnabled(false, animated: true);
                return;
            }

            // COSMETIC ANTICIPATION: start the enable fade enableLeadTime seconds
            // before the input window opens. Read-only against the same virtual-time
            // sources the gating uses (C&R response start via CoyoteWindowStartOverride,
            // otherwise the current bar's InputWindowStart); allowInput itself still
            // flips exactly where it does today.
            if (!inputReader.EncounterActive) return;

            var clock = GameClock.Instance;
            if (clock == null || clock.IsPaused) return;

            double? openTime = inputReader.CoyoteWindowStartOverride;
            if (!openTime.HasValue)
            {
                var coordinator = TimingCoordinator.Instance;
                if (coordinator == null) return;
                openTime = coordinator.CurrentBar.InputWindowStart;
            }

            double untilOpen = openTime.Value - clock.GameTime;
            if (untilOpen >= 0 && untilOpen <= enableLeadTime)
                SetInputEnabled(true, animated: true);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Snap instantly to the current allowInput state. Called by
        /// DungeonInputLayoutController.SwitchLayout whenever a rig is activated for
        /// a room, so the incoming rig never tweens from stale values.
        /// </summary>
        public void SnapToCurrent()
            => SetInputEnabled(CurrentAllowInput, animated: false);

        /// <summary>Drive the visuals toward the enabled or disabled state.</summary>
        public void SetInputEnabled(bool inputEnabled, bool animated)
        {
            float target = inputEnabled ? 1f : 0f;

            // Skip redundant animated calls (e.g. AllowInputChanged firing after the
            // anticipation in Update already started the same fade) so the tween is
            // not restarted mid-flight. Snaps (animated=false) always apply.
            if (animated && _targetEnabled == inputEnabled &&
                (Mathf.Approximately(_state, target) || (_stateTween != null && _stateTween.IsActive())))
                return;

            _targetEnabled = inputEnabled;

            // Rigs are persistent and reused — always kill the running tween before
            // rebuilding so a new state never fights a stale one.
            _stateTween?.Kill();
            _stateTween = null;

            if (!animated)
            {
                _state = target;
                Apply();
                return;
            }

            float duration = inputEnabled ? enableDuration : disableDuration;
            _stateTween = DOTween.To(() => _state, s => { _state = s; Apply(); }, target, duration)
                .SetEase(ease)
                .SetLink(gameObject);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private bool CurrentAllowInput =>
            inputReader != null ? inputReader.allowInput
                                : bongoInputReader != null && bongoInputReader.allowInput;

        private void OnAllowInputChanged(bool inputEnabled) => SetInputEnabled(inputEnabled, animated: true);

        private void Apply()
        {
            float scaleMul = Mathf.Lerp(disabledScale, 1f, _state);
            float alpha    = Mathf.Lerp(disabledAlpha, 1f, _state);

            foreach (var g in padGroups)
                if (g != null) g.alpha = alpha;

            foreach (var b in bounceOnBeats)
                if (b != null) b.ScaleMultiplier = scaleMul;

            foreach (var f in padBounceFeedbacks)
                if (f != null) f.ScaleMultiplier = scaleMul;

            for (int i = 0; i < directScaleTargets.Length; i++)
                if (directScaleTargets[i] != null)
                    directScaleTargets[i].localScale = _directBaseScales[i] * scaleMul;
        }
    }
}
