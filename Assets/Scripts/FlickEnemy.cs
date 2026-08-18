using System;
using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Per-bar flick enemy: HP + a sprite-based health bar riding the sweep. One enemy
    /// lives per bar — hosts call TakeDamage per hit, PlayDeathDespawn (HP depleted) or
    /// PlayRetreatDespawn (survived the seam), then ResetForSpawn(nextBarStartVirtual)
    /// to bring in the replacement ON the bar boundary (polled from GameClock virtual
    /// time — the FlickBallController respawn pattern, pause-safe for free).
    ///
    /// Lives on the FlickTarget object, whose world position is OWNED by
    /// FlickTargetSweep every frame — so every tween here animates CHILD local
    /// transforms only (project convention). All tweens are cosmetic placeholders,
    /// SetLink(gameObject); nothing graded reads from this component.
    ///
    /// Health bar: FillPivot is an empty anchored at the bar's LEFT edge with the fill
    /// sprite offset +halfWidth inside it, so scaling FillPivot.x shrinks the fill
    /// from the right (EliteEnemyController's fillAmount pattern without a canvas).
    ///
    /// INSPECTOR SETUP (FlickTarget object):
    ///   visual    — FlickTargetVisual child (hidden while despawned)
    ///   barRoot   — EnemyHealthBar child (bar back + FillPivot/BarFill under it)
    ///   fillPivot — FillPivot child of barRoot (left-edge anchor, scaled on X)
    /// </summary>
    public class FlickEnemy : MonoBehaviour
    {
        [Header("Combat")]
        [Tooltip("Hit points per enemy. Every spawn starts at full HP.")]
        [SerializeField] private int maxHP = 60;

        [Header("References")]
        [Tooltip("Target visual child — hidden between despawn and the next spawn.")]
        [SerializeField] private FlickTargetVisual visual;

        [Tooltip("Health bar root child (background + fill live under it).")]
        [SerializeField] private Transform barRoot;

        [Tooltip("Left-edge pivot whose X scale is the HP fraction.")]
        [SerializeField] private Transform fillPivot;

        [Header("Placeholder Animations")]
        [Tooltip("Seconds for the death scale-out.")]
        [SerializeField] private float deathDuration = 0.35f;

        [Tooltip("Seconds for the retreat slide-off.")]
        [SerializeField] private float retreatDuration = 0.3f;

        [Tooltip("Local units the enemy slides (upward) while retreating.")]
        [SerializeField] private float retreatDistance = 3f;

        /// <summary>Fired once when HP reaches zero. Cleared on every spawn.</summary>
        public event Action OnDied;

        /// <summary>True while the current enemy has HP left (and is not despawned).</summary>
        public bool IsAlive { get; private set; }

        private int _currentHP;
        private bool _spawnScheduled;
        private double _spawnAtVirtual;
        private Tween _visualAnim; // active death/retreat/pop-in — Sequences are not reachable via transform.DOKill
        private Vector3 _fillBaseScale = Vector3.one;
        private Vector3 _visualBaseLocalPos;
        private Vector3 _visualBaseScale;
        private Vector3 _barBaseLocalPos;
        private Vector3 _barBaseScale;

        private void Awake()
        {
            if (fillPivot != null) _fillBaseScale = fillPivot.localScale;
            if (visual != null)
            {
                _visualBaseLocalPos = visual.transform.localPosition;
                _visualBaseScale    = visual.transform.localScale;
            }
            if (barRoot != null)
            {
                _barBaseLocalPos = barRoot.localPosition;
                _barBaseScale    = barRoot.localScale;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Applies hit damage; fires OnDied once when HP depletes.</summary>
        public void TakeDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            _currentHP = Mathf.Max(0, _currentHP - amount);
            UpdateBar();
            Debug.Log($"[FlickEnemy] -{amount} → HP {_currentHP}/{maxHP}");

            if (_currentHP == 0)
            {
                IsAlive = false;
                OnDied?.Invoke();
            }
        }

        /// <summary>
        /// Schedules the next enemy to appear at <paramref name="spawnAtVirtual"/> —
        /// normally the next bar's start. Polled in Update from GameClock virtual time.
        /// </summary>
        public void ResetForSpawn(double spawnAtVirtual)
        {
            _spawnAtVirtual = spawnAtVirtual;
            _spawnScheduled = true;
        }

        /// <summary>HP depleted: placeholder death — one smooth shrink to nothing, then hide.</summary>
        public void PlayDeathDespawn()
        {
            IsAlive = false;
            KillTweens();

            if (visual != null)
            {
                // Mute the hit pop / beat pulse for the rest of this enemy's life — a
                // late-landing ball's HitPop punches the same transform and snaps the
                // shrink. Single tween FROM THE CURRENT SCALE (a killed mid-punch may
                // have left it enlarged), so there is no visible jump.
                visual.MuteFeedback();
                _visualAnim = visual.transform
                       .DOScale(Vector3.zero, deathDuration)
                       .SetEase(Ease.InOutQuad)
                       .SetLink(gameObject)
                       .OnComplete(HideAll);
            }
            if (barRoot != null)
                barRoot.DOScale(Vector3.zero, deathDuration).SetEase(Ease.InOutQuad).SetLink(gameObject);

            Debug.Log("[FlickEnemy] Death despawn");
        }

        /// <summary>Survived the seam: placeholder retreat — slide off locally, then hide.</summary>
        public void PlayRetreatDespawn()
        {
            IsAlive = false;
            KillTweens();

            if (visual != null)
            {
                visual.MuteFeedback(); // same HitPop-vs-despawn conflict as the death path
                var t = visual.transform;
                _visualAnim = DOTween.Sequence()
                       .Append(t.DOLocalMoveY(_visualBaseLocalPos.y + retreatDistance, retreatDuration).SetEase(Ease.InQuad))
                       .Join(t.DOScale(t.localScale * 0.5f, retreatDuration))
                       .SetLink(gameObject)
                       .OnComplete(HideAll);
            }
            if (barRoot != null)
                barRoot.DOScale(Vector3.zero, retreatDuration).SetEase(Ease.InQuad).SetLink(gameObject);

            Debug.Log("[FlickEnemy] Retreat despawn");
        }

        /// <summary>
        /// Host teardown/start hook: kills tweens, cancels any scheduled spawn, and
        /// restores a full-HP, fully visible enemy immediately.
        /// </summary>
        public void ResetImmediate()
        {
            _spawnScheduled = false;
            KillTweens();
            RestoreBasePose();
            _currentHP = maxHP;
            IsAlive = true;
            UpdateBar();
            if (visual != null) visual.SetVisible(true);
            if (barRoot != null) barRoot.gameObject.SetActive(true);
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_spawnScheduled || GameClock.Instance == null || GameClock.Instance.IsPaused) return;
            if (GameClock.Instance.GameTime < _spawnAtVirtual) return;

            _spawnScheduled = false;
            KillTweens();
            RestoreBasePose();
            _currentHP = maxHP;
            IsAlive = true;
            UpdateBar();
            if (visual != null) visual.SetVisible(true);
            if (barRoot != null) barRoot.gameObject.SetActive(true);

            // Cosmetic pop-in.
            if (visual != null)
            {
                visual.transform.localScale = Vector3.zero;
                visual.transform.DOScale(_visualBaseScale, 0.2f).SetEase(Ease.OutBack).SetLink(gameObject);
            }
            if (barRoot != null)
            {
                barRoot.localScale = Vector3.zero;
                barRoot.DOScale(_barBaseScale, 0.2f).SetEase(Ease.OutBack).SetLink(gameObject);
            }
            Debug.Log($"[FlickEnemy] Spawned at vt={_spawnAtVirtual:F4} — HP {maxHP}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void HideAll()
        {
            if (visual != null) visual.SetVisible(false);
            if (barRoot != null) barRoot.gameObject.SetActive(false);
        }

        private void KillTweens()
        {
            if (_visualAnim != null && _visualAnim.IsActive()) _visualAnim.Kill();
            _visualAnim = null;
            if (visual != null)  visual.transform.DOKill();
            if (barRoot != null) barRoot.DOKill();
        }

        private void RestoreBasePose()
        {
            if (visual != null)
            {
                visual.transform.localPosition = _visualBaseLocalPos;
                visual.transform.localScale    = _visualBaseScale;
            }
            if (barRoot != null)
            {
                barRoot.localPosition = _barBaseLocalPos;
                barRoot.localScale    = _barBaseScale;
            }
        }

        private void UpdateBar()
        {
            if (fillPivot == null) return;
            var s = _fillBaseScale;
            s.x *= Mathf.Clamp01(_currentHP / (float)maxHP);
            fillPivot.localScale = s;
        }
    }
}
