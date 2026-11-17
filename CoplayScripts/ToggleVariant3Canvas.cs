using UnityEngine;

/// <summary>PLAY-MODE diagnostic: toggles MenuSign_Variant3's SignCanvas.</summary>
public static class ToggleVariant3Canvas
{
    public static string Execute()
    {
        GameObject sign = GameObject.Find("MenuSign_Variant3");
        if (sign == null) return "FAIL: MenuSign_Variant3 not found.";
        Transform canvas = sign.transform.Find("SignCanvas");
        canvas.gameObject.SetActive(!canvas.gameObject.activeSelf);
        return "SignCanvas now active=" + canvas.gameObject.activeSelf;
    }
}
