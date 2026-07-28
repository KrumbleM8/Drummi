using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// PHASE 1.2 DEBUG HARNESS — standard-combat check for the window-open boundary
    /// tolerance in DungeonInputReader's combat branch.
    ///
    /// Every bar: taps all scheduled beats at their response-window times (correct
    /// pad types read from DungeonBeatManager.ScheduledBeats), with the FIRST tap at
    /// EXACTLY CurrentBar.InputWindowStart — the sub-frame race target. Must be
    /// buffered/accepted, never penalized.
    ///
    /// Every 2nd bar: adds one stray tap at the midpoint of the largest gap between
    /// scheduled beats — only when that midpoint is safely outside every beat's Good
    /// window — expecting EXACTLY one out-of-window penalty (the unchanged in-window
    /// stray-tap path).
    ///
    /// Public fields so a play-mode editor script can AddComponent + wire at runtime
    /// (create inactive, wire, then activate). Editor-only tooling.
    /// </summary>
    public class CombatTestRunner : MonoBehaviour
    {
        public DungeonInputReader inputReader;
        public DungeonBeatManager beatManager;
        public Metronome          metronome;
        public bool               runScenarios = true;

        private struct PendingTap
        {
            public double           time;
            public DungeonEnemyType type;
            public string           label;
        }

        private readonly List<PendingTap> _pending = new();
        private int _lastScheduledBar = int.MinValue;
        private int _barsHandled;

        private void Update()
        {
            if (!runScenarios) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;
            var coordinator = TimingCoordinator.Instance;
            if (coordinator == null || inputReader == null || beatManager == null || metronome == null) return;
            if (!inputReader.EncounterActive) return;

            double now = GameClock.Instance.GameTime;
            var bar    = coordinator.CurrentBar;

            // Arm taps for the current bar once its pattern exists, before the window opens.
            if (bar.BarIndex != _lastScheduledBar
                && now >= bar.PatternStartTime && now < bar.InputWindowStart
                && beatManager.ScheduledBeats.Count > 0)
            {
                _lastScheduledBar = bar.BarIndex;
                ArmBar(bar);
            }

            // Fire pending taps.
            while (_pending.Count > 0 && now >= _pending[0].time)
            {
                var tap = _pending[0];
                _pending.RemoveAt(0);
                Debug.Log($"[CombatTest] Inject tap | {tap.label} type={tap.type} " +
                          $"scheduled={tap.time:F6} actual={now:F6} delta=+{(now - tap.time) * 1000.0:F3}ms");
                inputReader.TriggerInput(tap.type);
            }
        }

        private void ArmBar(TimingCoordinator.BarTiming bar)
        {
            _pending.Clear();
            double timeOffset   = bar.InputWindowStart - bar.PatternStartTime;
            double beatInterval = 60.0 / metronome.bpm;
            var beats           = beatManager.ScheduledBeats;

            // Clean taps: one per beat at its response time. The FIRST tap fires at
            // EXACTLY InputWindowStart (race target); its beat's true response time is
            // +PATTERN_OFFSET (3 ms) later — deep inside the Perfect window.
            var responseTimes = new List<double>(beats.Count);
            for (int i = 0; i < beats.Count; i++)
            {
                double tapTime = i == 0 ? bar.InputWindowStart : beats[i].scheduledTime + timeOffset;
                responseTimes.Add(beats[i].scheduledTime + timeOffset);
                string tag = i == 0 ? "BOUNDARY tap at InputWindowStart (race target)" : "clean tap";
                _pending.Add(new PendingTap { time = tapTime, type = beats[i].enemyType, label = $"bar={bar.BarIndex} beat={i} {tag}" });
            }

            // Every 2nd handled bar: stray tap in the largest inter-beat gap, only if the
            // midpoint clears every beat's Good window (0.25 beat) with 60% margin.
            if (_barsHandled % 2 == 1)
            {
                double goodThreshold = 0.25 * beatInterval;
                double bestMid = -1, bestClearance = 0;
                for (int i = 0; i < responseTimes.Count - 1; i++)
                {
                    double mid       = 0.5 * (responseTimes[i] + responseTimes[i + 1]);
                    double clearance = 0.5 * (responseTimes[i + 1] - responseTimes[i]);
                    if (clearance > bestClearance) { bestClearance = clearance; bestMid = mid; }
                }
                if (bestMid > 0 && bestClearance > goodThreshold * 1.6)
                {
                    _pending.Add(new PendingTap { time = bestMid, type = DungeonEnemyType.Center,
                        label = $"bar={bar.BarIndex} STRAY gap tap (expect exactly one penalty, clearance={bestClearance * 1000.0:F0}ms)" });
                }
                else
                {
                    Debug.Log($"[CombatTest] bar={bar.BarIndex}: no safe gap for stray tap (best clearance {bestClearance * 1000.0:F0}ms) — skipped.");
                }
            }

            _pending.Sort((a, b) => a.time.CompareTo(b.time));
            _barsHandled++;
            Debug.Log($"[CombatTest] Bar armed | bar={bar.BarIndex} beats={beats.Count} taps={_pending.Count}");
        }
    }
}
