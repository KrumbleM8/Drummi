using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public static class ConvertBackgroundPatternToScrolling
{
    // 1. Sets the striped pattern texture's wrap mode to Repeat (needed for UV scroll).
    // 2. Swaps BackgroundPattern's Image for a RawImage (same texture/color/raycast,
    //    uvRect (0,0,1,1) = identical look) + ScrollingPatternBackground.
    public static string Execute()
    {
        var go = GameObject.Find("MainCanvas/BackgroundPattern");
        if (go == null) return "ERROR: MainCanvas/BackgroundPattern not found";
        if (go.GetComponent<RawImage>() != null) return "ERROR: already converted (RawImage present)";

        var image = go.GetComponent<Image>();
        if (image == null || image.sprite == null) return "ERROR: no Image with sprite on BackgroundPattern";

        Texture2D tex = image.sprite.texture;
        Color color = image.color;
        bool raycast = image.raycastTarget;

        // Texture importer: wrap Repeat.
        string texPath = AssetDatabase.GetAssetPath(tex);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        Object.DestroyImmediate(image);
        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = color;
        raw.raycastTarget = raycast;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
        go.AddComponent<ScrollingPatternBackground>();

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        return $"Converted: tex={texPath} (wrap=Repeat), color={color}, raycast={raycast}. Scene '{go.scene.name}' saved.";
    }
}
