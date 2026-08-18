using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Host-agnostic flick ENEMY ORCHESTRATION — the shared seam between the flick
    /// grading core (FlickBeatManager) and the per-bar enemy (FlickEnemy). Both hosts
    /// (standalone FlickModeController, dungeon RoomController) drive this one
    /// component instead of duplicating the enemy loop:
    ///
    ///   BeginEncounter  — subscribes the enemy loop, assigns the aim check, resets
    ///                     the enemy to full HP, and starts the sweep. Does NOT arm
    ///                     the beat manager: hosts own Initialize(bpm) + Arm(gradingStart),
    ///                     because grading start (a bar boundary) and sweep origin
    ///                     (phase 0, live during lead-in) are host decisions.
    ///   TeardownEncounter — the SINGLE teardown entry point for every exit path
    ///                     (mode cleanup, room completion, player death): unsubscribes,
    ///                     stops the beat manager (gating the reader off), drops any
    ///                     active touch track, and hard-resets ball/visual/enemy.
    ///
    /// Enemy loop owned here (moved verbatim from FlickModeController):
    ///   - Consumed Perfect/Good releases damage the enemy: off-beat = half base
    ///     (combo already broken), clean on-beat = baseDamage × comboMultiplier^ComboIndex.
    ///   - Bar seam: a surviving (non-grace) enemy retreats; a replacement is always
    ///     scheduled for the next bar start (FlickBarResult.NextBarStartVirtual).
    ///   - Enemy HP depleted: NotifyEnemyDefeated (grace period) + death despawn.
    ///
    /// Scoring stays host-side: hosts subscribe OnReleaseResolved/OnBarResolved in
    /// parallel — safe because outcome payloads are immutable structs and this
    /// component mutates only enemy HP.
    ///
    /// INSPECTOR SETUP: assign all six refs from the hosting scene's flick stack.
    /// </summary>
    public class FlickEncounterDirector : MonoBehaviour
    {
        #region Inspector
        [Header("Flick Stack")]
        [Tooltip("Beat manager whose release/bar events drive the enemy loop.")]
        [SerializeField] private FlickBeatManager beatManager;

        [Tooltip("Input reader — active touch track is dropped on teardown.")]
        [SerializeField] private FlickInputReader flickReader;

        [Tooltip("Target sweep: started on begin, stopped on teardown, and used for the aim check.")]
        [SerializeField] private FlickTargetSweep targetSweep;

        [Tooltip("Flick ball — hard-reset to home on teardown.")]
        [SerializeField] private FlickBallController ball;

        [Tooltip("Target visual — reset (tweens killed, pose restored) on teardown.")]
        [SerializeField] private FlickTargetVisual targetVisual;

        [Tooltip("Per-bar enemy this director spawns, damages, and despawns.")]
        [SerializeField] private FlickEnemy flickEnemy;

        [Header("Enemy Damage")]
        [Tooltip("Damage of an unboosted hit on the enemy.")]
        [SerializeField] private int baseDamage = 10;

        [Tooltip("Combo factor: clean on-beat hit N of a streak deals baseDamage * multiplier^(N-1).")]
        [SerializeField] private float comboMultiplier = 1.5f;
        #endregion

        private bool _active;

        #region Public API
        /// <summary>
        /// Starts the enemy loop: assigns the beat manager's aim check, subscribes the
        /// damage/bar-cycle/death handlers, resets the enemy to a fresh full-HP spawn,
        /// and starts the sweep. Idempotent via teardown-first. The host must call
        /// FlickBeatManager.Initialize before this and Arm after it.
        /// </summary>
        /// <param name="sweepOriginVirtual">Virtual time of sweep phase 0 — hosts pass their
        /// session/room start so the sweep is live during any lead-in.</param>
        /// <param name="beatInterval">Seconds per beat (60 / bpm).</param>
        public void BeginEncounter(double sweepOriginVirtual, double beatInterval)
        {
            if (_active) TeardownEncounter();

            if (beatManager == null || targetSweep == null)
            {
                Debug.LogError("[FlickDirector] beatManager or targetSweep not assigned — cannot begin.");
                return;
            }

            beatManager.AimCheck = r => targetSweep.IsThrowOnTarget(r.DragVector, r.StartPosition);
            beatManager.OnReleaseResolved += HandleReleaseResolved;
            beatManager.OnBarResolved     += HandleBarResolved;
            if (flickEnemy != null)
            {
                flickEnemy.ResetImmediate();
                flickEnemy.OnDied += HandleEnemyDied;
            }

            targetSweep.StartSweep(sweepOriginVirtual, beatInterval);
            _active = true;
            Debug.Log($"[FlickDirector] Encounter began — sweep origin vt={sweepOriginVirtual:F4}");
        }

        /// <summary>
        /// The single teardown entry point for EVERY exit path (mode cleanup, room
        /// completion, player death). Unsubscribes and disarms the whole flick stack —
        /// safe to call repeatedly, from any state, including mid-Held (reader track
        /// dropped, ball tweens killed).
        /// </summary>
        public void TeardownEncounter()
        {
            if (beatManager != null)
            {
                beatManager.OnReleaseResolved -= HandleReleaseResolved;
                beatManager.OnBarResolved     -= HandleBarResolved;
                beatManager.StopEncounter();
                beatManager.AimCheck = null;
            }
            if (flickEnemy != null)
            {
                flickEnemy.OnDied -= HandleEnemyDied;
                flickEnemy.ResetImmediate();
            }
            if (targetSweep != null)  targetSweep.StopSweep();
            if (flickReader != null)  flickReader.CancelActiveTracking();
            if (ball != null)         ball.ResetToHome();
            if (targetVisual != null) targetVisual.ResetVisual();
            _active = false;
            Debug.Log("[FlickDirector] Encounter torn down");
        }
        #endregion

        #region Enemy Loop
        // Enemy damage on consumed Perfect/Good releases: off-beat = half base + combo
        // already broken (ComboIndex 0); clean on-beat = base compounded by the streak.
        private void HandleReleaseResolved(FlickReleaseOutcome outcome)
        {
            if (!_active || !outcome.Consumed || flickEnemy == null) return;
            if (outcome.Grade != InputMatch.MatchQuality.Perfect &&
                outcome.Grade != InputMatch.MatchQuality.Good) return; // consumed as fizzle

            int dmg = outcome.IsOffBeat
                ? Mathf.Max(1, baseDamage / 2)
                : Mathf.RoundToInt(baseDamage * Mathf.Pow(comboMultiplier, outcome.ComboIndex));
            flickEnemy.TakeDamage(dmg);
        }

        private void HandleBarResolved(FlickBarResult result)
        {
            if (!_active || flickEnemy == null) return;
            if (!result.Grace) flickEnemy.PlayRetreatDespawn(); // death path already despawned
            flickEnemy.ResetForSpawn(result.NextBarStartVirtual);
        }

        // Enemy HP depleted mid-bar: the rest of the bar is a grace period (no grading,
        // no seam attack); the replacement spawns via HandleBarResolved's grace seam.
        private void HandleEnemyDied()
        {
            if (!_active) return;
            beatManager.NotifyEnemyDefeated();
            flickEnemy.PlayDeathDespawn();
        }
        #endregion
    }
}
