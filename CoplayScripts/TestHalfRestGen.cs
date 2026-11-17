using UnityEngine;

public class TestHalfRestGen
{
    public static void Execute()
    {
        var genType = System.Type.GetType("KrumbleHut.Drummi.Dungeons.FlashCardPatternGenerator, Assembly-CSharp");
        if (genType == null) { Debug.Log("[HalfRestTest] generator type not found"); return; }
        object gen = System.Activator.CreateInstance(genType);
        genType.GetField("halfRestChance").SetValue(gen, 1f);
        genType.GetField("halfRestCooldownCards").SetValue(gen, 2);
        genType.GetField("offbeatCardsPerEpisode").SetValue(gen, 1);
        // Episode-shape validator: idle → opener(HalfRest, 8 slots, 7.5 beats) →
        // one off-beat card (8 slots, 8 beats, no flip) → resolver(HalfRest not in
        // the last two slots + appended balancing Rest: 9 slots, 8.5 beats) →
        // >=2 cooldown cards → idle. Episodes must be NET-ZERO in time.
        int epState = 0, sinceResolve = 99, episodeViolations = 0;
        double totalBeats = 0.0;

        var method = genType.GetMethod("GenerateCard");
        var vocabType = System.Type.GetType("KrumbleHut.Drummi.Dungeons.FlashCardVocab, Assembly-CSharp");
        var enemyType = System.Type.GetType("DungeonEnemyType, Assembly-CSharp");
        object vocab = System.Enum.ToObject(vocabType, 1 | 2 | 8 | 16 | 64 | 128); // Q|E|O|R|T|h
        object center = System.Enum.ToObject(enemyType, 0);

        int cards = 300, episodes = 0, errors = 0;
        for (int c = 0; c < cards; c++)
        {
            object[] args = { 8, 3, 4, center, vocab, null };
            object beatsObj = method.Invoke(gen, args);
            var templates = (System.Array)args[5];
            int len = templates.Length;

            int halfCount = 0, flipSlot = -1;
            for (int s = 0; s < len; s++)
                if (templates.GetValue(s).ToString() == "HalfRest") { halfCount++; flipSlot = s; }
            if (halfCount > 1) { errors++; Debug.Log("[HalfRestTest] card " + c + ": multiple half-rests!"); }
            if (flipSlot == 0) { errors++; Debug.Log("[HalfRestTest] card " + c + ": half-rest in slot 0!"); }

            if (epState == 0)
            {
                if (len != 8) { errors++; Debug.Log("[HalfRestTest] card " + c + ": " + len + " slots outside resolver"); }
                if (halfCount > 0)
                {
                    if (sinceResolve < 2) { episodeViolations++; Debug.Log("[HalfRestTest] card " + c + ": opener inside cooldown"); }
                    episodes++;
                    epState = 1;
                }
                else sinceResolve++;
            }
            else if (epState == 1)
            {
                if (halfCount > 0) { episodeViolations++; Debug.Log("[HalfRestTest] card " + c + ": flip on the off-beat card"); }
                if (len != 8) { errors++; Debug.Log("[HalfRestTest] card " + c + ": off-beat card has " + len + " slots"); }
                epState = 2;
            }
            else
            {
                // Resolver: half-rest present, 9 slots, last is the balancing Rest,
                // and the half-rest never touches it (no fused 1.5 gap).
                if (halfCount == 0) { episodeViolations++; Debug.Log("[HalfRestTest] card " + c + ": resolver missing half-rest"); }
                if (len != 9) { errors++; Debug.Log("[HalfRestTest] card " + c + ": resolver has " + len + " slots"); }
                if (templates.GetValue(len - 1).ToString() != "Rest")
                { errors++; Debug.Log("[HalfRestTest] card " + c + ": balancing slot is " + templates.GetValue(len - 1)); }
                if (flipSlot >= len - 2)
                { errors++; Debug.Log("[HalfRestTest] card " + c + ": resolver half-rest at slot " + flipSlot + " fuses with balancing rest"); }
                epState = 0; sinceResolve = 0;
            }

            // Validate beat emission: recompute expected cumulative starts and check
            // every emitted timeSlot falls inside a note-bearing slot's span.
            var beats = (System.Collections.IEnumerable)beatsObj;
            float[] starts = new float[len + 1];
            float cursor = 0f;
            for (int s = 0; s < len; s++)
            {
                starts[s] = cursor;
                cursor += templates.GetValue(s).ToString() == "HalfRest" ? 0.5f : 1f;
            }
            starts[len] = cursor;
            float expectedLen = len == 9 ? 8.5f : halfCount > 0 ? 7.5f : 8f;
            if (Mathf.Abs(cursor - expectedLen) > 0.001f)
            { errors++; Debug.Log("[HalfRestTest] card " + c + ": length " + cursor + " expected " + expectedLen); }
            totalBeats += cursor;

            // Net-zero check: at idle (outside an episode) the running total must
            // sit on a whole multiple of 8 beats — bar alignment preserved.
            if (epState == 0 && System.Math.Abs(totalBeats % 8.0) > 0.001 &&
                System.Math.Abs(totalBeats % 8.0 - 8.0) > 0.001)
            { errors++; Debug.Log("[HalfRestTest] card " + c + ": cumulative " + totalBeats + " off the bar grid at idle"); }

            var tsField = System.Type.GetType("DungeonBeat, Assembly-CSharp").GetField("timeSlot");
            foreach (object b in beats)
            {
                float t = (float)tsField.GetValue(b);
                int host = -1;
                for (int s = len - 1; s >= 0; s--)
                    if (t >= starts[s] - 0.001f) { host = s; break; }
                string tpl = templates.GetValue(host).ToString();
                if (tpl == "Rest" || tpl == "HalfRest")
                { errors++; Debug.Log("[HalfRestTest] card " + c + ": beat at " + t + " lands in " + tpl + " slot " + host); }
            }
        }
        // Stage-pending suppression: from a clean idle state with suppression on,
        // NO card may start an episode (chance is still 1).
        genType.GetMethod("ResetEpisode").Invoke(gen, null);
        genType.GetField("suppressNewEpisodes").SetValue(gen, true);
        int suppressedHalfRests = 0;
        for (int c = 0; c < 20; c++)
        {
            object[] args = { 8, 3, 4, center, vocab, null };
            method.Invoke(gen, args);
            var templates = (System.Array)args[5];
            for (int s = 0; s < templates.Length; s++)
                if (templates.GetValue(s).ToString() == "HalfRest") suppressedHalfRests++;
        }
        if (suppressedHalfRests > 0) errors++;

        Debug.Log("[HalfRestTest] " + cards + " cards, " + episodes + " episodes, errors=" + errors +
                  ", episodeViolations=" + episodeViolations + ", totalBeats=" + totalBeats +
                  ", suppressedHalfRests=" + suppressedHalfRests + " (expect 0)");
    }
}
