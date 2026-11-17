using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates placeholder notation sprites into Assets/Images/Notation/:
///   QuarterNote.png    — filled circle notehead with a stem
///   QuarterRest.png    — bold Z-block placeholder rest
///   EighthPair.png     — two beamed eighth notes
///   OffbeatEighth.png  — eighth-rest mark (left) + flagged eighth note (right)
///   SixteenthQuad.png  — four beamed sixteenth notes (double beam)
///   Triplet.png        — three beamed eighth notes with a "3" above the beam
/// Imported as single sprites. Final art is a drop-in swap on FlashCardView.
/// </summary>
public static class GenerateNotationSprites
{
    private const int Size = 256;

    public static string Execute()
    {
        Directory.CreateDirectory("Assets/Images/Notation");

        var files = new (string path, Color32[] pixels)[]
        {
            ("Assets/Images/Notation/QuarterNote.png",   DrawQuarterNote()),
            ("Assets/Images/Notation/QuarterRest.png",   DrawQuarterRest()),
            ("Assets/Images/Notation/EighthPair.png",    DrawEighthPair()),
            ("Assets/Images/Notation/OffbeatEighth.png", DrawOffbeatEighth()),
            ("Assets/Images/Notation/SixteenthQuad.png", DrawSixteenthQuad()),
            ("Assets/Images/Notation/Triplet.png",       DrawTriplet()),
        };

        foreach (var (path, pixels) in files) WritePng(path, pixels);
        AssetDatabase.Refresh();
        foreach (var (path, _) in files) ConfigureSprite(path);
        AssetDatabase.Refresh();

        var report = "";
        foreach (var (path, _) in files)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            report += $"{Path.GetFileNameWithoutExtension(path)}: {(sprite != null ? "OK" : "FAIL")}, ";
        }
        return report.TrimEnd(' ', ',');
    }

    private static Color32[] NewCanvas()
    {
        var px = new Color32[Size * Size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        return px;
    }

    private static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return;
        px[y * Size + x] = c;
    }

    private static void FillRect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Set(px, x, y, c);
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

    private static Color32[] DrawQuarterNote()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255); // white; tinted by Image.color at runtime
        // Notehead: filled ellipse, lower-left.
        FillEllipse(px, 100, 70, 52, 40, ink);
        // Stem: right edge of the notehead up to the top.
        FillRect(px, 138, 70, 158, 215, ink);
        return px;
    }

    private static Color32[] DrawQuarterRest()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255);
        // Bold angular Z-block placeholder.
        FillRect(px, 64, 180, 192, 208, ink);   // top bar
        FillRect(px, 64, 48, 192, 76, ink);     // bottom bar
        // Diagonal from top-right to bottom-left, thickness ~28px.
        for (int y = 76; y <= 180; y++)
        {
            float t  = (y - 76) / 104f;                 // 0 at bottom, 1 at top
            int   cx = Mathf.RoundToInt(Mathf.Lerp(78, 178, t));
            FillRect(px, cx - 14, y, cx + 14, y, ink);
        }
        return px;
    }

    // ── Eighth glyphs ─────────────────────────────────────────────────────────

    /// <summary>Small eighth notehead + stem; flag when unbeamed.</summary>
    private static void DrawEighthNote(Color32[] px, int headCx, Color32 ink, bool flag)
    {
        FillEllipse(px, headCx, 66, 30, 24, ink);
        FillRect(px, headCx + 20, 66, headCx + 32, 215, ink);
        if (flag)
        {
            // Flag: diagonal stroke off the stem top, sweeping right and down.
            for (int y = 150; y <= 215; y++)
            {
                float t  = (215 - y) / 65f;                             // 0 at stem top, 1 at flag tip
                int   cx = headCx + 32 + Mathf.RoundToInt(t * 34f);
                FillRect(px, cx, y, cx + 12, y, ink);
            }
        }
    }

    /// <summary>Eighth-rest placeholder: dot + diagonal stroke (like a small '7').</summary>
    private static void DrawEighthRest(Color32[] px, int cx, Color32 ink)
    {
        FillEllipse(px, cx - 22, 160, 16, 16, ink);
        // Diagonal from upper-right down to lower-left.
        for (int y = 70; y <= 170; y++)
        {
            float t  = (y - 70) / 100f;                                  // 0 bottom, 1 top
            int   sx = cx - 26 + Mathf.RoundToInt(t * 44f);
            FillRect(px, sx, y, sx + 12, y, ink);
        }
    }

    private static Color32[] DrawEighthPair()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255);
        DrawEighthNote(px, 58,  ink, flag: false);
        DrawEighthNote(px, 166, ink, flag: false);
        // Beam joining the two stem tops.
        FillRect(px, 78, 186, 198, 215, ink);
        return px;
    }

    private static Color32[] DrawOffbeatEighth()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255);
        DrawEighthRest(px, 68, ink);
        DrawEighthNote(px, 172, ink, flag: true);
        return px;
    }

    /// <summary>Small notehead + stem for dense clusters (no flag; beams drawn by caller).</summary>
    private static void DrawSmallNote(Color32[] px, int headCx, Color32 ink)
    {
        FillEllipse(px, headCx, 56, 22, 17, ink);
        FillRect(px, headCx + 14, 56, headCx + 24, 215, ink);
    }

    private static Color32[] DrawSixteenthQuad()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255);
        int[] xs = { 38, 96, 154, 212 };
        foreach (int x in xs) DrawSmallNote(px, x, ink);
        // Double beam across all four stem tops.
        FillRect(px, 52, 194, 236, 215, ink);
        FillRect(px, 52, 158, 236, 179, ink);
        return px;
    }

    private static Color32[] DrawTriplet()
    {
        var px  = NewCanvas();
        var ink = new Color32(255, 255, 255, 255);
        int[] xs = { 48, 124, 200 };
        foreach (int x in xs) DrawSmallNote(px, x, ink);
        // Single beam across the three stem tops (lower band so the "3" fits above).
        FillRect(px, 62, 170, 224, 191, ink);
        // "3" above the beam: three horizontal bars + right vertical connector.
        FillRect(px, 118, 240, 158, 250, ink);  // top bar
        FillRect(px, 122, 218, 158, 227, ink);  // middle bar
        FillRect(px, 118, 196, 158, 205, ink);  // bottom bar (sits on beam)
        FillRect(px, 150, 196, 160, 250, ink);  // right spine
        return px;
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
}
