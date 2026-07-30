using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// PHASE 1 DEBUG HARNESS — injects synthetic taps into DungeonInputReader at
    /// scheduled virtual times to verify the Flash Card reveal/perform cycle without
    /// touch input. Editor-only tooling; safe to leave disabled in the scene.
    ///
    /// One scenario per card index (cycling for encounters longer than 5 cards):
    ///   card 0 — CLEAN:        tap every note at its perform-bar time (eighths
    ///                          included: offbeat taps at slot + 0.5).
    ///   card 1 — REST TAP:     clean + one extra tap on the first full-rest slot
    ///                          (expects the out-of-window penalty).
    ///   card 2 — DOUBLE TAP:   clean + a second tap 65 ms after the first note
    ///                          (observes double-hit consumption behaviour).
    ///   card 3 — REVEAL TAP:   one tap mid-reveal-bar (expects silent ignore,
    ///                          no penalty, no evaluator pollution) + clean perform.
    ///   card 4 — OFFBEAT PROBE: clean + one extra tap on the DOWNBEAT of the first
    ///                          OffbeatEighth square (expects the out-of-window
    ///                          penalty — the square's only note is at +0.5, whose
    ///                          clean tap grades Perfect). Skipped with a warning
    ///                          when the card has no OffbeatEighth square.
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
                // External stop (death/interrupt) must also clear queued taps —
                // otherwise they keep injecting after the encounter is torn down.
                controller.OnEncounterStopped += HandleEncounterComplete;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnCardRevealStart -= HandleCardRevealStart;
                controller.OnEncounterComplete -= HandleEncounterComplete;
                controller.OnEncounterStopped -= HandleEncounterComplete;
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

            var timing    = controller.CurrentTiming;
            var templates = controller.CurrentTemplates;
            if (templates == null) return;

            double beatInterval = (timing.ResponseStartTime - timing.CallStartTime)
                                  / FlashCardPatternGenerator.SlotCount;
            double performStart = timing.ResponseStartTime;

            int scenario = cardIndex % 5;

            // Base: clean perform — one tap per note position at its perform-bar time
            // (offbeat eighths at slot + 0.5). Slot 0 fires at EXACTLY the window-open
            // boundary: this intentionally exercises the sub-frame race covered by
            // DungeonInputReader's windowOpenLateToleranceSeconds (Phase 1.1) — the
            // tap must be coyote-buffered and replayed, not silently dropped.
            for (int slot = 0; slot < templates.Count; slot++)
            {
                switch (templates[slot])
                {
                    case SquareTemplate.Quarter:
                        Enqueue(performStart + slot * beatInterval, $"card={cardIndex} slot={slot} QUARTER (clean)");
                        break;
                    case SquareTemplate.EighthPair:
                        Enqueue(performStart + slot * beatInterval,          $"card={cardIndex} slot={slot} EIGHTH-PAIR down (clean)");
                        Enqueue(performStart + (slot + 0.5) * beatInterval,  $"card={cardIndex} slot={slot} EIGHTH-PAIR off (clean)");
                        break;
                    case SquareTemplate.DownbeatEighth:
                        Enqueue(performStart + slot * beatInterval, $"card={cardIndex} slot={slot} DOWNBEAT-8TH (clean)");
                        break;
                    case SquareTemplate.OffbeatEighth:
                        Enqueue(performStart + (slot + 0.5) * beatInterval,
                                $"card={cardIndex} slot={slot} OFFBEAT-8TH +0.5 (clean, expect Perfect)");
                        break;
                    // SquareTemplate.Rest: no tap.
                }
            }

            switch (scenario)
            {
                case 1: // Rest tap — extra tap on the first full-rest slot.
                {
                    int restSlot = FindFirstTemplate(templates, SquareTemplate.Rest);
                    if (restSlot >= 0)
                        Enqueue(performStart + restSlot * beatInterval,
                                $"card={cardIndex} slot={restSlot} REST-TAP (expect out-of-window penalty)");
                    else
                        Debug.LogWarning($"[FlashCardTest] card={cardIndex}: no rest slot on this card — rest-tap scenario skipped.");
                    break;
                }
                case 2: // Double tap — second tap 65 ms after the first note
                        // (both inside the note's Good window).
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
                case 4: // Offbeat probe — tap the DOWNBEAT of the first OffbeatEighth
                        // square. The square's only note is at +0.5, a half beat away
                        // (beyond the Good window) — geometric rest enforcement must
                        // penalize this tap; the clean +0.5 tap grades Perfect.
                {
                    int offSlot = FindFirstTemplate(templates, SquareTemplate.OffbeatEighth);
                    if (offSlot >= 0)
                        Enqueue(performStart + offSlot * beatInterval,
                                $"card={cardIndex} slot={offSlot} OFFBEAT-PROBE downbeat tap (expect out-of-window penalty)");
                    else
                        Debug.LogWarning($"[FlashCardTest] card={cardIndex}: no OffbeatEighth square on this card — offbeat-probe scenario skipped.");
                    break;
                }
            }

            _pending.Sort((a, b) => a.time.CompareTo(b.time));

            Debug.Log($"[FlashCardTest] Scenario armed | card={cardIndex} " +
                      $"scenario={(scenario == 0 ? "CLEAN" : scenario == 1 ? "REST-TAP" : scenario == 2 ? "DOUBLE-TAP" : scenario == 3 ? "REVEAL-TAP" : "OFFBEAT-PROBE")} " +
                      $"taps={_pending.Count}");
        }

        private void HandleEncounterComplete() => _pending.Clear();

        private void Enqueue(double time, string label)
            => _pending.Add(new PendingTap { time = time, label = label });

        private static int FindFirstTemplate(IReadOnlyList<SquareTemplate> templates, SquareTemplate wanted)
        {
            for (int i = 0; i < templates.Count; i++)
                if (templates[i] == wanted) return i;
            return -1;
        }
    }
}
