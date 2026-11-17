using UnityEditor;
using UnityEngine;

/// <summary>
/// MenuSign prefab: with all sign sprites on sorting order 0, perspective sort
/// compares sprite-center distances — the post's lower center sits nearer the
/// camera than the board's, so it drew in front. Push the post back in z so
/// its center is farther than the board's from any dolly anchor.
/// </summary>
public static class FixMenuSignPostDepth
{
    public static string Execute()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/MenuSign.prefab");
        try
        {
            var post = root.transform.Find("Post");
            if (post == null) return "FATAL: Post child not found";
            var p = post.localPosition;
            post.localPosition = new Vector3(p.x, p.y, 0.4f);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/MenuSign.prefab");
            return $"Post localPosition z: {p.z} -> 0.4";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
