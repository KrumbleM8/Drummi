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
    ///   HitPop        — ball lock-on landed on the target.
    ///   Attack LANDS  — red wind-up flash + downward lunge (bar had fizzles).
    ///   Attack WHIFFS — desaturated flash + limp wobble (clean bar).
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

        [Tooltip("Color flashed when the enemy attack whiffs.")]
        [SerializeField] private Color whiffColor = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Punch scale magnitude (fraction of base scale) for the required-beat pulse.")]
        [SerializeField] private float beatPulseScale = 0.15f;

        private Color _baseColor;
        private Vector3 _baseScale;
        private Vector3 _baseLocalPos;

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
            if (DOTween.IsTweening(transform)) return;
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
        }

        /// <summary>Cosmetic pop when the ball's lock-on flight lands on the target.</summary>
        public void HitPop()
        {
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

        private void HandleBarResolved(int fizzleCount)
        {
            transform.DOKill(complete: true);
            if (spriteRenderer != null) spriteRenderer.DOKill();

            if (fizzleCount > 0)
            {
                // Attack LANDS: red flash + sharp downward lunge.
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = attackColor;
                    spriteRenderer.DOColor(_baseColor, 0.5f).SetLink(gameObject);
                }
                DOTween.Sequence()
                       .Append(transform.DOScale(_baseScale * 1.35f, 0.1f).SetEase(Ease.OutQuad))
                       .Append(transform.DOPunchPosition(Vector3.down * 0.6f, 0.3f, vibrato: 1))
                       .Append(transform.DOScale(_baseScale, 0.15f))
                       .SetLink(gameObject);
            }
            else
            {
                // Attack WHIFFS: desaturated flash + limp wobble.
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = whiffColor;
                    spriteRenderer.DOColor(_baseColor, 0.4f).SetLink(gameObject);
                }
                transform.DOPunchRotation(new Vector3(0f, 0f, 20f), 0.4f, vibrato: 3)
                         .SetLink(gameObject);
            }
        }
    }
}
