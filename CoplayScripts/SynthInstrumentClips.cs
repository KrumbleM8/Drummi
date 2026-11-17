using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Synthesizes real Woodblock + Clap one-shot WAVs (no such samples exist in the
/// project and AI SFX generation is unauthorized), assigns Woodblock to the
/// existing Woodblock instrument, and adds a 4th "Clap" instrument to the mode
/// controller's cycle. Saves assets + scene.
/// </summary>
public static class SynthInstrumentClips
{
    private const int Rate = 44100;

    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log($"[Synth] ABORT: scene '{scene.name}'."); return; }

        // ── Synthesize + write WAVs ──
        WriteWav("Assets/Audio/Instruments/Woodblock.wav", SynthWoodblock());
        WriteWav("Assets/Audio/Instruments/Clap.wav",      SynthClap());
        AssetDatabase.ImportAsset("Assets/Audio/Instruments/Woodblock.wav");
        AssetDatabase.ImportAsset("Assets/Audio/Instruments/Clap.wav");
        var woodClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Instruments/Woodblock.wav");
        var clapClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Instruments/Clap.wav");
        if (woodClip == null || clapClip == null) { Debug.Log("[Synth] ABORT: clips failed to import."); return; }

        // ── Woodblock instrument: swap the click.wav placeholder for the real synth ──
        var wood = AssetDatabase.LoadAssetAtPath<FlashCardInstrument>(
            "Assets/ScriptableObjects/FlashCards/Instruments/Instrument_Woodblock.asset");
        if (wood == null) { Debug.Log("[Synth] ABORT: Woodblock instrument asset missing."); return; }
        wood.tapClip = woodClip;
        EditorUtility.SetDirty(wood);

        // ── New Clap instrument ──
        string clapPath = "Assets/ScriptableObjects/FlashCards/Instruments/Instrument_Clap.asset";
        var clap = AssetDatabase.LoadAssetAtPath<FlashCardInstrument>(clapPath);
        if (clap == null)
        {
            clap = ScriptableObject.CreateInstance<FlashCardInstrument>();
            AssetDatabase.CreateAsset(clap, clapPath);
        }
        clap.displayName = "Clap";
        clap.tapClip     = clapClip;
        clap.buttonTint  = new Color(0.55f, 0.75f, 0.95f); // light blue
        EditorUtility.SetDirty(clap);
        AssetDatabase.SaveAssets();

        // ── Append Clap to the mode controller's cycle ──
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[Synth] ABORT: mode controller not found."); return; }
        var so  = new SerializedObject(mode);
        var arr = so.FindProperty("instruments");
        bool present = false;
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == clap) present = true;
        if (!present)
        {
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = clap;
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Synth] Woodblock.wav ({woodClip.length:F2}s) + Clap.wav ({clapClip.length:F2}s) synthesized; " +
                  $"Woodblock instrument retargeted, Clap appended (cycle size {arr.arraySize}). Saved={saved}.");
    }

    // Woodblock: sharp attack, fast-decaying sine around 850 Hz with a bright
    // partial and a tiny noise transient — the classic hollow "tok".
    private static float[] SynthWoodblock()
    {
        int n = (int)(Rate * 0.18f);
        var s = new float[n];
        var rng = new System.Random(12345);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env1 = Mathf.Exp(-t / 0.030f);
            float env2 = Mathf.Exp(-t / 0.012f);
            float v = 0.9f * Mathf.Sin(2f * Mathf.PI * 850f * t) * env1
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * 1810f * t) * env2;
            if (t < 0.002f) v += 0.5f * ((float)rng.NextDouble() * 2f - 1f) * (1f - t / 0.002f);
            s[i] = v;
        }
        Normalize(s, 0.85f);
        return s;
    }

    // Clap: four tightly-spaced band-shaped noise bursts (loose "group" flams)
    // with the last burst carrying a longer tail.
    private static float[] SynthClap()
    {
        int n = (int)(Rate * 0.25f);
        var s = new float[n];
        var rng = new System.Random(54321);
        float[] starts = { 0f, 0.011f, 0.023f, 0.038f };
        float[] taus   = { 0.006f, 0.006f, 0.007f, 0.055f };
        // Band-shape the noise with a crude 2-pole band emphasis (~1.2 kHz).
        float lp1 = 0f, lp2 = 0f;
        const float a1 = 0.25f, a2 = 0.08f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = 0f;
            for (int b = 0; b < starts.Length; b++)
                if (t >= starts[b]) env += Mathf.Exp(-(t - starts[b]) / taus[b]);
            float white = (float)rng.NextDouble() * 2f - 1f;
            lp1 += a1 * (white - lp1);
            lp2 += a2 * (lp1 - lp2);
            float band = lp1 - lp2; // band-passed noise
            s[i] = band * env * 2.2f;
        }
        Normalize(s, 0.85f);
        return s;
    }

    private static void Normalize(float[] s, float peak)
    {
        float max = 0f;
        foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
        if (max < 1e-6f) return;
        float g = peak / max;
        for (int i = 0; i < s.Length; i++) s[i] *= g;
    }

    private static void WriteWav(string path, float[] samples)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var fs = new FileStream(path, FileMode.Create);
        using var w  = new BinaryWriter(fs);
        int dataLen = samples.Length * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1);          // PCM, mono
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);
        foreach (float v in samples)
            w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * short.MaxValue), short.MinValue, short.MaxValue));
    }
}
