using System.Text;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class DumpSigns
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        var pivot = GameObject.Find("PlanetPivot");
        var spinner = pivot.GetComponent<PlanetSpinner>();
        var flow = GameObject.Find("MenuFlow").GetComponent<FlashCardsMenuController>();
        var so = new SerializedObject(spinner); var mounts = so.FindProperty("signMounts");
        for (int i = 0; i < mounts.arraySize; i++)
        {
            var t = mounts.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            sb.AppendLine($"mount[{i}] = {(t ? t.name : "null")} active={(t ? t.gameObject.activeSelf : false)} pos={(t ? t.localPosition : Vector3.zero)} rot={(t ? t.localEulerAngles : Vector3.zero)} children={(t ? t.childCount : 0)} child0={(t && t.childCount > 0 ? t.GetChild(0).name : "-")}");
        }
        var fso = new SerializedObject(flow); var nodes = fso.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var n = nodes.GetArrayElementAtIndex(i).objectReferenceValue as MenuSignNode;
            var nso = n ? new SerializedObject(n) : null;
            var def = nso?.FindProperty("definition")?.objectReferenceValue;
            sb.AppendLine($"node[{i}] = {(n ? n.name : "null")} active={(n ? n.gameObject.activeSelf : false)} parent={(n ? n.transform.parent.name : "-")} def={(def ? def.name : "null")} locked={nso?.FindProperty("lockedByDefault")?.boolValue}");
        }
        foreach (Transform c in pivot.transform) sb.AppendLine("pivotChild: " + c.name + " active=" + c.gameObject.activeSelf + " pos=" + c.localPosition);
        Debug.Log("[DumpSigns]\n" + sb);
    }
}
