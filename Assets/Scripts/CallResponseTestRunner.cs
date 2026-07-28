using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// PHASE 1.1 DEBUG HARNESS — C&R regression check for the window-open boundary
    /// tolerance (DungeonInputReader.windowOpenLateToleranceSeconds).
    ///
    /// Per round: injects the FIRST echo tap at EXACTLY the response boundary
    /// (the sub-frame race target — must now be coyote-buffered and replayed, not
    /// dropped), the remaining echo taps on their beats, and one mid-call-window
    /// tap that must still be silently ignored (no penalty, no evaluator pollution).
    ///
    /// Public fields (not SerializeField) so a play-mode editor script can
    /// AddComponent + wire at runtime without scene changes. Editor-only tooling.
    /// </summary>
    public class CallResponseTestRunner : MonoBehaviour
    {
        public CallAndResponseController controller;
        public DungeonInputReader        inputReader;
        public Metronome                 metronome;
        public bool                      runScenarios = true;

        private struct PendingTap
        {
            public double time;
            public string label;
        }

        private readonly List<PendingTap> _pending = new();

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnCallStart          += HandleCallStart;
                controller.OnEncounterComplete  += HandleEncounterComplete;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCallStart          -= HandleCallStart;
                controller.OnEncounterComplete  -= HandleEncounterComplete;
            }
            _pending.Clear();
        }

        private void Update()
        {
            if (!runScenarios || _pending.Count == 0) return;
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;
            while (_pending.Count > 0 && now >= _pending[0].time)
            {
                var tap = _pending[0];
                _pending.RemoveAt(0);
                Debug.Log($"[CRTest] Inject tap | {tap.label} " +
                          $"scheduled={tap.time:F6} actual={now:F6} delta=+{(now - tap.time) * 1000.0:F3}ms");
                inputReader?.TriggerInput(DungeonEnemyType.Center);
            }
        }

        private void HandleCallStart(int roundIndex, int n)
        {
            if (!runScenarios || metronome == null) return;
            _pending.Clear();

            var phrase = controller.CurrentCallPhrase;
            if (phrase == null || phrase.Count == 0) return;

            double beatInterval  = 60.0 / metronome.bpm;
            // Call phrases are anchored: beat[0].scheduledTime == CallStartTime exactly.
            double callStart     = phrase[0].scheduledTime;
            double responseStart = callStart + n * beatInterval;

            // Mid-call tap — well outside the coyote window; must be silently ignored.
            Enqueue(callStart + 0.4 * n * beatInterval,
                    $"round={roundIndex} CALL-WINDOW tap (expect silent ignore)");

            // Echo taps: one per call beat at its response-window time.
            // Beat 0 lands at EXACTLY the boundary (the Phase 1.1 race target).
            for (int i = 0; i < phrase.Count; i++)
            {
                double tapTime = phrase[i].scheduledTime + n * beatInterval;
                string tag     = i == 0 ? "BOUNDARY echo tap (race target)" : "echo tap";
                Enqueue(tapTime, $"round={roundIndex} beat={i} {tag}");
            }

            _pending.Sort((a, b) => a.time.CompareTo(b.time));
            Debug.Log($"[CRTest] Scenario armed | round={roundIndex} N={n} taps={_pending.Count}");
        }

        private void HandleEncounterComplete() => _pending.Clear();

        private void Enqueue(double time, string label)
            => _pending.Add(new PendingTap { time = time, label = label });
    }
}
