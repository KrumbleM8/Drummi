using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

public static class CheckProxyState
{
    public static void Execute()
    {
        var proxy = Object.FindObjectOfType<FlickBallUIProxy>(true);
        if (proxy == null) { Debug.LogError("[ProxyCheck] No proxy"); return; }
        var img = proxy.GetComponent<Image>();
        var rt  = (RectTransform)proxy.transform;
        Debug.Log($"[ProxyCheck] active={proxy.gameObject.activeInHierarchy} imgEnabled={img.enabled} " +
                  $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} sibling={rt.GetSiblingIndex()}");

        var ball = GameObject.Find("FlickRig/FlickBall");
        if (ball != null)
        {
            var sr = ball.GetComponent<SpriteRenderer>();
            var cam = Camera.main;
            Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(ball.transform.position) : Vector2.zero;
            Debug.Log($"[ProxyCheck] worldBall pos={ball.transform.position} srEnabled={sr.enabled} screen={screen}");
        }
    }
}
