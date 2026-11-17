using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Creates (or updates) the two new globe-sign definition assets:
///   FlashCards Classic — original 2x4 ruleset via rule overrides.
///   Bongo Scene        — SceneLaunchDefinition that loads Bongo.unity.
/// </summary>
public static class CreateSignDefinitions
{
    public static void Execute()
    {
        const string folder = "Assets/ScriptableObjects/FlashCards";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "FlashCards");

        // ── Classic ──────────────────────────────────────────────────────────
        string classicPath = folder + "/FlashCards Classic.asset";
        var classic = AssetDatabase.LoadAssetAtPath<FlashCardsModeDefinition>(classicPath);
        bool classicNew = classic == null;
        if (classicNew) classic = ScriptableObject.CreateInstance<FlashCardsModeDefinition>();

        classic.displayName     = "Classic";
        classic.highScoreKey    = "FlashCardsClassicHS";
        classic.tempoMultiplier = 1f;
        classic.layout          = FlashCardLayout.Grid;
        classic.overrideRules   = true;
        classic.pipelineReveal  = false;
        classic.compressReveal  = false;
        classic.revealBars      = 2;
        classic.onDeckReveal    = false;
        classic.wholeCardGrading   = true;  // one grade per card, not per row
        classic.allowAdjacentRests = true;  // original rest placement (varied cards)

        FlashCardVocab q    = FlashCardVocab.Quarter | FlashCardVocab.Rest;
        FlashCardVocab qe   = q    | FlashCardVocab.EighthPair;
        FlashCardVocab qeo  = qe   | FlashCardVocab.OffbeatEighth;
        FlashCardVocab qeot = qeo  | FlashCardVocab.Triplet;
        FlashCardVocab all  = qeot | FlashCardVocab.SixteenthQuad;

        classic.escalationSteps = new[]
        {
            Step(0,   q,    3, 4),
            Step(32,  qe,   3, 5),
            Step(80,  qeo,  4, 6),
            Step(128, qeot, 4, 6),
            Step(176, all,  4, 6),
        };

        if (classicNew) AssetDatabase.CreateAsset(classic, classicPath);
        else EditorUtility.SetDirty(classic);

        // ── Bongo (scene launch) ─────────────────────────────────────────────
        string bongoPath = folder + "/Bongo Scene.asset";
        var bongo = AssetDatabase.LoadAssetAtPath<SceneLaunchDefinition>(bongoPath);
        bool bongoNew = bongo == null;
        if (bongoNew) bongo = ScriptableObject.CreateInstance<SceneLaunchDefinition>();

        bongo.displayName  = "Bongo";
        bongo.highScoreKey = "BongoSign"; // unlock-key root only; per-song boards live on SongDefinition
        bongo.sceneName    = "Bongo";

        if (bongoNew) AssetDatabase.CreateAsset(bongo, bongoPath);
        else EditorUtility.SetDirty(bongo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateSignDefinitions] Classic " + (classicNew ? "created" : "updated")
                  + " (" + classic.escalationSteps.Length + " steps); Bongo " + (bongoNew ? "created" : "updated") + ".");
    }

    private static FlashCardsModeController.EscalationStep Step(int minBlocks, FlashCardVocab vocab, int minNotes, int maxNotes)
        => new FlashCardsModeController.EscalationStep
        {
            minBlocksCleared = minBlocks,
            vocab            = vocab,
            noteRange        = new Vector2Int(minNotes, maxNotes),
            doubleTimeChance = 0f,   // user decision: no in-run DT in Classic
            rows             = 2,    // original 2x4 card
            expandRows       = 0,    // never expands
            expandChance     = 0f,
            tempoMultiplier  = 1f,   // no stage tempo-ups
        };
}
