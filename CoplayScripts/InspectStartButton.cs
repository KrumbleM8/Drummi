using UnityEngine;
using UnityEngine.UI;

public static class InspectStartButton
{
    public static void Execute()
    {
        var middle = GameObject.Find("MenuCanvas_Manager/MainMenu/MiddleElements");
        if (middle == null) { Debug.Log("[StartBtn] MiddleElements not found"); return; }

        var mainMenu = middle.transform.parent as RectTransform;
        Debug.Log($"[StartBtn] MainMenu anchoredPos={mainMenu.anchoredPosition} " +
                  $"localPos={mainMenu.localPosition}");

        foreach (Transform child in middle.transform)
        {
            var rt = child as RectTransform;
            var btn = child.GetComponent<Button>();
            string clicks = "";
            if (btn != null)
            {
                int n = btn.onClick.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                    clicks += $" [{btn.onClick.GetPersistentTarget(i)?.name}.{btn.onClick.GetPersistentMethodName(i)}]";
            }
            Debug.Log($"[StartBtn] child '{child.name}' active={child.gameObject.activeSelf} " +
                      $"anchoredPos={rt.anchoredPosition} worldPos={rt.position} " +
                      $"button={(btn != null)}{clicks}");
        }
    }
}
