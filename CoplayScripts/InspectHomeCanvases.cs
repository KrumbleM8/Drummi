using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// EDIT-MODE ONLY: opens Home.unity, reports every Canvas (render mode, sorting order)
/// and the Background root, plus CurtainController/BongoInputReader wiring on HomeController.
/// Reopens New_Home when done.
/// </summary>
public static class InspectHomeCanvases
{
    public static string Execute()
    {
        if (Application.isPlaying) return "FAIL: exit play mode first.";
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"Canvas '{canvas.name}': mode={canvas.renderMode} sort={canvas.sortingOrder} " +
                          $"scaler(res={canvas.GetComponent<UnityEngine.UI.CanvasScaler>()?.referenceResolution})");

        var bg = GameObject.Find("Background");
        if (bg != null)
        {
            sb.AppendLine($"Background root: comps={string.Join(",", System.Array.ConvertAll(bg.GetComponents<Component>(), c => c.GetType().Name))}");
            var sr = bg.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sb.AppendLine($"  SpriteRenderer sprite={AssetDatabase.GetAssetPath(sr.sprite)} color={sr.color}");
            var img = bg.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null) sb.AppendLine($"  Image sprite={AssetDatabase.GetAssetPath(img.sprite)} color={img.color}");
            foreach (Transform child in bg.transform) sb.AppendLine($"  child: {child.name}");
        }

        var hc = GameObject.Find("HomeController");
        if (hc != null)
        {
            foreach (var comp in hc.GetComponents<MonoBehaviour>())
            {
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                sb.AppendLine($"HomeController.{comp.GetType().Name}:");
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                        sb.AppendLine($"  {it.name} = {(it.objectReferenceValue != null ? it.objectReferenceValue.name : "null")}");
                    else if (it.propertyType == SerializedPropertyType.Float || it.propertyType == SerializedPropertyType.Integer)
                        sb.AppendLine($"  {it.name} = {(it.propertyType == SerializedPropertyType.Float ? it.floatValue : it.intValue)}");
                }
            }
        }

        EditorSceneManager.OpenScene("Assets/Scenes/New_Home.unity", OpenSceneMode.Single);
        return sb.ToString();
    }
}
