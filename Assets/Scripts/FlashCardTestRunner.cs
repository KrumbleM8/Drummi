using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// PHASE 1 DEBUG HARNESS — injects synthetic taps into DungeonInputReader at
    /// scheduled virtual times to verify the Flash Card reveal/perform cycle without
    /// touch input. Editor-only tooling; safe to leave disabled in the scene.
    ///
    /// One scenario per card index (cycling for encounters longer than 4 cards):
    ///   card 0 — CLEAN:      tap every note at its perform-bar time.
    ///   card 1 — REST TAP:   clean + one extra tap on the first rest slot
    ///                        (expects the out-of-window penalty).
    ///   card 2 — DOUBLE TAP: clean + a second tap 40 ms after the first note
    ///                        (observes double-hit consumption behaviour).
    ///   card 3 — REVEAL TAP: one tap mid-reveal-bar (expects silent ignore,
    ///                        no penalty, no evaluator pollution) + clean perform.
    ///
    /// All schedule times are GameClock virtual time; injection fires from Update
    /// polling, so tap accuracy is one frame (~16 ms at 60 fps) — well inside the
    /// Perfect window at any sane BPM.
    /// </summary>
    public class FlashCardTestRunner : MonoBehaviour
    {
        [SerializeField] private FlashCardController controller;
        [SerializeField] private DungeonInputReader  inputReader;

        [Tooltip("Master switch — when false, this component does nothing.")]
        [SerializeField] private bool runScenarios = true;

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
                controller.OnCardRevealStart += HandleCardRevealStart;
                controller.OnEncounterComplete += HandleEncounterComplete;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCardRevealStart -= HandleCardRevealStart;
                controller.OnEncounterComplete -= HandleEncounterComplete;
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
                Debug.Log($"[FlashCardTest] Inject tap | {tap.label} " +
                          $"scheduled={tap.time:F6} actual={now:F6} delta=+{(now - tap.time) * 1000.0:F3}ms");
                inputReader?.TriggerInput(DungeonEnemyType.Center);
            }
        }

        // ── Scenario construction ─────────────────────────────────────────────

        private void HandleCardRevealStart(int cardIndex)
        {
            if (!runScenarios) return;
            _pending.Clear();

            var timing   = controller.CurrentTiming;
            var noteMask = controller.CurrentNoteMask;
            if (noteMask == null) return;

            double beatInterval = (timing.ResponseStartTime - timing.CallStartTime)
                                  / FlashCardPatternGenerator.SlotCount;
            double performStart = timing.ResponseStartTime;

            int scenario = cardIndex % 4;

            // Base: clean perform — one tap per note slot at its perform-bar time.
            // Slot 0 is nudged +25 ms (still deep in the Perfect window): a tap at
            // EXACTLY the window-open boundary races the controller's same-frame
            // window-open and is silently dropped (see Phase 1 report, finding on the
            // [ResponseStartTime, window-open] frame gap — also present in C&R).
            const double FirstSlotNudge = 0.025;
            for (int slot = 0; slot < noteMask.Count; slot++)
            {
                if (noteMask[slot])
                    Enqueue(performStart + slot * beatInterval + (slot == 0 ? FirstSlotNudge : 0.0),
                            $"card={cardIndex} slot={slot} NOTE (clean)");
            }

            switch (scenario)
            {
                case 1: // Rest tap — extra tap on the first rest slot.
                {
                    int restSlot = FindFirstRest(noteMask);
                    if (restSlot >= 0)
                        Enqueue(performStart + restSlot * beatInterval,
                                $"card={cardIndex} slot={restSlot} REST-TAP (expect out-of-window penalty)");
                    else
                        Debug.LogWarning($"[FlashCardTest] card={cardIndex}: no rest slot on this card — rest-tap scenario skipped.");
                    break;
                }
                case 2: // Double tap — second tap 65 ms after the first note
                        // (first tap lands at +25 ms; both inside the note's Good window).
                {
                    Enqueue(performStart + 0.065,
                            $"card={cardIndex} slot=0 DOUBLE-TAP (+65ms after first note)");
                    break;
                }
                case 3: // Reveal tap — mid-reveal-bar tap, expects silent ignore.
                {
                    Enqueue(timing.CallStartTime + 2.5 * beatInterval,
                            $"card={cardIndex} REVEAL-TAP at beat 2.5 of reveal bar (expect silent ignore)");
                    break;
                }
            }

            _pending.Sort((a, b) => a.time.CompareTo(b.time));

            Debug.Log($"[FlashCardTest] Scenario armed | card={cardIndex} " +
                      $"scenario={(scenario == 0 ? "CLEAN" : scenario == 1 ? "REST-TAP" : scenario == 2 ? "DOUBLE-TAP" : "REVEAL-TAP")} " +
                      $"taps={_pending.Count}");
        }

        private void HandleEncounterComplete() => _pending.Clear();

        private void Enqueue(double time, string label)
            => _pending.Add(new PendingTap { time = time, label = label });

        private static int FindFirstRest(IReadOnlyList<bool> noteMask)
        {
            for (int i = 0; i < noteMask.Count; i++)
                if (!noteMask[i]) return i;
            return -1;
        }
    }
}
