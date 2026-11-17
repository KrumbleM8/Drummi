using System.Collections.Generic;
using KrumbleHut.Drummi.Dungeons;
using UnityEngine;

public static class VerifyNoConsecutiveRests
{
    public static void Execute()
    {
        var gen  = new FlashCardPatternGenerator();
        var fail = new List<string>();

        // (slotCount, minNotes, maxNotes) covering the live ladder: base 4-slot
        // cards, 8-slot expansions, 20-slot legacy expansions, dungeon 8-slot,
        // plus deliberately rest-heavy rolls to exercise the noteCount clamp.
        var cases = new (int slots, int min, int max)[]
        {
            (4, 2, 3), (4, 3, 4), (8, 4, 6), (8, 3, 6), (8, 1, 8),
            (20, 10, 15), (20, 1, 20), (4, 1, 4),
        };
        var vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair |
                    FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.Rest;

        int cards = 0, totalRests = 0;
        foreach (var c in cases)
        {
            for (int i = 0; i < 2000; i++)
            {
                gen.GenerateCard(c.slots, c.min, c.max, DungeonEnemyType.Center, vocab, out var t);
                cards++;
                if (t.Length != c.slots) { fail.Add($"{c}: wrong length {t.Length}"); break; }
                if (t[0] == SquareTemplate.Rest) { fail.Add($"{c}: rest at slot 0"); break; }
                for (int s = 1; s < t.Length; s++)
                {
                    if (t[s] == SquareTemplate.Rest) totalRests++;
                    if (t[s] == SquareTemplate.Rest && t[s - 1] == SquareTemplate.Rest)
                    { fail.Add($"{c}: consecutive rests at {s - 1},{s}"); break; }
                }
                int notes = 0;
                foreach (var x in t) if (x.IsNoteBearing()) notes++;
                // noteCount may exceed the authored max only via the non-adjacency
                // clamp floor; it must never drop below authored min.
                if (notes < c.min) { fail.Add($"{c}: notes {notes} below min"); break; }
            }
        }

        // No-Rest vocab regression: all slots note-bearing.
        for (int i = 0; i < 300; i++)
        {
            gen.GenerateCard(8, 3, 6, DungeonEnemyType.Center, FlashCardVocab.Quarter, out var t);
            foreach (var x in t) if (x == SquareTemplate.Rest) { fail.Add("no-rest vocab produced a rest"); break; }
        }

        Debug.Log(fail.Count == 0
            ? $"PASS: {cards} cards, {totalRests} rests placed, zero consecutive, zero slot-0 rests, mins respected"
            : "FAIL: " + string.Join(" | ", fail));
    }
}
