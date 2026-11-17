using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Synthesizes the Pulse mode placeholder audio into Assets/Audio/Pulse/ at a
/// fixed 100 BPM, 44.1 kHz (one beat = 26460 samples exactly, so the 8-beat
/// loop stems are sample-exact). Same synth path as BuildOffbeatTick /
/// SynthInstrumentClips (AI SFX generation is unauthorized).
///   Shaker.wav — 1 beat: four semiquaver shakes, first accented (played per tap)
///   Kick.wav   — every beat                     (the plain pulse)
///   Hats.wav   — off-beat eighths
///   Clap.wav   — beats 2 and 4
///   Bass.wav   — syncopated, two-beat gap at the end of bar 2
/// Idempotent: overwrites the WAVs. Replace with real stems later; the mode
/// only reads a clip list.
/// </summary>
public static class BuildPulseAudio
{
    private const int Rate = 44100;
    private const int Bpm = 100;
    private const int BeatSamples = Rate * 60 / Bpm; // 26460
    private const int LoopBeats = 8;
    private const string Dir = "Assets/Audio/Pulse/";

    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "ABORT: exit play mode first.";

        Write("Shaker", SynthShaker(), loop: false);
        Write("Kick",   SynthKick(),   loop: true);
        Write("Hats",   SynthHats(),   loop: true);
        Write("Clap",   SynthClap(),   loop: true);
        Write("Bass",   SynthBass(),   loop: true);
        AssetDatabase.SaveAssets();

        var sb = new System.Text.StringBuilder();
        foreach (var n in new[] { "Shaker", "Kick", "Hats", "Clap", "Bass" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + n + ".wav");
            sb.AppendLine(clip == null ? $"{n}: FAILED to import" : $"{n}: {clip.samples} samples, {clip.length:F3}s");
        }
        return sb.ToString();
    }

    // ── Synths ──────────────────────────────────────────────────────────────

    private static float[] SynthShaker()
    {
        var s = new float[BeatSamples];
        for (int q = 0; q < 4; q++)
            AddNoiseBurst(s, q * BeatSamples / 4, q == 0 ? 1f : 0.55f, decay: 0.035f, seed: 100 + q);
        Normalize(s, 0.8f);
        return s;
    }

    private static float[] SynthKick()
    {
        var s = new float[BeatSamples * LoopBeats];
        for (int b = 0; b < LoopBeats; b++)
        {
            int at = b * BeatSamples;
            int n = (int)(Rate * 0.25f);
            float phase = 0f;
            for (int i = 0; i < n && at + i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = Mathf.Lerp(130f, 48f, Mathf.Clamp01(t / 0.08f));
                phase += 2f * Mathf.PI * freq / Rate;
                s[at + i] += Mathf.Sin(phase) * Mathf.Exp(-t / 0.11f);
            }
        }
        Normalize(s, 0.85f);
        return s;
    }

    private static float[] SynthHats()
    {
        var s = new float[BeatSamples * LoopBeats];
        for (int b = 0; b < LoopBeats; b++)
            AddNoiseBurst(s, b * BeatSamples + BeatSamples / 2, 1f, decay: 0.018f, seed: 200 + b, bright: true);
        Normalize(s, 0.45f);
        return s;
    }

    private static float[] SynthClap()
    {
        var s = new float[BeatSamples * LoopBeats];
        for (int b = 1; b < LoopBeats; b += 2) // beats 2 and 4 of each bar
            for (int r = 0; r < 3; r++)        // three micro-repeats = clap flam
                AddNoiseBurst(s, b * BeatSamples + r * 440, r == 2 ? 1f : 0.6f, decay: r == 2 ? 0.06f : 0.012f, seed: 300 + b * 3 + r);
        Normalize(s, 0.6f);
        return s;
    }

    private static float[] SynthBass()
    {
        var s = new float[BeatSamples * LoopBeats];
        // Eighth-note slots 0..15 over 2 bars; bar 2 ends with a two-beat gap (slots 12-15 silent).
        int[]   slots = { 0, 3, 6, 8, 11 };
        float[] freqs = { 55f, 55f, 65.4f, 55f, 73.4f };
        for (int i = 0; i < slots.Length; i++)
        {
            int at = slots[i] * BeatSamples / 2;
            int n = (int)(Rate * 0.32f);
            for (int j = 0; j < n && at + j < s.Length; j++)
            {
                float t = j / (float)Rate;
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t / 0.14f);
                float v = Mathf.Sin(2f * Mathf.PI * freqs[i] * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freqs[i] * 2f * t);
                s[at + j] += v * env;
            }
        }
        Normalize(s, 0.8f);
        return s;
    }

    // Band-passed noise "tk" (BuildOffbeatTick.SynthTick), placed additively at an offset.
    private static void AddNoiseBurst(float[] s, int at, float amp, float decay, int seed, bool bright = false)
    {
        var rng = new System.Random(seed);
        float lp1 = 0f, lp2 = 0f;
        float a1 = bright ? 0.7f : 0.45f, a2 = bright ? 0.3f : 0.18f;
        int n = (int)(Rate * decay * 5f);
        for (int i = 0; i < n && at + i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)rng.NextDouble() * 2f - 1f;
            lp1 += a1 * (white - lp1);
            lp2 += a2 * (lp1 - lp2);
            s[at + i] += (lp1 - lp2) * 1.8f * amp * Mathf.Exp(-t / decay);
        }
    }

    // ── IO ──────────────────────────────────────────────────────────────────

    private static void Write(string name, float[] samples, bool loop)
    {
        string path = Dir + name + ".wav";
        WriteWav(path, samples);
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            var st = importer.defaultSampleSettings;
            st.loadType = AudioClipLoadType.DecompressOnLoad; // loops must stay sample-accurate
            st.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = st;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
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
