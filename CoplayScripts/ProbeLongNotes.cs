using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Headless generator probe for the long-note blocks (run via coplay execute_script):
/// 300 cards at longNoteChance 1 — every card stays exactly 8 beats, every
/// DottedCrotchet is immediately followed by a Quaver (and vice versa), at most
/// one long block per card, and every scheduled beat lands inside a note slot.
/// </summary>
public static class ProbeLongNotes
{
    public static void Execute()
    {
        var gen = new FlashCardPatternGenerator { longNoteChance = 1f };
        var vocab = FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.Rest
                  | FlashCardVocab.Minim | FlashCardVocab.DottedPair;
        int cards = 300, minims = 0, pairs = 0, violations = 0;
        for (int c = 0; c < cards; c++)
        {
            var beats = gen.GenerateCard(8, 3, 4, DungeonEnemyType.Left, vocab, out var t);
            var starts = SquareTemplateUtil.BuildSlotStartBeats(t);
            if (System.Math.Abs(starts[t.Length] - 8.0) > 1e-6) { violations++; Debug.LogError($"card {c}: length {starts[t.Length]}"); }

            int longBlocks = 0;
            for (int s = 0; s < t.Length; s++)
            {
                if (t[s] == SquareTemplate.Minim) { minims++; longBlocks++; }
                if (t[s] == SquareTemplate.DottedCrotchet)
                {
                    pairs++; longBlocks++;
                    if (s + 1 >= t.Length || t[s + 1] != SquareTemplate.Quaver) { violations++; Debug.LogError($"card {c}: dotted without quaver at {s}"); }
                }
                if (t[s] == SquareTemplate.Quaver && (s == 0 || t[s - 1] != SquareTemplate.DottedCrotchet)) { violations++; Debug.LogError($"card {c}: orphan quaver at {s}"); }
            }
            if (longBlocks > 1) { violations++; Debug.LogError($"card {c}: {longBlocks} long blocks"); }

            foreach (var b in beats)
            {
                int slot = 0;
                for (int s = t.Length - 1; s >= 1; s--) if (b.timeSlot >= starts[s] - 1e-6) { slot = s; break; }
                if (!t[slot].IsNoteBearing()) { violations++; Debug.LogError($"card {c}: beat at {b.timeSlot} inside rest slot {slot}"); }
            }
        }
        Debug.Log($"[ProbeLongNotes] cards={cards} minims={minims} pairs={pairs} violations={violations}");
    }
}
