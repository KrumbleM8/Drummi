using System;
using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Run-scoped holder of active dungeon effects (challenge modifiers).
    /// Systems query the aggregate at point of use (multipliers multiply, bools OR)
    /// and must treat a missing instance as "no effects" — this component only
    /// exists in the Dungeon scene.
    ///
    /// Lifecycle hooks (called by DungeonRunner):
    ///   ResetForRun()        — run start, clears everything
    ///   NotifyRoomStarted()  — every room start; ticks/expires NRooms effects
    ///   NotifyFloorAdvanced()— floor transition; expires Floor effects
    /// </summary>
    public class DungeonEffectsController : MonoBehaviour
    {
        public static DungeonEffectsController Instance { get; private set; }

        [Tooltip("All effects that can appear in a pick offer.")]
        [SerializeField] private List<EffectDefinition> effectPool = new List<EffectDefinition>();

        /// <summary>Fired when an effect is applied via ApplyEffect.</summary>
        public event Action<EffectDefinition> OnEffectGained;

        /// <summary>Fired when an active effect expires (floor change or room countdown).</summary>
        public event Action<EffectDefinition> OnEffectExpired;

        private class ActiveEffect
        {
            public EffectDefinition Definition;
            public int RoomsRemaining; // only meaningful for NRooms
        }

        private readonly List<ActiveEffect> _active = new List<ActiveEffect>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Queries (read-sites aggregate across active effects) ──────────────

        /// <summary>
        /// Combined timing-window scale from all active effects. 1 = unmodified.
        /// Multipliers multiply, so two shrink effects stack multiplicatively.
        /// </summary>
        public float TimingWindowMultiplier
        {
            get
            {
                float m = 1f;
                foreach (var e in _active)
                    foreach (var mod in e.Definition.Modifiers)
                        if (mod.Type == EffectModifierType.TimingWindowMultiplier)
                            m *= mod.Value;
                return m;
            }
        }

        /// <summary>True when any active effect hides the slider spawn markers.</summary>
        public bool HideSliderMarkers
        {
            get
            {
                foreach (var e in _active)
                    foreach (var mod in e.Definition.Modifiers)
                        if (mod.Type == EffectModifierType.HideSliderMarkers)
                            return true;
                return false;
            }
        }

        /// <summary>Currently active effect definitions (for UI/debug).</summary>
        public IReadOnlyList<EffectDefinition> ActiveEffects
        {
            get
            {
                var list = new List<EffectDefinition>(_active.Count);
                foreach (var e in _active) list.Add(e.Definition);
                return list;
            }
        }

        // ── Offers / application ──────────────────────────────────────────────

        /// <summary>
        /// Random distinct effects from the pool for a pick offer, excluding effects
        /// that are currently active (they become eligible again after expiry).
        /// Returns fewer than <paramref name="count"/> when the pool runs short;
        /// empty list = caller should skip the pick phase entirely.
        /// </summary>
        public List<EffectDefinition> GetOffer(int count)
        {
            var eligible = new List<EffectDefinition>();
            foreach (var def in effectPool)
            {
                if (def == null) continue;
                if (IsActive(def)) continue;
                eligible.Add(def);
            }

            // Fisher–Yates partial shuffle, take first `count`.
            for (int i = 0; i < eligible.Count - 1; i++)
            {
                int j = UnityEngine.Random.Range(i, eligible.Count);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }
            if (eligible.Count > count)
                eligible.RemoveRange(count, eligible.Count - count);
            return eligible;
        }

        /// <summary>Activates <paramref name="definition"/> and fires OnEffectGained.</summary>
        public void ApplyEffect(EffectDefinition definition)
        {
            if (definition == null || IsActive(definition)) return;

            _active.Add(new ActiveEffect
            {
                Definition     = definition,
                RoomsRemaining = definition.RoomCount
            });
            Debug.Log($"[DungeonEffectsController] Effect gained: '{definition.DisplayName}' ({definition.DurationText})");
            OnEffectGained?.Invoke(definition);
        }

        // ── Lifecycle hooks ───────────────────────────────────────────────────

        /// <summary>
        /// Call at every room start. NRooms effects expire once their room budget is
        /// spent, then remaining budgets tick down — an effect picked between rooms
        /// is active for exactly RoomCount rooms starting with the next one.
        /// </summary>
        public void NotifyRoomStarted()
        {
            ExpireWhere(e => e.Definition.Duration == EffectDuration.NRooms && e.RoomsRemaining <= 0);
            foreach (var e in _active)
                if (e.Definition.Duration == EffectDuration.NRooms)
                    e.RoomsRemaining--;
        }

        /// <summary>Call when advancing to a new floor. Expires Floor-scoped effects.</summary>
        public void NotifyFloorAdvanced()
        {
            ExpireWhere(e => e.Definition.Duration == EffectDuration.Floor);
        }

        /// <summary>Clears all active effects at run start (no expiry events fired).</summary>
        public void ResetForRun()
        {
            _active.Clear();
            Debug.Log("[DungeonEffectsController] Reset for new run");
        }

        // ── Private ───────────────────────────────────────────────────────────

        private bool IsActive(EffectDefinition def)
        {
            foreach (var e in _active)
                if (e.Definition == def) return true;
            return false;
        }

        private void ExpireWhere(Predicate<ActiveEffect> predicate)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!predicate(_active[i])) continue;
                var def = _active[i].Definition;
                _active.RemoveAt(i);
                Debug.Log($"[DungeonEffectsController] Effect expired: '{def.DisplayName}'");
                OnEffectExpired?.Invoke(def);
            }
        }
    }
}
