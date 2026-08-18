using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Minimal target visual riding the sweep. Lives on a CHILD of the FlickTargetSweep
    /// object: the sweep animates the parent's position, all cosmetic pops here animate
    /// this child's local scale / color — never the same transform property (project
    /// convention). DOTween cosmetic only; nothing graded reads from this.
    ///
    /// Reads:
    ///   HitPop       — ball lock-on landed on the target.
    ///   Attack LANDS — red flash, color only (surviving enemies always attack; the
    ///                  retreat despawn owns transform motion at the seam; grace
    ///                  seams — enemy died mid-bar — play nothing).
    ///   SetVisible   — FlickEnemy hides/shows the sprite across despawn/spawn
    ///                  (renderer toggle, never SetActive — subscriptions + tween
    ///                  links must stay alive).
    /// </summary>
    public class FlickTargetVisual : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("FlickBeatManager whose bar resolutions drive the attack telegraph.")]
        [SerializeField] private FlickBeatManager beatManager;

        [Tooltip("SpriteRenderer tinted for hit/attack feedback. Defaults to the one on this object.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Feedback")]
        [Tooltip("Punch scale magnitude for a lock-on hit.")]
        [SerializeField] private float hitPopScale = 0.5f;

        [Tooltip("Color flashed when the enemy attack lands.")]
        [SerializeField] private Color attackColor = new Color(1f, 0.25f, 0.2f);

        [Tooltip("Punch scale magnitude (fraction of base scale) for the required-beat pulse.")]
        [SerializeField] private float beatPulseScale = 0.15f;

        private Color _baseColor;
        private Vector3 _baseScale;
        private Vector3 _baseLocalPos;
        private bool _hidden;
        private bool _feedbackMuted;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;
            _baseScale = transform.localScale;
            _baseLocalPos = transform.localPosition;
        }

        private void OnEnable()
        {
            if (beatManager != null)
            {
                beatManager.OnBarResolved += HandleBarResolved;
                beatManager.OnRequiredBeatCue += HandleRequiredBeatCue;
            }
        }

        private void OnDisable()
        {
            if (beatManager != null)
            {
                beatManager.OnBarResolved -= HandleBarResolved;
                beatManager.OnRequiredBeatCue -= HandleRequiredBeatCue;
            }
        }

        // Small pulse on each required beat — the visual parity anchor. Skipped while a
        // hit pop or attack telegraph is animating so it never fights those tweens.
        private void HandleRequiredBeatCue()
        {
            if (_hidden || _feedbackMuted || DOTween.IsTweening(transform)) return;
            transform.DOPunchScale(_baseScale * beatPulseScale, 0.18f, vibrato: 1)
                     .SetLink(gameObject);
        }

        /// <summary>
        /// Host cleanup hook: kills all cosmetic tweens without completing them and
        /// restores base scale/color, so a mode teardown never leaves a mid-pop state.
        /// </summary>
        public void ResetVisual()
        {
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = _baseLocalPos;
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = _baseColor;
            }
            _feedbackMuted = false;
            SetVisible(true);
        }

        /// <summary>
        /// FlickEnemy despawn/spawn hook: toggles the renderer (never SetActive — event
        /// subscriptions and tween SetLinks must survive). Hidden also mutes the beat
        /// pulse and hit pop.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _hidden = !visible;
            if (visible) _feedbackMuted = false; // fresh enemy — pops live again
            if (spriteRenderer != null) spriteRenderer.enabled = visible;
        }

        /// <summary>
        /// FlickEnemy despawn hook: mutes the hit pop and beat pulse while a death or
        /// retreat tween owns this transform (a late-landing ball's HitPop would fight
        /// it and snap the scale). Cleared by SetVisible(true) / ResetVisual.
        /// </summary>
        public void MuteFeedback()
        {
            _feedbackMuted = true;
        }

        /// <summary>Cosmetic pop when the ball's lock-on flight lands on the target.</summary>
        public void HitPop()
        {
            if (_hidden || _feedbackMuted) return;
            transform.DOKill(complete: true);
            transform.DOPunchScale(_baseScale * hitPopScale, 0.2f, vibrato: 4)
                     .SetLink(gameObject);

            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = Color.white;
                spriteRenderer.DOColor(_baseColor, 0.25f).SetLink(gameObject);
            }
        }

        private void HandleBarResolved(FlickBarResult result)
        {
            // Grace seam = the enemy died mid-bar; nothing left to telegraph. Hidden =
            // FlickEnemy already despawned this sprite.
            if (result.Grace || _hidden) return;

            // Attack LANDS (survivors always attack): red flash. COLOR ONLY — the
            // retreat despawn (FlickEnemy) owns this transform's motion at the seam,
            // so the old scale/lunge sequence would fight it.
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = attackColor;
                spriteRenderer.DOColor(_baseColor, 0.5f).SetLink(gameObject);
            }
        }
    }
}
