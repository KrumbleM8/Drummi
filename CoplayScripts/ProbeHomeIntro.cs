using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: dumps the title-intro state (camera, spinner, banner, mountains, HUD, rim sorting).</summary>
public static class ProbeHomeIntro
{
    public static void Execute()
    {
        var sb = new StringBuilder("[ProbeHomeIntro] playing=" + Application.isPlaying + "\n");
        var cam = GameObject.Find("Main Camera");
        if (cam != null)
            sb.AppendLine("camera pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F1") + " active=" + cam.activeInHierarchy);

        var spinner = Object.FindFirstObjectByType<PlanetSpinner>(FindObjectsInactive.Include);
        if (spinner != null)
            sb.AppendLine("spinner angle=" + spinner.CurrentAngle.ToString("F2") + " focused=" + spinner.FocusedIndex + " pivotEuler=" + spinner.transform.localEulerAngles.ToString("F1"));

        var director = Object.FindFirstObjectByType<HomeIntroDirector>(FindObjectsInactive.Include);
        sb.AppendLine("director=" + (director != null) + " introPending=" + (director != null && director.IntroPending));
        var idle = Object.FindFirstObjectByType<PlanetIdleMotion>(FindObjectsInactive.Include);
        if (idle != null)
            sb.AppendLine("idle enabled=" + idle.enabled + " weight=" + idle.Weight.ToString("F2") + " pivotPos=" + idle.transform.localPosition.ToString("F2") + " pivotEuler=" + idle.transform.localEulerAngles.ToString("F1"));

        var overlay = GameObject.Find("OverlayCanvas");
        Transform banner = overlay != null ? overlay.transform.Find("TitleBanner") : null;
        if (banner != null)
        {
            var cg = banner.GetComponent<CanvasGroup>();
            var play = banner.Find("PlayButton");
            sb.AppendLine("banner active=" + banner.gameObject.activeSelf + " alpha=" + (cg != null ? cg.alpha.ToString("F2") : "?") +
                          " blocks=" + (cg != null && cg.blocksRaycasts) + " playInteractable=" + (play != null && play.GetComponent<Button>().interactable));
            var art = banner.Find("TitleArt") as RectTransform;
            if (art != null) sb.AppendLine("titleArt anchoredPos=" + art.anchoredPosition + " size=" + art.sizeDelta);
        }
        else sb.AppendLine("banner: NOT FOUND");

        var instr = overlay != null ? overlay.transform.Find("InstrumentButton") : null;
        sb.AppendLine("instrumentButton active=" + (instr != null && instr.gameObject.activeSelf));

        var mid = GameObject.Find("ParallaxMid");
        Transform mm = mid != null ? mid.transform.Find("MountainMid") : null;
        if (mm != null)
        {
            var mr = mm.GetComponent<Renderer>();
            sb.AppendLine("MountainMid localPos=" + mm.localPosition.ToString("F2") + " worldPos=" + mm.position.ToString("F2") + " rendererEnabled=" + (mr != null && mr.enabled));
        }

        if (spinner != null)
        {
            foreach (Transform child in spinner.transform)
            {
                if (child.name == "GlobeMesh") continue;
                var r = child.GetComponentInChildren<Renderer>(true);
                sb.AppendLine("  rim " + child.name + " order=" + (r != null ? r.sortingOrder.ToString() : "?") + " worldPos=" + child.position.ToString("F1"));
            }
        }

        var ctl = Object.FindFirstObjectByType<FlashCardsMenuController>(FindObjectsInactive.Include);
        sb.AppendLine("menuController=" + (ctl != null));
        Debug.Log(sb.ToString());
    }
}
