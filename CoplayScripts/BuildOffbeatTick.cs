using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Synthesizes Assets/Audio/OffbeatTick.wav — a soft, short shaker-like "tk"
/// (band-passed noise burst, quiet) that marks the player's shifted beats while
/// play is off-beat — and assigns it to FlashCardView.offbeatTickClip. Same
/// synth path as SynthInstrumentClips (AI SFX generation is unauthorized).
/// </summary>
public static class BuildOffbeatTick
{
    private const int Rate = 44100;

    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log("[OffbeatTick] ABORT: scene is " + scene.name); return; }

        WriteWav("Assets/Audio/OffbeatTick.wav", SynthTick());
        AssetDatabase.ImportAsset("Assets/Audio/OffbeatTick.wav");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OffbeatTick.wav");
        if (clip == null) { Debug.Log("[OffbeatTick] ABORT: clip failed to import."); return; }

        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[OffbeatTick] ABORT: FlashCardView not found."); return; }
        var so = new SerializedObject(view);
        so.FindProperty("offbeatTickClip").objectReferenceValue = clip;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[OffbeatTick] OffbeatTick.wav (" + clip.length.ToString("F2") +
                  "s) built and wired to FlashCardView. Saved=" + saved);
    }

    // Soft closed-hat/shaker "tk": very short band-passed noise with a fast
    // decay and a tiny 2.4 kHz ring. Normalized QUIET (0.35) — it is a rail
    // under the music, not a sound effect.
    private static float[] SynthTick()
    {
        int n = (int)(Rate * 0.07f);
        var s = new float[n];
        var rng = new System.Random(24680);
        float lp1 = 0f, lp2 = 0f;
        const float a1 = 0.45f, a2 = 0.18f;
        for (int i = 0; i < n; i++)
        {
            float t   = i / (float)Rate;
            float env = Mathf.Exp(-t / 0.012f);
            float white = (float)rng.NextDouble() * 2f - 1f;
            lp1 += a1 * (white - lp1);
            lp2 += a2 * (lp1 - lp2);
            float band = lp1 - lp2;
            float ring = 0.3f * Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t / 0.006f);
            s[i] = (band * 1.8f + ring) * env;
        }
        Normalize(s, 0.35f);
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
        w.Write(16); w.Write((short)1); w.Write((short)1); // PCM, mono
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);
        foreach (float v in samples)
            w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * short.MaxValue), short.MinValue, short.MaxValue));
    }
}
