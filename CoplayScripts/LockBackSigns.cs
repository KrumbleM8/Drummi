using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class LockBackSigns
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int changed = 0;

        foreach (var node in nodes)
        {
            var so = new SerializedObject(node);
            var defProp = so.FindProperty("definition");
            var def = defProp != null ? defProp.objectReferenceValue : null;
            string defName = def != null ? def.name : "<null>";
            bool shouldLock = def != null && defName != "FlashCards Snake";

            var lockProp = so.FindProperty("lockedByDefault");
            if (lockProp == null)
            {
                sb.AppendLine("lockedByDefault property NOT FOUND on " + node.name);
                continue;
            }

            if (lockProp.boolValue != shouldLock)
            {
                lockProp.boolValue = shouldLock;
                so.ApplyModifiedProperties();
                changed++;
            }

            string parent = node.transform.parent != null ? node.transform.parent.name : "<root>";
            sb.AppendLine(parent + "/" + node.name + " definition=" + defName + " lockedByDefault=" + shouldLock);
        }

        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("Changed " + changed + " sign(s); SaveOpenScenes=" + saved);
        return sb.ToString();
    }
}
