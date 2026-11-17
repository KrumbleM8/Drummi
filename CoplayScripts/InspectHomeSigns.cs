using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>EDIT-MODE: dumps each Home sign's definition, lock flag, canvas camera and helper components, plus the pivot's rim decor.</summary>
public static class InspectHomeSigns
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var pivot = GameObject.Find("PlanetPivot").transform;
        for (int i = 0; i < 8; i++)
        {
            var m = pivot.Find("SignMount_" + i);
            if (m == null) break;
            var node = m.GetComponentInChildren<MenuSignNode>(true);
            var so = new UnityEditor.SerializedObject(node);
            var canvas = m.GetComponentInChildren<Canvas>(true);
            var btn = m.GetComponentInChildren<Button>(true);
            sb.Append("mount ").Append(i).Append(' ').Append(node.name)
              .Append(" def=").Append(so.FindProperty("definition").objectReferenceValue != null ? so.FindProperty("definition").objectReferenceValue.name : "null")
              .Append(" locked=").Append(so.FindProperty("lockedByDefault").boolValue)
              .Append(" canvasCam=").Append(canvas != null && canvas.worldCamera != null ? canvas.worldCamera.name : "null")
              .Append(" anchor=").Append(node.transform.Find("CameraAnchor") != null)
              .Append(" tapFilter=").Append(btn != null && btn.GetComponent<TapOrDragFilter>() != null)
              .Append(" prefab=").Append(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject) != null ? UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject)) : "none")
              .AppendLine();
        }
        var mtn = pivot.Find("MountainOnGlobe");
        var sr = mtn.GetComponent<SpriteRenderer>();
        sb.Append("MountainOnGlobe rot=").Append(mtn.localEulerAngles).Append(" bounds=").Append(sr.bounds.size).Append(" sprite=").Append(sr.sprite.name).Append(" ppu=").Append(sr.sprite.pixelsPerUnit).Append(" rect=").Append(sr.sprite.rect.size).AppendLine();
        var intro = Object.FindFirstObjectByType<HomeIntroDirector>(FindObjectsInactive.Include);
        var iso = new UnityEditor.SerializedObject(intro);
        sb.Append("introRollAngle=").Append(iso.FindProperty("introRollAngle").floatValue).Append(" introCam=").Append(iso.FindProperty("introCameraPosition").vector3Value).Append(' ').Append(iso.FindProperty("introCameraEuler").vector3Value);
        return sb.ToString();
    }
}
