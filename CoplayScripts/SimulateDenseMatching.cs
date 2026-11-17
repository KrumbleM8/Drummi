using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// PHASE 6 RECON — play-mode simulation of dense-cluster matching against the
/// REAL DungeonEvaluator (scene-serialized fractions). Read-only with respect to
/// game code: fabricates SixteenthQuad beats + tap sets, runs EvaluateBar and
/// EvaluateSingleInput, reports grades. Resets evaluator score after each case.
/// Run in the FlashCards scene (evaluator has no health; sounds may play).
/// </summary>
public static class SimulateDenseMatching
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var evaluator = Object.FindFirstObjectByType<DungeonEvaluator>(FindObjectsInactive.Include);
        var metronome = Object.FindFirstObjectByType<Metronome>(FindObjectsInactive.Include);
        if (evaluator == null || metronome == null) return "FAIL: evaluator/metronome missing";
        if (metronome.bpm <= 0) metronome.bpm = 105;

        double b    = 60.0 / metronome.bpm;   // beat seconds
        double baseT = GameClock.Instance.GameTime; // arbitrary anchor
        var sb = new StringBuilder();
        sb.AppendLine($"bpm={metronome.bpm}  beat={b * 1000:F1}ms  goodBeats={evaluator.EffectiveGoodWindowBeats:F3}");

        // SixteenthQuad: notes at +0, .25, .5, .75 beat.
        List<DungeonScheduledBeat> Quad() => new()
        {
            new DungeonScheduledBeat(baseT + 0.00 * b, DungeonEnemyType.Center),
            new DungeonScheduledBeat(baseT + 0.25 * b, DungeonEnemyType.Center),
            new DungeonScheduledBeat(baseT + 0.50 * b, DungeonEnemyType.Center),
            new DungeonScheduledBeat(baseT + 0.75 * b, DungeonEnemyType.Center),
        };

        List<DungeonInput> Taps(params double[] beats)
        {
            var list = new List<DungeonInput>();
            foreach (var t in beats) list.Add(new DungeonInput(baseT + t * b, DungeonEnemyType.Center));
            return list;
        }

        void Case(string name, double[] taps)
        {
            var result = evaluator.EvaluateBar(Taps(taps), Quad(), 0.0);
            sb.AppendLine(result == null
                ? $"{name}: NULL result"
                : $"{name}: grade={result.Grade} correct={result.CorrectHits}/{result.TotalBeats} perfect={result.PerfectHits} points={result.PointsAwarded}");
            evaluator.ResetScore();
        }

        Case("CONTROL  on-time      ", new[] { 0.00, 0.25, 0.50, 0.75 });
        Case("A1 late +0.10 each    ", new[] { 0.10, 0.35, 0.60, 0.85 });
        Case("A2 late +0.22 each    ", new[] { 0.22, 0.47, 0.72, 0.97 });
        Case("B  mash first half    ", new[] { 0.05, 0.15, 0.25, 0.35 });
        Case("C  6-tap spam         ", new[] { 0.00, 0.18, 0.36, 0.54, 0.72, 0.90 });
        Case("C2 5-tap spam         ", new[] { 0.00, 0.22, 0.44, 0.66, 0.88 });
        Case("TRIPLET-ish on-time   ", new[] { 0.0, 1.0 / 3.0, 2.0 / 3.0, 0.75 }); // sanity: not a real case, ignore

        // Live per-hit attribution probes (nearest-note): quality + which beat matched.
        void Probe(string name, double tapBeat)
        {
            var (q, matched) = evaluator.EvaluateSingleInput(
                new DungeonInput(baseT + tapBeat * b, DungeonEnemyType.Center), Quad(), 0.0);
            double matchedBeat = matched != null ? (matched.scheduledTime - baseT) / b : double.NaN;
            sb.AppendLine($"{name}: quality={q} matchedBeat=+{matchedBeat:F3}");
        }
        Probe("PROBE tap +0.10 (intent beat0)", 0.10);
        Probe("PROBE tap +0.13 (past midpoint)", 0.13);
        Probe("PROBE tap +0.125 (exact midpoint)", 0.125);
        Probe("PROBE tap +0.95 (post-quad rest zone)", 0.95);

        return sb.ToString();
    }
}
