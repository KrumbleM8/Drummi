using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Builds the HalfRest card's assets and wires them to the FlashCardView:
///   Assets/Images/Notation/HalfRest.png — big standalone eighth-rest glyph
///     (same placeholder style as GenerateNotationSprites, scaled 1.6x).
///   Assets/Audio/HalfRestCue.wav — two-note rising synth chirp, the wordless
///     "on-beat is about to change" telegraph (same synth path as
///     SynthInstrumentClips; AI SFX generation is unauthorized).
/// Assigns halfRestSprite + halfRestCueClip on the scene's FlashCardView and saves.
/// </summary>
public static class BuildHalfRestAssets
{
    private const int Size = 256;
    private const int Rate = 44100;

    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log("[HalfRestAssets] ABORT: scene '" + scene.name + "'."); return; }

        // ── Glyph ──
        Directory.CreateDirectory("Assets/Images/Notation");
        WritePng("Assets/Images/Notation/HalfRest.png", DrawHalfRest());
        AssetDatabase.ImportAsset("Assets/Images/Notation/HalfRest.png");
        ConfigureSprite("Assets/Images/Notation/HalfRest.png");

        // ── Cue ──
        WriteWav("Assets/Audio/HalfRestCue.wav", SynthCue());
        AssetDatabase.ImportAsset("Assets/Audio/HalfRestCue.wav");

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/HalfRest.png");
        var clip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HalfRestCue.wav");
        if (sprite == null || clip == null) { Debug.Log("[HalfRestAssets] ABORT: sprite=" + sprite + " clip=" + clip); return; }

        // ── Wire to the view ──
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[HalfRestAssets] ABORT: FlashCardView not found."); return; }
        var so = new SerializedObject(view);
        so.FindProperty("halfRestSprite").objectReferenceValue  = sprite;
        so.FindProperty("halfRestCueClip").objectReferenceValue = clip;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HalfRestAssets] HalfRest.png + HalfRestCue.wav (" + clip.length.ToString("F2") +
                  "s) built and wired to FlashCardView. Saved=" + saved);
    }

    // ── Glyph: standalone eighth rest (dot + down-left diagonal stroke), the
    // OffbeatEighth rest mark scaled 1.6x about the canvas center so it reads
    // as THE content of the square, not an accessory. White ink; the Image
    // tint carries color.
    private static Color32[] DrawHalfRest()
    {
        var px  = new Color32[Size * Size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        var ink = new Color32(255, 255, 255, 255);

        // Diagonal from upper-right (157, 195) down to lower-left (86, 35), ~19px thick.
        for (int y = 35; y <= 195; y++)
        {
            float t  = (y - 35) / 160f; // 0 bottom, 1 top
            int   sx = 86 + Mathf.RoundToInt(t * 71f);
            for (int x = sx; x <= sx + 19; x++) Set(px, x, y, ink);
        }
        // Dot at the stroke's top-left.
        FillEllipse(px, 93, 179, 26, 26, ink);
        return px;
    }

    private static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return;
        px[y * Size + x] = c;
    }

    private static void FillEllipse(Color32[] px, int cx, int cy, int rx, int ry, Color32 c)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float nx = (x - cx) / (float)rx, ny = (y - cy) / (float)ry;
                if (nx * nx + ny * ny <= 1f) Set(px, x, y, c);
            }
    }

    // ── Cue: two short rising sine blips (660 → 990 Hz, a perfect fifth up) —
    // reads as "step UP / something shifts", wordless and snappy, distinct from
    // the Pickup11 DT cue.
    private static float[] SynthCue()
    {
        int n = (int)(Rate * 0.34f);
        var s = new float[n];
        AddBlip(s, 0.00f, 0.10f, 660f);
        AddBlip(s, 0.13f, 0.16f, 990f);
        Normalize(s, 0.8f);
        return s;
    }

    private static void AddBlip(float[] s, float start, float dur, float freq)
    {
        int i0 = (int)(start * Rate);
        int i1 = Mathf.Min(s.Length, i0 + (int)(dur * Rate));
        for (int i = i0; i < i1; i++)
        {
            float t   = (i - i0) / (float)Rate;
            float env = Mathf.Min(t / 0.005f, 1f) * Mathf.Exp(-t / (dur * 0.45f));
            s[i] += (Mathf.Sin(2f * Mathf.PI * freq * t)
                     + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t)) * env;
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

    private static void WritePng(string path, Color32[] pixels)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.mipmapEnabled       = false;
        importer.filterMode          = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
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
