using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bakes the Bongo results paper report into the open scene so it can be tuned in
/// the inspector instead of being built at runtime on first use.
///
/// Drummi → Bongo → Bake Results Report Into Open Scene
///   1. Finds the ScoreScreen page (the "ResultsScreen" object, may be inactive).
///   2. Adds a BongoReport child with BongoResultsReport if there isn't one.
///   3. Builds the paper + text hierarchy (BongoResultsReport.EnsureBuilt).
///   4. Saves the procedural placeholder paper as a PNG sprite asset so the scene
///      serialises cleanly (a runtime-generated sprite can't be saved).
///   5. Wires ScoreScreen.bongoReport, marks the scene dirty and saves it.
/// </summary>
public static class BongoResultsReportBaker
{
    private const string PaperAssetPath = "Assets/Images/Generated/BongoReportPaper.png";

    [MenuItem("Drummi/Bongo/Bake Results Report Into Open Scene")]
    public static void Bake()
    {
        ScoreScreen page = null;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<ScoreScreen>())
        {
            if (candidate.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(candidate)) { page = candidate; break; }
        }
        if (page == null)
        {
            Debug.LogError("[BongoResultsReportBaker] No ScoreScreen in the open scene. Open Bongo.unity first.");
            return;
        }

        var report = page.bongoReport != null ? page.bongoReport : page.GetComponentInChildren<BongoResultsReport>(true);
        if (report == null)
        {
            var go = new GameObject(BongoResultsReport.DefaultObjectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Bake Bongo results report");
            go.transform.SetParent(page.transform, false);
            report = go.AddComponent<BongoResultsReport>();
        }

        Undo.RecordObject(report, "Bake Bongo results report");
        report.EnsureBuilt();

        // Persist the placeholder paper as an asset and point the Image at it.
        var paperImage = report.GetComponentInChildren<Image>(true);
        if (paperImage != null && (paperImage.sprite == null || !EditorUtility.IsPersistent(paperImage.sprite)))
        {
            var sprite = SaveGeneratedPaper();
            if (sprite != null)
            {
                var so = new SerializedObject(report);
                so.FindProperty("paperSprite").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                paperImage.sprite = sprite;
            }
        }

        Undo.RecordObject(page, "Bake Bongo results report");
        page.bongoReport = report;
        report.gameObject.SetActive(false); // GameManager shows it only after a Bongo run

        EditorUtility.SetDirty(page);
        EditorUtility.SetDirty(report);
        EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        EditorSceneManager.SaveScene(page.gameObject.scene);
        Debug.Log($"[BongoResultsReportBaker] Baked '{report.name}' under '{page.name}' and saved {page.gameObject.scene.path}.");
    }

    private static Sprite SaveGeneratedPaper()
    {
        var sprite = BongoResultsReport.GeneratePaperSprite();
        var tex = sprite.texture;

        Directory.CreateDirectory(Path.GetDirectoryName(PaperAssetPath));
        File.WriteAllBytes(PaperAssetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(PaperAssetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(PaperAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(PaperAssetPath);
    }
}
