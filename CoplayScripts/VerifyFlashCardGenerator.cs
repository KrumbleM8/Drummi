using System.Collections.Generic;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PHASE 6 verification (headless generator sample):
///   - 2000 full-vocab cards (Q|E|O|R|S|T): slot 0 always downbeat-bearing
///     (Quarter/EighthPair/SixteenthQuad/Triplet), note-square count in [3, 6],
///     every beat lies on a legal fractional slot with the matching duration.
///   - 500 Quarter|Rest cards: legacy shape unchanged.
///   - Stale-bit test: vocab int 23 (Q|E|retired-bit-4|R) must behave as Q|E|R.
///   - Fallbacks: no note-bearing flag; offbeat-only anchor.
/// </summary>
public static class VerifyFlashCardGenerator
{
    private const float Third = 1f / 3f;

    public static string Execute()
    {
        var gen  = new FlashCardPatternGenerator();
        var fail = new List<string>();
        const FlashCardVocab full = FlashCardVocab.Quarter | FlashCardVocab.EighthPair |
                                    FlashCardVocab.OffbeatEighth | FlashCardVocab.Rest |
                                    FlashCardVocab.SixteenthQuad | FlashCardVocab.Triplet;

        // ── Full vocab, 2000 cards ──
        var counts = new Dictionary<SquareTemplate, int>();
        int anchorViolations = 0, rangeViolations = 0, beatViolations = 0;
        for (int i = 0; i < 2000; i++)
        {
            var beats = gen.GenerateCard(3, 6, DungeonEnemyType.Center, full, out var templates);

            if (!templates[0].HasDownbeatNote()) anchorViolations++;

            int noteSquares = 0;
            foreach (var t in templates)
            {
                counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
                if (t.IsNoteBearing()) noteSquares++;
            }
            if (noteSquares < 3 || noteSquares > 6) rangeViolations++;

            foreach (var b in beats)
            {
                float frac = b.timeSlot - Mathf.Floor(b.timeSlot);
                bool ok =
                    (Approx(frac, 0f)      && (Approx(b.duration, 1f) || Approx(b.duration, 0.5f) || Approx(b.duration, 0.25f) || Approx(b.duration, Third))) ||
                    (Approx(frac, 0.5f)    && (Approx(b.duration, 0.5f) || Approx(b.duration, 0.25f))) ||
                    (Approx(frac, 0.25f)   && Approx(b.duration, 0.25f)) ||
                    (Approx(frac, 0.75f)   && Approx(b.duration, 0.25f)) ||
                    (Approx(frac, Third)   && Approx(b.duration, Third)) ||
                    (Approx(frac, 2f * Third) && Approx(b.duration, Third));
                if (!ok) beatViolations++;
            }
        }
        if (anchorViolations > 0) fail.Add($"ANCHOR VIOLATIONS: {anchorViolations}");
        if (rangeViolations  > 0) fail.Add($"RANGE VIOLATIONS: {rangeViolations}");
        if (beatViolations   > 0) fail.Add($"BEAT VIOLATIONS: {beatViolations}");

        // ── Quarter|Rest regression, 500 cards ──
        int legacyViolations = 0;
        for (int i = 0; i < 500; i++)
        {
            var beats = gen.GenerateCard(3, 6, DungeonEnemyType.Center,
                FlashCardVocab.Quarter | FlashCardVocab.Rest, out var templates);
            foreach (var t in templates)
                if (t != SquareTemplate.Quarter && t != SquareTemplate.Rest) legacyViolations++;
            foreach (var b in beats)
                if (!Approx(b.duration, 1f) || !Approx(b.timeSlot, Mathf.Round(b.timeSlot))) legacyViolations++;
        }
        if (legacyViolations > 0) fail.Add($"LEGACY-SHAPE VIOLATIONS: {legacyViolations}");

        // ── Stale bit 4 (retired DownbeatEighth): int 23 == Q|E|4|R must act as Q|E|R ──
        int staleViolations = 0;
        for (int i = 0; i < 300; i++)
        {
            gen.GenerateCard(3, 6, DungeonEnemyType.Center, (FlashCardVocab)23, out var templates);
            foreach (var t in templates)
                if (t != SquareTemplate.Quarter && t != SquareTemplate.EighthPair && t != SquareTemplate.Rest)
                    staleViolations++;
        }
        if (staleViolations > 0) fail.Add($"STALE-BIT VIOLATIONS: {staleViolations}");

        // ── Fallbacks ──
        gen.GenerateCard(3, 6, DungeonEnemyType.Center, FlashCardVocab.Rest, out var fb1);
        bool fb1Ok = fb1[0] == SquareTemplate.Quarter;
        gen.GenerateCard(8, 8, DungeonEnemyType.Center, FlashCardVocab.OffbeatEighth, out var fb2);
        bool fb2Ok = fb2[0] == SquareTemplate.Quarter;
        for (int s = 1; s < 8; s++) fb2Ok &= fb2[s] == SquareTemplate.OffbeatEighth;
        if (!fb1Ok) fail.Add("FALLBACK(no note-bearing) produced non-Quarter slot 0");
        if (!fb2Ok) fail.Add("FALLBACK(offbeat-only anchor) wrong shape");

        string dist = "dist";
        foreach (var kv in counts) dist += $" {kv.Key.ToLogChar()}={kv.Value}";

        return fail.Count == 0
            ? $"PASS: 2000 full-vocab + 500 legacy + 300 stale-bit cards clean; fallbacks OK; {dist}"
            : $"FAIL: {string.Join(" | ", fail)} | {dist}";
    }

    private static bool Approx(float a, float b) => Mathf.Abs(a - b) < 1e-4f;
}
